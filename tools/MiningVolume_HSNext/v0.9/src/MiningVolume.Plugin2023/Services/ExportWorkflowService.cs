using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiningVolume.Core.Model;
using MiningVolume.Core.Reporting;
using MiningVolume.Core.Sections;
using MiningVolume.Core.Volumes;

namespace MiningVolume2023.Services
{
    public sealed class ExportOptions
    {
        public bool IncludeSourceXyz { get; set; } = true;
        public bool IncludeSectionData { get; set; } = true;
        public bool IncludeSectionAreas { get; set; } = true;
        public bool IncludeIntervals { get; set; } = true;
        public bool IncludeLevels { get; set; } = true;
        public bool IncludeSummary { get; set; } = true;
        public bool IncludeWarnings { get; set; } = true;
        public string DeveloperName { get; set; } = string.Empty;
        public string DeveloperContact { get; set; } = string.Empty;
    }

    public static class ExportWorkflowService
    {
        public static void ExportXlsx(string filePath, ProjectState state, ExportOptions options)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Chưa chọn file Excel để lưu.", nameof(filePath));
            if (state.SectionProfiles.Count == 0) throw new InvalidOperationException("Chưa có dữ liệu mặt cắt để xuất Excel.");
            if (state.VolumeResult == null) throw new InvalidOperationException("Chưa tính khối lượng. Hãy thực hiện Bước 8 trước khi xuất Excel.");

            var report = BuildReport(state, options);
            SimpleXlsxWriter.Write(filePath, report);
        }

        public static SpreadsheetReport BuildReport(ProjectState state, ExportOptions options)
        {
            var now = DateTime.Now;
            var report = new SpreadsheetReport
            {
                Title = "MiningVolume 2023 - Báo cáo mặt cắt và khối lượng",
                Creator = string.IsNullOrWhiteSpace(options.DeveloperName) ? "MiningVolume 2023" : options.DeveloperName.Trim(),
                CreatedAt = now
            };

            report.Sheets.Add(BuildInfoSheet(state, options, now));
            if (options.IncludeSourceXyz) report.Sheets.Add(BuildSourceXyzSheet(state));
            if (options.IncludeSectionData) report.Sheets.Add(BuildSectionDataSheet(state));
            if (options.IncludeSectionAreas) report.Sheets.Add(BuildSectionParameterSheet(state));
            if (options.IncludeIntervals) report.Sheets.Add(BuildIntervalSheet(state.VolumeResult));
            if (options.IncludeLevels) report.Sheets.Add(BuildLevelSheet(state.VolumeResult));
            if (options.IncludeSummary) report.Sheets.Add(BuildSummarySheet(state, options));
            if (options.IncludeWarnings) report.Sheets.Add(BuildWarningSheet(state));
            return report;
        }

        private static ReportSheet BuildInfoSheet(ProjectState st, ExportOptions opt, DateTime now)
        {
            var s = NewSheet("Thông tin", "THÔNG TIN BÁO CÁO MINING VOLUME", new[] { "Nội dung", "Giá trị" }, 32, 60);
            s.Landscape = false;
            s.Notes.Add("Báo cáo được xuất tự động từ MiningVolume 2023 - HS-Next.");
            AddKV(s, "Thời gian xuất", now.ToString("dd/MM/yyyy HH:mm:ss"));
            AddKV(s, "Người phát triển phần mềm", EmptyAsNotSet(opt.DeveloperName));
            AddKV(s, "Địa chỉ liên hệ / email / điện thoại", EmptyAsNotSet(opt.DeveloperContact));
            AddKV(s, "Layer hiện trạng", EmptyAsNotSet(st.Existing.Layer));
            AddKV(s, "Layer thiết kế", EmptyAsNotSet(st.Design.Layer));
            AddKV(s, "Số đối tượng hiện trạng", st.Existing.EntityCount.ToString("n0"));
            AddKV(s, "Số đỉnh đang dùng - hiện trạng", st.Existing.ActiveVertexCount.ToString("n0"));
            AddKV(s, "Số tam giác TIN hiện trạng", (st.Existing.Tin?.Triangles?.Count ?? 0).ToString("n0"));
            AddKV(s, "Số đối tượng thiết kế", st.Design.EntityCount.ToString("n0"));
            AddKV(s, "Số đỉnh đang dùng - thiết kế", st.Design.ActiveVertexCount.ToString("n0"));
            AddKV(s, "Số tam giác TIN thiết kế", (st.Design.Tin?.Triangles?.Count ?? 0).ToString("n0"));
            AddKV(s, "Số tuyến mặt cắt", (st.SectionSystem?.Lines?.Count ?? 0).ToString("n0"));
            AddKV(s, "Khoảng cách mặt cắt, m", st.SectionSpacing.ToString("0.###"));
            AddKV(s, "Mức tính từ", FormatLevel(st.FromLevel));
            AddKV(s, "Mức tính đến", FormatLevel(st.ToLevel));
            AddKV(s, "Bước chia tầng, m", st.LevelStep.ToString("0.###"));
            AddKV(s, "Công thức chính", "Prismoid: V = L/6 × (F1 + 4Fm + F2)");
            AddKV(s, "Công thức dự phòng", "Trung bình hai đầu: V = L/2 × (F1 + F2), chỉ dùng khi không lấy được mặt cắt giữa");
            return s;
        }

