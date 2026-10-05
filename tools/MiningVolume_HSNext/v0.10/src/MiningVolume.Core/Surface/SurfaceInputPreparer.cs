using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;

namespace MiningVolume.Core.Surface
{
    /// <summary>
    /// Chuẩn hóa dữ liệu trước khi dựng TIN. Mốc v0.3 dùng spatial hash để tránh
    /// các vòng lặp site×segment / segment×segment toàn phần trên bản vẽ lớn.
    /// </summary>
    public sealed class SurfaceInputPreparer
    {
        public PreparedSurfaceInput Prepare(SurfaceModel model, SurfaceBuildOptions options)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            options = options ?? new SurfaceBuildOptions();
            var result = new PreparedSurfaceInput();

            var active = model.Entities.Where(e => e.IsEnabled).ToList();
            foreach (var e in active)
            {
                foreach (var v in e.ActiveVertices()) result.Sites.Add(v.Position);
                foreach (var s in e.ActiveBreaklineSegments())
                {
                    if (s.Length2D <= options.XyTolerance)
                    {
                        result.Issues.Add(new ValidationIssue(ValidationSeverity.Warning, "ZERO_LENGTH_BREAKLINE",
                            "Bỏ qua đoạn breakline có chiều dài XY gần bằng 0.", s.SourceId));
                    }
                    else result.Breaklines.Add(s);
                }
            }

            ResolveDuplicateSites(result, options);
            SplitBreaklinesAtExistingSites(result, options);
            ValidateBreaklineTopology(result, options);

            if (result.Sites.Count < 3)
                result.Issues.Add(new ValidationIssue(ValidationSeverity.Error, "TOO_FEW_POINTS",
                    "Mô hình cần ít nhất 3 điểm XY hợp lệ để dựng TIN."));

            return result;
        }

        private static void ResolveDuplicateSites(PreparedSurfaceInput result, SurfaceBuildOptions options)
        {
            if (options.XyTolerance <= 0) throw new ArgumentOutOfRangeException(nameof(options.XyTolerance));
            var unique = new List<Vec3>();
            var grid = new Dictionary<string, List<int>>();
            foreach (var p in result.Sites)
            {
                long ix = (long)Math.Floor(p.X / options.XyTolerance);
                long iy = (long)Math.Floor(p.Y / options.XyTolerance);
                int match = -1;
                for (long dx = -1; dx <= 1 && match < 0; dx++)
                for (long dy = -1; dy <= 1 && match < 0; dy++)
                {
                    string k = (ix + dx) + ":" + (iy + dy);
                    if (!grid.TryGetValue(k, out var ids)) continue;
                    foreach (int id in ids)
                    {
                        if (unique[id].XY.DistanceTo(p.XY) <= options.XyTolerance) { match = id; break; }
                    }
                }

                if (match < 0)
                {
                    string key = ix + ":" + iy;
                    if (!grid.TryGetValue(key, out var ids)) { ids = new List<int>(); grid[key] = ids; }
                    ids.Add(unique.Count);
                    unique.Add(p);
                }
                else
                {
                    var old = unique[match];
                    if (Math.Abs(old.Z - p.Z) > options.ZConflictTolerance)
                    {
                        result.Issues.Add(new ValidationIssue(ValidationSeverity.Error, "DUPLICATE_XY_CONFLICT_Z",
                            $"Hai điểm trùng XY nhưng khác Z: ({p.X:0.###}, {p.Y:0.###}) có Z={old.Z:0.###} và Z={p.Z:0.###}."));
                    }
                }
            }
            result.Sites.Clear();
            result.Sites.AddRange(unique);
        }

