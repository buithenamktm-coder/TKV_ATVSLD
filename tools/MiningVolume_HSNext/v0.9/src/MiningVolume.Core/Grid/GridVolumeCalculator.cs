using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Surface;

namespace MiningVolume.Core.Grid
{
    /// <summary>Hệ lưới có gốc, kích thước và góc xoay do người dùng xác định.</summary>
    public sealed class GridVolumeOptions
    {
        public Vec2 Origin { get; set; }
        public double SizeX { get; set; } = 20;
        public double SizeY { get; set; } = 20;
        public double RotationDegrees { get; set; }
        public int MaxCells { get; set; } = 100000;
        public IReadOnlyList<Vec2> Boundary { get; set; }
        public IReadOnlyList<IReadOnlyList<Vec2>> Holes { get; set; }
    }

    public sealed class GridVolumeCell
    {
        public int I { get; internal set; }
        public int J { get; internal set; }
        public string CellCode => "O-" + I + "-" + J;
        public IReadOnlyList<Vec2> Corners { get; internal set; }
        public IReadOnlyList<double?> ExistingNodeHeights { get; internal set; }
        public IReadOnlyList<double?> DesignNodeHeights { get; internal set; }
        public GridCellVolume Volume { get; internal set; }
        // A zero-volume grid cell with this flag is NOT certified zero.
        public bool IsVerticalConflict { get; internal set; }
    }

    public sealed class GridVolumeReport
    {
        public IReadOnlyList<GridVolumeCell> Cells { get; internal set; }
        public double TotalCutVolume => Cells.Sum(x => x.Volume.CutVolume);
        public double TotalFillVolume => Cells.Sum(x => x.Volume.FillVolume);
        public double TotalNoDataArea => Cells.Sum(x => x.Volume.NoDataArea);
        public bool IsComplete => Cells.All(x => x.Volume.IsComplete) && AmbiguousCells == 0;
        public int AmbiguousCells => Cells.Count(x => x.IsVerticalConflict);
        public double AmbiguousArea => Cells.Where(x => x.IsVerticalConflict).Sum(x => x.Volume.CalculationArea);
        public string Method => "TIN overlay – exact signed integration";
    }