        private static ReportSheet BuildSourceXyzSheet(ProjectState st)
        {
            var s = NewSheet("Dữ liệu XYZ", "DỮ LIỆU X-Y-Z ĐÃ NẠP VÀO MÔ HÌNH",
                new[] { "Mô hình", "Loại đối tượng", "Layer", "Handle", "Đỉnh", "X", "Y", "Z", "Đang sử dụng" },
                14, 18, 22, 14, 9, 16, 16, 14, 15);
            s.Notes.Add("Các dòng 'Không' không tham gia dựng mô hình TIN tại thời điểm xuất báo cáo.");
            AddModelRows(s, "Hiện trạng", st.Existing.Source);
            AddModelRows(s, "Thiết kế", st.Design.Source);
            return s;
        }

        private static void AddModelRows(ReportSheet s, string modelName, SurfaceModel model)
        {
            if (model == null) return;
            foreach (var e in model.Entities)
            {
                foreach (var v in e.Vertices)
                {
                    bool active = e.IsEnabled && v.IsEnabled;
                    s.Rows.Add(new[]
                    {
                        ReportCell.Text(modelName), ReportCell.Text(EntityTypeName(e.Type)), ReportCell.Text(e.Layer), ReportCell.Text(e.Handle),
                        ReportCell.Int(v.Index), ReportCell.N3(v.Position.X), ReportCell.N3(v.Position.Y), ReportCell.N3(v.Position.Z), ReportCell.Text(active ? "Có" : "Không")
                    });
                }
            }
        }

        private static ReportSheet BuildSectionDataSheet(ProjectState st)
        {
            var s = NewSheet("Dữ liệu mặt cắt", "DỮ LIỆU HÌNH HỌC CÁC MẶT CẮT",
                new[] { "Mặt cắt", "Đoạn", "S0, m", "X0", "Y0", "Z hiện trạng 0", "Z thiết kế 0", "S1, m", "X1", "Y1", "Z hiện trạng 1", "Z thiết kế 1" },
                13, 9, 12, 16, 16, 16, 16, 12, 16, 16, 16, 16);
            s.Notes.Add("Tọa độ X-Y là tọa độ bản đồ WCS; S là khoảng cách dọc theo tuyến mặt cắt.");
            foreach (var p in st.SectionProfiles.OrderBy(x => x.Line.Index))
            {
                int n = 1;
                foreach (var g in p.Segments)
                {
                    s.Rows.Add(new[]
                    {
                        ReportCell.Text(p.Line.Name), ReportCell.Int(n++), ReportCell.N3(g.S0), ReportCell.N3(g.Map0.X), ReportCell.N3(g.Map0.Y),
                        ReportCell.N3(g.ExistingZ0), ReportCell.N3(g.DesignZ0), ReportCell.N3(g.S1), ReportCell.N3(g.Map1.X), ReportCell.N3(g.Map1.Y),
                        ReportCell.N3(g.ExistingZ1), ReportCell.N3(g.DesignZ1)
                    });
                }
            }
            return s;
        }

        private static ReportSheet BuildSectionParameterSheet(ProjectState st)
        {
            var s = NewSheet("Thông số mặt cắt", "BẢNG THÔNG SỐ VÀ DIỆN TÍCH MẶT CẮT",
                new[] { "STT", "Tên mặt cắt", "Lý trình / offset, m", "Chiều dài tuyến, m", "Số đoạn ranh", "Cao độ min", "Cao độ max", "F đào, m²", "F đắp, m²", "Cảnh báo" },
                8, 14, 18, 18, 13, 14, 14, 15, 15, 35);
            int i = 1;
            foreach (var p in st.SectionProfiles.OrderBy(x => x.Line.Index))
            {
                s.Rows.Add(new[]
                {
                    ReportCell.Int(i++), ReportCell.Text(p.Line.Name), ReportCell.N3(p.Line.Offset), ReportCell.N3(p.Line.TotalLength),
                    ReportCell.Int(p.Line.Spans.Count), ReportCell.N3(p.MinZ), ReportCell.N3(p.MaxZ), ReportCell.N2(p.CutArea), ReportCell.N2(p.FillArea),
                    ReportCell.Text(p.Warnings == null || p.Warnings.Count == 0 ? string.Empty : string.Join(" | ", p.Warnings))
                });
            }
            return s;
        }

