using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Sections;

namespace MiningVolume2023.Services
{
    // Requires the real AutoCAD runtime. Uses disposable side databases only.
    // Does not touch the user's drawing or substitute a mock for host rollback.
    internal static class SectionCadRegression
    {
        internal static void Run()
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
