using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        // A rebuilt surface is a new identity. Weak keys release the index when
        // its surface is no longer retained by a project/snapshot. All samplers
        // (normal, quick and interval-midpoint) share the same completed index.
        private static readonly ConditionalWeakTable<TinSurface, IndexSlot> IndexCache =
            new ConditionalWeakTable<TinSurface, IndexSlot>();
        private sealed class LocalIndex
        {
            internal readonly TinSurface Tin;
            internal readonly TriangleGridIndex Index;
            internal LocalIndex(TinSurface tin, TriangleGridIndex index) { Tin = tin; Index = index; }
        }
        private LocalIndex _firstIndex, _secondIndex;

        private sealed class IndexSlot
        {
            internal readonly object Sync = new object();
            internal TriangleGridIndex Index;
        }

        public SectionProfile BuildProfile(SectionLine line, TinSurface existing, TinSurface design, double tol = 1e-7, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            if (design == null) throw new ArgumentNullException(nameof(design));

            var existingIndex = IndexFor(existing, cancellationToken);
            var designIndex = IndexFor(design, cancellationToken);
            var profile = AnnotateVerticalConflicts(
                BuildProfileIndexed(line, existingIndex, designIndex, tol, cancellationToken),
                existing, design, tol);
            cancellationToken.ThrowIfCancellationRequested();
            return profile;
        }

        public IReadOnlyList<SectionProfile> BuildProfiles(
            IReadOnlyList<SectionLine> lines,
            TinSurface existing,
            TinSurface design,
            Action<int, int, string> progress = null,
            double tol = 1e-7, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            if (design == null) throw new ArgumentNullException(nameof(design));

            // Build each spatial index once, then reuse it for the whole section system.
            // Profiles are independent and both triangle indices are immutable after
            // construction, so large section systems can use multiple CPU cores safely.
            var existingIndex = IndexFor(existing, cancellationToken);
            var designIndex = IndexFor(design, cancellationToken);
            var result = new SectionProfile[lines.Count];
            int completed = 0;

            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                // File-backed tiles share a bounded cache and disk. Unrestricted
                // core-count parallelism evicts hot tiles and serializes reads.
                MaxDegreeOfParallelism =
                    existingIndex.IsTiled || designIndex.IsTiled
                        ? Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1))
                        : Math.Max(1, Environment.ProcessorCount - 1)
            };

            Parallel.For(0, lines.Count, options, i =>
            {
                result[i] = AnnotateVerticalConflicts(
                    BuildProfileIndexed(lines[i], existingIndex, designIndex, tol, cancellationToken),
                    existing, design, tol);
                int done = Interlocked.Increment(ref completed);
                progress?.Invoke(done, lines.Count, lines[i].Name);
            });

            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        public bool TryElevation(TinSurface tin, Vec2 p, double tol, out double z)
        {
            if (tin == null) throw new ArgumentNullException(nameof(tin));
            return IndexFor(tin).TryElevation(p, tol, out z);
        }

        private static SectionProfile AnnotateVerticalConflicts(SectionProfile profile,
            TinSurface existing, TinSurface design, double tol)
        {
            if (!existing.RequiresSectionReview && !design.RequiresSectionReview)
                return profile;
            // Remote double-Z points must not force review of unrelated
            // sections. Only projected 3D curtains crossing a route matter.
            if (!VerticalConflictFootprint.TouchesSection(
                profile.Line, existing, design, tol))
                return profile;
            var warnings = profile.Warnings.ToList();
            warnings.Add("MẶT ĐỨNG NHIỀU Z: tuyến " + profile.Line.Name +
                " giao vùng nhiều cao độ tại cùng XY. Kiểm tra hình học CAD và " +
                "bấm Cập nhật mặt cắt nếu cần; dữ liệu Z gốc được giữ nguyên.");
            return new SectionProfile(profile.Line, profile.Segments,
                profile.CutArea, profile.FillArea, warnings, profile.IsUserEdited);
        }

        private SectionProfile BuildProfileIndexed(
            SectionLine line,
            TriangleGridIndex existing,
            TriangleGridIndex design,
            double tol, CancellationToken cancellationToken)
        {
            var segments = new List<SectionProfileSegment>();
            var warnings = new List<string>();
            double cut = 0, fill = 0;
            double globalMinT = line.MinT;

            foreach (var span in line.Spans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var knots = new List<double> { span.StartT, span.EndT };
                CollectTriangleEdgeIntersections(line, span, existing, knots, tol, cancellationToken);
                CollectTriangleEdgeIntersections(line, span, design, knots, tol, cancellationToken);
                knots.Sort();
                knots = Deduplicate(knots, tol * 10);

                for (int i = 0; i < knots.Count - 1; i++)
                {
                    if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                    double t0 = knots[i], t1 = knots[i + 1];
                    if (t1 - t0 <= tol) continue;
                    double tm = (t0 + t1) * 0.5;
                    Vec2 pm = line.PointAt(tm);
                    double dummy;
                    if (!existing.TryElevation(pm, tol * 20, out dummy) ||
                        !design.TryElevation(pm, tol * 20, out dummy))
                    {
                        warnings.Add(line.Name +
                            ": có đoạn thiếu giao TIN hiện trạng/thiết kế; " +
                            "không được coi phần mặt cắt còn lại là đã tính đủ.");
                        continue;
                    }

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

        private TriangleGridIndex IndexFor(TinSurface tin, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Keep the per-sampler two-surface hot path lock-free for hundreds
            // of interval midpoint queries; publish identity/index as one object.
            var first = _firstIndex;
            if (first != null && ReferenceEquals(first.Tin, tin)) return first.Index;
            var second = _secondIndex;
            if (second != null && ReferenceEquals(second.Tin, tin)) return second.Index;
            var slot = IndexCache.GetValue(tin, _ => new IndexSlot());
            // Waiting for another build is cancelable too. Never cache Lazy<T>
            // exceptions: a canceled build must be retryable by the next caller.
            while (!Monitor.TryEnter(slot.Sync, 50))
                cancellationToken.ThrowIfCancellationRequested();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (slot.Index == null)
                {
                    var index = new TriangleGridIndex(tin, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    slot.Index = index;
                }
                var local = new LocalIndex(tin, slot.Index);
                if (_firstIndex == null) _firstIndex = local;
                else _secondIndex = local;
                return local.Index;
            }
            finally { Monitor.Exit(slot.Sync); }
        }

        private static void CollectTriangleEdgeIntersections(
            SectionLine line,
            SectionSpan span,
            TriangleGridIndex index,
            List<double> output,
            double tol, CancellationToken cancellationToken)
        {
            Vec2 a = line.PointAt(span.StartT);
            Vec2 b = line.PointAt(span.EndT);
            int visited = 0;
            foreach (var tri in index.QuerySegmentTriangles(a, b))
            {
                if ((visited++ & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
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
            double den = RobustOrientation.Determinant(t.A.XY,t.B.XY,t.C.XY);
            if (den == 0) { z = 0; return false; }
            double w1 = RobustOrientation.Determinant(p,t.B.XY,t.C.XY)/den;
            double w2 = RobustOrientation.Determinant(t.A.XY,p,t.C.XY)/den;
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
            private readonly ITiledTriangleSource _tiled;
            // Only hot tiles are retained for this section-sampling operation.
            // Unlike rereading all ~8192 triangles at EACH elevation knot,
            // each hot tile has a small XY bounding-box index.
            private const int MaxHotTiles = 24;
            private readonly object _tileSync = new object();
            private readonly Dictionary<int, TilePointIndex> _hotTiles =
                new Dictionary<int, TilePointIndex>();
            private readonly LinkedList<int> _hotTileLru = new LinkedList<int>();
            private readonly Dictionary<long, List<int>> _cells =
                new Dictionary<long, List<int>>();
            private readonly double[] _minX;
            private readonly double[] _maxX;
            private readonly double[] _minY;
            private readonly double[] _maxY;
            private readonly double _originX;
            private readonly double _originY;
            private readonly double _cell;

            public TriangleGridIndex(TinSurface tin, CancellationToken cancellationToken)
            {
                _triangles = tin.Triangles ?? MiningVolume.Core.Compatibility.EmptyArray<Triangle3>.Value;
                _tiled = _triangles as ITiledTriangleSource;
                int count = _triangles.Count;

                // Large-mine TIN is already spatially partitioned on disk. Do not
                // allocate four double arrays + a global triangle grid for millions
                // of faces; query only intersecting tiles on demand.
                if (_tiled != null)
                {
                    _minX = _maxX = _minY = _maxY = MiningVolume.Core.Compatibility.EmptyArray<double>.Value;
                    _originX = _originY = 0;
                    _cell = 1;
                    return;
                }

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
                    if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
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
                    if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
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
            public bool IsTiled => _tiled != null;

            private TilePointIndex GetTile(int index)
            {
                lock (_tileSync)
                {
                    TilePointIndex item;
                    if (_hotTiles.TryGetValue(index, out item))
                    {
                        _hotTileLru.Remove(index);
                        _hotTileLru.AddLast(index);
                        return item;
                    }
                    item = new TilePointIndex(
                        _tiled.ReadTile(index), _tiled.Tiles[index]);
                    _hotTiles.Add(index, item);
                    _hotTileLru.AddLast(index);
                    while (_hotTiles.Count > MaxHotTiles)
                    {
                        int victim = _hotTileLru.First.Value;
                        _hotTileLru.RemoveFirst();
                        _hotTiles.Remove(victim);
                    }
                    return item;
                }
            }

            public bool TryElevation(Vec2 p, double tol, out double z)
            {
                if (_tiled != null)
                {
                    double pad = Math.Max(tol * 50.0, 1e-9);
                    foreach (int tileIndex in _tiled.QueryTiles(
                        p.X - pad, p.Y - pad, p.X + pad, p.Y + pad))
                    {
                        if (GetTile(tileIndex).TryElevation(p, tol, out z))
                            return true;
                    }
                    z = 0;
                    return false;
                }

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

            public IEnumerable<Triangle3> QuerySegmentTriangles(Vec2 a, Vec2 b)
            {
                if (_tiled != null)
                {
                    double minX = Math.Min(a.X, b.X);
                    double maxX = Math.Max(a.X, b.X);
                    double minY = Math.Min(a.Y, b.Y);
                    double maxY = Math.Max(a.Y, b.Y);

                    foreach (int tileIndex in _tiled.QueryTiles(minX, minY, maxX, maxY))
                    {
                        var tris = GetTile(tileIndex).Triangles;
                        foreach (var tri in tris)
                        {
                            double tMinX = Math.Min(tri.A.X, Math.Min(tri.B.X, tri.C.X));
                            double tMaxX = Math.Max(tri.A.X, Math.Max(tri.B.X, tri.C.X));
                            double tMinY = Math.Min(tri.A.Y, Math.Min(tri.B.Y, tri.C.Y));
                            double tMaxY = Math.Max(tri.A.Y, Math.Max(tri.B.Y, tri.C.Y));
                            if (tMaxX < minX || tMinX > maxX || tMaxY < minY || tMinY > maxY)
                                continue;
                            yield return tri;
                        }
                    }
                    yield break;
                }

                foreach (int triIndex in QuerySegment(a, b))
                    yield return _triangles[triIndex];
            }

            // Exact geometry: candidate bins only filter triangle bounding
            // rectangles. Elevation still uses the original robust barycentric
            // test, with no thinning, interpolation shortcuts or skipped faces.
            private sealed class TilePointIndex
            {
                private const int GridSide = 32;
                private const int MaxCoveredCells = 64;
                private readonly IReadOnlyList<Triangle3> _triangles;
                private readonly Dictionary<int, List<int>> _bins =
                    new Dictionary<int, List<int>>();
                private readonly List<int> _broad = new List<int>();
                private readonly double _x, _y, _dx, _dy;
                private readonly double[] _minX, _maxX, _minY, _maxY;

                public TilePointIndex(IReadOnlyList<Triangle3> triangles, TinTileInfo tile)
                {
                    _triangles = triangles;
                    _x = tile.MinX;
                    _y = tile.MinY;
                    _dx = Math.Max(1e-9, (tile.MaxX - tile.MinX) / GridSide);
                    _dy = Math.Max(1e-9, (tile.MaxY - tile.MinY) / GridSide);
                    _minX = new double[triangles.Count];
                    _maxX = new double[triangles.Count];
                    _minY = new double[triangles.Count];
                    _maxY = new double[triangles.Count];
                    for (int i = 0; i < triangles.Count; i++)
                    {
                        var t = triangles[i];
                        double x0 = Math.Min(t.A.X, Math.Min(t.B.X, t.C.X));
                        double x1 = Math.Max(t.A.X, Math.Max(t.B.X, t.C.X));
                        double y0 = Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y));
                        double y1 = Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y));
                        _minX[i] = x0; _maxX[i] = x1;
                        _minY[i] = y0; _maxY[i] = y1;
                        int ix0 = Bin(x0, _x, _dx), ix1 = Bin(x1, _x, _dx);
                        int iy0 = Bin(y0, _y, _dy), iy1 = Bin(y1, _y, _dy);
                        if ((ix1 - ix0 + 1) * (iy1 - iy0 + 1) > MaxCoveredCells)
                        {
                            // Very wide triangles are tested globally: never
                            // silently omit one that spans several cells.
                            _broad.Add(i);
                            continue;
                        }
                        for (int ix = ix0; ix <= ix1; ix++)
                        for (int iy = iy0; iy <= iy1; iy++)
                        {
                            int key = ix * GridSide + iy;
                            List<int> list;
                            if (!_bins.TryGetValue(key, out list))
                            {
                                list = new List<int>();
                                _bins[key] = list;
                            }
                            list.Add(i);
                        }
                    }
                }

                public IReadOnlyList<Triangle3> Triangles => _triangles;

                public bool TryElevation(Vec2 p, double tol, out double z)
                {
                    int ix0 = Bin(p.X - tol, _x, _dx), ix1 = Bin(p.X + tol, _x, _dx);
                    int iy0 = Bin(p.Y - tol, _y, _dy), iy1 = Bin(p.Y + tol, _y, _dy);
                    for (int ix = ix0; ix <= ix1; ix++)
                    for (int iy = iy0; iy <= iy1; iy++)
                    {
                        List<int> candidates;
                        if (!_bins.TryGetValue(ix * GridSide + iy, out candidates))
                            continue;
                        foreach (int i in candidates)
                            if (ContainsXY(i, p, tol) &&
                                TryElevationOnTriangle(_triangles[i], p, tol, out z))
                                return true;
                    }
                    foreach (int i in _broad)
                        if (ContainsXY(i, p, tol) &&
                            TryElevationOnTriangle(_triangles[i], p, tol, out z))
                            return true;
                    z = 0;
                    return false;
                }

                private bool ContainsXY(int index, Vec2 p, double tol)
                    => p.X >= _minX[index] - tol && p.X <= _maxX[index] + tol &&
                       p.Y >= _minY[index] - tol && p.Y <= _maxY[index] + tol;

                private static int Bin(double point, double origin, double cell)
                    => Math.Max(0, Math.Min(GridSide - 1,
                        (int)Math.Floor((point - origin) / cell)));
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
