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

            // Actual AutoCAD 2023 MVSELFTEST exposed incomplete rollback when
            // the second (TK) surface was rejected AFTER erasing both TINs.
            // Perform every user-cancelable/invalid-input check BEFORE CAD writes.
            ValidateSurfaceBeforePairMutation(existing, existingLayer);
            ValidateSurfaceBeforePairMutation(design, designLayer);
            var originalFaces = ValidateExistingPairBeforeMutation(db, existingLayer, designLayer);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // One cleanup traversal for BOTH output layers before adding
                // either 3DFACE collection. Previously the second cleanup
                // re-opened every face just appended for the first TIN.
                ClearExistingPairInTransaction(db, tr, existingLayer, designLayer, originalFaces);
                // Do not allow Esc in the destructive phase: host rollback of
                // erased native 3DFACEs was NOT reliable on this side database.
                int ht = ReplaceInTransaction(db, tr, existingLayer, existing, 1, false, false);
                int tk = ReplaceInTransaction(db, tr, designLayer, design, 3, false, false);
                // No state adoption or old-store disposal before both writes succeed.
                tr.Commit();
                return new[] { ht, tk };
            }
        }

        /// <summary>
        /// Remove BOTH application-owned TINs from an existing DWG, including a
        /// DWG reopened without a restored in-memory TinSurface. All 3DFACE
        /// objects are validated before mutation; both layers are then erased
        /// in ONE transaction, so failure on TK cannot delete only HT.
        /// Does not touch survey entities or change any layer's prior flags.
        /// </summary>
        public static int ClearPair(Database db, string existingLayer, string designLayer,
            Action<Transaction> afterClearInSameTransaction = null)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrWhiteSpace(existingLayer) ||
                string.IsNullOrWhiteSpace(designLayer) ||
                string.Equals(existingLayer, designLayer, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Cần hai layer TIN đầu ra khác nhau.");

            // Inspect the physical DWG, not ProjectState/TinCadFaceCount.
            // Same preflight safety protocol as ReplacePair: reject ANY
            // foreign entity on either reserved layer before erasing a face.
            var verifiedFaces = ValidateExistingPairBeforeMutation(
                db, existingLayer, designLayer);
            if (verifiedFaces.Count == 0 && afterClearInSameTransaction == null)
                return 0;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var existingId = lt.Has(existingLayer) ? lt[existingLayer] : ObjectId.Null;
                var designId = lt.Has(designLayer) ? lt[designLayer] : ObjectId.Null;

                // Only existing output layers are touched. Never synthesize a
                // layer as a side effect of the delete command.
                var originalLocks = new Dictionary<ObjectId, bool>();
                foreach (var layerId in new[] { existingId, designId })
                {
                    if (layerId.IsNull) continue;
                    MoveCurrentLayerAwayIfNeeded(db, tr, layerId);
                    var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                    originalLocks[layerId] = layer.IsLocked;
                    layer.IsLocked = false;
                }

                // One atomic, non-interruptible phase after the last Esc poll.
                // If any entity changed, dispose without Commit to roll back.
                foreach (ObjectId id in verifiedFaces)
                {
                    var face = tr.GetObject(id, OpenMode.ForWrite) as Face;
                    if (face == null ||
                        (face.LayerId != existingId && face.LayerId != designId))
                        throw new InvalidOperationException(
                            "TIN trong DWG thay đổi sau khi kiểm tra; không xóa.");
                    face.Erase();
                }

                foreach (var lockState in originalLocks)
                {
                    var layer = (LayerTableRecord)tr.GetObject(
                        lockState.Key, OpenMode.ForWrite);
                    layer.IsLocked = lockState.Value;
                }
                // Store DWG project flags and erased CAD faces atomically:
                // never let a saved project rebuild the removed TINs at reopen.
                afterClearInSameTransaction?.Invoke(tr);
                tr.Commit();
            }
            return verifiedFaces.Count;
        }

        private static void ValidateSurfaceBeforePairMutation(TinSurface tin, string layer)
        {
            if (tin == null) throw new ArgumentNullException(nameof(tin));
            if (tin.Triangles == null || tin.Triangles.Count == 0)
                throw new InvalidOperationException("TIN " + layer + " không có tam giác để ghi CAD.");
            using (var progress = new SectionCadProgress(1, "kiểm tra TIN " + layer + " trước khi thay thế"))
            {
                int visited = 0;
                foreach (var triangle in tin.Triangles)
                {
                    if ((visited++ & 1023) == 0) progress.Poll();
                    ValidateTriangle(triangle);
                }
                foreach (var triangle in tin.VerticalFaces)
                {
                    if ((visited++ & 1023) == 0) progress.Poll();
                    ValidateVerticalFace(triangle);
                }
                progress.Poll();
            }
        }

        private static List<ObjectId> ValidateExistingPairBeforeMutation(Database db,
            string existingLayer, string designLayer)
        {
            var oldFaces = new List<ObjectId>();
            using (var check = db.TransactionManager.StartOpenCloseTransaction())
            using (var progress = new SectionCadProgress(1, "kiểm tra hai layer TIN"))
            {
                var lt = (LayerTable)check.GetObject(db.LayerTableId, OpenMode.ForRead);
                ObjectId existingId = lt.Has(existingLayer) ? lt[existingLayer] : ObjectId.Null;
                ObjectId designId = lt.Has(designLayer) ? lt[designLayer] : ObjectId.Null;
                var bt = (BlockTable)check.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)check.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                int visited = 0;
                foreach (ObjectId id in ms)
                {
                    if ((visited++ & 1023) == 0) progress.Poll();
                    var entity = check.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;
                    string layer = entity.LayerId == existingId ? existingLayer :
                        entity.LayerId == designId ? designLayer : null;
                    if (layer == null) continue;
                    if (!(entity is Face))
                        throw new InvalidOperationException(
                            "Layer " + layer + " chứa đối tượng không phải 3DFACE. " +
                            "Không xóa tự động để tránh mất dữ liệu CAD.");
                    oldFaces.Add(id);
                }
                // LAST Esc checkpoint: no TIN faces or layer settings touched.
                progress.Poll();
            }
            return oldFaces;
        }

        private static void ClearExistingPairInTransaction(Database db, Transaction tr,
            string existingLayer, string designLayer, IReadOnlyList<ObjectId> originalFaces)
        {
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
            // IDs were checked in a read-only preflight before ANY changes.
            // There is no progress callback or Esc polling during Erase().
            foreach (ObjectId id in originalFaces)
            {
                var face = tr.GetObject(id, OpenMode.ForWrite) as Face;
                if (face == null ||
                    (face.LayerId != existingId && face.LayerId != designId))
                    throw new InvalidOperationException(
                        "TIN thay đổi giữa kiểm tra và ghi CAD; dừng để bảo vệ bản vẽ.");
                face.Erase();
            }
        }

        private static int ReplaceInTransaction(Database db, Transaction tr, string layerName,
            TinSurface tin, short aciColor, bool clearExisting = true,
            bool allowCancellation = true)
        {
            if (tin == null) throw new ArgumentNullException(nameof(tin));
            if (tin.Triangles == null || tin.Triangles.Count == 0)
                throw new InvalidOperationException("TIN không có tam giác để ghi xuống AutoCAD.");

            int written = 0;
            using(var progress=new TinCadProgress(tin.Triangles.Count+tin.VerticalFaces.Count, layerName, allowCancellation))
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
                    if(allowCancellation && (written&1023)==0)progress.Poll(written,true);
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
                    if(allowCancellation && (written&1023)==0)progress.Poll(written,true);
                }

                if (written != tin.Triangles.Count + tin.VerticalFaces.Count)
                    throw new InvalidOperationException("Số mặt TIN ghi CAD không khớp; hoàn tác toàn bộ thao tác.");
                if (allowCancellation) progress.Poll(written, true);
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

        /// <summary>
        /// Rebuild the calculation TIN from actual native 3DFACE geometry in an
        /// existing drawing (no triangulation and NO CAD writes). A prior
        /// source model is not required: older DWGs often contain faces but no
        /// NAMVECTA Project Xrecord. Only our exact reserved output layer is
        /// eligible; arbitrary CAD or survey layers are never auto-imported.
        /// </summary>
        public static TinSurface ReadStoredTin(Database db, string layerName, string modelName)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrWhiteSpace(layerName))
                throw new ArgumentException("Thiếu tên layer TIN.", nameof(layerName));

            var support = new List<MiningVolume.Core.Geometry.Triangle3>();
            var vertical = new List<MiningVolume.Core.Geometry.Triangle3>();
            var zeroEdges = new List<RedundantCadFaceCandidate>();
            using (var progress = new SectionCadProgress(1, "đọc TIN đã lưu trong DWG"))
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!layers.Has(layerName)) return null;
                ObjectId layerId = layers[layerName];
                var blocks = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(
                    blocks[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                int inspected = 0;
                foreach (ObjectId id in model)
                {
                    if ((inspected++ & 2047) == 0) progress.Poll();
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity == null || entity.LayerId != layerId) continue;
                    var face = entity as Face;
                    if (face == null)
                        throw new InvalidOperationException(
                            "Layer " + layerName + " có đối tượng không phải 3DFACE. " +
                            "Không sử dụng như TIN để tránh tính khối lượng sai.");
                    var p0 = face.GetVertexAt(0);
                    var p1 = face.GetVertexAt(1);
                    var p2 = face.GetVertexAt(2);
                    var p3 = face.GetVertexAt(3);
                    if (!Finite(p0) || !Finite(p1) || !Finite(p2) || !Finite(p3))
                        throw new InvalidOperationException(
                            "Layer " + layerName + " có 3DFACE chứa tọa độ không hữu hạn.");
                    if (!Close(p2, p3))
                        throw new InvalidOperationException(
                            "Layer " + layerName + " có mặt tứ giác. " +
                            "Không tự suy đoán cách chia thành tam giác TIN.");
                    var triangle = new MiningVolume.Core.Geometry.Triangle3(
                        new MiningVolume.Core.Geometry.Vec3(p0.X,p0.Y,p0.Z),
                        new MiningVolume.Core.Geometry.Vec3(p1.X,p1.Y,p1.Z),
                        new MiningVolume.Core.Geometry.Vec3(p2.X,p2.Y,p2.Z));
                    if (triangle.Area2D <= 1e-12)
                    {
                        // Vertical faces are retained as geometry evidence, but
                        // must never enter the single-Z grid support sampler.
                        double d01 = p0.DistanceTo(p1), d12 = p1.DistanceTo(p2),
                            d20 = p2.DistanceTo(p0);
                        if (d01 <= 1e-9 || d12 <= 1e-9 || d20 <= 1e-9)
                        {
                            // Never round a very short, meaningful edge to zero.
                            // Only exactly coincident 3D points qualify for proof.
                            Point3d e0, e1;
                            if (Same3D(p0, p1)) { e0 = p0; e1 = p2; }
                            else if (Same3D(p1, p2)) { e0 = p1; e1 = p0; }
                            else if (Same3D(p2, p0)) { e0 = p2; e1 = p1; }
                            else throw InvalidStoredCadFace(layerName, face.Handle.ToString(),
                                "Cạnh 3D rất ngắn nhưng hai đỉnh không trùng chính xác");
                            if (Same3D(e0, e1))
                                throw InvalidStoredCadFace(layerName, face.Handle.ToString(),
                                    "Cả ba đỉnh trùng nhau, không có cạnh để đối chiếu");
                            zeroEdges.Add(new RedundantCadFaceCandidate(e0, e1,
                                face.Handle.ToString()));
                            continue;
                        }
                        vertical.Add(triangle);
                    }
                    else
                        support.Add(triangle);
                }
                progress.Poll();
            }
            if (support.Count == 0)
            {
                if (vertical.Count == 0 && zeroEdges.Count == 0) return null;
                throw new InvalidOperationException(
                    "Layer " + layerName + " không có tam giác TIN hợp lệ phủ XY; " +
                    "không tự loại mặt lỗi để tạo kết quả tính khối lượng.");
            }
            // Check each distinct suspect edge against valid support edges in
            // a SINGLE pass. Prior code rescanned every TIN triangle for every
            // collapsed face (O(suspects * triangles)) and rejected at 32.
            // Never accept an unproven edge; never modify the saved 3DFACEs.
            var unproven = new HashSet<ExactStoredEdge>();
            foreach (var suspect in zeroEdges)
                unproven.Add(new ExactStoredEdge(suspect.A, suspect.B));
            if (unproven.Count > 0)
            {
                foreach (var t in support)
                {
                    unproven.Remove(new ExactStoredEdge(
                        new Point3d(t.A.X, t.A.Y, t.A.Z),
                        new Point3d(t.B.X, t.B.Y, t.B.Z)));
                    unproven.Remove(new ExactStoredEdge(
                        new Point3d(t.B.X, t.B.Y, t.B.Z),
                        new Point3d(t.C.X, t.C.Y, t.C.Z)));
                    unproven.Remove(new ExactStoredEdge(
                        new Point3d(t.C.X, t.C.Y, t.C.Z),
                        new Point3d(t.A.X, t.A.Y, t.A.Z)));
                    if (unproven.Count == 0) break;
                }
                foreach (var suspect in zeroEdges)
                    if (unproven.Contains(new ExactStoredEdge(suspect.A, suspect.B)))
                        throw InvalidStoredCadFace(layerName, suspect.Handle,
                            "Cạnh còn lại không thuộc tam giác TIN hợp lệ. " +
                            "Không chứng minh được việc bỏ qua là an toàn");
            }
            var issues = new List<ValidationIssue>();
            if (zeroEdges.Count > 0)
                issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                    "REDUNDANT_ZERO_EDGE_CAD_FACE",
                    layerName + ": xác minh " + zeroEdges.Count +
                    " mặt 3DFACE suy biến có cạnh còn lại trùng chính xác cạnh TIN hợp lệ; " +
                    "không sửa/xóa DWG. Handle đầu tiên: " +
                    zeroEdges[0].Handle, zeroEdges[0].Handle));
            return new TinSurface(modelName, support, issues, vertical,
                verifiedRedundantCadFaces: zeroEdges.Count);
        }

        private sealed class RedundantCadFaceCandidate
        {
            public RedundantCadFaceCandidate(Point3d a, Point3d b, string handle)
            { A = a; B = b; Handle = handle; }
            public Point3d A { get; }
            public Point3d B { get; }
            public string Handle { get; }
        }

        private struct ExactStoredEdge : IEquatable<ExactStoredEdge>
        {
            internal readonly Point3d A, B;
            internal ExactStoredEdge(Point3d a, Point3d b) { A = a; B = b; }
            public bool Equals(ExactStoredEdge e)
                => (Same3D(A, e.A) && Same3D(B, e.B)) ||
                   (Same3D(A, e.B) && Same3D(B, e.A));
            public override bool Equals(object value)
                => value is ExactStoredEdge && Equals((ExactStoredEdge)value);
            public override int GetHashCode()
            {
                // Symmetric hash for the same undirected 3D edge. Normalize -0.
                unchecked { return PointHash(A) ^ PointHash(B); }
            }
            private static int PointHash(Point3d p)
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + NormalizedBitsHash(p.X);
                    h = h * 31 + NormalizedBitsHash(p.Y);
                    return h * 31 + NormalizedBitsHash(p.Z);
                }
            }
            private static int NormalizedBitsHash(double value)
            {
                long bits = BitConverter.DoubleToInt64Bits(value == 0.0 ? 0.0 : value);
                return unchecked((int)bits ^ (int)(bits >> 32));
            }
        }

        private static bool Same3D(Point3d a, Point3d b)
            => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        private static bool Same3D(MiningVolume.Core.Geometry.Vec3 a, Point3d b)
            => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        private static InvalidOperationException InvalidStoredCadFace(
            string layer, string handle, string reason)
            => new InvalidOperationException(
                "Layer " + layer + " có 3DFACE suy biến, Handle " + handle +
                ". " + reason +
                ". Giữ nguyên DWG; kiểm tra đối tượng theo Handle hoặc dựng lại " +
                "TIN từ nguồn trên bản sao DWG trước khi tính khối lượng.");

        private static bool Finite(Point3d point)
            => !double.IsNaN(point.X) && !double.IsNaN(point.Y) &&
               !double.IsNaN(point.Z) && !double.IsInfinity(point.X) &&
               !double.IsInfinity(point.Y) && !double.IsInfinity(point.Z);

        private static bool Close(Point3d a, Point3d b)
            => a.DistanceTo(b) <= 1e-7;

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