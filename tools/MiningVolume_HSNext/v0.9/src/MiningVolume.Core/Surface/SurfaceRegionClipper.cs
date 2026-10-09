using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;

namespace MiningVolume.Core.Surface
{
    /// <summary>
    /// Tạo mô hình nguồn tạm chỉ trong một polygon chọn trên CAD.
    /// Không sửa dữ liệu nguồn. Breakline cắt biên được cắt đúng tại giao điểm
    /// và nội suy Z theo chính đoạn nguồn.
    /// </summary>
    public static class SurfaceRegionClipper
    {
        public static SurfaceModel Clip(
            SurfaceModel source,
            IReadOnlyList<Vec2> polygon,
            double tolerance = 1e-7,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (polygon == null || polygon.Count < 3)
                throw new ArgumentException("Vùng tạo TIN phải có ít nhất 3 đỉnh.", nameof(polygon));

            var result = new SurfaceModel(source.Name + " - vùng chọn");
            int generated = 0;

            foreach (var entity in source.Entities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!entity.IsEnabled) continue;

                if (!entity.IsBreakline)
                {
                    var inside = entity.ActiveVertices()
                        .Select(v => v.Position)
                        .Where(p => ContainsInclusive(p.XY, polygon, tolerance))
                        .ToList();

                    if (inside.Count > 0)
                    {
                        result.Entities.Add(new SourceEntity(
                            entity.Id + "#REG",
                            entity.Handle,
                            entity.Layer,
                            entity.Type,
                            inside,
                            false));
                    }
                    continue;
                }

                foreach (var segment in entity.ActiveBreaklineSegments())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var clipped in ClipSegment(segment, polygon, tolerance))
                    {
                        result.Entities.Add(new SourceEntity(
                            entity.Id + "#REG" + (++generated).ToString(),
                            entity.Handle,
                            entity.Layer,
                            SourceEntityType.Line,
                            new[] { clipped.A, clipped.B },
                            false));
                    }
                }
            }

            result.Touch();
            return result;
        }

        public static bool ContainsInclusive(
            Vec2 p,
            IReadOnlyList<Vec2> polygon,
            double tolerance = 1e-9)
        {
            if (polygon == null || polygon.Count < 3) return false;

            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                if (Geometry2D.PointOnSegment(p, a, b, tolerance))
                    return true;
            }

            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                bool crosses = ((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                               (p.X < (pj.X - pi.X) * (p.Y - pi.Y) /
                                ((pj.Y - pi.Y) == 0 ? double.Epsilon : (pj.Y - pi.Y)) + pi.X);
                if (crosses) inside = !inside;
            }
            return inside;
        }

        public static IReadOnlyList<Segment3> ClipSegment(
            Segment3 segment,
            IReadOnlyList<Vec2> polygon,
            double tolerance = 1e-9)
        {
            var ts = new List<double> { 0.0, 1.0 };

            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                AddIntersectionParameters(segment.A.XY, segment.B.XY, a, b, ts, tolerance);
            }

            ts.Sort();
            var unique = new List<double>();
            foreach (var t in ts)
            {
                double clamped = Math.Max(0.0, Math.Min(1.0, t));
                if (unique.Count == 0 || Math.Abs(clamped - unique[unique.Count - 1]) > 1e-10)
                    unique.Add(clamped);
            }

            var result = new List<Segment3>();
            for (int i = 0; i < unique.Count - 1; i++)
            {
                double t0 = unique[i], t1 = unique[i + 1];
                if (t1 - t0 <= 1e-12) continue;

                double tm = (t0 + t1) * 0.5;
                var mid = Interpolate(segment, tm);
                if (!ContainsInclusive(mid.XY, polygon, tolerance)) continue;

                var p0 = Interpolate(segment, t0);
                var p1 = Interpolate(segment, t1);
                if (p0.XY.DistanceTo(p1.XY) <= tolerance) continue;

                result.Add(new Segment3(p0, p1, segment.SourceId));
            }

            return result;
        }

        private static Vec3 Interpolate(Segment3 s, double t)
        {
            return new Vec3(
                s.A.X + (s.B.X - s.A.X) * t,
                s.A.Y + (s.B.Y - s.A.Y) * t,
                s.A.Z + (s.B.Z - s.A.Z) * t);
        }

        private static void AddIntersectionParameters(
            Vec2 p,
            Vec2 p2,
            Vec2 q,
            Vec2 q2,
            List<double> ts,
            double tolerance)
        {
            double rx = p2.X - p.X, ry = p2.Y - p.Y;
            double sx = q2.X - q.X, sy = q2.Y - q.Y;
            double rxs = rx * sy - ry * sx;
            double qpx = q.X - p.X, qpy = q.Y - p.Y;
            double qpxr = qpx * ry - qpy * rx;

            double scale = Math.Max(
                1.0,
                Math.Max(Math.Sqrt(rx * rx + ry * ry), Math.Sqrt(sx * sx + sy * sy)));
            double eps = tolerance * scale;

            if (Math.Abs(rxs) <= eps)
            {
                if (Math.Abs(qpxr) > eps) return;

                double rr = rx * rx + ry * ry;
                if (rr <= tolerance * tolerance) return;
                double t0 = ((q.X - p.X) * rx + (q.Y - p.Y) * ry) / rr;
                double t1 = ((q2.X - p.X) * rx + (q2.Y - p.Y) * ry) / rr;
                if (t0 >= -1e-10 && t0 <= 1.0 + 1e-10) ts.Add(t0);
                if (t1 >= -1e-10 && t1 <= 1.0 + 1e-10) ts.Add(t1);
                return;
            }

            double t = (qpx * sy - qpy * sx) / rxs;
            double u = (qpx * ry - qpy * rx) / rxs;
            if (t >= -1e-10 && t <= 1.0 + 1e-10 &&
                u >= -1e-10 && u <= 1.0 + 1e-10)
                ts.Add(t);
        }
    }
}
