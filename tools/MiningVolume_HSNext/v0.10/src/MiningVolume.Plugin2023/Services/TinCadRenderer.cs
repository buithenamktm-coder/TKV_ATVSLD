using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Surface;

namespace MiningVolume2023.Services
{
    internal static class TinCadRenderer
    {
        public static void Replace(Database db, string layerName, TinSurface tin, short aciColor)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layerId = EnsureLayer(db, tr, layerName, aciColor);
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null && ent.LayerId == layerId)
                    {
                        ent.UpgradeOpen();
                        ent.Erase();
                    }
                }

                foreach (var t in tin.Triangles)
                {
                    var a = new Point3d(t.A.X, t.A.Y, t.A.Z);
                    var b = new Point3d(t.B.X, t.B.Y, t.B.Z);
                    var c = new Point3d(t.C.X, t.C.Y, t.C.Z);
                    var face = new Face(a, b, c, c, true, true, true, false) { LayerId = layerId };
                    ms.AppendEntity(face);
                    tr.AddNewlyCreatedDBObject(face, true);
                }
                tr.Commit();
            }
        }

        public static void SetVisible(Database db, string layerName, bool visible)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) { tr.Commit(); return; }
                var ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
                ltr.IsOff = !visible;
                tr.Commit();
            }
        }

        private static ObjectId EnsureLayer(Database db, Transaction tr, string name, short color)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return lt[name];
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name = name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, color)
            };
            var id = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return id;
        }
    }
}