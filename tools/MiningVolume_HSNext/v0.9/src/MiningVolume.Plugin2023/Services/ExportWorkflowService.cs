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
        public const string FixedDeveloperName = "Bùi Thế Nam";
        public const string FixedDeveloperContact = "ĐT/Zalo: 0967280686";

        public bool IncludeSourceXyz { get; set; } = true;
        public bool IncludeSectionData { get; set; } = true;
        public bool IncludeSectionAreas { get; set; } = true;
        public bool IncludeIntervals { get; set; } = true;
        public bool IncludeLevels { get; set; } = true;
        public bool IncludeSummary { get; set; } = true;
        public bool IncludeWarnings { get; set; } = true;
        public string DeveloperName { get; set; } = FixedDeveloperName;
        public string DeveloperContact { get; set; } = FixedDeveloperContact;
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
            WriteXlsx(filePath, report);
        }

        public const string TkvParentHeading = "TẬP ĐOÀN CÔNG NGHIỆP THAN - KHOÁNG SẢN VIỆT NAM";

        // Stacks the organizational hierarchy in the existing first title row
        // instead of inserting rows: ALL Excel formula addresses remain intact.
        // Do not falsely affiliate users selecting "outside TKV" with the group.
        public static void ApplyOrganizationHeader(SpreadsheetReport report, ProjectState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            ApplyOrganizationHeader(report, state.MiningUnitGroup, state.MiningUnitName);
        }

        public static void ApplyOrganizationHeader(SpreadsheetReport report,
            string unitGroup, string unitName)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            string unit = (unitName ?? string.Empty).Trim();
            if (string.Equals(unitGroup, MiningUnitCatalog.OutsideTkv,
                    StringComparison.Ordinal) && unit.Length == 0)
                throw new InvalidOperationException(
                    "Đã chọn 'Đơn vị khác (ngoài TKV)'. Hãy nhập tên đơn vị trước khi xuất Excel.");
            if (unit.Length == 0) return;
            bool outside = string.Equals(unitGroup, MiningUnitCatalog.OutsideTkv,
                StringComparison.Ordinal);
            string prefix = outside ? unit : TkvParentHeading + "\n" + unit;
            foreach (var sheet in report.Sheets)
            {
                if (sheet == null || string.IsNullOrWhiteSpace(sheet.Title)) continue;
                if (string.IsNullOrWhiteSpace(sheet.OrganizationBaseTitle))
                    sheet.OrganizationBaseTitle = sheet.Title;
                // Each workbook receives a fixed organization snapshot at
                // REPORT-BUILD time, before any asynchronous file output.
                sheet.Title = prefix + "\n" + sheet.OrganizationBaseTitle;
            }
        }

        public static void WriteXlsx(string filePath, SpreadsheetReport report)
        {
            // Applies to section volumes, grid volumes and seam/mineral reports,
            // including their formula-linked detail/summary worksheets.
            // Never consult mutable ProjectState.Current on the Excel I/O thread:
            // an AutoCAD DWG switch could silently relabel an approved report.
            var target=Path.GetFullPath(filePath);
            var directory=Path.GetDirectoryName(target);
            Directory.CreateDirectory(directory);
            var staging=Path.Combine(directory,"."+Path.GetFileName(target)+"."+Guid.NewGuid().ToString("N")+".tmp");
            try {
                SimpleXlsxWriter.Write(staging, report);
                XlsxBrandingInjector.Apply(staging, report);
                if(File.Exists(target))File.Replace(staging,target,null);
                else File.Move(staging,target);
            } finally {if(File.Exists(staging))File.Delete(staging);}
        }

        public static SpreadsheetReport BuildReport(ProjectState state, ExportOptions options)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (options == null) throw new ArgumentNullException(nameof(options));
            // Both fields come from the Project selection and must be preserved
            // verbatim in Excel, including a user-entered non-TKV organization.
            if (string.Equals(state.MiningUnitGroup, MiningUnitCatalog.OutsideTkv, StringComparison.Ordinal) &&
                string.IsNullOrWhiteSpace(state.MiningUnitName))
                throw new InvalidOperationException(
                    "Đã chọn 'Đơn vị khác (ngoài TKV)'. Hãy nhập tên đơn vị trước khi xuất báo cáo Excel.");
            string frozenGroup = state.MiningUnitGroup;
            string frozenUnit = state.MiningUnitName;
            var now = DateTime.Now;
            var report = new SpreadsheetReport
            {
                Title = "IMSAT VOLUME - Báo cáo mặt cắt và khối lượng",
                Creator = ExportOptions.FixedDeveloperName,
                CreatedAt = now
            };

            report.Sheets.Add(BuildInfoSheet(state, options, now, frozenGroup, frozenUnit));
            if (options.IncludeSourceXyz) report.Sheets.Add(BuildSourceXyzSheet(state));
            if (options.IncludeSectionData) report.Sheets.Add(BuildSectionDataSheet(state));
            if (options.IncludeSectionAreas) report.Sheets.Add(BuildSectionParameterSheet(state));
            if ((options.IncludeIntervals || options.IncludeLevels) && state.VolumeResult.Intervals.Any(x=>x.BandVolumes.Count>0))
                report.Sheets.Add(BuildBandIntervalSheet(state.VolumeResult));
            if (options.IncludeIntervals) report.Sheets.Add(BuildIntervalSheet(state.VolumeResult));
            if (options.IncludeLevels) report.Sheets.Add(BuildLevelSheet(state.VolumeResult));
            if (options.IncludeSummary) report.Sheets.Add(BuildSummarySheet(state, options, frozenGroup, frozenUnit));
            if (options.IncludeWarnings) report.Sheets.Add(BuildWarningSheet(state));
            ApplyOrganizationHeader(report, frozenGroup, frozenUnit);
            return report;
        }

        private static ReportSheet BuildInfoSheet(ProjectState st, ExportOptions opt,
            DateTime now, string unitGroup, string unitName)
        {
            var s = NewSheet("Thông tin", "THÔNG TIN BÁO CÁO IMSAT VOLUME", new[] { "Nội dung", "Giá trị" }, 45, 45);
            s.Landscape = false;
            s.AutoFilter = false;
            s.Notes.Add("Báo cáo được xuất tự động từ IMSAT VOLUME.");
            if (unitGroup != MiningUnitCatalog.OutsideTkv)
                AddKV(s, "Đơn vị cấp trên", TkvParentHeading);
            AddKV(s, "Đơn vị tính toán", EmptyAsNotSet(unitName));
            AddKV(s, "Loại đơn vị", unitGroup);
            AddKV(s, "Thông số mặt cắt", st.SectionParameterMode);
            AddKV(s, "Tỷ lệ mặt cắt (ngang / đứng)",
                "1:" + st.HorizontalScale.ToString("N0") +
                " / 1:" + st.VerticalScale.ToString("N0"));
            AddKV(s, "Bước tuyến thiết lập, m", st.SectionSpacing.ToString("0.###"));
            double? observedGap = MaxActualSectionGap(st.SectionSystem);
            if (observedGap.HasValue)
                AddKV(s, "Khoảng cách tuyến lớn nhất thực tế, m",
                    observedGap.Value.ToString("0.###"));
            AddKV(s, "Bước chia trục Z, m", st.SectionAxisElevationInterval.ToString("0.###"));
            AddKV(s, "Mức chia tầng tính khối lượng, m", st.LevelStep.ToString("0.###"));
            AddKV(s, "Kỳ tính khối lượng", st.VolumePeriodLabel);
            AddKV(s, "Thời điểm tính khối lượng", FormatCalculationTime(st.VolumeCalculatedUtc));
            AddKV(s, "Thời gian thực tính khối lượng, giây", FormatDuration(st.VolumeCalculationSeconds));
            AddKV(s, "Thời gian xuất Excel", now.ToString("dd/MM/yyyy HH:mm:ss"));
            AddKV(s, "Người phát triển phần mềm", ExportOptions.FixedDeveloperName);
            AddKV(s, "ĐT/Zalo", "0967280686");
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
            AddKV(s, "Quy tắc công thức theo từng dải cao độ", "1 đầu bằng 0: hình chóp; chênh F1-F2 ≤ 40%: TB hai đầu; chênh > 40%: hình chóp cụt");
            AddKV(s, "Nguyên tắc Excel", "Công thức tham chiếu trực tiếp các ô L, F1, F2 và tự đổi nhánh khi dữ liệu ô thay đổi");
            return s;
        }

        // Do not mislabel a manually moved/deleted section system by only
        // showing the originally requested station step. Include the actual
        // maximum projected gap, including both polygon-boundary margins.
        private static double? MaxActualSectionGap(SectionSystem system)
        {
            if (system == null || system.Lines == null ||
                system.Lines.Count == 0 || system.Boundary == null ||
                system.Boundary.Count == 0)
                return null;
            var stations = system.Lines.Select(x => x.Offset).OrderBy(x => x).ToArray();
            var projected = system.Boundary.Select(x =>
                x.X * system.Normal.X + x.Y * system.Normal.Y).ToArray();
            double gap = Math.Max(stations[0] - projected.Min(),
                projected.Max() - stations[stations.Length - 1]);
            for (int i = 1; i < stations.Length; i++)
                gap = Math.Max(gap, stations[i] - stations[i - 1]);
            return Math.Max(0.0, gap);
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
            s.Notes.Add("Các ô V đào/V đắp là công thức Excel thực: 1 đầu bằng 0 dùng hình chóp; chênh diện tích ≤ 40% dùng TB hai đầu; chênh > 40% dùng hình chóp cụt.");
            s.Notes.Add("Sửa L/F1/F2 tại Chi tiết tầng: tổng theo khoảng và theo tầng cùng liên kết, không nội suy lại TIN cho mặt cắt đã sửa tay. F giữa để trống khi không có số đo phù hợp.");
            const int firstDataRow = 4; // tiêu đề + ghi chú + hàng tiêu đề cột
            int excelRow = firstDataRow;
            int bandRow=4;
            foreach (var x in r.Intervals)
            {
                int bandLast=bandRow+x.BandVolumes.Count-1;
                string BandSum(string column)=>"SUM('Chi tiết tầng'!"+column+bandRow+":"+column+bandLast+")";
                s.Rows.Add(new[]
                {
                    ReportCell.Int(x.Index), ReportCell.Text(x.StartSection), ReportCell.Text(x.EndSection), ReportCell.N3(x.Distance),
                    x.BandVolumes.Count>0?ReportCell.Formula2(BandSum("F")):ReportCell.N2(x.CutAreaStart),
                    x.HasMidArea?ReportCell.N2(x.CutAreaMid):ReportCell.Text(""),
                    x.BandVolumes.Count>0?ReportCell.Formula2(BandSum("G")):ReportCell.N2(x.CutAreaEnd),
                    ReportCell.Formula2(x.BandVolumes.Count>0?BandSum("H"):AdaptiveVolumeFormula(excelRow, "D", "E", "G")),
                    x.BandVolumes.Count>0?ReportCell.Formula2(BandSum("I")):ReportCell.N2(x.FillAreaStart),
                    x.HasMidArea?ReportCell.N2(x.FillAreaMid):ReportCell.Text(""),
                    x.BandVolumes.Count>0?ReportCell.Formula2(BandSum("J")):ReportCell.N2(x.FillAreaEnd),
                    ReportCell.Formula2(x.BandVolumes.Count>0?BandSum("K"):AdaptiveVolumeFormula(excelRow, "D", "I", "K")),
                    ReportCell.Text(x.BandVolumes.Count>0?"Tổng các tầng":FormulaName(x.CutFormula)),
                    ReportCell.Text(x.BandVolumes.Count>0?"Tổng các tầng":FormulaName(x.FillFormula)), ReportCell.Text(x.Note ?? string.Empty)
                });
                bandRow+=x.BandVolumes.Count;
                excelRow++;
            }

            int lastDataRow = excelRow - 1;
            s.Rows.Add(new[]
            {
                ReportCell.Text("TỔNG", true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true),
                ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true),
                ReportCell.Formula2(SumFormula("H", firstDataRow, lastDataRow), true),
                ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true),
                ReportCell.Formula2(SumFormula("L", firstDataRow, lastDataRow), true),
                ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true)
            });
            return s;
        }

        private static ReportSheet BuildBandIntervalSheet(VolumeResult r)
        {
            var s=NewSheet("Chi tiết tầng","KHỐI LƯỢNG TỪNG KHOẢNG VÀ DẢI CAO ĐỘ",
                new[]{"Khoảng","Mã tầng","MC đầu","MC cuối","L (m)","F đào đầu","F đào cuối","V đào (m³)","F đắp đầu","F đắp cuối","V đắp (m³)"},
                10,10,15,15,12,16,16,18,16,16,18);
            s.Notes.Add("Khối lượng chính thức là tổng đóng góp của từng dải. Sửa tham số tại đây để tính lại các bảng tổng hợp.");
            int row=4;
            foreach(var interval in r.Intervals)foreach(var b in interval.BandVolumes) {
                s.Rows.Add(new[]{ReportCell.Int(interval.Index),ReportCell.Int(b.Index),ReportCell.Text(b.StartSection),
                    ReportCell.Text(b.EndSection),ReportCell.N3(b.Distance),ReportCell.N3(b.CutAreaStart),ReportCell.N3(b.CutAreaEnd),
                    ReportCell.Formula3(AdaptiveVolumeFormula(row,"E","F","G")),ReportCell.N3(b.FillAreaStart),ReportCell.N3(b.FillAreaEnd),
                    ReportCell.Formula3(AdaptiveVolumeFormula(row,"E","I","J"))});row++;
            }
            return s;
        }

        private static ReportCell LevelCell(VolumeResult r,LevelVolumeSummary level,string column,double fallback)
        {
            int count=r.Intervals.Sum(x=>x.BandVolumes.Count);
            return count==0?ReportCell.N2(fallback):ReportCell.Formula2("SUMIF('Chi tiết tầng'!$B$4:$B$"+(3+count)+","+
                level.Band.Index+",'Chi tiết tầng'!$"+column+"$4:$"+column+"$"+(3+count)+")");
        }

        private static ReportSheet BuildLevelSheet(VolumeResult r)
        {
            var s = NewSheet("Tổng hợp theo tầng", "TỔNG HỢP KHỐI LƯỢNG THEO TẦNG / MỨC",
                new[] { "STT", "Tầng / mức", "Cao độ dưới", "Cao độ trên", "V đào, m³", "V đắp, m³", "Chênh lệch, m³" },
                8, 18, 14, 14, 18, 18, 18);
            const int firstDataRow = 3; // tiêu đề + hàng tiêu đề cột
            int excelRow = firstDataRow;
            int i = 1;
            foreach (var x in r.Levels)
            {
                s.Rows.Add(new[]
                {
                    ReportCell.Int(i++), ReportCell.Text(x.Band.Name), ReportCell.N3(x.Band.LowerZ), ReportCell.N3(x.Band.UpperZ),
                    LevelCell(r,x,"H",x.CutVolume), LevelCell(r,x,"K",x.FillVolume), ReportCell.Formula2("=E" + excelRow + "-F" + excelRow)
                });
                excelRow++;
            }
            int lastDataRow = excelRow - 1;
            int totalRow = excelRow;
            s.Rows.Add(new[]
            {
                ReportCell.Text("TỔNG", true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true), ReportCell.Text(string.Empty, true),
                ReportCell.Formula2(SumFormula("E", firstDataRow, lastDataRow), true),
                ReportCell.Formula2(SumFormula("F", firstDataRow, lastDataRow), true),
                ReportCell.Formula2("=E" + totalRow + "-F" + totalRow, true)
            });
            return s;
        }

        private static ReportSheet BuildSummarySheet(ProjectState st, ExportOptions opt,
            string unitGroup, string unitName)
        {
            var r = st.VolumeResult;
            double levelCut = r.Levels.Sum(x => x.CutVolume);
            double levelFill = r.Levels.Sum(x => x.FillVolume);
            var s = NewSheet("Tổng khối", "BẢNG TỔNG HỢP KHỐI LƯỢNG",
                new[] { "Chỉ tiêu", "Đơn vị", "Giá trị" }, 45, 15, 45);
            s.Landscape = false;
            s.AutoFilter = false;
            s.HasSignatures = true;
            s.Notes.Add("Người phát triển: " + ExportOptions.FixedDeveloperName);
            s.Notes.Add(ExportOptions.FixedDeveloperContact);

            int detailTotalRow = 4 + r.Intervals.Count;
            int levelTotalRow = 3 + r.Levels.Count;

            s.Rows.Add(new[] { ReportCell.Text("Tổng khối lượng đào", true), ReportCell.Text("m³"), opt.IncludeIntervals ? ReportCell.Formula2("='Khối lượng chi tiết'!H" + detailTotalRow, true) : ReportCell.N2(r.TotalCutVolume, true) });
            s.Rows.Add(new[] { ReportCell.Text("Tổng khối lượng đắp", true), ReportCell.Text("m³"), opt.IncludeIntervals ? ReportCell.Formula2("='Khối lượng chi tiết'!L" + detailTotalRow, true) : ReportCell.N2(r.TotalFillVolume, true) });
            s.Rows.Add(new[] { ReportCell.Text("Chênh lệch đào - đắp", true), ReportCell.Text("m³"), ReportCell.Formula2("=C5-C6", true) });
            s.Rows.Add(new[] { ReportCell.Text("Tổng theo tầng - đào"), ReportCell.Text("m³"), opt.IncludeLevels ? ReportCell.Formula2("='Tổng hợp theo tầng'!E" + levelTotalRow) : ReportCell.N2(levelCut) });
            s.Rows.Add(new[] { ReportCell.Text("Tổng theo tầng - đắp"), ReportCell.Text("m³"), opt.IncludeLevels ? ReportCell.Formula2("='Tổng hợp theo tầng'!F" + levelTotalRow) : ReportCell.N2(levelFill) });
            s.Rows.Add(new[] { ReportCell.Text("Sai lệch kiểm soát đào (chi tiết - theo tầng)"), ReportCell.Text("m³"), ReportCell.Formula3("=C5-C8") });
            s.Rows.Add(new[] { ReportCell.Text("Sai lệch kiểm soát đắp (chi tiết - theo tầng)"), ReportCell.Text("m³"), ReportCell.Formula3("=C6-C9") });
            s.Rows.Add(new[] { ReportCell.Text("Số khoảng mặt cắt"), ReportCell.Text("khoảng"), opt.IncludeIntervals ? ReportCell.FormulaInt("=COUNTA('Khối lượng chi tiết'!A4:A" + (3 + r.Intervals.Count) + ")") : ReportCell.Int(r.Intervals.Count) });
            s.Rows.Add(new[] { ReportCell.Text("Số tầng / mức"), ReportCell.Text("tầng"), opt.IncludeLevels ? ReportCell.FormulaInt("=COUNTA('Tổng hợp theo tầng'!A3:A" + (2 + r.Levels.Count) + ")") : ReportCell.Int(r.Levels.Count) });

            // Keep C5:C9 and all existing linked volume formulas unchanged.
            // Append the project metadata and sign-off block after the fixed
            // calculation rows. Do not put extra Notes above the table.
            s.Rows.Add(new[] { ReportCell.Text(""), ReportCell.Text(""), ReportCell.Text("") });
            if (unitGroup != MiningUnitCatalog.OutsideTkv)
                s.Rows.Add(new[] { ReportCell.Text("Đơn vị cấp trên", true),
                    ReportCell.Text(""), ReportCell.Text(TkvParentHeading) });
            s.Rows.Add(new[] { ReportCell.Text("Đơn vị tính toán", true), ReportCell.Text(""),
                ReportCell.Text(EmptyAsNotSet(unitName)) });
            s.Rows.Add(new[] { ReportCell.Text("Loại đơn vị", true), ReportCell.Text(""),
                ReportCell.Text(unitGroup) });
            s.Rows.Add(new[] { ReportCell.Text("Kỳ tính khối lượng", true), ReportCell.Text(""),
                ReportCell.Text(st.VolumePeriodLabel) });
            s.Rows.Add(new[] { ReportCell.Text("Thời điểm tính", true), ReportCell.Text(""),
                ReportCell.Text(FormatCalculationTime(st.VolumeCalculatedUtc)) });
            s.Rows.Add(new[] { ReportCell.Text("Thời gian thực tính (giây)", true), ReportCell.Text(""),
                ReportCell.Text(FormatDuration(st.VolumeCalculationSeconds)) });
            s.Rows.Add(new[] { ReportCell.Text(""), ReportCell.Text(""), ReportCell.Text("") });
            s.Rows.Add(new[] { ReportCell.Text("NGƯỜI TÍNH TOÁN", true),
                ReportCell.Text(""), ReportCell.Text("NGƯỜI KIỂM TRA", true) });
            s.Rows.Add(new[] { ReportCell.Text("(Ký, ghi rõ họ tên)"),
                ReportCell.Text(""), ReportCell.Text("(Ký, ghi rõ họ tên)") });
            s.Rows.Add(new[] { ReportCell.Text(""), ReportCell.Text(""), ReportCell.Text("") });
            s.Rows.Add(new[] { ReportCell.Text(""), ReportCell.Text(""), ReportCell.Text("") });
            s.Rows.Add(new[] { ReportCell.Text(""), ReportCell.Text(""), ReportCell.Text("") });
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
        private static string FormatCalculationTime(DateTime? utc) =>
            utc.HasValue ? utc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
                         : "Chưa xác nhận trong phiên hiện tại";
        private static string FormatDuration(double? seconds) =>
            seconds.HasValue && !double.IsNaN(seconds.Value) && !double.IsInfinity(seconds.Value)
                ? seconds.Value.ToString("0.000") : "Chưa xác nhận trong phiên hiện tại";
        private static string FormulaName(VolumeFormulaKind x)
        {
            switch (x)
            {
                case VolumeFormulaKind.Pyramid: return "Hình chóp";
                case VolumeFormulaKind.Frustum: return "Hình chóp cụt";
                case VolumeFormulaKind.Prismoidal: return "Prismoid (cũ)";
                default: return "TB hai đầu";
            }
        }

        internal static string AdaptiveVolumeFormula(
            int row,
            string lengthCol,
            string startCol,
            string endCol,
            double threshold = 0.40)
        {
            string l = lengthCol + row;
            string a = startCol + row;
            string b = endCol + row;
            string t = threshold.ToString("0.###############", System.Globalization.CultureInfo.InvariantCulture);

            // Công thức Excel tự chọn phương pháp theo chính dữ liệu ô:
            // - cả hai diện tích bằng 0 => 0
            // - một đầu bằng 0 => hình chóp
            // - chênh tương đối <= ngưỡng => TB hai đầu
            // - chênh tương đối > ngưỡng => hình chóp cụt
            return "=IF(MAX(" + a + "," + b + ")=0,0," +
                   "IF(MIN(" + a + "," + b + ")=0," +
                       l + "/3*(" + a + "+" + b + ")," +
                       "IF(ABS(" + a + "-" + b + ")/MAX(" + a + "," + b + ")<=" + t + "," +
                           l + "/2*(" + a + "+" + b + ")," +
                           l + "/3*(" + a + "+" + b + "+SQRT(" + a + "*" + b + "))" +
                       ")" +
                   ")" +
                   ")";
        }
        private static string SumFormula(string col, int firstRow, int lastRow)
        {
            return lastRow < firstRow ? "=0" : "=SUM(" + col + firstRow + ":" + col + lastRow + ")";
        }
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