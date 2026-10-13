using System;
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
                var layerId = EnsureLayer(db, tr, layerName, aciColor);
                MoveCurrentLayerAwayIfNeeded(db, tr, layerId);
                var outLayer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                outLayer.Color = Color.FromColorIndex(ColorMethod.ByAci, aciColor);
                outLayer.IsOff = false;
                if (outLayer.IsFrozen) outLayer.IsFrozen = false;
                outLayer.IsLocked = true;
                tr.Commit();
            }
        }

        public static int Replace(Database db, string layerName, TinSurface tin, short aciColor)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                int written = ReplaceInTransaction(db, tr, layerName, tin, aciColor);
                tr.Commit();
                return written;
            }
        }

        public static int[] ReplacePair(Database db, string existingLayer, TinSurface existing,
            string designLayer, TinSurface design)
        {
            if (string.Equals(existingLayer, designLayer, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Hai TIN phải có hai layer riêng.");
            using (var tr = db.TransactionManager.StartTransaction())
            {
                int ht = ReplaceInTransaction(db, tr, existingLayer, existing, 1);
                int tk = ReplaceInTransaction(db, tr, designLayer, design, 3);
                // No state adoption or old-store disposal before both writes succeed.
                tr.Commit();
                return new[] { ht, tk };
            }
        }

        private static int ReplaceInTransaction(Database db, Transaction tr, string layerName, TinSurface tin, short aciColor)
        {
            if (tin == null) throw new ArgumentNullException(nameof(tin));
            if (tin.Triangles == null || tin.Triangles.Count == 0)
                throw new InvalidOperationException("TIN không có tam giác để ghi xuống AutoCAD.");

            int written = 0;
            using(var progress=new TinCadProgress(tin.Triangles.Count+tin.VerticalFaces.Count, layerName))
            {
                var layerId = EnsureLayer(db, tr, layerName, aciColor);
                MoveCurrentLayerAwayIfNeeded(db, tr, layerId);
                var outLayer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                outLayer.IsLocked = false;
                outLayer.IsOff = false;
                if (outLayer.IsFrozen) outLayer.IsFrozen = false;
                outLayer.Color = Color.FromColorIndex(ColorMethod.ByAci, aciColor);

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // One ModelSpace pass only: validate the reserved layer and erase the
                // previous MiningVolume faces in the same transaction. This avoids a
                // second full scan on large mine drawings.
                int scanned=0;
                foreach (ObjectId id in ms)
                {
                    if((++scanned&1023)==0)progress.Poll(0,false);
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.LayerId != layerId) continue;
                    if (!(ent is Face))
                        throw new InvalidOperationException(
                            $"Layer {layerName} chứa đối tượng không phải 3DFACE. " +
                            "MiningVolume không xóa tự động để tránh mất dữ liệu CAD. " +
                            "Hãy chuyển đối tượng đó sang layer khác rồi tạo lại TIN.");
                    ent.UpgradeOpen();
                    ent.Erase();
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
                        ColorIndex = 256
                    };
                    ms.AppendEntity(face);
                    tr.AddNewlyCreatedDBObject(face, true);
                    written++;
                    if((written&1023)==0)progress.Poll(written,true);
                }

                // These faces have zero XY area by design. They are valid
                // 3D triangles with one upright edge, not planar CDT cells.
                foreach (var t in tin.VerticalFaces)
                {
                    ValidateVerticalFace(t);
                    var a = new Point3d(t.A.X, t.A.Y, t.A.Z);
                    var b = new Point3d(t.B.X, t.B.Y, t.B.Z);
                    var c = new Point3d(t.C.X, t.C.Y, t.C.Z);
                    var face = new Face(a, b, c, c, true, true, true, false)
                    {
                        LayerId = layerId,
                        ColorIndex = 256
                    };
                    ms.AppendEntity(face);
                    tr.AddNewlyCreatedDBObject(face, true);
                    written++;
                    if((written&1023)==0)progress.Poll(written,true);
                }

                if (written != tin.Triangles.Count + tin.VerticalFaces.Count)
                    throw new InvalidOperationException("Số mặt TIN ghi CAD không khớp; hoàn tác toàn bộ thao tác.");
                progress.Poll(written, true);
                outLayer.IsLocked = true;
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
                MoveCurrentLayerAwayIfNeeded(db, tr, layerId);
                var outLayer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                outLayer.IsLocked = false;
                outLayer.IsOff = false;
                if (outLayer.IsFrozen) outLayer.IsFrozen = false;

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                // Validate and erase in one pass. A dedicated TIN layer must contain
                // faces only; foreign CAD entities are never deleted automatically.
                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.LayerId != layerId) continue;
                    if (!(ent is Face))
                        throw new InvalidOperationException(
                            $"Layer {layerName} chứa đối tượng không phải 3DFACE; " +
                            "không xóa tự động để tránh mất dữ liệu CAD.");
                    ent.UpgradeOpen();
                    ent.Erase();
                    erased++;
                }

                outLayer.IsLocked = true;
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

        public static int CountUnexpectedEntities(Database db, string layerName)
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
                    if (ent != null && ent.LayerId == layerId && !(ent is Face)) count++;
                }
            }
            return count;
        }

        public static bool IsLayerVisible(Database db, string layerName)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) return false;
                var ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                return !ltr.IsOff && !ltr.IsFrozen;
            }
        }

        public static bool IsLayerLocked(Database db, string layerName)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) return false;
                var ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                return ltr.IsLocked;
            }
        }

        public static short? GetLayerColorIndex(Database db, string layerName)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) return null;
                var ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                return ltr.Color.ColorIndex;
            }
        }

        public static void SetVisible(Database db, string layerName, bool visible)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName)) { tr.Commit(); return; }
                var ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
                ltr.IsLocked = false;
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
                if (db.Clayer == layerId) MoveCurrentLayerAwayIfNeeded(db, tr, layerId);

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null && ent.LayerId == layerId)
                    {
                        tr.Commit();
                        return false;
                    }
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

            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, color);
            ltr.IsLocked = false;
            if (ltr.IsFrozen) ltr.IsFrozen = false;
            ltr.IsOff = false;
            return id;
        }

        private static void MoveCurrentLayerAwayIfNeeded(Database db, Transaction tr, ObjectId outputLayerId)
        {
            if (db.Clayer != outputLayerId) return;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has("0")) db.Clayer = lt["0"];
        }

        private static void ValidateTriangle(MiningVolume.Core.Geometry.Triangle3 t)
        {
            TinTriangleQuality.ValidateForCad(t);
        }

        private static void ValidateVerticalFace(MiningVolume.Core.Geometry.Triangle3 t)
        {
            if (!Finite(t.A.X) || !Finite(t.A.Y) || !Finite(t.A.Z) ||
                !Finite(t.B.X) || !Finite(t.B.Y) || !Finite(t.B.Z) ||
                !Finite(t.C.X) || !Finite(t.C.Y) || !Finite(t.C.Z))
                throw new InvalidOperationException("Mặt đứng 3D có tọa độ không hợp lệ.");
            // Polygon clipping can triangulate the upright curtain with
            // the vertical edge at AB, BC OR CA. The previous validator
            // demanded AB only and rejected correctly clipped 3D triangles.
            // Require any vertical pair and a third XY-distinct point.
            // Never accept an arbitrary flat or zero-area face.
            bool ab = VerticalEdge(t.A, t.B, t.C);
            bool bc = VerticalEdge(t.B, t.C, t.A);
            bool ca = VerticalEdge(t.C, t.A, t.B);
            if (!(ab || bc || ca))
                throw new InvalidOperationException("Mặt đứng 3D không phải tam giác hợp lệ.");
        }

        private static bool VerticalEdge(MiningVolume.Core.Geometry.Vec3 a,
            MiningVolume.Core.Geometry.Vec3 b, MiningVolume.Core.Geometry.Vec3 other)
        {
            return a.XY.DistanceTo(b.XY) <= 1e-6 &&
                   Math.Abs(a.Z - b.Z) > 1e-8 &&
                   a.XY.DistanceTo(other.XY) > 1e-8;
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}