using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Sections;
using MiningVolume.Core.Surface;

namespace MiningVolume.Core.Volumes
{
    /// <summary>
    /// Tính khối lượng giữa các mặt cắt song song theo quy tắc thích ứng V1.0:
    /// - một đầu bằng 0: hình chóp;
    /// - hai đầu > 0 và chênh tương đối <= ngưỡng: trung bình diện tích hai đầu;
    /// - hai đầu > 0 và chênh tương đối > ngưỡng: hình chóp cụt.
    /// Mặt cắt giữa vẫn được lấy để kiểm tra/hồ sơ nhưng không ép mọi khoảng dùng một công thức cố định.
    /// Khối lượng chi tiết được tính trên đúng các dải cao độ nên tổng chi tiết và tổng theo tầng
    /// là hai phép cộng của cùng một tập giá trị.
    /// </summary>
    public sealed class SectionVolumeCalculator
    {
        public VolumeResult Calculate(SectionSystem system, IReadOnlyList<SectionProfile> endProfiles,
            TinSurface existing, TinSurface design, VolumeCalculationOptions options)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (endProfiles == null) throw new ArgumentNullException(nameof(endProfiles));
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            if (design == null) throw new ArgumentNullException(nameof(design));
            options = options ?? new VolumeCalculationOptions();
            var bands = BuildBands(options.FromLevel, options.ToLevel, options.LevelStep, options.Tolerance);
            if (bands.Count == 0) throw new InvalidOperationException("Không tạo được dải cao độ tính khối lượng.");

            // One-time scan: candidate 2.5D support triangles incident to
            // an XY with multiple source Z are not authoritative.
            var incidentSupport = VerticalConflictFootprint.IncidentSupport(existing, design);
            var warnings = new List<string>();
            var profileByName = endProfiles.Where(p => p != null).GroupBy(p => p.Line.Name)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var ordered = system.Lines.OrderBy(x => x.Offset).ToList();
            var areaCalc = new SectionBandAreaCalculator();
            var sectionAreas = new List<SectionBandAreas>();
            var areaByName = new Dictionary<string, SectionBandAreas>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in ordered)
            {
                if (!profileByName.TryGetValue(line.Name, out SectionProfile p) || !p.HasData)
                {
                    warnings.Add(line.Name + ": chưa có mặt cắt hợp lệ, không dùng để tính khối lượng.");
                    continue;
                }
                // The support TIN may contain remote double-Z points; only a
                // route actually touching a preserved vertical curtain needs
                // inspection. Approved/edited CAD geometry is authoritative.
                if (!p.IsUserEdited &&
                    (VerticalConflictFootprint.TouchesSection(
                        line, existing, design, options.Tolerance) ||
                     VerticalConflictFootprint.TouchesSupportSection(
                        line, incidentSupport, options.Tolerance)))
                {
                    warnings.Add(line.Name + ": tuyến gặp mặt đứng hoặc tam giác hỗ trợ gần đỉnh nhiều Z; cần kiểm tra/Cập nhật mặt cắt trước khi xác nhận diện tích.");
                    continue;
                }
                var ba = areaCalc.Calculate(p, bands, options.Tolerance);
                sectionAreas.Add(ba); areaByName[line.Name] = ba;
            }

            var intervalRows = new List<SectionIntervalVolume>();
            int skippedIntervals = 0;
            var levelCut = new double[bands.Count];
            var levelFill = new double[bands.Count];
            var builder = new SectionSystemBuilder();
            var sampler = new TinSectionSampler();

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var a = ordered[i]; var b = ordered[i + 1];
                double distance = Math.Abs(b.Offset - a.Offset);
                if (distance <= options.Tolerance) { warnings.Add($"{a.Name}-{b.Name}: khoảng cách bằng 0, bỏ qua."); continue; }
                if (!profileByName.TryGetValue(a.Name, out SectionProfile pa) || !pa.HasData ||
                    !profileByName.TryGetValue(b.Name, out SectionProfile pb) || !pb.HasData ||
                    !areaByName.TryGetValue(a.Name, out SectionBandAreas aa) ||
                    !areaByName.TryGetValue(b.Name, out SectionBandAreas ab))
                {
                    warnings.Add($"{a.Name}-{b.Name}: thiếu dữ liệu mặt cắt đầu/cuối; không tính khoảng này.");
                    skippedIntervals++;
                    continue;
                }

