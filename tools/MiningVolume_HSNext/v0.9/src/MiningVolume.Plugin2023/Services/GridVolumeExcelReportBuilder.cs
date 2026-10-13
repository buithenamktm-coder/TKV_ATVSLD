using System;
using System.Collections.Generic;
using System.Linq;
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
            if (grid == null || grid.Cells == null) throw new InvalidOperationException("Chưa tính lưới ô vuông.");
            if (options == null) throw new ArgumentNullException(nameof(options));
            var r = new SpreadsheetReport
            {
                Title = "IMSAT MININGVOLUME – KHỐI LƯỢNG LƯỚI Ô VUÔNG",
                Creator = "Bùi Thế Nam", CreatedAt = DateTime.Now
            };
            r.Sheets.Add(Info(grid, options));
            r.Sheets.Add(Nodes(grid));
            r.Sheets.Add(Details(grid));
            r.Sheets.Add(GridSummary(grid));
            r.Sheets.Add(Totals(grid));
            r.Sheets.Add(CalculationDetail(grid));
            ExportWorkflowService.ApplyOrganizationHeader(r, ProjectState.Current);
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
            Row(s,"Tỷ lệ mặt cắt (ngang / đứng)","1:1.000 / 1:1.000");
            Row(s,"Thông số mặt cắt",state.SectionParameterMode);
            Row(s,"Bước chia trục Z, m",state.SectionAxisElevationInterval.ToString("0.###"));
            Row(s,"Kỳ tính khối lượng", state.VolumePeriodLabel);
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
            Row(s,"Độ đầy đủ dữ liệu",grid.IsComplete?"Đủ hai TIN":"THIẾU DỮ LIỆU – CHƯA ĐƯỢC NGHIỆM THU");
            return s;
        }

        private static ReportSheet Nodes(GridVolumeReport grid)
        {
            var s=Sheet("CAO ĐỘ NÚT LƯỚI","CAO ĐỘ CÁC NÚT LƯỚI",
                "STT","Mã nút","I","J","X","Y","Ztn","Ztk","ΔZ","Trạng thái");
            s.Notes.Add("ΔZ=Ztn−Ztk; nút thiếu cao độ để trống, không điền số 0.");
            var nodes=new SortedDictionary<(int,int), (double x,double y,double? tn,double? tk)>(
                Comparer<(int,int)>.Create((a,b)=>a.Item1!=b.Item1?a.Item1.CompareTo(b.Item1):a.Item2.CompareTo(b.Item2)));
            foreach(var c in grid.Cells)
            {
                for(int k=0;k<4;k++)
                {
                    int x=c.I+(k==1||k==2?1:0), y=c.J+(k>=2?1:0);
                    var key=(x,y);
                    double? tn=c.ExistingNodeHeights?[k], tk=c.DesignNodeHeights?[k];
                    if(!nodes.ContainsKey(key) || (!nodes[key].tn.HasValue && tn.HasValue))
                        nodes[key]=(c.Corners[k].X,c.Corners[k].Y,tn,tk);
                }
            }
            int row=FirstDataRow, i=0;
            foreach(var item in nodes)
            {
                var p=item.Value;
                bool valid=p.tn.HasValue && p.tk.HasValue;
                s.Rows.Add(new ReportCell[]
                {
                    ReportCell.Int(++i),ReportCell.Text("N-"+item.Key.Item1+"-"+item.Key.Item2),
                    ReportCell.Int(item.Key.Item1),ReportCell.Int(item.Key.Item2),
                    ReportCell.N3(p.x),ReportCell.N3(p.y),
                    p.tn.HasValue?ReportCell.N3(p.tn.Value):ReportCell.Text(""),
                    p.tk.HasValue?ReportCell.N3(p.tk.Value):ReportCell.Text(""),
                    valid?ReportCell.Formula3("G"+row+"-H"+row):ReportCell.Text(""),
                    ReportCell.Text(valid?"Đủ":"THIẾU Z")
                });
                row++;
            }
            return s;
        }

        private static ReportSheet Details(GridVolumeReport grid)
        {
            var s=Sheet("CHI TIẾT Ô LƯỚI","KHỐI LƯỢNG ĐÀO ĐẮP CHI TIẾT TỪNG Ô",
                "STT","Mã ô","I","J","S ô (m²)","S có TIN (m²)","S thiếu (m²)",
                "V đào (m³)","V đắp (m³)","V thuần (m³)","Trạng thái");
            s.Notes.Add("V đào/V đắp do AutoCAD tích phân giao TIN, không phải thể tích trung bình 4 nút.");
            int row=FirstDataRow,i=0;
            foreach(var c in grid.Cells)
            {
                s.Rows.Add(new ReportCell[]
                {
                    ReportCell.Int(++i),ReportCell.Text(c.CellCode),ReportCell.Int(c.I),ReportCell.Int(c.J),
                    ReportCell.N3(c.Volume.CalculationArea),ReportCell.N3(c.Volume.CoveredArea),
                    ReportCell.N3(c.Volume.NoDataArea),ReportCell.N3(c.Volume.CutVolume),
                    ReportCell.N3(c.Volume.FillVolume),ReportCell.Formula3("H"+row+"-I"+row),
                    ReportCell.Text(c.Volume.IsComplete?"Đủ":"THIẾU DỮ LIỆU")
                });
                row++;
            }
            return s;
        }

        private static ReportSheet GridSummary(GridVolumeReport grid)
        {
            var s=Sheet("BẢNG LƯỚI TỔNG HỢP","BẢNG LƯỚI TỔNG HỢP LIÊN KẾT",
                "Mã ô","Diện tích (m²)","V đào (m³)","V đắp (m³)","V thuần (m³)");
            s.Notes.Add("Dữ liệu lấy qua công thức liên kết với CHI TIẾT Ô LƯỚI.");
            foreach(var c in grid.Cells.Select((v,i)=>new{v,i}))
            {
                int n=FirstDataRow+c.i;
                string pre="'CHI TIẾT Ô LƯỚI'!";
                s.Rows.Add(new ReportCell[]
                {
                    ReportCell.Text(c.v.CellCode),
                    ReportCell.Formula3(pre+"E"+n),
                    ReportCell.Formula3(pre+"H"+n),
                    ReportCell.Formula3(pre+"I"+n),
                    ReportCell.Formula3("C"+n+"-D"+n)
                });
            }
            return s;
        }

        private static ReportSheet Totals(GridVolumeReport grid)
        {
            var s=Sheet("TỔNG HỢP KHỐI LƯỢNG","KHỐI LƯỢNG TỔNG HỢP LƯỚI Ô VUÔNG",
                "Chỉ tiêu","Giá trị","Đơn vị");
            s.ColumnWidths[0] = 43;
            s.ColumnWidths[1] = 22;
            s.ColumnWidths[2] = 34;
            s.AutoFilter = false;
            s.HasSignatures = true;
            s.Notes.Add("Không phát hành kết quả chính thức khi còn diện tích thiếu dữ liệu.");
            int end=FirstDataRow+grid.Cells.Count-1;
            string from="'CHI TIẾT Ô LƯỚI'!";
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Tổng thể tích đào"),
                ReportCell.Formula3("SUM("+from+"H"+FirstDataRow+":H"+end+")"),ReportCell.Text("m³")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Tổng thể tích đắp"),
                ReportCell.Formula3("SUM("+from+"I"+FirstDataRow+":I"+end+")"),ReportCell.Text("m³")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Chênh lệch đào – đắp"),
                ReportCell.Formula3("B4-B5"),ReportCell.Text("m³")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Diện tích thiếu TIN"),
                ReportCell.Formula3("SUM("+from+"G"+FirstDataRow+":G"+end+")"),ReportCell.Text("m²")});
            // Sign-off area belongs to the same volume-summary worksheet.
            // Keep all formula row numbers intact by appending after totals.
            var state = ProjectState.Current;
            s.Rows.Add(new ReportCell[]{ReportCell.Text(""), ReportCell.Text(""), ReportCell.Text("")});
            if (state.MiningUnitGroup != MiningUnitCatalog.OutsideTkv)
                s.Rows.Add(new ReportCell[]{ReportCell.Text("Đơn vị cấp trên",true),
                    ReportCell.Text(""),ReportCell.Text(ExportWorkflowService.TkvParentHeading)});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Đơn vị tính toán",true),
                ReportCell.Text(""),ReportCell.Text(state.MiningUnitName ?? "")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("Kỳ tính khối lượng",true),
                ReportCell.Text(""),ReportCell.Text(state.VolumePeriodLabel)});
            s.Rows.Add(new ReportCell[]{ReportCell.Text(""),ReportCell.Text(""),ReportCell.Text("")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("NGƯỜI TÍNH TOÁN",true),
                ReportCell.Text(""),ReportCell.Text("NGƯỜI KIỂM TRA",true)});
            s.Rows.Add(new ReportCell[]{ReportCell.Text("(Ký, ghi rõ họ tên)"),
                ReportCell.Text(""),ReportCell.Text("(Ký, ghi rõ họ tên)")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text(""),ReportCell.Text(""),ReportCell.Text("")});
            s.Rows.Add(new ReportCell[]{ReportCell.Text(""),ReportCell.Text(""),ReportCell.Text("")});
            return s;
        }

        private static ReportSheet CalculationDetail(GridVolumeReport grid)
        {
            var s=Sheet("CHI TIẾT TÍNH TOÁN","THUYẾT MINH PHƯƠNG PHÁP TÍCH PHÂN LƯỚI",
                "Mã ô","Thuật toán","S có TIN (m²)","V đào (m³)","V đắp (m³)","Kiểm tra");
            s.Notes.Add("Giao tam giác hiện trạng/thiết kế/ô lưới, cắt tại ΔZ=0, tích phân tuyến tính; không thể tái giao hình học nếu chỉ sửa Excel.");
            for(int i=0;i<grid.Cells.Count;i++)
            {
                int row=FirstDataRow+i;
                string prefix="'CHI TIẾT Ô LƯỚI'!";
                var c=grid.Cells[i];
                s.Rows.Add(new ReportCell[]
                {
                    ReportCell.Text(c.CellCode),ReportCell.Text("TIN overlay exact"),
                    ReportCell.Formula3(prefix+"F"+row),
                    ReportCell.Formula3(prefix+"H"+row),
                    ReportCell.Formula3(prefix+"I"+row),
                    ReportCell.Text(c.Volume.IsComplete?"Đã phủ":"Chưa đủ hai TIN")
                });
            }
            return s;
        }

        private static void Row(ReportSheet s,string k,string v) =>
            s.Rows.Add(new ReportCell[]{ReportCell.Text(k),ReportCell.Text(v)});
    }
}
