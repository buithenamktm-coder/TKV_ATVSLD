using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Sections;

namespace MiningVolume.Core.Volumes
{
    /// <summary>Information entered and confirmed by the designer; NOT inferred from 2D outcrop lines.</summary>
    public sealed class MineralSeamInput
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double DensityTonPerCubicMetre { get; set; }
    }

    /// <summary>
    /// Validated mineral cross-section area (m2).
    /// AbsentConfirmed means a human explicitly verified ZERO mineral at this section.
    /// Omitting a record is NEVER interpreted as zero.
    /// </summary>
    public sealed class MineralSectionInput
    {
        public string BodyId { get; set; }
        public string SectionName { get; set; }
        public double AreaSquareMetres { get; set; }
        public bool AbsentConfirmed { get; set; }
    }

    public sealed class MineralIntervalResult
    {
        public string BodyId { get; internal set; }
        public string BodyName { get; internal set; }
        public string StartSection { get; internal set; }
        public string EndSection { get; internal set; }
        public double DistanceMetres { get; internal set; }
        public double StartAreaSquareMetres { get; internal set; }
        public double EndAreaSquareMetres { get; internal set; }
        public VolumeFormulaKind Formula { get; internal set; }
        public double VolumeCubicMetres { get; internal set; }
        public double MassTonnes { get; internal set; }
    }

    public sealed class MineralBodyResult
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public double DensityTonPerCubicMetre { get; internal set; }
        public double VolumeCubicMetres { get; internal set; }
        public double MassTonnes { get; internal set; }
    }

    public sealed class MineralVolumeSummary
    {
        public IReadOnlyList<MineralIntervalResult> Intervals { get; internal set; }
        public IReadOnlyList<MineralBodyResult> Bodies { get; internal set; }
        public double TotalCutCubicMetres { get; internal set; }
        public double TotalMineralCubicMetres => Bodies.Sum(x => x.VolumeCubicMetres);
        public double TotalMineralTonnes => Bodies.Sum(x => x.MassTonnes);
        public double WasteCubicMetres => Math.Max(0, TotalCutCubicMetres - TotalMineralCubicMetres);
        public double? StrippingRatioCubicMetresPerCubicMetre => TotalMineralCubicMetres > 0
            ? (double?)(WasteCubicMetres / TotalMineralCubicMetres) : null;
        public double? StrippingRatioCubicMetresPerTonne => TotalMineralTonnes > 0
            ? (double?)(WasteCubicMetres / TotalMineralTonnes) : null;
        public string Method => "Mặt cắt song song: công thức thích ứng theo diện tích hai đầu";
    }

    /// <summary>
    /// Converts *explicitly verified* mineral polygon areas in each section to
    /// mineral volume, tonnes and waste. The approximation is the documented
    /// end-area method; it is NOT a geological 3D solid calculation.
    ///
    /// Hard gates: all seams named, positive density, every seam has an area or
    /// explicit zero at EVERY participating section, no overdrawn mineral area,
    /// total mineral per interval must not exceed physical excavation interval.
    /// </summary>
    public static class MineralVolumeCalculator
    {
        public static MineralVolumeSummary Calculate(
            VolumeResult excavation,
            IReadOnlyList<MineralSeamInput> seams,
            IReadOnlyList<MineralSectionInput> confirmedAreas,
            double relativeAreaDifferenceThreshold = .40)
        {
            if (excavation == null || excavation.Intervals == null || excavation.Intervals.Count == 0)
                throw new InvalidOperationException("Cần tính khối lượng đào/đắp hợp lệ trước khi tính khoáng sản.");
            if (seams == null || seams.Count == 0)
                throw new InvalidOperationException("Chưa có vỉa than/thân quặng được kích hoạt.");
            if (confirmedAreas == null)
                throw new ArgumentNullException(nameof(confirmedAreas));
            if (double.IsNaN(relativeAreaDifferenceThreshold) ||
                double.IsInfinity(relativeAreaDifferenceThreshold) ||
                relativeAreaDifferenceThreshold < 0 || relativeAreaDifferenceThreshold > 1)
                throw new ArgumentOutOfRangeException(nameof(relativeAreaDifferenceThreshold));

            var seamById = new Dictionary<string,MineralSeamInput>(StringComparer.OrdinalIgnoreCase);
            var seamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var seam in seams)
            {
                if (seam == null || string.IsNullOrWhiteSpace(seam.Id) ||
                    string.IsNullOrWhiteSpace(seam.Name) ||
                    seamById.ContainsKey(seam.Id) ||
                    !seamNames.Add(seam.Name.Trim()))
                    throw new InvalidOperationException("Tên hoặc mã định danh vỉa bị thiếu hoặc trùng.");
                seamById.Add(seam.Id,seam);
                if (!Finite(seam.DensityTonPerCubicMetre) || seam.DensityTonPerCubicMetre <= 0)
                    throw new InvalidOperationException(seam.Name+": thể trọng phải > 0 t/m³ để quy đổi tấn.");
            }

            var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var interval in excavation.Intervals)
            {
                if (interval == null || string.IsNullOrWhiteSpace(interval.StartSection) ||
                    string.IsNullOrWhiteSpace(interval.EndSection) ||
                    !Finite(interval.Distance) || interval.Distance <= 0 ||
                    !Finite(interval.CutVolume) || interval.CutVolume < 0)
                    throw new InvalidOperationException("Danh sách khoảng mặt cắt đào không hợp lệ.");
                sections.Add(interval.StartSection);
                sections.Add(interval.EndSection);
            }

            var bySectionAndBody = new Dictionary<string,MineralSectionInput>(StringComparer.OrdinalIgnoreCase);
            foreach (var sample in confirmedAreas)
            {
                if (sample == null || string.IsNullOrWhiteSpace(sample.BodyId) ||
                    string.IsNullOrWhiteSpace(sample.SectionName) ||
                    !seamById.ContainsKey(sample.BodyId) ||
                    !sections.Contains(sample.SectionName) ||
                    !Finite(sample.AreaSquareMetres) || sample.AreaSquareMetres < 0)
                    throw new InvalidOperationException("Mẫu diện tích khoáng sản không hợp lệ hoặc tham chiếu vỉa/mặt cắt không tồn tại.");
                if (sample.AbsentConfirmed && sample.AreaSquareMetres != 0)
                    throw new InvalidOperationException(sample.SectionName+": vỉa xác nhận vắng mặt nhưng vẫn có diện tích.");
                if (!sample.AbsentConfirmed && sample.AreaSquareMetres <= 0)
                    throw new InvalidOperationException(sample.SectionName+": diện tích 0 cần xác nhận vỉa vắng mặt.");
                var key = sample.BodyId+"|"+sample.SectionName;
                if (bySectionAndBody.ContainsKey(key))
                    throw new InvalidOperationException("Một vỉa bị ghi diện tích lặp trong "+sample.SectionName+". Hãy cộng các vùng trên cùng mặt cắt trước.");
                bySectionAndBody.Add(key,sample);
            }
            foreach (var body in seams)
                foreach (var section in sections)
                    if (!bySectionAndBody.ContainsKey(body.Id+"|"+section))
                        throw new InvalidOperationException(
                            body.Name+" tại "+section+" chưa gán vùng khoáng sản hoặc xác nhận không có vỉa. "+
                            "Không được tự coi thiếu dữ liệu là diện tích bằng 0.");

            // Validated section areas must be bounded by the actual excavated
            // section, including all bodies. This does not replace polygon
            // overlap validation in AutoCAD: that gate happens before this call.
            var cutAreas = (excavation.SectionAreas ?? Array.Empty<SectionBandAreas>())
                .Where(x=>x != null)
                .GroupBy(x=>x.SectionName,StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x=>x.Key,x=>x.First().CutArea,StringComparer.OrdinalIgnoreCase);
            foreach (var section in sections)
            {
                if (!cutAreas.TryGetValue(section,out double cut))
                    throw new InvalidOperationException("Thiếu kết quả diện tích đào gốc của "+section+".");
                double mineralArea=seams.Sum(b=>bySectionAndBody[b.Id+"|"+section].AreaSquareMetres);
                if (mineralArea > cut + Math.Max(1e-6, cut * 1e-6))
                    throw new InvalidOperationException(section+": tổng diện tích khoáng sản "+
                        mineralArea.ToString("0.###")+" m² vượt diện tích đào "+cut.ToString("0.###")+" m².");
            }

            var results=new List<MineralIntervalResult>();
            foreach (var interval in excavation.Intervals)
            {
                double mineralInterval=0;
                foreach (var seam in seams)
                {
                    var a=bySectionAndBody[seam.Id+"|"+interval.StartSection];
                    var b=bySectionAndBody[seam.Id+"|"+interval.EndSection];
                    var formula=SectionVolumeCalculator.SelectFormula(a.AreaSquareMetres,
                        b.AreaSquareMetres,relativeAreaDifferenceThreshold);
                    double volume=SectionVolumeCalculator.VolumeByFormula(a.AreaSquareMetres,
                        b.AreaSquareMetres,interval.Distance,formula);
                    mineralInterval+=volume;
                    results.Add(new MineralIntervalResult
                    {
                        BodyId=seam.Id,BodyName=seam.Name,
                        StartSection=interval.StartSection,EndSection=interval.EndSection,
                        DistanceMetres=interval.Distance,
                        StartAreaSquareMetres=a.AreaSquareMetres,
                        EndAreaSquareMetres=b.AreaSquareMetres,
                        Formula=formula,VolumeCubicMetres=volume,
                        MassTonnes=volume*seam.DensityTonPerCubicMetre
                    });
                }
                if (mineralInterval > interval.CutVolume +
                    Math.Max(1e-5, interval.CutVolume * 1e-6))
                    throw new InvalidOperationException(
                        interval.StartSection+"–"+interval.EndSection+
                        ": khối lượng khoáng sản vượt khối lượng đào cùng khoảng mặt cắt. "+
                        "Kiểm tra vùng địa chất, phương pháp nội suy và khối lượng đào.");
            }

            var bodies=seams.Select(b=>
            {
                double v=results.Where(x=>x.BodyId==b.Id).Sum(x=>x.VolumeCubicMetres);
                return new MineralBodyResult
                {
                    Id=b.Id,Name=b.Name,DensityTonPerCubicMetre=b.DensityTonPerCubicMetre,
                    VolumeCubicMetres=v,MassTonnes=v*b.DensityTonPerCubicMetre
                };
            }).ToList();
            return new MineralVolumeSummary
            {
                Intervals=results,
                Bodies=bodies,
                TotalCutCubicMetres=excavation.TotalCutVolume
            };
        }

        private static bool Finite(double x) => !double.IsNaN(x)&&!double.IsInfinity(x);
    }
}
