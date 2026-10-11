using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Grid;

namespace MiningVolume2023.Services
{
    /// <summary>Vẽ ô lưới vào AutoCAD theo lớp đào, đắp, hỗn hợp và thiếu dữ liệu.</summary>
    internal static class GridCadRenderer
    {
        private const string CutLayer = "MV_LUOI_DAO";
        private const string FillLayer = "MV_LUOI_DAP";
        private const string MixedLayer = "MV_LUOI_HONHOP";
        private const string NoDataLayer = "MV_LUOI_THIEU_DL";
        private const string EmptyLayer = "MV_LUOI_O";
        private const string LabelLayer = "MV_LUOI_NHAN";

        public static void Draw(Database database, GridVolumeReport report, bool labels)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (report == null || report.Cells == null) throw new InvalidOperationException("Chưa có kết quả lưới.");
            if (report.Cells.Count > 5000)
                throw new InvalidOperationException("Hệ lưới trên 5.000 ô: hãy thu nhỏ phạm vi hoặc xuất Excel để tránh tạo quá nhiều đối tượng CAD.");
            using (var tr = database.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                var cut=EnsureLayer(database,tr,CutLayer,1);
                var fill=EnsureLayer(database,tr,FillLayer,5);
                var mixed=EnsureLayer(database,tr,MixedLayer,6);
                var noData=EnsureLayer(database,tr,NoDataLayer,8);
                var empty=EnsureLayer(database,tr,EmptyLayer,7);
                var text=EnsureLayer(database,tr,LabelLayer,7);

                foreach(var cell in report.Cells)
                {
                    var v=cell.Volume;
                    ObjectId layer = !v.IsComplete ? noData :
                        v.CutVolume > 1e-8 && v.FillVolume > 1e-8 ? mixed :
                        v.CutVolume > 1e-8 ? cut : v.FillVolume > 1e-8 ? fill : empty;
                    var poly = new Polyline(cell.Corners.Count) { LayerId=layer, Closed=true };
                    double x=0,y=0;
                    for(int i=0;i<cell.Corners.Count;i++)
                    {
                        var point=cell.Corners[i];
                        poly.AddVertexAt(i,new Point2d(point.X,point.Y),0,0,0);
                        x+=point.X; y+=point.Y;
                    }
                    model.AppendEntity(poly);
                    tr.AddNewlyCreatedDBObject(poly,true);
                    if(labels)
                    {
                        x/=cell.Corners.Count; y/=cell.Corners.Count;
                        double length=cell.Corners[0].DistanceTo(cell.Corners[1]);
                        double height=Math.Max(0.4,Math.Min(3.0,length/12.0));
                        var label=new DBText
                        {
                            LayerId=text,
                            Position=new Point3d(x,y,0),
                            Height=height,
                            TextString= !v.IsComplete
                                ? cell.CellCode + " THIEU TIN"
                                : cell.CellCode + " D" + v.CutVolume.ToString("0.0") +
                                    " / P" + v.FillVolume.ToString("0.0")
                        };
                        model.AppendEntity(label);
                        tr.AddNewlyCreatedDBObject(label,true);
                    }
                }
                tr.Commit();
            }
        }

        private static ObjectId EnsureLayer(Database db,Transaction tr,string name,short aci)
        {
            var table=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
            if(table.Has(name))
            {
                var existing=(LayerTableRecord)tr.GetObject(table[name],OpenMode.ForRead);
                if(existing.IsLocked || existing.IsFrozen || existing.IsOff)
                    throw new InvalidOperationException("Layer "+name+" đang khóa/tắt; không thể ghi hệ lưới.");
                return table[name];
            }
            table.UpgradeOpen();
            var record=new LayerTableRecord
            {
                Name=name,
                Color=Color.FromColorIndex(ColorMethod.ByAci,aci)
            };
            var id=table.Add(record);
            tr.AddNewlyCreatedDBObject(record,true);
            return id;
        }
    }
}
