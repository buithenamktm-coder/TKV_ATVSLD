using System;
using System.Collections.Generic;
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
                // One cleanup traversal for BOTH output layers before adding
                // either 3DFACE collection. Previously the second cleanup
                // re-opened every face just appended for the first TIN.
                ClearExistingPairInTransaction(db, tr, existingLayer, designLayer);
                int ht = ReplaceInTransaction(db, tr, existingLayer, existing, 1, false);
                int tk = ReplaceInTransaction(db, tr, designLayer, design, 3, false);
                // No state adoption or old-store disposal before both writes succeed.
                tr.Commit();
                return new[] { ht, tk };
            }
        }

        private static void ClearExistingPairInTransaction(Database db, Transaction tr,
            string existingLayer, string designLayer)
        {
            // The enclosing ReplacePair transaction owns every erased Face and
            // both new TINs; failure or Esc restores the original two layers.
            var existingId = EnsureLayer(db, tr, existingLayer, 1);
            var designId = EnsureLayer(db, tr, designLayer, 3);
            MoveCurrentLayerAwayIfNeeded(db, tr, existingId);
            MoveCurrentLayerAwayIfNeeded(db, tr, designId);
            foreach (var id in new[] { existingId, designId })
            {
                var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                rec.IsLocked = false;
                rec.IsOff = false;
                if (rec.IsFrozen) rec.IsFrozen = false;
            }
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            using (var progress = new SectionCadProgress(1, "kiểm tra hai layer TIN"))
            {
                int scanned = 0;
                foreach (ObjectId id in ms)
                {
                    if ((scanned++ & 1023) == 0) progress.Poll();
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    string layer = ent.LayerId == existingId ? existingLayer :
                        ent.LayerId == designId ? designLayer : null;
                    if (layer == null) continue;
                    if (!(ent is Face))
                        throw new InvalidOperationException(
                            "Layer " + layer + " chứa đối tượng không phải 3DFACE. " +
                            "Không xóa tự động để tránh mất dữ liệu CAD.");
                    ent.UpgradeOpen();
                    ent.Erase();
                }
                progress.Poll();
            }
        }

        private static int ReplaceInTransaction(Database db, Transaction tr, string layerName,
            TinSurface tin, short aciColor, bool clearExisting = true)
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
                if (clearExisting)
                {
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

        public static int Clear(Database db, string layerName, Action pollCancellation = null)
        {
            // Two phases on the AutoCAD host:
            //   1. Scan + validate every entity on the reserved TIN layer, while
            //      allowing Esc and injected cancellation. DO NOT erase anything.
            //   2. Only after a final cancellation poll, erase the verified faces
            //      in one CAD transaction without interruption.
            //
            // Host regression found that canceling in the middle of DBObject.Erase
            // may leave fewer faces visible after transaction disposal on some
            // AutoCAD side databases. No CAD mutation is therefore allowed before
            // the last cancellation opportunity. The irreversible phase must be
            // atomic and is deliberately not cancellable. Do not insert an Esc
            // poll or any callback into phase 2 without a host-proven rollback.
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrWhiteSpace(layerName))
                throw new ArgumentException("Chưa chọn layer TIN để xóa.", nameof(layerName));

            var faces = new List<ObjectId>();
            using (var progress = new SectionCadProgress(1, "kiểm tra TIN trước khi xóa " + layerName))
            using (var check = db.TransactionManager.StartOpenCloseTransaction())
            {
                var lt = (LayerTable)check.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                {
                    pollCancellation?.Invoke();
                    progress.Poll();
                    return 0;
                }
                var layerId = lt[layerName];
                var bt = (BlockTable)check.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)check.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                int visited = 0;
                foreach (ObjectId id in ms)
                {
                    if ((visited++ & 1023) == 0)
                    {
                        pollCancellation?.Invoke();
                        progress.Poll();
                    }
                    var ent = check.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.LayerId != layerId) continue;
                    if (!(ent is Face))
                        throw new InvalidOperationException(
                            $"Layer {layerName} chứa đối tượng không phải 3DFACE; " +
                            "không xóa tự động để tránh mất dữ liệu CAD.");
                    faces.Add(id);
                }
                // This is the LAST cancellation opportunity. All 3DFACE objects
                // are still untouched, so Esc is guaranteed to preserve the TIN.
                pollCancellation?.Invoke();
                progress.Poll();
            }

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                    throw new InvalidOperationException("Layer TIN bị thay đổi khi chuẩn bị xóa.");
                var layerId = lt[layerName];
                MoveCurrentLayerAwayIfNeeded(db, tr, layerId);
                var outLayer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                outLayer.IsLocked = false;
                outLayer.IsOff = false;
                if (outLayer.IsFrozen) outLayer.IsFrozen = false;

                // No cancellation callbacks, no UI pumping and no intermediate
                // commits after the first Erase(). Treat phase 2 as atomic.
                foreach (ObjectId id in faces)
                {
                    var face = tr.GetObject(id, OpenMode.ForWrite) as Face;
                    if (face == null || face.LayerId != layerId)
                        throw new InvalidOperationException(
                            "TIN thay đổi giữa bước kiểm tra và bước xóa; dừng để bảo vệ bản vẽ.");
                    face.Erase();
                }
                outLayer.IsLocked = true;
                tr.Commit();
            }
            return faces.Count;
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

        internal sealed class LayerInspection
        {
            internal bool Exists;
            internal int Faces;
            internal int Unexpected;
        }

        // One model-space traversal for both TIN layers and both entity counts.
        // No retained database cache: manual edits remain visible next time.
        internal static Dictionary<string, LayerInspection> InspectLayers(Database db,
            IEnumerable<string> names, Action pollCancellation = null)
        {
            var result = new Dictionary<string, LayerInspection>(StringComparer.OrdinalIgnoreCase);
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var byId = new Dictionary<ObjectId, LayerInspection>();
                foreach (string name in names)
                {
                    if (result.ContainsKey(name)) continue;
                    var entry = new LayerInspection { Exists = lt.Has(name) };
                    result.Add(name, entry);
                    if (entry.Exists) byId[lt[name]] = entry;
                }
                if (byId.Count == 0) return result;
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                int visited = 0;
                foreach (ObjectId id in ms)
                {
                    if ((visited++ & 255) == 0) pollCancellation?.Invoke();
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    LayerInspection entry;
                    if (entity == null || !byId.TryGetValue(entity.LayerId, out entry)) continue;
                    if (entity is Face) entry.Faces++;
                    else entry.Unexpected++;
                }
                pollCancellation?.Invoke();
            }
            return result;
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
            if (!VerticalFaceBuilder.IsValidCadFace(t))
                throw new InvalidOperationException(
                    "Mặt đứng 3D không phải tam giác hợp lệ; kiểm tra dữ liệu TIN sau cắt đường bao.");
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}