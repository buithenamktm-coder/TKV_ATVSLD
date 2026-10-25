using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using MiningVolume.Core.Grid;
using MiningVolume.Core.Reporting;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Tạo workbook riêng cho lưới ô vuông, dùng cùng định dạng Arial 12,
    /// không tô nền, có công thức liên kết kết quả và logo IMSAT qua
    /// ExportWorkflowService.WriteXlsx.
    /// </summary>
    internal static class GridVolumeExcelReportBuilder
    {
        private const int FirstDataRow = 4; // 1 title + 1 note + 1 header.
        public static SpreadsheetReport Build(GridVolumeReport grid, GridVolumeOptions options)
        {
            var state = ProjectState.Current;
            var report = Capture(grid, options);
            return BuildDataSheets(grid, report, state.MiningUnitGroup, state.MiningUnitName, state.VolumePeriodLabel);
        }

        // Called on the host thread. Capture all project metadata before yielding;
        // the worker only reads the accepted grid and owns its new workbook.
        public static Task<SpreadsheetReport> BuildAsync(GridVolumeReport grid, GridVolumeOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            var report = Capture(grid, options);
            string group = ProjectState.Current.MiningUnitGroup;
            string unit = ProjectState.Current.MiningUnitName;
            string period = ProjectState.Current.VolumePeriodLabel;
            return Task.Run(() => BuildDataSheets(grid, report, group, unit, period, cancellationToken));
        }

        private static SpreadsheetReport Capture(GridVolumeReport grid, GridVolumeOptions options)
        {
            if (grid == null || grid.Cells == null) throw new InvalidOperationException("Chưa tính lưới ô vuông.");
            if (options == null) throw new ArgumentNullException(nameof(options));
            var r = new SpreadsheetReport
            {
                Title = "IMSAT MININGVOLUME – KHỐI LƯỢNG LƯỚI Ô VUÔNG",
                Creator = "Bùi Thế Nam", CreatedAt = DateTime.Now
            };
            r.Sheets.Add(Info(grid, options));
            return r;
        }

        private static SpreadsheetReport BuildDataSheets(GridVolumeReport grid, SpreadsheetReport r,
            string unitGroup, string unitName, string period, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            r.Sheets.Add(Nodes(grid, cancellationToken));
            r.Sheets.Add(Details(grid, cancellationToken));
            r.Sheets.Add(GridSummary(grid, cancellationToken));
            r.Sheets.Add(Totals(grid, unitGroup, unitName, period));
            r.Sheets.Add(CalculationDetail(grid, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            ExportWorkflowService.ApplyOrganizationHeader(r, unitGroup, unitName);
            return r;
        }

        private static ReportSheet Sheet(string name, string title, params string[] headers)
        {
            var sheet = new ReportSheet { Name = name, Title = title };
            sheet.Headers.AddRange(headers);
            for (int i = 0; i < headers.Length; i++) sheet.ColumnWidths.Add(i <= 1 ? 20 : 17);
            return sheet;
        }

        private static ReportSheet Info(GridVolumeReport grid, GridVolumeOptions opt)
        {
            var s = Sheet("THÔNG TIN", "THÔNG TIN TÍNH KHỐI LƯỢNG LƯỚI", "Chỉ tiêu", "Giá trị");
            s.ColumnWidths[0] = 45;
            s.ColumnWidths[1] = 45;
            s.Notes.Add("Khối lượng hình học chính thức phải được tính lại trong AutoCAD sau thay đổi TIN/ranh lưới.");
            var state = ProjectState.Current;
            if (state.MiningUnitGroup != MiningUnitCatalog.OutsideTkv)
                Row(s, "Đơn vị cấp trên", ExportWorkflowService.TkvParentHeading);
            Row(s,"Đơn vị tính toán", string.IsNullOrWhiteSpace(state.MiningUnitName) ? "Chưa thiết lập" : state.MiningUnitName);
            Row(s,"Loại đơn vị", state.MiningUnitGroup);
            Row(s,"Tỷ lệ mặt cắt (ngang / đứng)",
                "1:"+state.HorizontalScale.ToString("N0")+" / 1:"+state.VerticalScale.ToString("N0"));
            Row(s,"Thông số mặt cắt",state.SectionParameterMode);
            Row(s,"Bước chia trục Z, m",state.SectionAxisElevationInterval.ToString("0.###"));
            Row(s,"Kỳ tính khối lượng", state.VolumePeriodLabel);
            Row(s,"Mã phiên tính (CalculationId)", grid.CalculationId);
            Row(s,"Thời điểm tính lưới", state.GridCalculatedUtc.HasValue
                ? state.GridCalculatedUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
                : "Chưa xác nhận trong phiên hiện tại");
            Row(s,"Thời gian thực tính lưới, giây", state.GridCalculationSeconds.HasValue
                ? state.GridCalculationSeconds.Value.ToString("0.000")
                : "Chưa xác nhận trong phiên hiện tại");
            Row(s,"Thời điểm xuất Excel",DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            Row(s,"Người phát triển","Bùi Thế Nam");
            Row(s,"ĐT/Zalo","0967280686");
            Row(s,"Phương pháp",grid.Method);
            Row(s,"Gốc lưới X",opt.Origin.X.ToString("0.###"));
            Row(s,"Gốc lưới Y",opt.Origin.Y.ToString("0.###"));
            Row(s,"Kích thước X (m)",opt.SizeX.ToString("0.###"));
            Row(s,"Kích thước Y (m)",opt.SizeY.ToString("0.###"));
            Row(s,"Góc xoay (độ)",opt.RotationDegrees.ToString("0.###"));
            Row(s,"Số ô tính",grid.Cells.Count.ToString());
            Row(s,"Số ô nhiều Z chưa xác nhận",grid.AmbiguousCells.ToString());
            Row(s,"Số ô chứa tam giác TIN dương dưới ngưỡng tích phân",grid.SmallTriangleCells.ToString());
            Row(s,"Số ô thiếu phủ hiện trạng",grid.MissingExistingCells.ToString());
            Row(s,"Số ô thiếu phủ thiết kế",grid.MissingDesignCells.ToString());
            Row(s,"Số ô hai TIN đều phủ nhưng không giao đúng",grid.IntersectionMismatchCells.ToString());
            Row(s,"Diện tích nhiều Z chưa xác nhận (m²)",grid.AmbiguousArea.ToString("0.###"));
            Row(s,"Độ đầy đủ dữ liệu",grid.IsComplete?"Đủ hai TIN":"THIẾU DỮ LIỆU – CHƯA ĐƯỢC NGHIỆM THU");
            return s;
        }

        private static ReportSheet Nodes(GridVolumeReport grid, CancellationToken cancellationToken)
        {
            var s=Sheet("CAO ĐỘ NÚT LƯỚI","CAO ĐỘ CÁC NÚT LƯỚI",
                "STT","Mã nút","I","J","X","Y","Ztn","Ztk","ΔZ","Trạng thái");
            s.Notes.Add("ΔZ=Ztn−Ztk; nút thiếu cao độ để trống, không điền số 0. Với vùng nhiều Z, cao độ mẫu không phải nghiệm thu bề mặt.");
            var nodes=new SortedDictionary<(int,int), (double x,double y,double? tn,double? tk,bool uncertain)>(
                Comparer<(int,int)>.Create((a,b)=>a.Item1!=b.Item1?a.Item1.CompareTo(b.Item1):a.Item2.CompareTo(b.Item2)));
            foreach(var c in grid.Cells)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for(int k=0;k<4;k++)
                {
                    int x=c.I+(k==1||k==2?1:0), y=c.J+(k>=2?1:0);
                    var key=(x,y);
                    double? tn=c.ExistingNodeHeights?[k], tk=c.DesignNodeHeights?[k];
                    if(!nodes.ContainsKey(key))
                        nodes[key]=(c.Corners[k].X,c.Corners[k].Y,tn,tk,(c.IsVerticalConflict || c.Volume.HasUnresolvedSmallTriangles));
                    else
                    {
                        var previous=nodes[key];
                        nodes[key]=(previous.x,previous.y,
                            previous.tn ?? tn,previous.tk ?? tk,
                            previous.uncertain || (c.IsVerticalConflict || c.Volume.HasUnresolvedSmallTriangles));
                    }
                }
            }
            int row=FirstDataRow, i=0;
            foreach(var item in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var p=item.Value;
                bool valid=!p.uncertain && p.tn.HasValue && p.tk.HasValue;
                s.Rows.Add(new ReportCell[]
                {
                    ReportCell.Int(++i),ReportCell.Text("N-"+item.Key.Item1+"-"+item.Key.Item2),
                    ReportCell.Int(item.Key.Item1),ReportCell.Int(item.Key.Item2),
                    ReportCell.N3(p.x),ReportCell.N3(p.y),
                    valid?ReportCell.N3(p.tn.Value):ReportCell.Text(""),
                    valid?ReportCell.N3(p.tk.Value):ReportCell.Text(""),
                    valid?ReportCell.Formula3("G"+row+"-H"+row):ReportCell.Text(""),
                    ReportCell.Text(p.uncertain?"HÌNH HỌC TIN - CHƯA XÁC NHẬN":
                        valid?"Đủ":"THIẾU Z")
                });
                row++;
            }
            return s;
        }

        private static ReportSheet Details(GridVolumeReport grid, CancellationToken cancellationToken)
        {
            var s=Sheet("CHI TIẾT Ô LƯỚI","KHỐI LƯỢNG ĐÀO ĐẮP CHI TIẾT TỪNG Ô",
                "STT","Mã ô","I","J","S ô (m²)","S có TIN (m²)","S thiếu (m²)",
                "V đào (m³)","V đắp (m³)","V thuần (m³)","Trạng thái");
            s.Notes.Add("V đào/V đắp do AutoCAD tích phân giao TIN, không phải thể tích trung bình 4 nút. Ô có tam giác quá nhỏ chưa tích phân để trống khối lượng; phải sửa TIN và tính lại.");
            int row=FirstDataRow,i=0;
            foreach(var c in grid.Cells)
            {
                cancellationToken.ThrowIfCancellationRequested();
                s.Rows.Add(new ReportCell[]
                {
                    ReportCell.Int(++i),ReportCell.Text(c.CellCode),ReportCell.Int(c.I),ReportCell.Int(c.J),
                    ReportCell.N3(c.Volume.CalculationArea),ReportCell.N3(c.Volume.CoveredArea),
                    ReportCell.N3(c.Volume.NoDataArea),
                    c.IsVerticalConflict || c.Volume.HasUnresolvedSmallTriangles || c.Volume.CoveredArea<=1e-9
                        ? ReportCell.Text("") : ReportCell.N3(c.Volume.CutVolume),
                    c.IsVerticalConflict || c.Volume.HasUnresolvedSmallTriangles || c.Volume.CoveredArea<=1e-9
                        ? ReportCell.Text("") : ReportCell.N3(c.Volume.FillVolume),
                    c.IsVerticalConflict || c.Volume.HasUnresolvedSmallTriangles || c.Volume.CoveredArea<=1e-9
                        ? ReportCell.Text("") : ReportCell.Formula3("H"+row+"-I"+row),
                    ReportCell.Text(c.CoverageStatus)
                });
                row++;
            }
            return s;
        }

        private static ReportSheet GridSummary(GridVolumeReport grid, CancellationToken cancellationToken)
        {
            var s=Sheet("BẢNG LƯỚI TỔNG HỢP","BẢNG LƯỚI TỔNG HỢP LIÊN KẾT",
                "Mã ô","Diện tích (m²)","V đào (m³)","V đắp (m³)","V thuần (m³)");
            s.Notes.Add("Dữ liệu liên kết CHI TIẾT Ô LƯỚI; ô nhiều Z, ô có tam giác quá nhỏ chưa tích phân hoặc không có dữ liệu để trống khối lượng. Ô thiếu phủ một phần chỉ là tạm tính.");
            foreach(var c in grid.Cells.Select((v,i)=>new{v,i}))
            {
                cancellationToken.ThrowIfCancellationRequested();
                int n=FirstDataRow+c.i;
                string pre="'CHI TIẾT Ô LƯỚI'!";
                s.Rows.Add(new ReportCell[]
                {
                    ReportCell.Text(c.v.CellCode),
                    ReportCell.Formula3(pre+"E"+n),
                    c.v.IsVerticalConflict || c.v.Volume.HasUnresolvedSmallTriangles || c.v.Volume.CoveredArea<=1e-9
                        ? ReportCell.Text("") : ReportCell.Formula3(pre+"H"+n),
                    c.v.IsVerticalConflict || c.v.Volume.HasUnresolvedSmallTriangles || c.v.Volume.CoveredArea<=1e-9
                        ? ReportCell.Text("") : ReportCell.Formula3(pre+"I"+n),
                    c.v.IsVerticalConflict || c.v.Volume.HasUnresolvedSmallTriangles || c.v.Volume.CoveredArea<=1e-9
                        ? ReportCell.Text("") : ReportCell.Formula3("C"+n+"-D"+n)
                });
            }
            return s;
        }

        private static ReportSheet Totals(GridVolumeReport grid, string unitGroup, string unitName, string period)
        {
            var s=Sheet("TỔNG HỢP KHỐI LƯỢNG","KHỐI LƯỢNG TỔNG HỢP LƯỚI Ô VUÔNG",
                "Chỉ tiêu","Giá trị","Đơn vị");
            s.ColumnWidths[0] = 43;
            s.ColumnWidths[1] = 22;
            s.ColumnWidths[2] = 34;
            s.AutoFilter = false;
            s.HasSignatures = true;
            s.Notes.Add(grid.IsComplete
                ? "Các ô có đầy đủ TIN hiện trạng và thiết kế."
                : "CHƯA ĐẦY ĐỦ: tổng tạm tính, bỏ qua ô nhiều Z/ô có tam giác quá nhỏ; các ô thiếu TIN có thể chỉ là phần tính được, không được nghiệm thu.");
            int end=FirstDataRow+grid.Cells.Count-1;
            string from="'CHI TIẾT Ô LƯỚI'!";
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Tổng thể tích đào"),
                ReportCell.Formula3("SUM("+from+"H"+FirstDataRow+":H"+end+")"),ReportCell.Text("m³")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Tổng thể tích đắp"),
                ReportCell.Formula3("SUM("+from+"I"+FirstDataRow+":I"+end+")"),ReportCell.Text("m³")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Chênh lệch đào – đắp"),
                ReportCell.Formula3("B4-B5"),ReportCell.Text("m³")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Diện tích chưa xác nhận (thiếu TIN/nhiều Z)"),
                ReportCell.Formula3("SUM("+from+"G"+FirstDataRow+":G"+end+")"),ReportCell.Text("m²")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Tình trạng nghiệm thu"),
                ReportCell.Text(grid.IsComplete ? "ĐỦ DỮ LIỆU ĐỂ RÀ SOÁT" :
                    "CHƯA ĐẦY ĐỦ – TỔNG CHỈ LÀ PHẦN CÓ DỮ LIỆU"),
                ReportCell.Text("")});
            // Sign-off area belongs to the same volume-summary worksheet.
            // Keep all formula row numbers intact by appending after totals.
            s.Rows.Add(new ReportCell[]{ReportCell.Text(""), ReportCell.Text(""), ReportCell.Text("")});
            if (unitGroup != MiningUnitCatalog.OutsideTkv)
                s.Rows.Add(new ReportCell[]{ReportCell.Text("Đơn vị cấp trên",true),
                    ReportCell.Text(""),ReportCell.Text(ExportWorkflowService.TkvParentHeading)});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Đơn vị tính toán",true),
                ReportCell.Text(""),ReportCell.Text(unitName ?? "")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Kỳ tính khối lượng",true),
                ReportCell.Text(""),ReportCell.Text(period)});
            s.Rows.Add(new ReportCell[]{ReportCell.Text(""),ReportCell.Text(""),ReportCell.Text("")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("NGƯỜI TÍNH TOÁN",true),
                ReportCell.Text(""),ReportCell.Text("NGƯỜI KIỂM TRA",true)});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("(Ký, ghi rõ họ tên)"),
                ReportCell.Text(""),ReportCell.Text("(Ký, ghi rõ họ tên)")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text(""),ReportCell.Text(""),ReportCell.Text("")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text(""),ReportCell.Text(""),ReportCell.Text("")});
            return s;
        }

        private static ReportSheet CalculationDetail(GridVolumeReport grid, CancellationToken cancellationToken)
        {
            var s=Sheet("CHI TIẾT TÍNH TOÁN","THUYẾT MINH PHƯƠNG PHÁP TÍCH PHÂN LƯỚI",
                "Mã ô","Thuật toán","S có TIN (m²)","V đào (m³)","V đắp (m³)","Kiểm tra");
            s.Notes.Add("Giao tam giác hiện trạng/thiết kế/ô lưới, cắt tại ΔZ=0, tích phân tuyến tính; không thể tái giao hình học nếu chỉ sửa Excel.");
            for(int i=0;i<grid.Cells.Count;i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int row=FirstDataRow+i;
                string prefix="'CHI TIẾT Ô LƯỚI'!";
                var c=grid.Cells[i];
                s.Rows.Add(new ReportCell[]
                {
                    ReportCell.Text(c.CellCode),ReportCell.Text("TIN overlay exact"),
                    ReportCell.Formula3(prefix+"F"+row),
                    c.IsVerticalConflict || c.Volume.HasUnresolvedSmallTriangles || c.Volume.CoveredArea<=1e-9
                        ? ReportCell.Text("") : ReportCell.Formula3(prefix+"H"+row),
                    c.IsVerticalConflict || c.Volume.HasUnresolvedSmallTriangles || c.Volume.CoveredArea<=1e-9
                        ? ReportCell.Text("") : ReportCell.Formula3(prefix+"I"+row),
                    ReportCell.Text(c.CoverageStatus)
                });
            }
            return s;
        }

        private static void Row(ReportSheet s,string k,string v) =>
            s.Rows.Add(new ReportCell[]{ReportCell.Text(k),ReportCell.Text(v)});
    }
}
