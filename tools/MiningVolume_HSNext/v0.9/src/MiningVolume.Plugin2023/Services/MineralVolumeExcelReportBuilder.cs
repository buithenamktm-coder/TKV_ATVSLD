using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Reporting;
using MiningVolume.Core.Volumes;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// 4-sheet XLSX with connected formulas. Geometry is validated in AutoCAD;
    /// Excel can recompute V/t after editing accepted areas, density, distance,
    /// but CANNOT rerun AutoCAD TIN intersection.
    /// </summary>
    internal static class MineralVolumeExcelReportBuilder
    {
        // One title, one note and one header -> data starts at row 4.
        private const int DataStart = 4;

        public static SpreadsheetReport Build(MineralVolumeSummary summary,
            IReadOnlyList<MineralBody> bodies)
        {
            if (summary == null || summary.Intervals == null || summary.Bodies == null)
                throw new ArgumentNullException(nameof(summary));
            var report = new SpreadsheetReport
            {
                Title = "IMSAT MININGVOLUME – KHỐI LƯỢNG THAN / QUẶNG THEO VỈA",
                Creator = "Bùi Thế Nam",
                CreatedAt = DateTime.Now
            };

            var paramsSheet = Sheet("THAM SỐ VỈA",
                "THÔNG SỐ THAN / QUẶNG THEO VỈA",
                "Mã vỉa","Tên vỉa","Thể trọng (t/m³)","Nguồn thể trọng");
            paramsSheet.Notes.Add("Dữ liệu do người thiết kế nhập; thể trọng phải dương; tên vỉa được gán trong AutoCAD.");
            var rows = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            for (int i=0;i<summary.Bodies.Count;i++)
            {
                var b=summary.Bodies[i];
                rows.Add(b.Id,DataStart+i);
                var raw=bodies.FirstOrDefault(x=>x.Id==b.Id);
                paramsSheet.Rows.Add(new ReportCell[]{
                    ReportCell.Text(b.Id),ReportCell.Text(b.Name),
                    ReportCell.N3(b.DensityTonPerCubicMetre),ReportCell.Text(raw?.DensitySource??"")
                });
            }
            report.Sheets.Add(paramsSheet);

            var details = Sheet("CHI TIẾT VỈA",
                "KHỐI LƯỢNG TỪNG VỈA THEO KHOẢNG MẶT CẮT",
                "STT","Mã vỉa","Tên vỉa","Mặt cắt đầu","Mặt cắt cuối",
                "L (m)","F đầu (m²)","F cuối (m²)","Phương pháp",
                "Thể trọng (t/m³)","V (m³)","Khối lượng (t)");
            details.Notes.Add("V theo phương pháp diện tích hai đầu đã chọn; tính toán hình học phải xác nhận lại trên AutoCAD sau chỉnh sửa Polyline.");
            for (int i=0;i<summary.Intervals.Count;i++)
            {
                var v=summary.Intervals[i];
                int row=DataStart+i;
                string formula;
                switch(v.Formula)
                {
                    case VolumeFormulaKind.Pyramid:
                        formula="F"+row+"*(G"+row+"+H"+row+")/3";
                        break;
                    case VolumeFormulaKind.Frustum:
                        formula="F"+row+"*(G"+row+"+H"+row+
                            "+SQRT(G"+row+"*H"+row+"))/3";
                        break;
                    default:
                        formula="F"+row+"*(G"+row+"+H"+row+")/2";
                        break;
                }
                details.Rows.Add(new ReportCell[]{
                    ReportCell.Int(i+1),ReportCell.Text(v.BodyId),ReportCell.Text(v.BodyName),
                    ReportCell.Text(v.StartSection),ReportCell.Text(v.EndSection),
                    ReportCell.N3(v.DistanceMetres),ReportCell.N3(v.StartAreaSquareMetres),
                    ReportCell.N3(v.EndAreaSquareMetres),ReportCell.Text(v.Formula.ToString()),
                    ReportCell.Formula3("'THAM SỐ VỈA'!C"+rows[v.BodyId]),
                    ReportCell.Formula3(formula),
                    ReportCell.Formula3("J"+row+"*K"+row)
                });
            }
            report.Sheets.Add(details);

            var totals=Sheet("TỔNG HỢP VỈA",
                "TỔNG HỢP KHỐI LƯỢNG THAN / QUẶNG THEO TỪNG VỈA",
                "Mã vỉa","Tên vỉa","Thể trọng (t/m³)","Thể tích (m³)","Khối lượng (t)");
            totals.Notes.Add("Thể tích và tấn liên kết với sheet CHI TIẾT VỈA, thể trọng liên kết THAM SỐ VỈA.");
            int last=DataStart+summary.Intervals.Count-1;
            for (int i=0;i<summary.Bodies.Count;i++)
            {
                var body=summary.Bodies[i];
                int row=DataStart+i;
                string prefix="'CHI TIẾT VỈA'!";
                totals.Rows.Add(new ReportCell[]{
                    ReportCell.Text(body.Id),ReportCell.Text(body.Name),
                    ReportCell.Formula3("'THAM SỐ VỈA'!C"+row),
                    ReportCell.Formula3("SUMIF("+prefix+"$B$"+DataStart+":$B$"+last+
                        ",A"+row+","+prefix+"$K$"+DataStart+":$K$"+last+")"),
                    ReportCell.Formula3("SUMIF("+prefix+"$B$"+DataStart+":$B$"+last+
                        ",A"+row+","+prefix+"$L$"+DataStart+":$L$"+last+")")
                });
            }
            report.Sheets.Add(totals);

            var waste=Sheet("ĐẤT ĐÁ VÀ HSB",
                "KHỐI LƯỢNG ĐẤT ĐÁ VÀ HỆ SỐ BÓC",
                "Chỉ tiêu","Giá trị","Đơn vị");
            waste.Notes.Add("V đào lấy từ kết quả CAD đã kiểm chứng; thể tích/tấn quặng lấy từ công thức các sheet bên trên.");
            int lastBody=DataStart+summary.Bodies.Count-1;
            waste.Rows.Add(new[]{ReportCell.Text("Tổng khối lượng đào"),ReportCell.N3(summary.TotalCutCubicMetres),ReportCell.Text("m³")});
            waste.Rows.Add(new[]{ReportCell.Text("Tổng khoáng sản"),ReportCell.Formula3(
                "SUM('TỔNG HỢP VỈA'!D"+DataStart+":D"+lastBody+")"),ReportCell.Text("m³")});
            waste.Rows.Add(new[]{ReportCell.Text("Đất đá bóc"),ReportCell.Formula3("B4-B5"),ReportCell.Text("m³")});
            waste.Rows.Add(new[]{ReportCell.Text("Tổng khoáng sản quy đổi"),ReportCell.Formula3(
                "SUM('TỔNG HỢP VỈA'!E"+DataStart+":E"+lastBody+")"),ReportCell.Text("tấn")});
            waste.Rows.Add(new[]{ReportCell.Text("Hệ số bóc thể tích"),ReportCell.Formula3(
                "IF(B5>0,B6/B5,\"\")"),ReportCell.Text("m³/m³")});
            waste.Rows.Add(new[]{ReportCell.Text("Hệ số bóc theo tấn"),ReportCell.Formula3(
                "IF(B7>0,B6/B7,\"\")"),ReportCell.Text("m³/tấn")});
            report.Sheets.Add(waste);
            return report;
        }

        private static ReportSheet Sheet(string name,string title,params string[] columns)
        {
            var result=new ReportSheet{Name=name,Title=title};
            result.Headers.AddRange(columns);
            for(int i=0;i<columns.Length;i++)result.ColumnWidths.Add(i<=2?24:18);
            return result;
        }
    }
}