                if (!(pa.IsUserEdited && pb.IsUserEdited) &&
                    (VerticalConflictFootprint.TouchesStrip(a, b, existing, design, options.Tolerance) ||
                     VerticalConflictFootprint.TouchesSupportStrip(
                         a, b, incidentSupport, options.Tolerance)))
                {
                    warnings.Add($"{a.Name}-{b.Name}: mặt đứng hoặc tam giác TIN hỗ trợ chứa đỉnh nhiều Z ảnh hưởng vùng giữa hai tuyến; khối lượng chưa xác nhận, không cộng vào tổng.");
                    skippedIntervals++;
                    continue;
                }

                SectionProfile pm = null;
                SectionBandAreas am = null;
                double midOffset = (a.Offset + b.Offset) * 0.5;
                var midLine = builder.BuildLineAtOffset(system.Boundary, system.Direction, midOffset, -(i + 1), "MID-", options.Tolerance);
                if (midLine != null && !pa.IsUserEdited && !pb.IsUserEdited)
                {
                    pm = sampler.BuildProfile(midLine, existing, design, options.Tolerance);
                    if (pm.HasData) am = areaCalc.Calculate(pm, bands, options.Tolerance);
                }

                var cutFormula = SelectFormula(
                    aa.CutArea, ab.CutArea,
                    options.AreaDifferenceThreshold,
                    options.Tolerance);
                var fillFormula = SelectFormula(
                    aa.FillArea, ab.FillArea,
                    options.AreaDifferenceThreshold,
                    options.Tolerance);

                var row = new SectionIntervalVolume
                {
                    Index = intervalRows.Count + 1,
                    StartSection = a.Name,
                    EndSection = b.Name,
                    Distance = distance,
                    CutAreaStart = aa.CutArea,
                    CutAreaMid = am?.CutArea ?? 0,
                    CutAreaEnd = ab.CutArea,
                    FillAreaStart = aa.FillArea,
                    FillAreaMid = am?.FillArea ?? 0,
                    FillAreaEnd = ab.FillArea,
                    CutFormula = cutFormula,
                    FillFormula = fillFormula,
                    HasMidArea=am!=null,
                    Note = $"Tổng các dải cao độ; từng dải tự chọn công thức theo hai diện tích, ngưỡng {options.AreaDifferenceThreshold:P0}."
                };

                row.CutVolume = VolumeByFormula(
                    aa.CutArea, ab.CutArea, distance, cutFormula);
                row.FillVolume = VolumeByFormula(
                    aa.FillArea, ab.FillArea, distance, fillFormula);
                intervalRows.Add(row);

