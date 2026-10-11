using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Volumes;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Outputs verified seam volumes as native editable AutoCAD TABLE objects.
    /// The tables are snapshots from the current DWG geometry, not linked
    /// editable sources of truth; rerun calculation after editing section PLINE.
    /// </summary>
    internal static class MineralCadTableService
    {
        private const string Layer = "MV_KHOANGSAN_BANG";
        private const double H = 9;
        private const double Gap = 28;

        internal static void Insert(Database db, MineralVolumeSummary summary, Point3d origin)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (summary == null || summary.Bodies == null || summary.Intervals == null)
                throw new InvalidOperationException("Chưa có kết quả khoáng sản đã kiểm chứng.");
            if (summary.Intervals.Count > 1500 || summary.Bodies.Count > 100)
                throw new InvalidOperationException("Dữ liệu quá lớn cho bảng CAD. Hãy xuất Excel.");
            Check(summary.TotalCutCubicMetres, summary.TotalMineralCubicMetres,
                summary.TotalMineralTonnes, summary.WasteCubicMetres);

            using (var tr = db.TransactionManager.StartTransaction())
            {
                ObjectId layer = EnsureLayer(db, tr);
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                double y = origin.Y;

                var totals = Create(db, layer, 8, 2, new Point3d(origin.X, y, 0), 62);
                Cells(totals, 0, "IMSAT – KHOÁNG SẢN VÀ ĐẤT ĐÁ", "Giá trị");
                Cells(totals, 1, "Tổng đào (m³)", Format(summary.TotalCutCubicMetres));
                Cells(totals, 2, "Than/quặng (m³)", Format(summary.TotalMineralCubicMetres));
                Cells(totals, 3, "Than/quặng (tấn)", Format(summary.TotalMineralTonnes));
                Cells(totals, 4, "Đất đá bóc (m³)", Format(summary.WasteCubicMetres));
                Cells(totals, 5, "Hệ số bóc (m³/m³)",
                    summary.StrippingRatioCubicMetresPerCubicMetre?.ToString("0.###") ?? "N/A");
                Cells(totals, 6, "Hệ số bóc (m³/tấn)",
                    summary.StrippingRatioCubicMetresPerTonne?.ToString("0.###") ?? "N/A");
                Cells(totals, 7, "Người phát triển", "Bùi Thế Nam – 0967280686");
                Append(ms, tr, totals);
                y -= 8 * H + Gap;

                var seams = Create(db, layer, summary.Bodies.Count + 1, 5,
                    new Point3d(origin.X, y, 0), 39);
                Cells(seams, 0, "Mã vỉa", "Tên vỉa", "γ (t/m³)", "V (m³)", "Khối lượng (tấn)");
                for (int i = 0; i < summary.Bodies.Count; i++)
                {
                    var b = summary.Bodies[i];
                    Check(b.DensityTonPerCubicMetre, b.VolumeCubicMetres, b.MassTonnes);
                    Cells(seams, i+1, b.Id, b.Name, Format(b.DensityTonPerCubicMetre),
                        Format(b.VolumeCubicMetres), Format(b.MassTonnes));
                }
                Append(ms, tr, seams);
                y -= (summary.Bodies.Count + 1) * H + Gap;

                var intervals = Create(db, layer, summary.Intervals.Count + 1, 9,
                    new Point3d(origin.X, y, 0), 27);
                Cells(intervals, 0, "STT", "Vỉa", "MC đầu", "MC cuối", "L (m)",
                    "F đầu (m²)", "F cuối (m²)", "V (m³)", "Tấn");
                for (int i = 0; i < summary.Intervals.Count; i++)
                {
                    var v = summary.Intervals[i];
                    Check(v.DistanceMetres, v.StartAreaSquareMetres, v.EndAreaSquareMetres,
                        v.VolumeCubicMetres, v.MassTonnes);
                    Cells(intervals, i+1, (i+1).ToString(), v.BodyName, v.StartSection,
                        v.EndSection, Format(v.DistanceMetres), Format(v.StartAreaSquareMetres),
                        Format(v.EndAreaSquareMetres), Format(v.VolumeCubicMetres),
                        Format(v.MassTonnes));
                }
                Append(ms, tr, intervals);
                tr.Commit();
            }
        }

        private static string Format(double value) => value.ToString("0.00");
        private static void Check(params double[] values)
        {
            foreach (double v in values)
                if (double.IsNaN(v) || double.IsInfinity(v) || v < 0)
                    throw new InvalidOperationException("Kết quả khoáng sản có số liệu âm/không hữu hạn.");
        }
        private static Table Create(Database db,ObjectId layer,int n,int columns,Point3d p,double width)
        {
            var tab = new Table { LayerId = layer, TableStyle = db.Tablestyle, Position = p };
            tab.SetSize(n, columns);
            tab.SetRowHeight(H);
            tab.SetColumnWidth(width);
            return tab;
        }
        private static void Cells(Table tab,int row,params string[] values)
        {
            for (int i=0;i<values.Length;i++)tab.Cells[row,i].TextString=values[i]??"";
        }
        private static void Append(BlockTableRecord model,Transaction tr,Table t)
        {
            t.GenerateLayout();
            model.AppendEntity(t);
            tr.AddNewlyCreatedDBObject(t,true);
        }
        private static ObjectId EnsureLayer(Database db,Transaction tr)
        {
            var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
            if (layers.Has(Layer))
            {
                var record=(LayerTableRecord)tr.GetObject(layers[Layer],OpenMode.ForRead);
                if (record.IsLocked || record.IsOff || record.IsFrozen)
                    throw new InvalidOperationException("Layer "+Layer+" đang khóa hoặc tắt.");
                return layers[Layer];
            }
            layers.UpgradeOpen();
            var created=new LayerTableRecord
            {
                Name=Layer,Color=Color.FromColorIndex(ColorMethod.ByAci,7)
            };
            ObjectId id=layers.Add(created);
            tr.AddNewlyCreatedDBObject(created,true);
            return id;
        }
    }
}
