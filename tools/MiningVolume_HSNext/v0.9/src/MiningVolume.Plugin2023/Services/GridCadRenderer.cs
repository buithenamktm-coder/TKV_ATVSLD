using System;
using System.Threading;
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
        internal const string CalculationIdApp = "IMSAT_GRID_CALCULATION";

        public static void Draw(Database database, GridVolumeReport report, bool labels,
            Action<int, int> progress = null, CancellationToken cancellationToken = default(CancellationToken),
            Action pollCancellation = null)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (report == null || report.Cells == null) throw new InvalidOperationException("Chưa có kết quả lưới.");
            // Matching the calculator's existing 100,000-cell safety budget.
            // Each grid cell is ONE native editable closed polyline; do not
            // arbitrarily reject usable 5,001+ cell CAD drawings.
            if (report.Cells.Count > 100000)
                throw new InvalidOperationException(
                    "Hệ lưới vượt 100.000 ô; hãy tăng kích thước ô hoặc chia vùng tính.");
            cancellationToken.ThrowIfCancellationRequested();
            pollCancellation?.Invoke();
            using (var tr = database.TransactionManager.StartTransaction())
            using (var identity = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, CalculationIdApp),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, report.CalculationId)))
            {
                // Explicitly abort before leaving scope on Esc; real AutoCAD
                // host regression found canceled labels persisted as DBText.
                try
                {
                var apps = (RegAppTable)tr.GetObject(database.RegAppTableId, OpenMode.ForRead);
                if (!apps.Has(CalculationIdApp))
                {
                    apps.UpgradeOpen();
                    var app = new RegAppTableRecord { Name = CalculationIdApp };
                    apps.Add(app);
                    tr.AddNewlyCreatedDBObject(app, true);
                }
                var bt = (BlockTable)tr.GetObject(database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                var cut=EnsureLayer(database,tr,CutLayer,1);
                var fill=EnsureLayer(database,tr,FillLayer,5);
                var mixed=EnsureLayer(database,tr,MixedLayer,6);
                var noData=EnsureLayer(database,tr,NoDataLayer,8);
                var empty=EnsureLayer(database,tr,EmptyLayer,7);
                var text=EnsureLayer(database,tr,LabelLayer,7);

                int completed = 0;
                int total = report.Cells.Count;
                foreach(var cell in report.Cells)
                {
                    if ((completed & 63) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        pollCancellation?.Invoke();
                    }
                    if (cell == null || cell.Corners == null || cell.Corners.Count < 3)
                        throw new InvalidOperationException("Ô lưới có đường bao không hợp lệ.");
                    var v=cell.Volume;
                    ObjectId layer = cell.IsVerticalConflict ? noData : !v.IsComplete ? noData :
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
                    // XData follows each editable cell through save/reopen and
                    // multiple exports, without adding visible CAD entities.
                    poly.XData = identity;
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
                            TextString= cell.IsVerticalConflict
                                ? cell.CellCode + " NHIEU Z - CHUA XAC NHAN"
                                : !v.IsComplete
                                ? cell.CellCode + (cell.CoverageStatus == "Thiếu phủ TIN hiện trạng"
                                    ? " THIEU HT"
                                    : cell.CoverageStatus == "Thiếu phủ TIN thiết kế"
                                        ? " THIEU TK"
                                        : cell.CoverageStatus == "Thiếu cả TIN HT/TK"
                                            ? " THIEU HT+TK"
                                            : " LOI GIAO TIN")
                                : cell.CellCode + " D" + v.CutVolume.ToString("0.0") +
                                    " / P" + v.FillVolume.ToString("0.0")
                        };
                        model.AppendEntity(label);
                        tr.AddNewlyCreatedDBObject(label,true);
                    }
                    completed++;
                    if ((completed & 1023) == 0 || completed == total)
                        progress?.Invoke(completed, total);
                }
                cancellationToken.ThrowIfCancellationRequested();
                pollCancellation?.Invoke();
                // Commit once only: any failure leaves no partial CAD grid.
                tr.Commit();
                }
                catch
                {
                    tr.Abort();
                    throw;
                }
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