        private static ReportSheet BuildIntervalSheet(VolumeResult r)
        {
            var s = NewSheet("Khối lượng chi tiết", "KHỐI LƯỢNG CHI TIẾT GIỮA CÁC MẶT CẮT",
                new[] { "Đoạn", "MC đầu", "MC cuối", "L, m", "F đào đầu", "F đào giữa", "F đào cuối", "V đào, m³", "F đắp đầu", "F đắp giữa", "F đắp cuối", "V đắp, m³", "Công thức đào", "Công thức đắp", "Ghi chú" },
                9, 13, 13, 12, 14, 14, 14, 16, 14, 14, 14, 16, 16, 16, 32);
            s.Notes.Add("Prismoid được ưu tiên; trung bình hai đầu chỉ sử dụng khi không lấy được mặt cắt giữa thực từ TIN.");
            foreach (var x in r.Intervals)
            {
                s.Rows.Add(new[]
                {
                    ReportCell.Int(x.Index), ReportCell.Text(x.StartSection), ReportCell.Text(x.EndSection), ReportCell.N3(x.Distance),
                    ReportCell.N2(x.CutAreaStart), ReportCell.N2(x.CutAreaMid), ReportCell.N2(x.CutAreaEnd), ReportCell.N2(x.CutVolume),
                    ReportCell.N2(x.FillAreaStart), ReportCell.N2(x.FillAreaMid), ReportCell.N2(x.FillAreaEnd), ReportCell.N2(x.FillVolume),
                    ReportCell.Text(FormulaName(x.CutFormula)), ReportCell.Text(FormulaName(x.FillFormula)), ReportCell.Text(x.Note ?? string.Empty)
                });
            }
            s.Rows.Add(new[]
            {
                ReportCell.Text("TỔNG", true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true),
                ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.N2(r.TotalCutVolume, true),
                ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.N2(r.TotalFillVolume, true),
                ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true)
            });
            return s;
        }

        private static ReportSheet BuildLevelSheet(VolumeResult r)
        {
            var s = NewSheet("Tổng hợp theo tầng", "TỔNG HỢP KHỐI LƯỢNG THEO TẦNG / MỨC",
                new[] { "STT", "Tầng / mức", "Cao độ dưới", "Cao độ trên", "V đào, m³", "V đắp, m³", "Chênh lệch, m³" },
                8, 18, 14, 14, 18, 18, 18);
            int i = 1;
            foreach (var x in r.Levels)
            {
                s.Rows.Add(new[]
                {
                    ReportCell.Int(i++), ReportCell.Text(x.Band.Name), ReportCell.N3(x.Band.LowerZ), ReportCell.N3(x.Band.UpperZ),
                    ReportCell.N2(x.CutVolume), ReportCell.N2(x.FillVolume), ReportCell.N2(x.NetVolume)
                });
            }
            s.Rows.Add(new[]
            {
                ReportCell.Text("TỔNG", true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true),
                ReportCell.N2(r.Levels.Sum(x => x.CutVolume), true), ReportCell.N2(r.Levels.Sum(x => x.FillVolume), true), ReportCell.N2(r.Levels.Sum(x => x.NetVolume), true)
            });
            return s;
        }

        private static ReportSheet BuildSummarySheet(ProjectState st, ExportOptions opt)
        {
            var r = st.VolumeResult;
            double levelCut = r.Levels.Sum(x => x.CutVolume);
            double levelFill = r.Levels.Sum(x => x.FillVolume);
            var s = NewSheet("Tổng khối", "BẢNG TỔNG HỢP KHỐI LƯỢNG",
                new[] { "Chỉ tiêu", "Đơn vị", "Giá trị" }, 40, 14, 22);
            s.Landscape = false;
            s.Notes.Add("Người phát triển: " + EmptyAsNotSet(opt.DeveloperName));
            s.Notes.Add("Liên hệ: " + EmptyAsNotSet(opt.DeveloperContact));
            s.Rows.Add(new[] { ReportCell.Text("Tổng khối lượng đào", true), ReportCell.Text("m³"), ReportCell.N2(r.TotalCutVolume, true) });
            s.Rows.Add(new[] { ReportCell.Text("Tổng khối lượng đắp", true), ReportCell.Text("m³"), ReportCell.N2(r.TotalFillVolume, true) });
            s.Rows.Add(new[] { ReportCell.Text("Chênh lệch đào - đắp", true), ReportCell.Text("m³"), ReportCell.N2(r.NetVolume, true) });
            s.Rows.Add(new[] { ReportCell.Text("Tổng theo tầng - đào"), ReportCell.Text("m³"), ReportCell.N2(levelCut) });
            s.Rows.Add(new[] { ReportCell.Text("Tổng theo tầng - đắp"), ReportCell.Text("m³"), ReportCell.N2(levelFill) });
            s.Rows.Add(new[] { ReportCell.Text("Sai lệch kiểm soát đào (chi tiết - theo tầng)"), ReportCell.Text("m³"), ReportCell.N3(r.TotalCutVolume - levelCut) });
            s.Rows.Add(new[] { ReportCell.Text("Sai lệch kiểm soát đắp (chi tiết - theo tầng)"), ReportCell.Text("m³"), ReportCell.N3(r.TotalFillVolume - levelFill) });
            s.Rows.Add(new[] { ReportCell.Text("Số khoảng mặt cắt"), ReportCell.Text("khoảng"), ReportCell.Int(r.Intervals.Count) });
            s.Rows.Add(new[] { ReportCell.Text("Số tầng / mức"), ReportCell.Text("tầng"), ReportCell.Int(r.Levels.Count) });
            return s;
        }

        private static ReportSheet BuildWarningSheet(ProjectState st)
        {
            var s = NewSheet("Cảnh báo", "CẢNH BÁO VÀ GHI CHÚ KIỂM SOÁT",
                new[] { "Nhóm", "Đối tượng", "Nội dung" }, 20, 20, 80);
            if (st.SectionSystem?.Warnings != null)
                foreach (var w in st.SectionSystem.Warnings) s.Rows.Add(new[] { ReportCell.Text("Hệ mặt cắt"), ReportCell.Text(string.Empty), ReportCell.Text(w) });
            foreach (var p in st.SectionProfiles)
                if (p.Warnings != null) foreach (var w in p.Warnings) s.Rows.Add(new[] { ReportCell.Text("Mặt cắt"), ReportCell.Text(p.Line.Name), ReportCell.Text(w) });
            if (st.VolumeResult?.Warnings != null)
                foreach (var w in st.VolumeResult.Warnings) s.Rows.Add(new[] { ReportCell.Text("Khối lượng"), ReportCell.Text(string.Empty), ReportCell.Text(w) });
            if (s.Rows.Count == 0) s.Rows.Add(new[] { ReportCell.Text("Kiểm soát"), ReportCell.Text(string.Empty), ReportCell.Text("Không có cảnh báo tại thời điểm xuất báo cáo.") });
            return s;
        }

        private static ReportSheet NewSheet(string name, string title, IEnumerable<string> headers, params double[] widths)
        {
            var s = new ReportSheet { Name = name, Title = title };
            if (headers != null) s.Headers.AddRange(headers);
            if (widths != null) s.ColumnWidths.AddRange(widths);
            return s;
        }
        private static void AddKV(ReportSheet s, string key, string value) => s.Rows.Add(new[] { ReportCell.Text(key, true), ReportCell.Text(value) });
        private static string EmptyAsNotSet(string s) => string.IsNullOrWhiteSpace(s) ? "Chưa thiết lập" : s.Trim();
        private static string FormulaName(VolumeFormulaKind x) => x == VolumeFormulaKind.Prismoidal ? "Prismoid" : "TB hai đầu";
        private static string FormatLevel(double z) => z > 0 ? "+" + z.ToString("0.###") : z.ToString("0.###");
        private static string EntityTypeName(SourceEntityType type)
        {
            switch (type)
            {
                case SourceEntityType.Point: return "Point";
                case SourceEntityType.Line: return "Line";
                case SourceEntityType.LwPolyline: return "LWPolyline";
                case SourceEntityType.Polyline2d: return "Polyline 2D";
                case SourceEntityType.Polyline3d: return "Polyline 3D";
                case SourceEntityType.Contour: return "Đường đồng mức";
                default: return type.ToString();
            }
        }
    }
}