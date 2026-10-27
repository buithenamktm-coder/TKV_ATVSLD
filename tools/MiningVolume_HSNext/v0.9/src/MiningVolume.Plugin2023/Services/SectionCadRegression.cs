using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Sections;
using MiningVolume.Core.Volumes;
using MiningVolume.Core.Grid;
using MiningVolume.Core.Surface;

namespace MiningVolume2023.Services
{
    // Requires the real AutoCAD runtime. Uses disposable side databases only.
    // Does not touch the user's drawing or substitute a mock for host rollback.
    internal static class SectionCadRegression
    {
        internal static void Run(Action<string> reportPassed = null)
        {
            // Each group owns disposable side databases. A failed fixture must
            // not hide failures in unrelated groups until another installation.
            var errors = new List<Exception>();
            RunGroup("Boundary selection", () => { VerifyBoundarySelectionSkipsTinAndSupportsCancel(); }, errors, reportPassed);
            RunGroup("TIN clear rollback", () => { VerifyTinClearRollbackOnCancelAndForeignObject(); }, errors, reportPassed);
            RunGroup("TIN pair rollback", () => { VerifyTinPairRollbackAfterSecondSurfaceFailure(); }, errors, reportPassed);
            RunGroup("Stored CAD TIN degenerate-face safety", VerifyStoredCadTinDegenerateFaceSafety, errors, reportPassed);
            RunGroup("Section erase preserves TIN", () => { VerifySectionEraseSkipsTinFaces(); }, errors, reportPassed);
            RunGroup("Quantity block rollback", () => { VerifyTableRollback(); }, errors, reportPassed);
            RunGroup("Grid calculation identity", () => { VerifyGridIdentity(); }, errors, reportPassed);
            RunGroup("Section layout and cancellation", VerifySectionLayoutAndCancellation, errors, reportPassed);
            if (errors.Count > 0)
                throw new AggregateException("CAD regression groups failed: " + errors.Count, errors);
        }

