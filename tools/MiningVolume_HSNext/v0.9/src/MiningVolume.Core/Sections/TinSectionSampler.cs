using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Surface;

namespace MiningVolume.Core.Sections
{
    /// <summary>
    /// Tạo mặt cắt chính xác từ hai TIN. Breakpoint lấy tại giao của tuyến với các cạnh tam giác,
    /// sau đó tích phân hiệu cao độ tuyến tính để tách diện tích đào/đắp, kể cả khi đổi dấu trong một đoạn.
    /// </summary>
    public sealed class TinSectionSampler
    {
        public SectionProfile BuildProfile(SectionLine line, TinSurface existing, TinSurface design, double tol = 1e-7)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            if (design == null) throw new ArgumentNullException(nameof(design));

            var segments = new List<SectionProfileSegment>();
            var warnings = new List<string>();
            double cut = 0, fill = 0;
            double globalMinT = line.MinT;

            foreach (var span in line.Spans)
            {
                var knots = new List<double> { span.StartT, span.EndT };
                CollectTriangleEdgeIntersections(line, span, existing, knots, tol);
                CollectTriangleEdgeIntersections(line, span, design, knots, tol);
                knots.Sort();
                knots = Deduplicate(knots, tol * 10);

                for (int i = 0; i < knots.Count - 1; i++)
                {
                    double t0 = knots[i], t1 = knots[i + 1];
                    if (t1 - t0 <= tol) continue;
                    double tm = (t0 + t1) * 0.5;
                    Vec2 pm = line.PointAt(tm);
                    if (!TryElevation(existing, pm, tol * 20, out _) || !TryElevation(design, pm, tol * 20, out _))
                        continue;

                    Vec2 p0 = line.PointAt(t0), p1 = line.PointAt(t1);
                    if (!TryElevationRobust(existing, p0, pm, tol, out double eh0) ||
                        !TryElevationRobust(existing, p1, pm, tol, out double eh1) ||
                        !TryElevationRobust(design, p0, pm, tol, out double dh0) ||
                        !TryElevationRobust(design, p1, pm, tol, out double dh1))
                    {
                        warnings.Add($"{line.Name}: bỏ qua một đoạn không xác định đủ cao độ TIN.");
                        continue;
                    }

                    double s0 = t0 - globalMinT, s1 = t1 - globalMinT;
                    var seg = new SectionProfileSegment(s0, s1, p0, p1, eh0, eh1, dh0, dh1);
                    segments.Add(seg);
                    AccumulateArea(seg.Length, eh0 - dh0, eh1 - dh1, ref cut, ref fill);
                }
            }

            if (segments.Count == 0) warnings.Add($"{line.Name}: không có vùng giao đồng thời giữa TIN hiện trạng và TIN thiết kế.");
            return new SectionProfile(line, segments, cut, fill, warnings);
        }

        public bool TryElevation(TinSurface tin, Vec2 p, double tol, out double z)
        {
            foreach (var tri in tin.Triangles)
                if (TryElevationOnTriangle(tri, p, tol, out z)) return true;
            z = 0;
            return false;
        }

        private static void CollectTriangleEdgeIntersections(SectionLine line, SectionSpan span, TinSurface tin, List<double> output, double tol)
        {
            Vec2 a = line.PointAt(span.StartT), b = line.PointAt(span.EndT);
            foreach (var tri in tin.Triangles)
            {
                AddEdgeIntersection(a, b, tri.A.XY, tri.B.XY, span.StartT, span.EndT, output, tol);
                AddEdgeIntersection(a, b, tri.B.XY, tri.C.XY, span.StartT, span.EndT, output, tol);
                AddEdgeIntersection(a, b, tri.C.XY, tri.A.XY, span.StartT, span.EndT, output, tol);
            }
        }

