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
            }
        }

        private static string TableSnapshot(Database db)
        {
            var output = new StringBuilder();
            int count = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var table = tr.GetObject(id, OpenMode.ForRead) as Table;
                    if (table == null) throw new InvalidOperationException("Unexpected quantity table entity.");
                    count++;
                    output.Append(table.Handle).Append('|').Append(table.Layer);
                    Point(output, table.Position);
                    for (int row = 0; row < table.Rows.Count; row++)
                        for (int col = 0; col < table.Columns.Count; col++)
                            output.Append('|').Append(table.Cells[row, col].TextString);
                }
            }
            if (count != 3) throw new InvalidOperationException("Expected exactly three quantity tables.");
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
