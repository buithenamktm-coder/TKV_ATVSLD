using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;
using MiningVolume.Core.Surface;

namespace MiningVolume.Surface
{
    /// <summary>
    /// Dựng TIN theo ô cho dữ liệu mỏ lớn. Mục tiêu thiết kế là hàng triệu đến
    /// khoảng 10 triệu đỉnh mà không đưa toàn bộ bài toán vào một phép Delaunay.
    ///
    /// Mỗi ô lõi được dựng với vùng đệm (halo), sau đó chỉ nhận tam giác có tâm
    /// nằm trong ô lõi. Vùng đệm được tăng tự động nếu tam giác lõi còn phụ thuộc
    /// biên vùng làm việc. Breakline được cắt theo vùng làm việc trước khi chuẩn hóa.
    /// </summary>
    public sealed class TiledConformingTinBuilder
    {
        private const int TargetCoreVertices = 25000;
        private static readonly double[] HaloFactors = { 0.20, 0.45, 0.90, 1.60 };

        private sealed class Bucket
        {
            public readonly List<Vec3> Points = new List<Vec3>();
            public readonly List<Segment3> Segments = new List<Segment3>();
        }

        public sealed class Result
        {
            public TinSurface Surface { get; set; }
            public int InputVertexCount { get; set; }
            public int InputBreaklineCount { get; set; }
            public int TileCount { get; set; }
            public int WarningCount { get; set; }
            public double MinZ { get; set; }
            public double MaxZ { get; set; }
            public long PrepareMilliseconds { get; set; }
            public long TriangulationMilliseconds { get; set; }
        }

        public Result Build(
            string name,
            SurfaceModel model,
            SurfaceBuildOptions options,
            Action<int, int, string> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            options = options ?? new SurfaceBuildOptions();

            int inputVertices = 0;
            int inputBreaklines = 0;
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            double minZ = double.PositiveInfinity, maxZ = double.NegativeInfinity;

            foreach (var e in model.Entities)
            {
                if (!e.IsEnabled) continue;
                foreach (var v in e.ActiveVertices())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var p = v.Position;
                    inputVertices++;
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                    minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
                }
                foreach (var seg in e.ActiveBreaklineSegments())
                    if (seg.Length2D > options.XyTolerance) inputBreaklines++;
            }

            if (inputVertices < 3)
                throw new InvalidOperationException("TIN cần tối thiểu 3 đỉnh hợp lệ.");
            if (!(maxX > minX) || !(maxY > minY))
                throw new InvalidOperationException("Phạm vi XY của dữ liệu không đủ để dựng TIN.");

