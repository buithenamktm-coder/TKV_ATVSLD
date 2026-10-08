using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Surface;

namespace MiningVolume.Core.Sections
{
    /// <summary>
    /// Tạo mặt cắt chính xác từ hai TIN. Với TIN lớn, mọi truy vấn tuyến/cao độ
    /// đi qua chỉ mục lưới tam giác để không quét toàn bộ TIN cho từng mặt cắt.
    /// </summary>
    public sealed class TinSectionSampler
    {
        private TinSurface _cachedTin1;
        private TinSurface _cachedTin2;
        private TriangleGridIndex _cachedIndex1;
        private TriangleGridIndex _cachedIndex2;

        public SectionProfile BuildProfile(SectionLine line, TinSurface existing, TinSurface design, double tol = 1e-7)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            if (design == null) throw new ArgumentNullException(nameof(design));

            var existingIndex = IndexFor(existing);
            var designIndex = IndexFor(design);
            return BuildProfileIndexed(line, existingIndex, designIndex, tol);
        }

        public IReadOnlyList<SectionProfile> BuildProfiles(
            IReadOnlyList<SectionLine> lines,
            TinSurface existing,
            TinSurface design,
            Action<int, int, string> progress = null,
            double tol = 1e-7)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            if (design == null) throw new ArgumentNullException(nameof(design));

