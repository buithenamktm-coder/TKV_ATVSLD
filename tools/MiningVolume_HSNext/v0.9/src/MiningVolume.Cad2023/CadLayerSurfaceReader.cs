using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;

namespace MiningVolume.Cad2023
{
    /// <summary>
    /// Đọc toàn bộ đối tượng địa hình trên một layer. Không dựng TIN tại đây.
    /// Đầu ra là SourceEntity để giao diện có thể hiển thị/loại/sửa trước khi bấm Cập nhật mô hình.
    /// </summary>
    public sealed class CadLayerSurfaceReader
    {
        public IReadOnlyList<SourceEntity> Read(Database db, string layerName, double arcChord = 0.50)
        {
            var output = new List<SourceEntity>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null || !string.Equals(ent.Layer, layerName, StringComparison.OrdinalIgnoreCase)) continue;
                    var rec = ConvertEntity(ent, tr, arcChord);
                    if (rec != null) output.Add(rec);
                }
                tr.Commit();
            }
            return output;
        }

        private static SourceEntity ConvertEntity(Entity ent, Transaction tr, double arcChord)
        {
            string id = ent.Handle.ToString();
            string layer = ent.Layer;

            if (ent is DBPoint p)
                return New(id, ent, SourceEntityType.Point, new[] { ToVec3(p.Position) });

            if (ent is Line ln)
                return New(id, ent, SourceEntityType.Line, new[] { ToVec3(ln.StartPoint), ToVec3(ln.EndPoint) });

            if (ent is Polyline pl)
            {
                var pts = SampleLwPolyline(pl, arcChord);
                var type = IsFlatContour(pts) ? SourceEntityType.Contour : SourceEntityType.LwPolyline;
                return New(id, ent, type, pts, pl.Closed);
            }

            if (ent is Polyline2d p2)
            {
                var pts = new List<Vec3>();
                foreach (ObjectId vid in p2)
                {
                    var v = tr.GetObject(vid, OpenMode.ForRead) as Vertex2d;
                    if (v == null) continue;
                    // AutoCAD 2D Polyline vertex Position is ECS/OCS; VertexPosition gives WCS.
                    pts.Add(ToVec3(p2.VertexPosition(v)));
                }
                var type = IsFlatContour(pts) ? SourceEntityType.Contour : SourceEntityType.Polyline2d;
                return New(id, ent, type, pts, p2.Closed);
            }

            if (ent is Polyline3d p3)
            {
                var pts = new List<Vec3>();
                foreach (ObjectId vid in p3)
                {
                    var v = tr.GetObject(vid, OpenMode.ForRead) as PolylineVertex3d;
                    if (v != null) pts.Add(ToVec3(v.Position));
                }
                return New(id, ent, SourceEntityType.Polyline3d, pts, p3.Closed);
            }

            return null;
        }

        private static SourceEntity New(string id, Entity e, SourceEntityType type, IEnumerable<Vec3> pts, bool closed = false)
            => new SourceEntity(id, e.Handle.ToString(), e.Layer, type, pts, closed);

        private static List<Vec3> SampleLwPolyline(Polyline pl, double maxChord)
        {
            var pts = new List<Vec3>();
            int n = pl.NumberOfVertices;
            if (n == 0) return pts;
            for (int i = 0; i < n; i++)
            {
                if (i == 0) pts.Add(ToVec3(pl.GetPoint3dAt(0)));
                int next = i + 1;
                bool hasSeg = next < n || pl.Closed;
                if (!hasSeg) continue;
                int j = next % n;
                double bulge = pl.GetBulgeAt(i);
                if (Math.Abs(bulge) < 1e-12)
                {
                    if (!(pl.Closed && j == 0)) pts.Add(ToVec3(pl.GetPoint3dAt(j)));
                    continue;
                }

                // CircularArc3d is in WCS. Segmentize arc instead of silently replacing it by a chord.
                CircularArc3d arc = pl.GetArcSegmentAt(i);
                double length = arc.Radius * Math.Abs(arc.EndAngle - arc.StartAngle);
                int pieces = Math.Max(2, (int)Math.Ceiling(length / Math.Max(0.01, maxChord)));
                double total = arc.EndAngle - arc.StartAngle;
                for (int k = 1; k <= pieces; k++)
                {
                    double a = arc.StartAngle + total * k / pieces;
                    Point3d q = arc.Center + arc.ReferenceVector.RotateBy(a - arc.StartAngle, arc.Normal) * arc.Radius;
                    if (!(pl.Closed && j == 0 && k == pieces)) pts.Add(ToVec3(q));
                }
            }
            return pts;
        }

        private static bool IsFlatContour(IReadOnlyList<Vec3> pts)
        {
            if (pts == null || pts.Count < 2) return false;
            double z = pts[0].Z;
            for (int i = 1; i < pts.Count; i++) if (Math.Abs(pts[i].Z - z) > 1e-6) return false;
            return true;
        }

        private static Vec3 ToVec3(Point3d p) => new Vec3(p.X, p.Y, p.Z);
    }
}