    /// <summary>
    /// Lập ô lưới, truy vấn ứng viên tam giác ở mỗi ô và tổng hợp.
    /// Mỗi ô vẫn dùng kernel ExactGridCellCalculator; không làm tròn tại bước tính.
    /// </summary>
    public sealed class GridVolumeCalculator
    {
        public GridVolumeReport Calculate(
            TinSurface existing,
            TinSurface design,
            GridVolumeOptions options,
            Action<int, int> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (existing == null || existing.Triangles == null || existing.Triangles.Count == 0)
                throw new ArgumentException("TIN hiện trạng chưa hợp lệ.", nameof(existing));
            if (design == null || design.Triangles == null || design.Triangles.Count == 0)
                throw new ArgumentException("TIN thiết kế chưa hợp lệ.", nameof(design));
            // Do not globally reject an otherwise valid grid because of a
            // remote double-Z point. Instead mark cells touched by the real
            // vertical curtain as unverified; never silently choose a Z.
            if (options == null || options.Boundary == null || options.Boundary.Count < 3)
                throw new ArgumentException("Cần đường bao khép kín có ít nhất ba đỉnh.", nameof(options));
            if (!Finite(options.SizeX) || !Finite(options.SizeY) ||
                options.SizeX <= 0 || options.SizeY <= 0)
                throw new ArgumentException("Kích thước ô lưới phải là số dương hữu hạn.");
            if (!Finite(options.RotationDegrees) ||
                !Finite(options.Origin.X) || !Finite(options.Origin.Y))
                throw new ArgumentException("Gốc lưới hoặc góc xoay không hợp lệ.");
            if (options.MaxCells < 1)
                throw new ArgumentException("Giới hạn số ô phải lớn hơn 0.");

            double angle = options.RotationDegrees * Math.PI / 180.0;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            double minU = double.PositiveInfinity, maxU = double.NegativeInfinity;
            double minV = double.PositiveInfinity, maxV = double.NegativeInfinity;
            foreach (var p in options.Boundary)
            {
                double dx = p.X - options.Origin.X, dy = p.Y - options.Origin.Y;
                double u = cos * dx + sin * dy, v = -sin * dx + cos * dy;
                minU = Math.Min(minU, u); maxU = Math.Max(maxU, u);
                minV = Math.Min(minV, v); maxV = Math.Max(maxV, v);
            }

            long startI = (long)Math.Floor(minU / options.SizeX);
            long endI = (long)Math.Ceiling(maxU / options.SizeX);
            long startJ = (long)Math.Floor(minV / options.SizeY);
            long endJ = (long)Math.Ceiling(maxV / options.SizeY);
            long ni = endI - startI, nj = endJ - startJ;
            if (ni <= 0 || nj <= 0 || ni > int.MaxValue || nj > int.MaxValue ||
                ni > options.MaxCells || nj > options.MaxCells ||
                ni * nj > options.MaxCells)
                throw new InvalidOperationException("Số ô lưới vượt giới hạn thiết lập; hãy tăng kích thước ô hoặc thu nhỏ đường bao.");

            // No cell-specific dependence in polygon triangulation. Reusing
            // this immutable clip context removes thousands of redundant
            // O(vertices^2) ear-clipping passes for large mine boundaries.
            var clipBoundary = ExactGridCellCalculator.PrepareBoundary(
                options.Boundary, options.Holes);
            var unsafeCells = VerticalConflictFootprint.AffectedGridCells(
                existing, design, options.Origin, options.SizeX, options.SizeY,
                cos, sin, startI, endI, startJ, endJ);
            // A collapsed multi-Z support vertex can affect every incident
            // planar triangle, not only the zero-plan-area vertical curtain.
            var rootIndex = new VerticalConflictFootprint.ConflictRootIndex(
                existing, design, 1e-6);
            using (var existingIndex = new TriangleGridIndex(existing.Triangles, options, cos, sin))
            using (var designIndex = new TriangleGridIndex(design.Triangles, options, cos, sin))
            {
                var cells = new List<GridVolumeCell>();
                int total = checked((int)(ni * nj)), processed = 0;
                for (long j = startJ; j < endJ; j++)
                    for (long i = startI; i < endI; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        double u0 = i * options.SizeX, v0 = j * options.SizeY;
                        var cell = new[]
                        {
                            World(u0, v0, options.Origin, cos, sin),
                            World(u0 + options.SizeX, v0, options.Origin, cos, sin),
                            World(u0 + options.SizeX, v0 + options.SizeY, options.Origin, cos, sin),
                            World(u0, v0 + options.SizeY, options.Origin, cos, sin)
                        };
                        bool ambiguous = unsafeCells.Contains((checked((int)i),checked((int)j)));
                        var tn = ambiguous ? (IReadOnlyList<Triangle3>)Array.Empty<Triangle3>()
                            : existingIndex.Candidates((int)i, (int)j, cell);
                        var tk = ambiguous ? (IReadOnlyList<Triangle3>)Array.Empty<Triangle3>()
                            : designIndex.Candidates((int)i, (int)j, cell);
                        if (!ambiguous && (rootIndex.AnyIncident(tn) || rootIndex.AnyIncident(tk)))
                            ambiguous = true;
                        // An incident support triangle has a non-authoritative Z
                        // near the multi-Z root. Its volume is unverified; do not
                        // silently use the CDT's arbitrarily retained elevation.
                        if (ambiguous)
                        {
                            tn = Array.Empty<Triangle3>();
                            tk = Array.Empty<Triangle3>();
                        }
                        var volume = ExactGridCellCalculator.CalculatePrepared(tn, tk, cell, clipBoundary);
                        if (volume.CalculationArea > 1e-9)
                            cells.Add(new GridVolumeCell
                            {
                                I = checked((int)i), J = checked((int)j),
                                Corners = cell,
                                ExistingNodeHeights = cell.Select(p => SampleZ(tn, p)).ToArray(),
                                DesignNodeHeights = cell.Select(p => SampleZ(tk, p)).ToArray(),
                                Volume = volume,
                                IsVerticalConflict = ambiguous
                            });
                        processed++;
                        if (processed == total || processed % 100 == 0) progress?.Invoke(processed, total);
                    }
                return new GridVolumeReport { Cells = cells };
            }
        }

        private static double? SampleZ(IReadOnlyList<Triangle3> candidates, Vec2 p)
        {
            foreach (var t in candidates)
            {
                double determinant = Vec2.Cross(t.A.XY, t.B.XY, t.C.XY);
                if (Math.Abs(determinant) < 1e-14) continue;
                double a = Vec2.Cross(p, t.B.XY, t.C.XY) / determinant;
                double b = Vec2.Cross(t.A.XY, p, t.C.XY) / determinant;
                double c = 1.0 - a - b;
                if (a >= -1e-8 && b >= -1e-8 && c >= -1e-8 &&
                    a <= 1+1e-8 && b <= 1+1e-8 && c <= 1+1e-8)
                    return a*t.A.Z + b*t.B.Z + c*t.C.Z;
            }
            return null;
        }

        private static Vec2 World(double u, double v, Vec2 origin, double c, double s)
            => new Vec2(origin.X + c * u - s * v, origin.Y + s * u + c * v);

        private static bool Finite(double value) => !double.IsInfinity(value) && !double.IsNaN(value);

        /// <summary>
        /// Với TIN tile-backed: truy vấn các tile theo bbox ô (có cache giới hạn).
        /// Với TIN in-memory: lập spatial hash theo ô lưới; tam giác lớn được
        /// lưu riêng, không nhân lên hàng nghìn ô. Không âm thầm lược tam giác.
        /// </summary>
        private sealed class TriangleGridIndex : IDisposable
        {
            private readonly IReadOnlyList<Triangle3> _triangles;
            private readonly ITiledTriangleSource _tiles;
            private readonly GridVolumeOptions _opt;
            private readonly double _c, _s;
            private readonly Dictionary<(int,int),List<int>> _bins;
            private readonly List<int> _large;
            private readonly Dictionary<int,IReadOnlyList<Triangle3>> _tileCache = new Dictionary<int,IReadOnlyList<Triangle3>>();
            private readonly Queue<int> _cacheOrder = new Queue<int>();
            private const int MaxCacheTiles = 8;