                for (int k = 0; k < bands.Count; k++)
                {
                    double c1 = aa.Bands[k].CutArea, c2 = ab.Bands[k].CutArea;
                    double f1 = aa.Bands[k].FillArea, f2 = ab.Bands[k].FillArea;
                    var cFormula = SelectFormula(
                        c1, c2, options.AreaDifferenceThreshold, options.Tolerance);
                    var fFormula = SelectFormula(
                        f1, f2, options.AreaDifferenceThreshold, options.Tolerance);
                    double cut=VolumeByFormula(c1,c2,distance,cFormula),fill=VolumeByFormula(f1,f2,distance,fFormula);
                    row.BandVolumes.Add(new SectionIntervalVolume {Index=bands[k].Index,StartSection=a.Name,EndSection=b.Name,
                        Distance=distance,CutAreaStart=c1,CutAreaEnd=c2,FillAreaStart=f1,FillAreaEnd=f2,
                        CutFormula=cFormula,FillFormula=fFormula,CutVolume=cut,FillVolume=fill});
                    levelCut[k] += cut;
                    levelFill[k] += fill;
                }
                row.CutVolume=row.BandVolumes.Sum(x=>x.CutVolume);
                row.FillVolume=row.BandVolumes.Sum(x=>x.FillVolume);
            }

            var levels = new List<LevelVolumeSummary>();
            for (int k = 0; k < bands.Count; k++)
                levels.Add(new LevelVolumeSummary { Band = bands[k], CutVolume = levelCut[k], FillVolume = levelFill[k] });

            // Tính lại tổng hàng chi tiết bằng đúng tổng các tầng để không phát sinh sai số do hai đường tính khác nhau.
            // Cộng cùng một tập đóng góp (khoảng, tầng); không phân bổ hay hiệu chỉnh tổng sau tính.
            double detailCut = intervalRows.Sum(x => x.CutVolume), detailFill = intervalRows.Sum(x => x.FillVolume);
            double levelCutTotal = levels.Sum(x => x.CutVolume), levelFillTotal = levels.Sum(x => x.FillVolume);
            if (Math.Abs(detailCut - levelCutTotal) > Math.Max(1e-6, Math.Abs(detailCut) * 1e-9) ||
                Math.Abs(detailFill - levelFillTotal) > Math.Max(1e-6, Math.Abs(detailFill) * 1e-9))
                warnings.Add("Cảnh báo kiểm soát: tổng chi tiết và tổng theo tầng có sai khác vượt dung sai số học.");

            if (skippedIntervals > 0)
                warnings.Add($"CHƯA ĐẦY ĐỦ: {skippedIntervals:n0} khoảng chưa có khối lượng xác nhận. Tổng hiện có chỉ gồm các khoảng đã tính hợp lệ.");
            if (intervalRows.Count == 0)
                throw new InvalidOperationException("Không có khoảng mặt cắt đủ điều kiện tính. Các tuyến trong phạm vi này đi qua vùng mặt đứng nhiều Z hoặc chưa đủ dữ liệu; không thể thay số chưa xác nhận bằng 0.");
            return new VolumeResult(bands, intervalRows, levels, sectionAreas, warnings,
                skippedIntervals);
        }

        public static IReadOnlyList<LevelBand> BuildBands(double fromLevel, double toLevel, double step, double tol = 1e-9)
        {
            if (double.IsNaN(fromLevel) || double.IsInfinity(fromLevel) ||
                double.IsNaN(toLevel) || double.IsInfinity(toLevel) ||
                double.IsNaN(step) || double.IsInfinity(step) ||
                double.IsNaN(tol) || double.IsInfinity(tol) || tol <= 0)
                throw new ArgumentException("Mức, bước và dung sai phải là số hữu hạn hợp lệ.");
            if (Math.Abs(toLevel-fromLevel)/step > 100000)
                throw new ArgumentException("Số dải cao độ vượt 100.000; hãy tăng bước chia tầng.");
            if (step <= tol) throw new ArgumentOutOfRangeException(nameof(step), "Bước chia tầng phải lớn hơn 0.");
            if (Math.Abs(toLevel - fromLevel) <= tol) throw new ArgumentException("Mức bắt đầu và mức kết thúc phải khác nhau.");
            var r = new List<LevelBand>();
            double sign = toLevel > fromLevel ? 1 : -1;
            double cur = fromLevel;
            int guard = 0, index = 1;
            while ((sign > 0 ? cur < toLevel - tol : cur > toLevel + tol) && guard++ < 100000)
            {
                double next = cur + sign * step;
                if (sign > 0 && next > toLevel) next = toLevel;
                if (sign < 0 && next < toLevel) next = toLevel;
                if(next==cur)throw new ArgumentException("Bước chia tầng quá nhỏ so với độ lớn cao độ.");
                r.Add(new LevelBand(index++, cur, next));
                cur = next;
            }
            return r;
        }

        public static VolumeFormulaKind SelectFormula(
            double a1,
            double a2,
            double areaDifferenceThreshold = 0.40,
            double tolerance = 1e-9)
        {
            a1 = Math.Max(0, a1);
            a2 = Math.Max(0, a2);

            double max = Math.Max(a1, a2);
            double min = Math.Min(a1, a2);
            if (max <= tolerance) return VolumeFormulaKind.AverageEndArea;
            if (min <= tolerance) return VolumeFormulaKind.Pyramid;

            double relativeDifference = Math.Abs(a1 - a2) / max;
            return relativeDifference <= areaDifferenceThreshold
                ? VolumeFormulaKind.AverageEndArea
                : VolumeFormulaKind.Frustum;
        }

        public static double VolumeByFormula(
            double a1,
            double a2,
            double length,
            VolumeFormulaKind formula)
        {
            a1 = Math.Max(0, a1);
            a2 = Math.Max(0, a2);
            if (length <= 0) return 0;

            switch (formula)
            {
                case VolumeFormulaKind.Pyramid:
                    return length * (a1 + a2) / 3.0;
                case VolumeFormulaKind.Frustum:
                    return length * (a1 + a2 + Math.Sqrt(a1 * a2)) / 3.0;
                case VolumeFormulaKind.Prismoidal:
                    // Chỉ giữ enum cũ để tương thích dữ liệu; V1.0 không chọn
                    // Prismoid chỉ từ hai diện tích đầu-cuối.
                    return length * (a1 + a2) * 0.5;
                default:
                    return length * (a1 + a2) * 0.5;
            }
        }
    }
}