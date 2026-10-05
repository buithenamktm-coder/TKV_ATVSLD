using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Surface;

namespace MiningVolume2023.Services
{
    internal static class TinCadRenderer
    {
        public static void PrepareLayer(Database db, string layerName, short aciColor)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var id = EnsureLayer(db, tr, layerName, aciColor);
                var ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                ltr.IsLocked = true;
                tr.Commit();
            }
        }

        public static int Replace(Database db, string layerName, TinSurface tin, short aciColor)
        {
            if (tin == null) throw new System.ArgumentNullException(nameof(tin));
            if (tin.Triangles == null || tin.Triangles.Count == 0)
                throw new System.InvalidOperationException("TIN không có tam giác để ghi xuống AutoCAD.");

            int written = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layerId = EnsureLayer(db, tr, layerName, aciColor);
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // Output layers are dedicated to MiningVolume TIN. Replacing a TIN must
                // remove the entire previous representation so stale triangles can never
                // be mixed with a newly built surface.
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
                    ValidateTriangle(t);
                    var a = new Point3d(t.A.X, t.A.Y, t.A.Z);
                    var b = new Point3d(t.B.X, t.B.Y, t.B.Z);
                    var c = new Point3d(t.C.X, t.C.Y, t.C.Z);
                    var face = new Face(a, b, c, c, true, true, true, false)
                    {
                        LayerId = layerId,
                        ColorIndex = 256 // ByLayer
                    };
                    ms.AppendEntity(face);
                    tr.AddNewlyCreatedDBObject(face, true);
                    written++;
                }

                var outLayer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                outLayer.IsLocked = true;
                outLayer.IsOff = false;
                tr.Commit();
            }
            return written;
        }

        public static int Clear(Database db, string layerName)
        {
            int erased = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) { tr.Commit(); return 0; }
                var layerId = lt[layerName];
                var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                ltr.IsLocked = false;
                ltr.IsOff = false;

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null && ent.LayerId == layerId)
                    {
                        ent.UpgradeOpen();
                        ent.Erase();
                        erased++;
                    }
                }
                ltr.IsLocked = true;
                tr.Commit();
            }
            return erased;
        }

        public static int CountFaces(Database db, string layerName)
        {
            int count = 0;
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) return 0;
                var layerId = lt[layerName];
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is Face && ent.LayerId == layerId) count++;
                }
            }
            return count;
        }

        public static bool LayerExists(Database db, string layerName)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                return lt.Has(layerName);
            }
        }

        public static void SetVisible(Database db, string layerName, bool visible)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) { tr.Commit(); return; }
                var ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
                if (visible && ltr.IsFrozen) ltr.IsFrozen = false;
                ltr.IsOff = !visible;
                ltr.IsLocked = true;
                tr.Commit();
            }
        }

        public static bool DeleteLayerIfEmpty(Database db, string layerName)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) { tr.Commit(); return true; }
                var layerId = lt[layerName];
                if (db.Clayer == layerId) { tr.Commit(); return false; }

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null && ent.LayerId == layerId) { tr.Commit(); return false; }
                }

                var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                ltr.IsLocked = false;
                ltr.Erase();
                tr.Commit();
                return true;
            }
        }

        private static ObjectId EnsureLayer(Database db, Transaction tr, string name, short color)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            LayerTableRecord ltr;
            ObjectId id;
            if (lt.Has(name))
            {
                id = lt[name];
                ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
            }
            else
            {
                lt.UpgradeOpen();
                ltr = new LayerTableRecord { Name = name };
                id = lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }

            // Never leave a calculation-output layer as the current drawing layer.
            // This avoids accidental drafting into TIN layers and lets us lock them safely.
            if (db.Clayer == id && lt.Has("0"))
                db.Clayer = lt["0"];

            // A dedicated TIN output layer must always be writable and visible during build.
            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, color);
            ltr.IsLocked = false;
            if (ltr.IsFrozen) ltr.IsFrozen = false;
            ltr.IsOff = false;
            return id;
        }

        private static void ValidateTriangle(MiningVolume.Core.Geometry.Triangle3 t)
        {
            if (!Finite(t.A.X) || !Finite(t.A.Y) || !Finite(t.A.Z) ||
                !Finite(t.B.X) || !Finite(t.B.Y) || !Finite(t.B.Z) ||
                !Finite(t.C.X) || !Finite(t.C.Y) || !Finite(t.C.Z))
                throw new System.InvalidOperationException("TIN có tam giác chứa tọa độ NaN/Infinity.");
            if (t.Area2D <= 1e-12)
                throw new System.InvalidOperationException("TIN có tam giác suy biến, diện tích XY gần bằng 0.");
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}