            public TriangleGridIndex(IReadOnlyList<Triangle3> triangles, GridVolumeOptions options, double cos, double sin)
            {
                _triangles = triangles;
                _tiles = triangles as ITiledTriangleSource;
                _opt = options; _c = cos; _s = sin;
                if (_tiles == null)
                {
                    _bins = new Dictionary<(int,int),List<int>>();
                    _large = new List<int>();
                    for (int index = 0; index < triangles.Count; index++)
                    {
                        var t = triangles[index];
                        GetLocalLimits(t, out double u0, out double u1, out double v0, out double v1);
                        int loI = checked((int)Math.Floor(u0 / options.SizeX));
                        int hiI = checked((int)Math.Floor(u1 / options.SizeX));
                        int loJ = checked((int)Math.Floor(v0 / options.SizeY));
                        int hiJ = checked((int)Math.Floor(v1 / options.SizeY));
                        long count = ((long)hiI - loI + 1) * ((long)hiJ - loJ + 1);
                        if (count > 256)
                        {
                            _large.Add(index);
                            continue;
                        }
                        for (int i = loI; i <= hiI; i++)
                            for (int j = loJ; j <= hiJ; j++)
                            {
                                var key = (i, j);
                                if (!_bins.TryGetValue(key, out var entries))
                                {
                                    entries = new List<int>();
                                    _bins.Add(key, entries);
                                }
                                entries.Add(index);
                            }
                    }
                }
            }

            public IReadOnlyList<Triangle3> Candidates(int i, int j, IReadOnlyList<Vec2> cell)
            {
                double minX=double.PositiveInfinity, minY=double.PositiveInfinity;
                double maxX=double.NegativeInfinity, maxY=double.NegativeInfinity;
                foreach (var p in cell)
                {
                    minX=Math.Min(minX,p.X); maxX=Math.Max(maxX,p.X);
                    minY=Math.Min(minY,p.Y); maxY=Math.Max(maxY,p.Y);
                }
                var result = new List<Triangle3>();
                if (_tiles != null)
                {
                    foreach (int tile in _tiles.QueryTiles(minX, minY, maxX, maxY))
                    {
                        var candidates = ReadCachedTile(tile);
                        foreach (var t in candidates)
                            if (Touch(t,minX,minY,maxX,maxY)) result.Add(t);
                    }
                }
                else
                {
                    if (_bins.TryGetValue((i,j), out var indices))
                        foreach (int index in indices)
                        {
                            var t = _triangles[index];
                            if (Touch(t,minX,minY,maxX,maxY)) result.Add(t);
                        }
                    foreach (int index in _large)
                    {
                        var t = _triangles[index];
                        if (Touch(t,minX,minY,maxX,maxY)) result.Add(t);
                    }
                }
                return result;
            }

            private IReadOnlyList<Triangle3> ReadCachedTile(int tile)
            {
                if (_tileCache.TryGetValue(tile, out var result)) return result;
                result = _tiles.ReadTile(tile);
                _tileCache.Add(tile,result);
                _cacheOrder.Enqueue(tile);
                while (_cacheOrder.Count > MaxCacheTiles)
                {
                    int old = _cacheOrder.Dequeue();
                    _tileCache.Remove(old);
                }
                return result;
            }

            private void GetLocalLimits(Triangle3 t, out double u0, out double u1, out double v0, out double v1)
            {
                u0=v0=double.PositiveInfinity; u1=v1=double.NegativeInfinity;
                foreach (var p in new[] {t.A,t.B,t.C})
                {
                    double x=p.X-_opt.Origin.X,y=p.Y-_opt.Origin.Y;
                    double u=x*_c+y*_s,v=-x*_s+y*_c;
                    u0=Math.Min(u0,u);u1=Math.Max(u1,u);
                    v0=Math.Min(v0,v);v1=Math.Max(v1,v);
                }
            }

            private static bool Touch(Triangle3 t,double a,double b,double c,double d)
            {
                double minX=Math.Min(t.A.X,Math.Min(t.B.X,t.C.X));
                double maxX=Math.Max(t.A.X,Math.Max(t.B.X,t.C.X));
                double minY=Math.Min(t.A.Y,Math.Min(t.B.Y,t.C.Y));
                double maxY=Math.Max(t.A.Y,Math.Max(t.B.Y,t.C.Y));
                return !(maxX<a-1e-9||minX>c+1e-9||maxY<b-1e-9||minY>d+1e-9);
            }

            public void Dispose() { _tileCache.Clear(); _cacheOrder.Clear(); }
        }
    }
}