        private static void RunGroup(string name, Action test, List<Exception> errors, Action<string> reportPassed)
        {
            try { test(); }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException("CAD group: " + name, ex));
                return;
            }
            reportPassed?.Invoke(name);
        }

        private static void VerifySectionLayoutAndCancellation()
        {
            var profiles = new List<SectionProfile>();
            for (int i = 0; i < 25; i++)
            {
                var line = new SectionLine(i, "QA-" + i, i * 20,
                    new Vec2(1, 0), new Vec2(0, 1), new[] { new SectionSpan(0, 300) });
                var segments = new List<SectionProfileSegment>();
                for (int j = 0; j < 300; j++)
                    segments.Add(new SectionProfileSegment(j, j + 1, line.PointAt(j), line.PointAt(j + 1),
                        100 + i, 100 + i, 90, 90));
                profiles.Add(new SectionProfile(line, segments, 300 * (10 + i), 0, new string[0]));
            }
            using (var quick = new Database(true, true))
            {
                int polls = 0;
                SectionCadRenderer.DrawQuickProfiles(quick, profiles, Point3d.Origin, 1000, 500, 5, 90, () => polls++);
                string accepted = Snapshot(quick, true);
                foreach (int cancelAt in new[] { 1, polls / 2, polls })
                {
                    int count = 0; bool canceled = false;
                    try { SectionCadRenderer.DrawQuickProfiles(quick, profiles, Point3d.Origin, 1000, 500, 5, 90,
                        () => { if (++count == cancelAt) throw new OperationCanceledException(); }); }
                    catch (OperationCanceledException) { canceled = true; }
                    if (!canceled || Snapshot(quick, true) != accepted)
                        throw new InvalidOperationException("Quick-section cancellation did not restore original drawing.");
                }
                var inspection = TinCadRenderer.InspectLayers(quick, new[] { "MV_MC_NHANH_HT", "ABSENT_QA" });
                if (!inspection["MV_MC_NHANH_HT"].Exists || inspection["MV_MC_NHANH_HT"].Faces != 0 ||
                    inspection["MV_MC_NHANH_HT"].Unexpected != TinCadRenderer.CountUnexpectedEntities(quick,"MV_MC_NHANH_HT") ||
                    inspection["ABSENT_QA"].Exists)
                    throw new InvalidOperationException("Batched TIN inspection lost unexpected/missing layer state.");
            }
            using (var whole = new Database(true, true))
            using (var batched = new Database(true, true))
            {
                SectionCadRenderer.ReplaceProfiles(whole, profiles, Point3d.Origin, 1000, 500, 5, 90);
                int totalPolls = 0;
                DrawBatches(batched, profiles, () => totalPolls++);
                if (Snapshot(whole) != Snapshot(batched))
                    throw new InvalidOperationException("Section batch carry changed CAD geometry or labels.");
                // Exercise the renderer directly, without an outer transaction:
                // cancellation at its final poll must undo deletion and redraw.
                int directPolls = 0;
                SectionCadRenderer.ReplaceProfilesBatched(whole, profiles, Point3d.Origin,
                    1000, 500, 5, 0, profiles.Count, true, pollCancellation: () => directPolls++);
                string directBefore = Snapshot(whole, true);
                int readPolls = 0;
                var reread = EditableSectionCadService.ReadEditedProfiles(whole, profiles,
                    pollCancellation: () => readPolls++);
                if (!EditedSectionProfileBuilder.SameGeometry(profiles, reread))
                    throw new InvalidOperationException("CAD edit reader changed accepted geometry.");
                foreach (int stop in new[] { 1, readPolls / 2, readPolls })
                {
                    int calls = 0; bool canceled = false;
                    try { EditableSectionCadService.ReadEditedProfiles(whole, profiles,
                        pollCancellation: () => { if (++calls == stop) throw new OperationCanceledException(); }); }
                    catch (OperationCanceledException) { canceled = true; }
                    if (!canceled || Snapshot(whole, true) != directBefore)
                        throw new InvalidOperationException("Canceled edit read changed CAD geometry.");
                }
                // Different areas ensure rollback is checked after real writes.
                var changedAreas = new List<SectionProfile>();
                foreach (var profile in profiles)
                    changedAreas.Add(new SectionProfile(profile.Line, profile.Segments,
                        profile.CutArea + 123, profile.FillArea + 45, new string[0]));
                int labelPolls = 0;
                EditableSectionCadService.UpdateAreaLabels(whole, profiles, () => labelPolls++);
                foreach (int stop in new[] { 1, labelPolls / 2, labelPolls })
                {
                    int calls = 0; bool canceled = false;
                    try { EditableSectionCadService.UpdateAreaLabels(whole, changedAreas,
                        () => { if (++calls == stop) throw new OperationCanceledException(); }); }
                    catch (OperationCanceledException) { canceled = true; }
                    if (!canceled || Snapshot(whole, true) != directBefore)
                        throw new InvalidOperationException("Canceled area update left partial labels.");
                }

                int directCount = 0; bool directCanceled = false;
                try {
                    SectionCadRenderer.ReplaceProfilesBatched(whole, profiles, Point3d.Origin,
                        1000, 500, 5, 0, profiles.Count, true, pollCancellation: () => {
                            if (++directCount == directPolls) throw new OperationCanceledException();
                        });
                } catch (OperationCanceledException) { directCanceled = true; }
                if (!directCanceled || Snapshot(whole, true) != directBefore)
                    throw new InvalidOperationException("Final renderer cancellation failed to restore drawing.");
                string before = Snapshot(batched, true);
                // Include cancellation during deletion, first batch, and after
                // an inner transaction has committed (the outer must undo it).
                foreach (int cancelAt in new[] { 1, totalPolls / 2, totalPolls - 1 })
                {
                    int polls = 0;
                    bool canceled = false;
                    try
                    {
                        DrawBatches(batched, profiles, () =>
                        {
                            if (++polls >= cancelAt) throw new OperationCanceledException();
                        });
                    }
                    catch (OperationCanceledException) { canceled = true; }
                    if (!canceled || Snapshot(batched, true) != before)
                        throw new InvalidOperationException("Canceled section export did not restore original entities.");
                }
            }
        }

        private static void VerifyBoundarySelectionSkipsTinAndSupportsCancel()
        {
            // Host side-database proof: a normal LWPOLYLINE boundary must
            // be found without treating millions of TIN faces as candidates.
            // Repeated boundaries and Esc must never silently accept a
            // different region or change previously stored CAD entities.
            using (var db = new Database(true, true))
            {
                string selectedHandle = null;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    var b = new LayerTableRecord { Name = "QA_BOUNDARY" };
                    var bId = lt.Add(b);
                    tr.AddNewlyCreatedDBObject(b, true);
                    var t = new LayerTableRecord { Name = "QA_TIN" };
                    var tId = lt.Add(t);
                    tr.AddNewlyCreatedDBObject(t, true);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    for (int i = 0; i < 2300; i++)
                    {
                        double x = i * 2;
                        var face = new Face(new Point3d(x,0,1),
                            new Point3d(x+1,0,1), new Point3d(x,1,1),
                            new Point3d(x,1,1),true,true,true,false) { LayerId = tId };
                        ms.AppendEntity(face);
                        tr.AddNewlyCreatedDBObject(face, true);
                    }
                    var border = new Polyline(4) { LayerId = bId, Closed = true };
                    border.AddVertexAt(0,new Point2d(0,0),0,0,0);
                    border.AddVertexAt(1,new Point2d(20,0),0,0,0);
                    border.AddVertexAt(2,new Point2d(20,20),0,0,0);
                    border.AddVertexAt(3,new Point2d(0,20),0,0,0);
                    ms.AppendEntity(border);
                    selectedHandle = border.Handle.ToString();
                    tr.AddNewlyCreatedDBObject(border, true);
                    tr.Commit();
                }

                int polls = 0;
                string found = BoundaryGeometryService.FindUniqueBoundaryOnLayer(
                    db, "QA_BOUNDARY", () => polls++);
                if (!string.Equals(found, selectedHandle,
                        StringComparison.OrdinalIgnoreCase) || polls < 3 ||
                    TinCadRenderer.CountFaces(db,"QA_TIN") != 2300)
                    throw new InvalidOperationException(
                        "TIN-dense drawing boundary lookup missed the closed polyline.");

                bool canceled = false;
                polls = 0;
                try
                {
                    BoundaryGeometryService.FindUniqueBoundaryOnLayer(
                        db, "QA_BOUNDARY", () =>
                        {
                            if (++polls >= 2) throw new OperationCanceledException();
                        });
                }
                catch (OperationCanceledException) { canceled = true; }
                if (!canceled || TinCadRenderer.CountFaces(db,"QA_TIN") != 2300)
                    throw new InvalidOperationException(
                        "Canceled boundary scan did not preserve source TIN.");

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var duplicate = new Polyline(4) { LayerId = lt["QA_BOUNDARY"], Closed = true };
                    duplicate.AddVertexAt(0,new Point2d(30,30),0,0,0);
                    duplicate.AddVertexAt(1,new Point2d(40,30),0,0,0);
                    duplicate.AddVertexAt(2,new Point2d(40,40),0,0,0);
                    duplicate.AddVertexAt(3,new Point2d(30,40),0,0,0);
                    ms.AppendEntity(duplicate);
                    tr.AddNewlyCreatedDBObject(duplicate, true);
                    tr.Commit();
                }
                bool rejected = false;
                try { BoundaryGeometryService.FindUniqueBoundaryOnLayer(db, "QA_BOUNDARY"); }
                catch (InvalidOperationException) { rejected = true; }
                if (!rejected || TinCadRenderer.CountFaces(db,"QA_TIN") != 2300)
                    throw new InvalidOperationException(
                        "Ambiguous closed boundaries must require explicit selection.");
            }
        }

        private static void VerifyTinClearRollbackOnCancelAndForeignObject()
        {
            // Use real 3DFACEs in a separate AutoCAD database. The cleanup
            // routine must never leave a partially erased survey when Esc
            // or a foreign entity aborts the operation.
            using (var db = new Database(true, true))
            {
                const string layer = "QA_CANCEL_TIN";
                ObjectId foreignId = ObjectId.Null;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    var record = new LayerTableRecord { Name = layer };
                    var layerId = lt.Add(record);
                    tr.AddNewlyCreatedDBObject(record, true);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    for (int i = 0; i < 2300; i++)
                    {
                        double x = i * 2;
                        var face = new Face(new Point3d(x, 0, 0),
                            new Point3d(x + 1, 0, 0),
                            new Point3d(x, 1, 0),
                            new Point3d(x, 1, 0), true, true, true, false)
                        { LayerId = layerId };
                        ms.AppendEntity(face);
                        tr.AddNewlyCreatedDBObject(face, true);
                    }
                    tr.Commit();
                }
                if (TinCadRenderer.CountFaces(db, layer) != 2300)
                    throw new InvalidOperationException("TIN clear QA fixture incomplete.");

                // Host regression: cancel at scan start, at both full
                // 1024-entity checkpoints and at the final gate. No 3DFACE
                // may have been erased before ANY cancellation callback.
                bool initialLocked = TinCadRenderer.IsLayerLocked(db, layer);
                foreach (int cancelAt in new[] { 1, 2, 3, 4 })
                {
                    int polls = 0;
                    bool canceled = false;
                    try
                    {
                        TinCadRenderer.Clear(db, layer, () => {
                            if (++polls >= cancelAt) throw new OperationCanceledException();
                        });
                    }
                    catch (OperationCanceledException) { canceled = true; }
                    int remaining = TinCadRenderer.CountFaces(db, layer);
                    if (!canceled || polls != cancelAt || remaining != 2300 ||
                        TinCadRenderer.IsLayerLocked(db, layer) != initialLocked)
                        throw new InvalidOperationException(
                            "Canceled TIN delete did not preserve the original faces/layer. " +
                            "CancelAt=" + cancelAt + ", polls=" + polls +
                            ", canceled=" + canceled + ", faces=" + remaining +
                            ", initialLocked=" + initialLocked +
                            ", nowLocked=" + TinCadRenderer.IsLayerLocked(db, layer));
                }

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var text = new DBText { LayerId = lt[layer],
                        Position = Point3d.Origin, Height = 2, TextString = "PRESERVE" };
                    foreignId = ms.AppendEntity(text);
                    tr.AddNewlyCreatedDBObject(text, true);
                    tr.Commit();
                }
                bool rejected = false;
                try { TinCadRenderer.Clear(db, layer); }
                catch (InvalidOperationException) { rejected = true; }
                if (!rejected || TinCadRenderer.CountFaces(db, layer) != 2300 ||
                    TinCadRenderer.CountUnexpectedEntities(db, layer) != 1)
                    throw new InvalidOperationException("TIN delete erased data despite foreign object.");

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    ((Entity)tr.GetObject(foreignId, OpenMode.ForWrite)).Erase();
                    tr.Commit();
                }
                if (TinCadRenderer.Clear(db, layer) != 2300 ||
                    TinCadRenderer.CountFaces(db, layer) != 0)
                    throw new InvalidOperationException("TIN delete did not recover after cancellation.");
            }
        }

        private static void VerifyStoredCadTinDegenerateFaceSafety()
        {
            // Real AutoCAD 3DFACE and side-database fixture. Only a collapsed
            // face whose surviving exact 3D edge is also a valid TIN edge
            // may be ignored; do not mutate accepted AutoCAD geometry.
            using (var db = new Database(true, true))
            {
                string redundantHandle;
                string unsafeHandle;
                string nearUnsafeHandle;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    var okLayer = new LayerTableRecord { Name = "QA_TIN_REDUNDANT" };
                    var okId = layers.Add(okLayer);
                    tr.AddNewlyCreatedDBObject(okLayer, true);
                    var badLayer = new LayerTableRecord { Name = "QA_TIN_UNSAFE" };
                    var badId = layers.Add(badLayer);
                    tr.AddNewlyCreatedDBObject(badLayer, true);
                    var nearBadLayer = new LayerTableRecord { Name = "QA_TIN_NEAR_UNSAFE" };
                    var nearBadId = layers.Add(nearBadLayer);
                    tr.AddNewlyCreatedDBObject(nearBadLayer, true);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var a = new Point3d(0, 0, 10);
                    var b = new Point3d(10, 0, 10);
                    var c = new Point3d(0, 10, 10);
                    var real = new Face(a, b, c, c, true, true, true, false)
                    { LayerId = okId };
                    ms.AppendEntity(real);
                    tr.AddNewlyCreatedDBObject(real, true);
                    var redundant = new Face(a, a, b, b, true, true, true, false)
                    { LayerId = okId };
                    ms.AppendEntity(redundant);
                    tr.AddNewlyCreatedDBObject(redundant, true);
                    redundantHandle = redundant.Handle.ToString();
                    // Reproduce BF9BD: a sub-nanometer (nonzero) 3D edge.
                    // Prove redundancy via either endpoint's exact existing
                    // support edge, without modifying any CAD vertex.
                    var almostA = new Point3d(0, 5e-10, 10);
                    var nearA = new Face(almostA, a, b, b, true, true, true, false)
                    { LayerId = okId };
                    ms.AppendEntity(nearA);
                    tr.AddNewlyCreatedDBObject(nearA, true);
                    var almostB = new Point3d(10, 4e-10, 10);
                    var nearB = new Face(a, b, almostB, almostB, true, true, true, false)
                    { LayerId = okId };
                    ms.AppendEntity(nearB);
                    tr.AddNewlyCreatedDBObject(nearB, true);
                    for (int i = 0; i < 48; i++)
                    {
                        var repeated = new Face(a, a, b, b, true, true, true, false)
                        { LayerId = okId };
                        ms.AppendEntity(repeated);
                        tr.AddNewlyCreatedDBObject(repeated, true);
                    }
                    var otherSupport = new Face(a, b, c, c, true, true, true, false)
                    { LayerId = badId };
                    ms.AppendEntity(otherSupport);
                    tr.AddNewlyCreatedDBObject(otherSupport, true);
                    var orphanA = new Point3d(100, 100, 10);
                    var orphanB = new Point3d(110, 100, 10);
                    var orphan = new Face(orphanA, orphanA, orphanB, orphanB,
                        true, true, true, false) { LayerId = badId };
                    ms.AppendEntity(orphan);
                    tr.AddNewlyCreatedDBObject(orphan, true);
                    unsafeHandle = orphan.Handle.ToString();
                    var nearSupport = new Face(a, b, c, c, true, true, true, false)
                    { LayerId = nearBadId };
                    ms.AppendEntity(nearSupport);
                    tr.AddNewlyCreatedDBObject(nearSupport, true);
                    var nearOrphanA = new Point3d(100, 100, 10);
                    var nearOrphanA2 = new Point3d(100, 100 + 5e-10, 10);
                    var nearOrphanB = new Point3d(110, 100, 10);
                    var nearOrphan = new Face(nearOrphanA, nearOrphanA2, nearOrphanB,
                        nearOrphanB, true, true, true, false)
                    { LayerId = nearBadId };
                    ms.AppendEntity(nearOrphan);
                    tr.AddNewlyCreatedDBObject(nearOrphan, true);
                    nearUnsafeHandle = nearOrphan.Handle.ToString();
                    tr.Commit();
                }
                var tin = TinCadRenderer.ReadStoredTin(db, "QA_TIN_REDUNDANT", "QA");
                if (tin == null || tin.Triangles.Count != 1 ||
                    tin.VerticalFaces.Count != 0 ||
                    tin.VerifiedRedundantCadFaces != 51 ||
                    !tin.Issues.Any(issue =>
                        issue.Code == "REDUNDANT_ZERO_EDGE_CAD_FACE" &&
                        issue.Message.Contains(redundantHandle)) ||
                    TinCadRenderer.CountFaces(db, "QA_TIN_REDUNDANT") != 52)
                    throw new InvalidOperationException(
                        "A redundant collapsed CAD face was not proven or the drawing changed.");
                bool blocked = false;
                try { TinCadRenderer.ReadStoredTin(db, "QA_TIN_UNSAFE", "QA"); }
                catch (InvalidOperationException ex)
                {
                    blocked = ex.Message.Contains(unsafeHandle) &&
                              ex.Message.Contains("không") &&
                              ex.Message.Contains("Handle");
                }
                if (!blocked || TinCadRenderer.CountFaces(db, "QA_TIN_UNSAFE") != 2)
                    throw new InvalidOperationException(
                        "An unproven degenerate CAD face must reject recovery with its exact Handle.");
                bool nearBlocked = false;
                try { TinCadRenderer.ReadStoredTin(db, "QA_TIN_NEAR_UNSAFE", "QA"); }
                catch (InvalidOperationException ex)
                {
                    nearBlocked = ex.Message.Contains(nearUnsafeHandle) &&
                                  ex.Message.Contains("không");
                }
                if (!nearBlocked || TinCadRenderer.CountFaces(db, "QA_TIN_NEAR_UNSAFE") != 2)
                    throw new InvalidOperationException(
                        "Unproven near-zero 3D edge must reject recovery and preserve all 3DFACEs.");
            }
            VerifyLegacyDwgRecognitionReadOnly();
        }

        private static void VerifyLegacyDwgRecognitionReadOnly()
        {
            using (var db = new Database(true, true))
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    var tk = new LayerTableRecord { Name = "MV_TIN_THIETKE" };
                    ObjectId tkId = layers.Add(tk);
                    tr.AddNewlyCreatedDBObject(tk, true);
                    var mc = new LayerTableRecord { Name = "MV_MC_HIENTRANG" };
                    ObjectId mcId = layers.Add(mc);
                    tr.AddNewlyCreatedDBObject(mc, true);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var model = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var a = new Point3d(0, 0, 0);
                    var b = new Point3d(5, 0, 0);
                    var c = new Point3d(0, 5, 0);
                    var face = new Face(a, b, c, c, true, true, true, false)
                    { LayerId = tkId };
                    model.AppendEntity(face);
                    tr.AddNewlyCreatedDBObject(face, true);
                    var section = new Polyline { LayerId = mcId };
                    section.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
                    section.AddVertexAt(1, new Point2d(5, 5), 0, 0, 0);
                    model.AppendEntity(section);
                    tr.AddNewlyCreatedDBObject(section, true);
                    tr.Commit();
                }
                string before = DwgRecognitionDiagnostic.Inspect(db, false);
                string after = DwgRecognitionDiagnostic.Inspect(db, true);
                if (!before.Contains("TIN thiết kế: 1 mặt 3DFACE") ||
                    !before.Contains("Đường mặt cắt CAD: HT 1, TK 0") ||
                    !before.Contains("KHÔNG CÓ / CHƯA TÌM THẤY") ||
                    !before.Contains("KHÔNG tự tạo lại hay ghi đè") ||
                    !after.Contains("Project Xrecord: CÓ") ||
                    TinCadRenderer.CountFaces(db, "MV_TIN_THIETKE") != 1)
                    throw new InvalidOperationException(
                        "Legacy DWG diagnostic did not recognize existing CAD evidence safely.");
            }
        }

        private static void VerifySectionEraseSkipsTinFaces()
        {
            // Host-only regression on an isolated DWG. The real mine model
            // can contain >1M native Face objects and a section redraw must
            // leave them entirely intact, even if canceled mid-transaction.
            using (var db = new Database(true, true))
            {
                const string tinLayer = "QA_TIN_FACES";
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                    var layer = new LayerTableRecord { Name = tinLayer };
                    var layerId = layers.Add(layer);
                    tr.AddNewlyCreatedDBObject(layer, true);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    for (int i = 0; i < 2048; i++)
                    {
                        var p = new Point3d(i * 2, 0, 0);
                        var q = new Point3d(i * 2 + 1, 0, 0);
                        var top = new Point3d(i * 2, 1, 0);
                        var face = new Face(p, q, top, top, true, true, true, false)
                        {
                            LayerId = layerId
                        };
                        model.AppendEntity(face);
                        tr.AddNewlyCreatedDBObject(face, true);
                    }
                    tr.Commit();
                }
                var line = new SectionLine(1, "QA-MC-1", 0,
                    new Vec2(1, 0), new Vec2(0, 1),
                    new[] { new SectionSpan(0, 10) });
                var segment = new SectionProfileSegment(0, 10, line.PointAt(0), line.PointAt(10),
                    100, 100, 95, 95);
                var profiles = new[]
                {
                    new SectionProfile(line, new[] { segment }, 50, 0, new string[0])
                };
                if (TinCadRenderer.CountFaces(db, tinLayer) != 2048)
                    throw new InvalidOperationException("TIN regression fixture was not created.");
                SectionCadRenderer.ReplaceProfiles(db, profiles, Point3d.Origin, 1000, 1000, 5);
                SectionCadRenderer.ReplaceProfiles(db, profiles, Point3d.Origin, 1000, 1000, 5);
                if (TinCadRenderer.CountFaces(db, tinLayer) != 2048)
                    throw new InvalidOperationException("Section redraw deleted unrelated TIN geometry.");
                bool canceled = false;
                try
                {
                    DrawBatches(db, profiles, () => { throw new OperationCanceledException(); });
                }
                catch (OperationCanceledException) { canceled = true; }
                if (!canceled || TinCadRenderer.CountFaces(db, tinLayer) != 2048)
                    throw new InvalidOperationException("Canceled section redraw modified accepted TIN.");
            }
        }

        private static void VerifyTinPairRollbackAfterSecondSurfaceFailure()
        {
            // Real AutoCAD side database, not a mocked transaction. If the
            // second TIN fails after first TIN was written, both originals
            // must retain their native CAD handles and face counts.
            Func<double, TinSurface> plane = z => new TinSurface("QA plane", new[] {
                new Triangle3(new Vec3(0,0,z), new Vec3(10,0,z), new Vec3(10,10,z)),
                new Triangle3(new Vec3(0,0,z), new Vec3(10,10,z), new Vec3(0,10,z))
            }, new ValidationIssue[0]);
            using (var db = new Database(true, true))
            {
                TinCadRenderer.ReplacePair(db, "QA_PAIR_HT", plane(10), "QA_PAIR_TK", plane(0));
                var original = TinPairHandles(db);
                if (TinCadRenderer.CountFaces(db, "QA_PAIR_HT") != 2 ||
                    TinCadRenderer.CountFaces(db, "QA_PAIR_TK") != 2)
                    throw new InvalidOperationException("TIN pair setup produced incorrect faces.");
                // Deliberately degenerate design triangle: writing HT first
                // cannot change the accepted drawings if design fails.
                var invalid = new TinSurface("Invalid QA", new[] {
                    new Triangle3(new Vec3(0,0,5), new Vec3(0,0,5), new Vec3(10,0,5))
                }, new ValidationIssue[0]);
                bool rejected = false;
                try { TinCadRenderer.ReplacePair(db, "QA_PAIR_HT", plane(20), "QA_PAIR_TK", invalid); }
                catch (InvalidOperationException) { rejected = true; }
                if (!rejected || TinPairHandles(db) != original)
                    throw new InvalidOperationException("TIN pair second-surface failure did not preserve both original layers.");

                // The first invalid input must also be rejected before any
                // handle/visibility/color changes on either accepted TIN.
                rejected = false;
                try { TinCadRenderer.ReplacePair(db, "QA_PAIR_HT", invalid, "QA_PAIR_TK", plane(3)); }
                catch (InvalidOperationException) { rejected = true; }
                if (!rejected || TinPairHandles(db) != original)
                    throw new InvalidOperationException("TIN pair first-surface failure changed accepted faces.");

                // A manual DBText accidentally placed on TK must be detected
                // before HT is erased. The foreign object and both prior TINs
                // must survive the refusal, not merely report an error.
                ObjectId foreignId;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    var foreign = new DBText { LayerId = lt["QA_PAIR_TK"],
                        Position = Point3d.Origin, Height = 1, TextString = "PRESERVE_PAIR" };
                    foreignId = ms.AppendEntity(foreign);
                    tr.AddNewlyCreatedDBObject(foreign, true);
                    tr.Commit();
                }
                rejected = false;
                try { TinCadRenderer.ReplacePair(db, "QA_PAIR_HT", plane(12), "QA_PAIR_TK", plane(2)); }
                catch (InvalidOperationException) { rejected = true; }
                if (!rejected || TinPairHandles(db) != original ||
                    TinCadRenderer.CountUnexpectedEntities(db, "QA_PAIR_TK") != 1)
                    throw new InvalidOperationException(
                        "TIN pair foreign-object preflight did not preserve original drawing.");
                // AutoCAD 2023 HOST: a foreign DBText on a locked TIN layer
                // cannot be opened ForWrite (eOnLockedLayer). The QA fixture
                // must unlock TK just for removing its OWN test DBText and
                // restore the original lock flag before committing. This is
                // NOT a change to the production ReplacePair safety contract.
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var tkLayer = (LayerTableRecord)tr.GetObject(
                        lt["QA_PAIR_TK"], OpenMode.ForWrite);
                    bool wasLocked = tkLayer.IsLocked;
                    tkLayer.IsLocked = false;
                    try
                    {
                        ((Entity)tr.GetObject(foreignId, OpenMode.ForWrite)).Erase();
                    }
                    finally
                    {
                        tkLayer.IsLocked = wasLocked;
                    }
                    tr.Commit();
                }
                if (TinCadRenderer.CountUnexpectedEntities(db, "QA_PAIR_TK") != 0 ||
                    !TinCadRenderer.IsLayerLocked(db, "QA_PAIR_TK") ||
                    TinPairHandles(db) != original)
                    throw new InvalidOperationException(
                        "QA foreign-object cleanup changed the accepted TIN pair or lock state.");

                TinCadRenderer.ReplacePair(db, "QA_PAIR_HT", plane(12), "QA_PAIR_TK", plane(2));
                if (TinCadRenderer.CountFaces(db, "QA_PAIR_HT") != 2 ||
                    TinCadRenderer.CountFaces(db, "QA_PAIR_TK") != 2 ||
                    TinPairHandles(db) == original)
                    throw new InvalidOperationException("TIN pair did not recover after a failed replacement.");
            }
        }

        private static string TinPairHandles(Database db)
        {
            var handles = new List<string>();
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var face = tr.GetObject(id, OpenMode.ForRead) as Face;
                    if (face != null &&
                        (face.Layer == "QA_PAIR_HT" || face.Layer == "QA_PAIR_TK"))
                        handles.Add(face.Layer + ":" + face.Handle);
                }
            }
            handles.Sort(StringComparer.Ordinal);
            return string.Join("|", handles);
        }

        private static void VerifyGridIdentity()
        {
            var boundary = new[] { new Vec2(0,0), new Vec2(10,0), new Vec2(10,10), new Vec2(0,10) };
            Func<double,TinSurface> plane = z => new TinSurface("QA", new[] {
                new Triangle3(new Vec3(0,0,z),new Vec3(10,0,z),new Vec3(10,10,z)),
                new Triangle3(new Vec3(0,0,z),new Vec3(10,10,z),new Vec3(0,10,z)) }, new ValidationIssue[0]);
            var options = new GridVolumeOptions { Boundary = boundary, SizeX = 1, SizeY = 1 };
            var calc = new GridVolumeCalculator();
            var first = calc.Calculate(plane(5), plane(0), options);
            var second = calc.Calculate(plane(6), plane(0), options);
            using (var inspectionDb = new Database(true,true))
            {
                TinCadRenderer.Replace(inspectionDb,"QA_HT",plane(5),1);
                TinCadRenderer.Replace(inspectionDb,"QA_TK",plane(0),3);
                var scan = TinCadRenderer.InspectLayers(inspectionDb,new[] {"QA_HT","QA_TK"});
                foreach (string name in new[] {"QA_HT","QA_TK"})
                    if (!scan[name].Exists || scan[name].Faces != 2 || scan[name].Unexpected != 0)
                        throw new InvalidOperationException("Batched TIN inspection changed face counts.");
            }
            using (var db = new Database(true,true))
            {
                GridCadRenderer.Draw(db, first, false);
                GridCadRenderer.Draw(db, second, false);
                // Cancel before mutation, after 64 cells, and just before commit.
                foreach (int cancelAt in new[] { 1, 3, 4 })
                {
                    int polls = 0;
                    bool canceled = false;
                    try
                    {
                        GridCadRenderer.Draw(db, first, true, pollCancellation: () =>
                        {
                            if (++polls == cancelAt) throw new OperationCanceledException();
                        });
                    }
                    catch (OperationCanceledException) { canceled = true; }
                    if (!canceled) throw new InvalidOperationException("Grid cancellation did not run.");
                }
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    int oldCount = 0, newCount = 0;
                    foreach (ObjectId id in model)
                    {
                        var poly = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                        if (poly == null) throw new InvalidOperationException("Grid identity added unexpected entities.");
                        using (var data = poly.GetXDataForApplication(GridCadRenderer.CalculationIdApp))
                        {
                            var values = data?.AsArray();
                            if (values == null || values.Length != 2)
                                throw new InvalidOperationException("Missing grid calculation XData.");
                            string value = values[1].Value as string;
                            if (value == first.CalculationId) oldCount++;
                            else if (value == second.CalculationId) newCount++;
                            else throw new InvalidOperationException("Wrong grid calculation identity.");
                        }
                    }
                    if (oldCount != first.Cells.Count || newCount != second.Cells.Count)
                        throw new InvalidOperationException("Later grid export overwrote earlier identity.");
                }
            }
        }

        private static void VerifyTableRollback()
        {
            var intervals = new List<SectionIntervalVolume>();
            var levels = new List<LevelVolumeSummary>();
            var bands = new List<LevelBand>();
            for (int i = 0; i < 85; i++)
            {
                intervals.Add(new SectionIntervalVolume { Index = i + 1,
                    StartSection = "QA-" + i, EndSection = "QA-" + (i + 1),
                    Distance = 20, CutAreaStart = 5, CutAreaEnd = 5, CutVolume = 100 });
                var band = new LevelBand(i, i, i + 1);
                bands.Add(band);
                levels.Add(new LevelVolumeSummary { Band = band, CutVolume = 100 });
            }
            var result = new VolumeResult(bands, intervals, levels,
                new SectionBandAreas[0], new string[0]);
            using (var db = new Database(true, true))
            {
                int totalPolls = 0;
                CadVolumeTableService.Insert(db, result, Point3d.Origin, () => totalPolls++);
                string before = TableSnapshot(db);
                if (!before.Contains(result.CalculationId))
                    throw new InvalidOperationException("CAD table lost calculation identity.");
                foreach (int cancelAt in new[] { 1, totalPolls / 2, totalPolls })
                {
                    int polls = 0;
                    bool canceled = false;
                    try
                    {
                        CadVolumeTableService.Insert(db, result, new Point3d(1000, 0, 0), () =>
                        {
                            if (++polls >= cancelAt) throw new OperationCanceledException();
                        });
                    }
                    catch (OperationCanceledException) { canceled = true; }
                    if (!canceled || before != TableSnapshot(db))
                        throw new InvalidOperationException("Canceled quantity table export left partial entities.");
                }
                // Invalid row occurs after the summary was appended: it must
                // roll back that summary and dispose the unfinished interval table.
                intervals[40].CutVolume = double.NaN;
                bool failed = false;
                try { CadVolumeTableService.Insert(db, result, new Point3d(1000, 0, 0)); }
                catch (InvalidOperationException) { failed = true; }
                if (!failed || before != TableSnapshot(db))
                    throw new InvalidOperationException("Failed quantity table export changed existing tables.");
                intervals[40].CutVolume = 100;
                var next = new VolumeResult(bands, intervals, levels,
                    new SectionBandAreas[0], new string[0]);
                CadVolumeTableService.Insert(db, next, new Point3d(1000, 200, 0));
                string repeated = TableSnapshot(db, 2);
                if (!repeated.StartsWith(before, StringComparison.Ordinal) || !repeated.Contains(next.CalculationId))
                    throw new InvalidOperationException("Re-export changed the accepted block or lost the new identity.");
            }
        }

        private static string TableSnapshot(Database db, int expectedExports = 1)
        {
            var output = new StringBuilder();
            int count = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                int references = 0, definitions = 0;
                foreach (ObjectId id in bt)
                {
                    var definition = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (definition.Name.StartsWith(CadVolumeTableService.BlockPrefix, StringComparison.Ordinal)) definitions++;
                }
                foreach (ObjectId id in ms)
                {
                    var reference = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                    if (reference == null) throw new InvalidOperationException("Quantity output must be a block reference.");
                    references++;
                    output.Append(reference.Handle).Append('|').Append(reference.Layer);
                    Point(output, reference.Position);
                    var definition = (BlockTableRecord)tr.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
                    if (!definition.Name.StartsWith(CadVolumeTableService.BlockPrefix, StringComparison.Ordinal))
                        throw new InvalidOperationException("Unexpected quantity block definition.");
                    output.Append(definition.Name);
                    int tables = 0;
                    foreach (ObjectId child in definition)
                    {
                        var table = tr.GetObject(child, OpenMode.ForRead) as Table;
                        if (table == null) throw new InvalidOperationException("Quantity block must retain native tables.");
                        count++; tables++;
                        output.Append(table.Handle).Append('|').Append(table.Layer);
                        Point(output, table.Position.TransformBy(reference.BlockTransform));
                        for (int row = 0; row < table.Rows.Count; row++)
                            for (int col = 0; col < table.Columns.Count; col++)
                                output.Append('|').Append(table.Cells[row, col].TextString);
                    }
                    if (tables != 3) throw new InvalidOperationException("Each quantity block must contain three tables.");
                }
                if (references != expectedExports || definitions != expectedExports)
                    throw new InvalidOperationException("Quantity export leaked a block reference/definition.");
            }
            if (count != expectedExports * 3) throw new InvalidOperationException("Unexpected quantity table count.");
            return output.ToString();
        }

        private static void DrawBatches(Database db, IReadOnlyList<SectionProfile> profiles, Action poll)
        {
            // Mirror the production-wide ObjectId journal. A late Esc must
            // restore old profiles even if earlier nested batches committed.
            var created = new List<ObjectId>();
            var erased = new List<ObjectId>();
            try
            {
                using (var outer = db.TransactionManager.StartTransaction())
                {
                    double nextY = 0;
                    for (int start = 0; start < profiles.Count; start += 20)
                        nextY = SectionCadRenderer.ReplaceProfilesBatched(db, profiles, Point3d.Origin,
                            1000, 500, 5, start, Math.Min(20, profiles.Count - start),
                            start == 0, 90, nextY, poll, created.Add, erased.Add);
                    poll();
                    outer.Commit();
                }
            }
            catch (Exception original)
            {
                try { SectionCadRenderer.RecoverFailedSectionExport(db, created, erased); }
                catch (Exception cleanup)
                {
                    throw new AggregateException(
                        "Host QA cannot restore all section batches after cancel.",
                        original, cleanup);
                }
                throw;
            }
        }

        private static string Snapshot(Database db, bool handles = false)
        {
            var output = new StringBuilder();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var entity = (Entity)tr.GetObject(id, OpenMode.ForRead);
                    if (handles) output.Append(entity.Handle.ToString());
                    output.Append(entity.GetType().Name).Append(':').Append(entity.Layer).Append('|');
                    if (entity is Polyline poly)
                        for (int i = 0; i < poly.NumberOfVertices; i++) Point(output, poly.GetPoint3dAt(i));
                    else if (entity is Line line) { Point(output, line.StartPoint); Point(output, line.EndPoint); }
                    else if (entity is DBText text) { Point(output, text.Position); output.Append(text.TextString); }
                    else throw new InvalidOperationException("Unexpected section entity type.");
                    output.Append('\n');
                }
            }
            return output.ToString();
        }
        private static void Point(StringBuilder s, Point3d p)
        {
            s.Append(p.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
             .Append(p.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
             .Append(p.Z.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }
    }
}