        private static void SplitBreaklinesAtExistingSites(PreparedSurfaceInput result, SurfaceBuildOptions options)
        {
            if (result.Breaklines.Count == 0 || result.Sites.Count == 0) return;
            var index = new PointGridIndex(result.Sites, options.XyTolerance);
            var split = new List<Segment3>(result.Breaklines.Count);

            foreach (var seg in result.Breaklines)
            {
                var pts = new List<Tuple<double, Vec3>>
                {
                    Tuple.Create(0.0, seg.A), Tuple.Create(1.0, seg.B)
                };
                double dx = seg.B.X - seg.A.X, dy = seg.B.Y - seg.A.Y;
                double den = dx * dx + dy * dy;
                if (den <= options.XyTolerance * options.XyTolerance) continue;

                foreach (int siteIndex in index.Query(seg.A.X, seg.A.Y, seg.B.X, seg.B.Y, options.XyTolerance))
                {
                    var site = result.Sites[siteIndex];
                    if (!Geometry2D.PointOnSegment(site.XY, seg.A.XY, seg.B.XY, options.XyTolerance)) continue;
                    double t = ((site.X - seg.A.X) * dx + (site.Y - seg.A.Y) * dy) / den;
                    if (t <= options.XyTolerance || t >= 1.0 - options.XyTolerance) continue;
                    double expected = Geometry2D.InterpolateZOnSegment(site.XY, seg);
                    if (Math.Abs(expected - site.Z) > options.ZConflictTolerance)
                    {
                        result.Issues.Add(new ValidationIssue(ValidationSeverity.Error, "POINT_ON_BREAKLINE_Z_CONFLICT",
                            $"Điểm nằm trên breakline {seg.SourceId} nhưng Z={site.Z:0.###} khác Z nội suy {expected:0.###}.", seg.SourceId));
                    }
                    pts.Add(Tuple.Create(t, site));
                }

                pts.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var a = pts[i].Item2; var b = pts[i + 1].Item2;
                    if (a.XY.DistanceTo(b.XY) > options.XyTolerance)
                        split.Add(new Segment3(a, b, seg.SourceId));
                }
            }
            result.Breaklines.Clear();
            result.Breaklines.AddRange(split);
        }

        private static void ValidateBreaklineTopology(PreparedSurfaceInput result, SurfaceBuildOptions options)
        {
            int count = result.Breaklines.Count;
            if (count < 2) return;
            var index = new SegmentGridIndex(result.Breaklines, options.XyTolerance);
            var seen = new HashSet<long>();

            for (int i = 0; i < count; i++)
            {
                var a = result.Breaklines[i];
                foreach (int j in index.Query(i))
                {
                    if (j <= i) continue;
                    long pair = ((long)i << 32) | (uint)j;
                    if (!seen.Add(pair)) continue;
                    var b = result.Breaklines[j];
                    if (Geometry2D.ProperIntersection(a.A.XY, a.B.XY, b.A.XY, b.B.XY, options.XyTolerance))
                    {
                        result.Issues.Add(new ValidationIssue(ValidationSeverity.Error, "BREAKLINE_CROSSING",
                            $"Hai breakline cắt nhau nhưng không có đỉnh chung ({a.SourceId} / {b.SourceId}). Cần tạo điểm nút tại giao điểm trước khi dựng TIN."));
                        continue;
                    }
                    if (CollinearOverlap(a, b, options.XyTolerance))
                    {
                        result.Issues.Add(new ValidationIssue(ValidationSeverity.Error, "BREAKLINE_OVERLAP",
                            $"Hai breakline chồng lấn nhau ({a.SourceId} / {b.SourceId}). Cần xử lý đoạn trùng trước khi dựng TIN."));
                    }
                }
            }
        }

        private static bool CollinearOverlap(Segment3 a, Segment3 b, double tol)
        {
            double la = a.Length2D;
            if (la <= tol || b.Length2D <= tol) return false;
            if (Math.Abs(Vec2.Cross(a.A.XY, a.B.XY, b.A.XY)) / la > tol) return false;
            if (Math.Abs(Vec2.Cross(a.A.XY, a.B.XY, b.B.XY)) / la > tol) return false;

            bool useX = Math.Abs(a.B.X - a.A.X) >= Math.Abs(a.B.Y - a.A.Y);
            double a0 = useX ? a.A.X : a.A.Y, a1 = useX ? a.B.X : a.B.Y;
            double b0 = useX ? b.A.X : b.A.Y, b1 = useX ? b.B.X : b.B.Y;
            if (a0 > a1) { double t = a0; a0 = a1; a1 = t; }
            if (b0 > b1) { double t = b0; b0 = b1; b1 = t; }
            double overlap = Math.Min(a1, b1) - Math.Max(a0, b0);
            return overlap > tol;
        }

        private sealed class PointGridIndex
        {
            private readonly IReadOnlyList<Vec3> _points;
            private readonly Dictionary<string, List<int>> _cells = new Dictionary<string, List<int>>();
            private readonly double _cell;
            private readonly double _minX, _minY;

            public PointGridIndex(IReadOnlyList<Vec3> points, double tol)
            {
                _points = points;
                Bounds(points.Select(p => p.X), points.Select(p => p.Y), out _minX, out _minY, out var maxX, out var maxY);
                double span = Math.Max(maxX - _minX, maxY - _minY);
                _cell = Math.Max(tol * 100.0, span / Math.Max(8.0, Math.Sqrt(Math.Max(1, points.Count))));
                for (int i = 0; i < points.Count; i++) Add(i, points[i].X, points[i].Y);
            }

            public IEnumerable<int> Query(double x0, double y0, double x1, double y1, double pad)
            {
                int ix0 = Ix(Math.Min(x0, x1) - pad), ix1 = Ix(Math.Max(x0, x1) + pad);
                int iy0 = Iy(Math.Min(y0, y1) - pad), iy1 = Iy(Math.Max(y0, y1) + pad);
                var seen = new HashSet<int>();
                for (int ix = ix0; ix <= ix1; ix++)
                for (int iy = iy0; iy <= iy1; iy++)
                    if (_cells.TryGetValue(Key(ix, iy), out var ids))
                        foreach (var id in ids) if (seen.Add(id)) yield return id;
            }

            private void Add(int i, double x, double y)
            {
                string key = Key(Ix(x), Iy(y));
                if (!_cells.TryGetValue(key, out var ids)) { ids = new List<int>(); _cells[key] = ids; }
                ids.Add(i);
            }
            private int Ix(double x) => (int)Math.Floor((x - _minX) / _cell);
            private int Iy(double y) => (int)Math.Floor((y - _minY) / _cell);
            private static string Key(int x, int y) => x + ":" + y;
        }

        private sealed class SegmentGridIndex
        {
            private readonly IReadOnlyList<Segment3> _segments;
            private readonly Dictionary<string, List<int>> _cells = new Dictionary<string, List<int>>();
            private readonly List<List<string>> _segmentCells = new List<List<string>>();
            private readonly double _cell, _minX, _minY;

            public SegmentGridIndex(IReadOnlyList<Segment3> segments, double tol)
            {
                _segments = segments;
                var xs = segments.SelectMany(s => new[] { s.A.X, s.B.X });
                var ys = segments.SelectMany(s => new[] { s.A.Y, s.B.Y });
                Bounds(xs, ys, out _minX, out _minY, out var maxX, out var maxY);
                double span = Math.Max(maxX - _minX, maxY - _minY);
                _cell = Math.Max(tol * 100.0, span / Math.Max(8.0, Math.Sqrt(Math.Max(1, segments.Count))));
                for (int i = 0; i < segments.Count; i++) IndexSegment(i, segments[i], tol);
            }

            public IEnumerable<int> Query(int segmentIndex)
            {
                var seen = new HashSet<int>();
                foreach (var key in _segmentCells[segmentIndex])
                    if (_cells.TryGetValue(key, out var ids))
                        foreach (var id in ids) if (seen.Add(id)) yield return id;
            }

            private void IndexSegment(int i, Segment3 s, double pad)
            {
                int ix0 = Ix(Math.Min(s.A.X, s.B.X) - pad), ix1 = Ix(Math.Max(s.A.X, s.B.X) + pad);
                int iy0 = Iy(Math.Min(s.A.Y, s.B.Y) - pad), iy1 = Iy(Math.Max(s.A.Y, s.B.Y) + pad);
                var keys = new List<string>();
                for (int ix = ix0; ix <= ix1; ix++)
                for (int iy = iy0; iy <= iy1; iy++)
                {
                    string key = Key(ix, iy);
                    keys.Add(key);
                    if (!_cells.TryGetValue(key, out var ids)) { ids = new List<int>(); _cells[key] = ids; }
                    ids.Add(i);
                }
                _segmentCells.Add(keys);
            }
            private int Ix(double x) => (int)Math.Floor((x - _minX) / _cell);
            private int Iy(double y) => (int)Math.Floor((y - _minY) / _cell);
            private static string Key(int x, int y) => x + ":" + y;
        }

        private static void Bounds(IEnumerable<double> xs, IEnumerable<double> ys,
            out double minX, out double minY, out double maxX, out double maxY)
        {
            var xa = xs.ToArray(); var ya = ys.ToArray();
            if (xa.Length == 0 || ya.Length == 0) { minX = minY = 0; maxX = maxY = 1; return; }
            minX = xa.Min(); maxX = xa.Max(); minY = ya.Min(); maxY = ya.Max();
            if (maxX <= minX) maxX = minX + 1.0;
            if (maxY <= minY) maxY = minY + 1.0;
        }
    }
}