            int desiredTiles = Math.Max(1, (int)Math.Ceiling(inputVertices / (double)TargetCoreVertices));
            double width = maxX - minX, height = maxY - minY;
            double aspect = Math.Max(0.05, Math.Min(20.0, width / Math.Max(height, 1e-12)));
            int nx = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(desiredTiles * aspect)));
            int ny = Math.Max(1, (int)Math.Ceiling(desiredTiles / (double)nx));
            double dx = width / nx, dy = height / ny;

            var buckets = new Bucket[nx * ny];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new Bucket();

            int Ix(double x)
            {
                if (x >= maxX) return nx - 1;
                int i = (int)Math.Floor((x - minX) / dx);
                return Math.Max(0, Math.Min(nx - 1, i));
            }
            int Iy(double y)
            {
                if (y >= maxY) return ny - 1;
                int i = (int)Math.Floor((y - minY) / dy);
                return Math.Max(0, Math.Min(ny - 1, i));
            }
            int Id(int ix, int iy) => iy * nx + ix;

            // Bucket hóa một lần. Không nhân bản toàn bộ đỉnh sang halo.
            foreach (var e in model.Entities)
            {
                if (!e.IsEnabled) continue;
                foreach (var v in e.ActiveVertices())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var p = v.Position;
                    buckets[Id(Ix(p.X), Iy(p.Y))].Points.Add(p);
                }

                foreach (var seg in e.ActiveBreaklineSegments())
                {
                    if (seg.Length2D <= options.XyTolerance) continue;
                    int x0 = Ix(Math.Min(seg.A.X, seg.B.X));
                    int x1 = Ix(Math.Max(seg.A.X, seg.B.X));
                    int y0 = Iy(Math.Min(seg.A.Y, seg.B.Y));
                    int y1 = Iy(Math.Max(seg.A.Y, seg.B.Y));
                    for (int ix = x0; ix <= x1; ix++)
                    for (int iy = y0; iy <= y1; iy++)
                        buckets[Id(ix, iy)].Segments.Add(seg);
                }
            }

            var allTriangles = new List<Triangle3>();
            var allIssues = new List<ValidationIssue>();
            long prepareMs = 0, triangulateMs = 0;
            int completed = 0;
            int total = nx * ny;
            var preparer = new SurfaceInputPreparer();
            var localBuilder = new ConformingTinBuilder();

            for (int iy = 0; iy < ny; iy++)
            for (int ix = 0; ix < nx; ix++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double coreMinX = minX + ix * dx;
                double coreMaxX = ix == nx - 1 ? maxX : coreMinX + dx;
                double coreMinY = minY + iy * dy;
                double coreMaxY = iy == ny - 1 ? maxY : coreMinY + dy;

                TinSurface accepted = null;
                PreparedSurfaceInput acceptedInput = null;
                List<Triangle3> acceptedCore = null;

                foreach (double haloFactor in HaloFactors)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double hx = Math.Max(dx * haloFactor, options.XyTolerance * 100.0);
                    double hy = Math.Max(dy * haloFactor, options.XyTolerance * 100.0);
                    double workMinX = coreMinX - hx, workMaxX = coreMaxX + hx;
                    double workMinY = coreMinY - hy, workMaxY = coreMaxY + hy;

                    int wx0 = Ix(Math.Max(minX, workMinX));
                    int wx1 = Ix(Math.Min(maxX, workMaxX));
                    int wy0 = Iy(Math.Max(minY, workMinY));
                    int wy1 = Iy(Math.Min(maxY, workMaxY));

                    var points = new List<Vec3>();
                    var segments = new List<Segment3>();
                    for (int bx = wx0; bx <= wx1; bx++)
                    for (int by = wy0; by <= wy1; by++)
                    {
                        var b = buckets[Id(bx, by)];
                        foreach (var p in b.Points)
                            if (p.X >= workMinX && p.X <= workMaxX &&
                                p.Y >= workMinY && p.Y <= workMaxY)
                                points.Add(p);

                        foreach (var seg in b.Segments)
                        {
                            Segment3 clipped;
                            if (TryClip(seg, workMinX, workMinY, workMaxX, workMaxY, out clipped))
                                segments.Add(clipped);
                        }
                    }

                    if (points.Count < 3)
                    {
                        acceptedCore = new List<Triangle3>();
                        break;
                    }

                    var pw = Stopwatch.StartNew();
                    var prepared = preparer.PrepareRaw(points, segments, options);
                    pw.Stop();
                    prepareMs += pw.ElapsedMilliseconds;

                    if (prepared.HasErrors)
                    {
                        var first = prepared.Issues
                            .Where(x => x.Severity == ValidationSeverity.Error)
                            .Take(10)
                            .Select(x => x.Code + ": " + x.Message);
                        throw new InvalidOperationException(
                            $"Ô TIN ({ix + 1},{iy + 1}) có lỗi dữ liệu:\r\n" +
                            string.Join("\r\n", first));
                    }

                    var tw = Stopwatch.StartNew();
                    var local = localBuilder.Build(name + $" [{ix + 1},{iy + 1}]", prepared, options);
                    tw.Stop();
                    triangulateMs += tw.ElapsedMilliseconds;

                    var core = local.Triangles
                        .Where(t => InCore(t.Centroid2D, coreMinX, coreMinY, coreMaxX, coreMaxY,
                            ix == nx - 1, iy == ny - 1))
                        .ToList();

                    bool stable = true;
                    foreach (var t in core)
                    {
                        if (!CircumcircleInside(t, workMinX, workMinY, workMaxX, workMaxY, options.XyTolerance))
                        {
                            stable = false;
                            break;
                        }
                    }

                    accepted = local;
                    acceptedInput = prepared;
                    acceptedCore = core;
                    if (stable || haloFactor == HaloFactors[HaloFactors.Length - 1])
                        break;
                }

                if (acceptedCore != null && acceptedCore.Count > 0)
                    allTriangles.AddRange(acceptedCore);

                if (acceptedInput != null)
                {
                    foreach (var issue in acceptedInput.Issues)
                    {
                        if (issue.Severity != ValidationSeverity.Info && allIssues.Count < 5000)
                            allIssues.Add(issue);
                    }
                }

                completed++;
                progress?.Invoke(completed, total,
                    $"TIN dữ liệu lớn: ô {completed:n0}/{total:n0} • tổng {allTriangles.Count:n0} tam giác");
            }

            if (allTriangles.Count == 0)
                throw new InvalidOperationException("TIN dữ liệu lớn không tạo được tam giác hợp lệ.");

            return new Result
            {
                Surface = new TinSurface(name, allTriangles, allIssues),
                InputVertexCount = inputVertices,
                InputBreaklineCount = inputBreaklines,
                TileCount = total,
                WarningCount = allIssues.Count(x => x.Severity == ValidationSeverity.Warning),
                MinZ = minZ,
                MaxZ = maxZ,
                PrepareMilliseconds = prepareMs,
                TriangulationMilliseconds = triangulateMs
            };
        }

        private static bool InCore(
            Vec2 p,
            double minX, double minY, double maxX, double maxY,
            bool includeMaxX, bool includeMaxY)
        {
            if (p.X < minX || p.Y < minY) return false;
            if (includeMaxX ? p.X > maxX : p.X >= maxX) return false;
            if (includeMaxY ? p.Y > maxY : p.Y >= maxY) return false;
            return true;
        }

        private static bool CircumcircleInside(
            Triangle3 t,
            double minX, double minY, double maxX, double maxY,
            double tol)
        {
            double ax = t.A.X, ay = t.A.Y;
            double bx = t.B.X - ax, by = t.B.Y - ay;
            double cx = t.C.X - ax, cy = t.C.Y - ay;
            double d = 2.0 * (bx * cy - by * cx);
            if (Math.Abs(d) <= 1e-24) return true;

            double b2 = bx * bx + by * by;
            double c2 = cx * cx + cy * cy;
            double ux = (cy * b2 - by * c2) / d;
            double uy = (bx * c2 - cx * b2) / d;
            double ccx = ax + ux, ccy = ay + uy;
            double r = Math.Sqrt(Math.Max(0.0, ux * ux + uy * uy)) + tol * 10.0;

            return ccx - r >= minX && ccx + r <= maxX &&
                   ccy - r >= minY && ccy + r <= maxY;
        }

        private static bool TryClip(
            Segment3 s,
            double minX, double minY, double maxX, double maxY,
            out Segment3 clipped)
        {
            double t0 = 0.0, t1 = 1.0;
            double dx = s.B.X - s.A.X, dy = s.B.Y - s.A.Y;

            if (!ClipTest(-dx, s.A.X - minX, ref t0, ref t1) ||
                !ClipTest( dx, maxX - s.A.X, ref t0, ref t1) ||
                !ClipTest(-dy, s.A.Y - minY, ref t0, ref t1) ||
                !ClipTest( dy, maxY - s.A.Y, ref t0, ref t1))
            {
                clipped = default(Segment3);
                return false;
            }

            Vec3 a = Interpolate(s.A, s.B, t0);
            Vec3 b = Interpolate(s.A, s.B, t1);
            clipped = new Segment3(a, b, s.SourceId);
            return a.XY.DistanceTo(b.XY) > 1e-12;
        }

        private static bool ClipTest(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) <= 1e-30) return q >= 0.0;
            double r = q / p;
            if (p < 0.0)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }

        private static Vec3 Interpolate(Vec3 a, Vec3 b, double t)
            => new Vec3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
    }
}