            // Build each spatial index once, then reuse it for the whole section system.
            // Profiles are independent and both triangle indices are immutable after
            // construction, so large section systems can use multiple CPU cores safely.
            var existingIndex = IndexFor(existing);
            var designIndex = IndexFor(design);
            var result = new SectionProfile[lines.Count];
            int completed = 0;

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };

            Parallel.For(0, lines.Count, options, i =>
            {
                result[i] = BuildProfileIndexed(lines[i], existingIndex, designIndex, tol);
                int done = Interlocked.Increment(ref completed);
                progress?.Invoke(done, lines.Count, lines[i].Name);
            });

            return result;
        }

        public bool TryElevation(TinSurface tin, Vec2 p, double tol, out double z)
        {
            if (tin == null) throw new ArgumentNullException(nameof(tin));
            return IndexFor(tin).TryElevation(p, tol, out z);
        }

        private SectionProfile BuildProfileIndexed(
            SectionLine line,
            TriangleGridIndex existing,
            TriangleGridIndex design,
            double tol)
        {
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
                    double dummy;
                    if (!existing.TryElevation(pm, tol * 20, out dummy) ||
                        !design.TryElevation(pm, tol * 20, out dummy))
                        continue;

                    Vec2 p0 = line.PointAt(t0), p1 = line.PointAt(t1);
                    double eh0, eh1, dh0, dh1;
                    if (!TryElevationRobust(existing, p0, pm, tol, out eh0) ||
                        !TryElevationRobust(existing, p1, pm, tol, out eh1) ||
                        !TryElevationRobust(design, p0, pm, tol, out dh0) ||
                        !TryElevationRobust(design, p1, pm, tol, out dh1))
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

            if (segments.Count == 0)
                warnings.Add($"{line.Name}: không có vùng giao đồng thời giữa TIN hiện trạng và TIN thiết kế.");
            return new SectionProfile(line, segments, cut, fill, warnings);
        }

        private TriangleGridIndex IndexFor(TinSurface tin)
        {
            if (ReferenceEquals(tin, _cachedTin1)) return _cachedIndex1;
            if (ReferenceEquals(tin, _cachedTin2)) return _cachedIndex2;

            var index = new TriangleGridIndex(tin);
            if (_cachedTin1 == null)
            {
                _cachedTin1 = tin;
                _cachedIndex1 = index;
            }
            else
            {
                _cachedTin2 = tin;
                _cachedIndex2 = index;
            }
            return index;
        }

        private static void CollectTriangleEdgeIntersections(
            SectionLine line,
            SectionSpan span,
            TriangleGridIndex index,
            List<double> output,
            double tol)
        {
            Vec2 a = line.PointAt(span.StartT);
            Vec2 b = line.PointAt(span.EndT);
            foreach (int triIndex in index.QuerySegment(a, b))
            {
                var tri = index.Triangles[triIndex];
                AddEdgeIntersection(a, b, tri.A.XY, tri.B.XY, span.StartT, span.EndT, output, tol);
                AddEdgeIntersection(a, b, tri.B.XY, tri.C.XY, span.StartT, span.EndT, output, tol);
                AddEdgeIntersection(a, b, tri.C.XY, tri.A.XY, span.StartT, span.EndT, output, tol);
            }
        }

        private static void AddEdgeIntersection(
            Vec2 a, Vec2 b, Vec2 c, Vec2 d,
            double tStart, double tEnd,
            List<double> output, double tol)
        {
            double rx = b.X - a.X, ry = b.Y - a.Y;
            double sx = d.X - c.X, sy = d.Y - c.Y;
            double den = rx * sy - ry * sx;
            double qpx = c.X - a.X, qpy = c.Y - a.Y;
            if (Math.Abs(den) <= tol)
            {
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

        private static void AddPointProjection(
            Vec2 a, Vec2 b, Vec2 p,
            double tStart, double tEnd,
            List<double> output, double tol)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double den = dx * dx + dy * dy;
            if (den <= tol * tol) return;
            double u = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / den;
            if (u >= -tol && u <= 1 + tol)
                output.Add(tStart + (tEnd - tStart) * Math.Max(0, Math.Min(1, u)));
        }

        private static bool TryElevationRobust(
            TriangleGridIndex index,
            Vec2 p,
            Vec2 interior,
            double tol,
            out double z)
        {
            if (index.TryElevation(p, tol * 20, out z)) return true;
            var q = new Vec2(
                p.X + (interior.X - p.X) * 1e-8,
                p.Y + (interior.Y - p.Y) * 1e-8);
            return index.TryElevation(q, tol * 50, out z);
        }

        private static bool TryElevationOnTriangle(Triangle3 t, Vec2 p, double tol, out double z)
        {
            double den = (t.B.Y - t.C.Y) * (t.A.X - t.C.X) +
                         (t.C.X - t.B.X) * (t.A.Y - t.C.Y);
            if (Math.Abs(den) <= tol) { z = 0; return false; }
            double w1 = ((t.B.Y - t.C.Y) * (p.X - t.C.X) +
                         (t.C.X - t.B.X) * (p.Y - t.C.Y)) / den;
            double w2 = ((t.C.Y - t.A.Y) * (p.X - t.C.X) +
                         (t.A.X - t.C.X) * (p.Y - t.C.Y)) / den;
            double w3 = 1.0 - w1 - w2;
            if (w1 < -tol || w2 < -tol || w3 < -tol) { z = 0; return false; }
            z = w1 * t.A.Z + w2 * t.B.Z + w3 * t.C.Z;
            return true;
        }

        private static void AccumulateArea(
            double length, double delta0, double delta1,
            ref double cut, ref double fill)
        {
            if (length <= 0) return;
            if (delta0 >= 0 && delta1 >= 0)
            {
                cut += 0.5 * (delta0 + delta1) * length;
                return;
            }
            if (delta0 <= 0 && delta1 <= 0)
            {
                fill += 0.5 * (-delta0 - delta1) * length;
                return;
            }
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
                if (r.Count == 0 || Math.Abs(v - r[r.Count - 1]) > tol)
                    r.Add(v);
            return r;
        }

        private sealed class TriangleGridIndex
        {
            private readonly IReadOnlyList<Triangle3> _triangles;
            private readonly Dictionary<long, List<int>> _cells =
                new Dictionary<long, List<int>>();
            private readonly double[] _minX;
            private readonly double[] _maxX;
            private readonly double[] _minY;
            private readonly double[] _maxY;
            private readonly double _originX;
            private readonly double _originY;
            private readonly double _cell;

            public TriangleGridIndex(TinSurface tin)
            {
                _triangles = tin.Triangles ?? Array.Empty<Triangle3>();
                int count = _triangles.Count;
                _minX = new double[count];
                _maxX = new double[count];
                _minY = new double[count];
                _maxY = new double[count];
                if (count == 0)
                {
                    _originX = _originY = 0;
                    _cell = 1;
                    return;
                }

                double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
                double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
                for (int i = 0; i < count; i++)
                {
                    var t = _triangles[i];
                    double ax = Math.Min(t.A.X, Math.Min(t.B.X, t.C.X));
                    double bx = Math.Max(t.A.X, Math.Max(t.B.X, t.C.X));
                    double ay = Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y));
                    double by = Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y));
                    _minX[i] = ax; _maxX[i] = bx; _minY[i] = ay; _maxY[i] = by;
                    minX = Math.Min(minX, ax); maxX = Math.Max(maxX, bx);
                    minY = Math.Min(minY, ay); maxY = Math.Max(maxY, by);
                }

                _originX = minX;
                _originY = minY;
                double span = Math.Max(maxX - minX, maxY - minY);
                double cellsAcross = Math.Max(16.0, Math.Min(384.0, Math.Sqrt(Math.Max(1, count) / 10.0)));
                _cell = Math.Max(1e-9, span / cellsAcross);

                for (int i = 0; i < count; i++)
                {
                    int ix0 = Ix(_minX[i]), ix1 = Ix(_maxX[i]);
                    int iy0 = Iy(_minY[i]), iy1 = Iy(_maxY[i]);
                    for (int ix = ix0; ix <= ix1; ix++)
                    for (int iy = iy0; iy <= iy1; iy++)
                    {
                        long key = Key(ix, iy);
                        List<int> ids;
                        if (!_cells.TryGetValue(key, out ids))
                        {
                            ids = new List<int>();
                            _cells[key] = ids;
                        }
                        ids.Add(i);
                    }
                }
            }

            public IReadOnlyList<Triangle3> Triangles => _triangles;

            public bool TryElevation(Vec2 p, double tol, out double z)
            {
                int ix = Ix(p.X), iy = Iy(p.Y);
                for (int radius = 0; radius <= 1; radius++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (radius > 0 && Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                            continue;
                        List<int> ids;
                        if (!_cells.TryGetValue(Key(ix + dx, iy + dy), out ids)) continue;
                        foreach (int id in ids)
                        {
                            if (p.X < _minX[id] - tol || p.X > _maxX[id] + tol ||
                                p.Y < _minY[id] - tol || p.Y > _maxY[id] + tol)
                                continue;
                            if (TryElevationOnTriangle(_triangles[id], p, tol, out z))
                                return true;
                        }
                    }
                }
                z = 0;
                return false;
            }

            public HashSet<int> QuerySegment(Vec2 a, Vec2 b)
            {
                var result = new HashSet<int>();
                double gx0 = (a.X - _originX) / _cell;
                double gy0 = (a.Y - _originY) / _cell;
                double gx1 = (b.X - _originX) / _cell;
                double gy1 = (b.Y - _originY) / _cell;
                int steps = Math.Max(1, (int)Math.Ceiling(
                    Math.Max(Math.Abs(gx1 - gx0), Math.Abs(gy1 - gy0)) * 2.0));

                double segMinX = Math.Min(a.X, b.X), segMaxX = Math.Max(a.X, b.X);
                double segMinY = Math.Min(a.Y, b.Y), segMaxY = Math.Max(a.Y, b.Y);

                for (int step = 0; step <= steps; step++)
                {
                    double u = step / (double)steps;
                    int ix = (int)Math.Floor(gx0 + (gx1 - gx0) * u);
                    int iy = (int)Math.Floor(gy0 + (gy1 - gy0) * u);

                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        List<int> ids;
                        if (!_cells.TryGetValue(Key(ix + dx, iy + dy), out ids)) continue;
                        foreach (int id in ids)
                        {
                            if (_maxX[id] < segMinX || _minX[id] > segMaxX ||
                                _maxY[id] < segMinY || _minY[id] > segMaxY)
                                continue;
                            result.Add(id);
                        }
                    }
                }
                return result;
            }

            private int Ix(double x) => (int)Math.Floor((x - _originX) / _cell);
            private int Iy(double y) => (int)Math.Floor((y - _originY) / _cell);
            private static long Key(int x, int y)
                => ((long)x << 32) ^ (uint)y;
        }
    }
}