        private static void AddEdgeIntersection(Vec2 a, Vec2 b, Vec2 c, Vec2 d, double tStart, double tEnd, List<double> output, double tol)
        {
            double rx = b.X - a.X, ry = b.Y - a.Y;
            double sx = d.X - c.X, sy = d.Y - c.Y;
            double den = rx * sy - ry * sx;
            double qpx = c.X - a.X, qpy = c.Y - a.Y;
            if (Math.Abs(den) <= tol)
            {
                // Collinear edge: its endpoints are enough as breakpoints when they lie on the section span.
                double cross = qpx * ry - qpy * rx;
                if (Math.Abs(cross) > tol) return;
                AddPointProjection(a, b, c, tStart, tEnd, output, tol);
                AddPointProjection(a, b, d, tStart, tEnd, output, tol);
                return;
            }
            double u = (qpx * sy - qpy * sx) / den;
            double v = (qpx * ry - qpy * rx) / den;
            if (u < -tol || u > 1 + tol || v < -tol || v > 1 + tol) return;
            u = Math.Max(0, Math.Min(1, u));
            output.Add(tStart + (tEnd - tStart) * u);
        }

        private static void AddPointProjection(Vec2 a, Vec2 b, Vec2 p, double tStart, double tEnd, List<double> output, double tol)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double den = dx * dx + dy * dy;
            if (den <= tol * tol) return;
            double u = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / den;
            if (u >= -tol && u <= 1 + tol) output.Add(tStart + (tEnd - tStart) * Math.Max(0, Math.Min(1, u)));
        }

        private bool TryElevationRobust(TinSurface tin, Vec2 p, Vec2 interior, double tol, out double z)
        {
            if (TryElevation(tin, p, tol * 20, out z)) return true;
            // Nudge an endpoint a tiny amount into the known interior interval to avoid numerical misses at TIN edges.
            var q = new Vec2(p.X + (interior.X - p.X) * 1e-8, p.Y + (interior.Y - p.Y) * 1e-8);
            return TryElevation(tin, q, tol * 50, out z);
        }

        private static bool TryElevationOnTriangle(Triangle3 t, Vec2 p, double tol, out double z)
        {
            double den = (t.B.Y - t.C.Y) * (t.A.X - t.C.X) + (t.C.X - t.B.X) * (t.A.Y - t.C.Y);
            if (Math.Abs(den) <= tol) { z = 0; return false; }
            double w1 = ((t.B.Y - t.C.Y) * (p.X - t.C.X) + (t.C.X - t.B.X) * (p.Y - t.C.Y)) / den;
            double w2 = ((t.C.Y - t.A.Y) * (p.X - t.C.X) + (t.A.X - t.C.X) * (p.Y - t.C.Y)) / den;
            double w3 = 1.0 - w1 - w2;
            if (w1 < -tol || w2 < -tol || w3 < -tol) { z = 0; return false; }
            z = w1 * t.A.Z + w2 * t.B.Z + w3 * t.C.Z;
            return true;
        }

        private static void AccumulateArea(double length, double delta0, double delta1, ref double cut, ref double fill)
        {
            if (length <= 0) return;
            if (delta0 >= 0 && delta1 >= 0) { cut += 0.5 * (delta0 + delta1) * length; return; }
            if (delta0 <= 0 && delta1 <= 0) { fill += 0.5 * (-delta0 - delta1) * length; return; }
            double a = Math.Abs(delta0), b = Math.Abs(delta1);
            double firstLength = length * a / Math.Max(1e-30, a + b);
            double secondLength = length - firstLength;
            double firstArea = 0.5 * a * firstLength;
            double secondArea = 0.5 * b * secondLength;
            if (delta0 > 0) { cut += firstArea; fill += secondArea; }
            else { fill += firstArea; cut += secondArea; }
        }

        private static List<double> Deduplicate(List<double> values, double tol)
        {
            var r = new List<double>();
            foreach (var v in values)
                if (r.Count == 0 || Math.Abs(v - r[r.Count - 1]) > tol) r.Add(v);
            return r;
        }
    }
}