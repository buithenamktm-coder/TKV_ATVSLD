using System;
using System.Collections.Generic;
using System.Globalization;
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
        internal static void Run()
        {
            VerifyBoundarySelectionSkipsTinAndSupportsCancel();
            VerifyTinClearRollbackOnCancelAndForeignObject();
            VerifyTinPairRollbackAfterSecondSurfaceFailure();
            VerifySectionEraseSkipsTinFaces();
            VerifyTableRollback();
            VerifyGridIdentity();
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
                ObjectId selected = ObjectId.Null;
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
                    selected = ms.AppendEntity(border);
                    tr.AddNewlyCreatedDBObject(border, true);
                    tr.Commit();
                }

                int polls = 0;
                string found = BoundaryGeometryService.FindUniqueBoundaryOnLayer(
                    db, "QA_BOUNDARY", () => polls++);
                if (!string.Equals(found, selected.Handle.ToString(),
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

                int polls = 0;
                bool canceled = false;
                try
                {
                    TinCadRenderer.Clear(db, layer, () => {
                        if (++polls >= 2) throw new OperationCanceledException();
                    });
                }
                catch (OperationCanceledException) { canceled = true; }
                if (!canceled || polls < 2 || TinCadRenderer.CountFaces(db, layer) != 2300)
                    throw new InvalidOperationException("Canceled TIN delete did not roll back all faces.");

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
                    throw new InvalidOperationException("TIN pair second-surface failure did not roll back both layers.");
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
            using (var outer = db.TransactionManager.StartTransaction())
            {
                double nextY = 0;
                for (int start = 0; start < profiles.Count; start += 20)
                    nextY = SectionCadRenderer.ReplaceProfilesBatched(db, profiles, Point3d.Origin,
                        1000, 500, 5, start, Math.Min(20, profiles.Count - start), start == 0, 90, nextY, poll);
                poll();
                outer.Commit();
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
