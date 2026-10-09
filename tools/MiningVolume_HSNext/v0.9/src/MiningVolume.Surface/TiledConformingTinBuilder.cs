using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private const int DefaultTargetCoreVertices = 18000;
        private const int MillionScaleTargetCoreVertices = 12000;
        private const int MultiMillionTargetCoreVertices = 8000;
        private static readonly double[] HaloFactors = { 0.20, 0.45, 0.90, 1.60 };

        private readonly struct VertexRef
        {
            public VertexRef(SourceEntity entity, ModelVertex vertex)
            {
                Entity = entity;
                Vertex = vertex;
            }
            public SourceEntity Entity { get; }
            public ModelVertex Vertex { get; }
        }

        private readonly struct SegmentRef
        {
            public SegmentRef(int id, Segment3 segment)
            {
                Id = id;
                Segment = segment;
            }
            public int Id { get; }
            public Segment3 Segment { get; }
        }

        private sealed class Bucket
        {
            public readonly List<VertexRef> Points = new List<VertexRef>();
            public readonly List<SegmentRef> Segments = new List<SegmentRef>();
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
            public IReadOnlyList<Triangle3> PreviewTriangles { get; set; }
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

            int targetCoreVertices =
                inputVertices >= 2000000 ? MultiMillionTargetCoreVertices :
                inputVertices >= 500000 ? MillionScaleTargetCoreVertices :
                DefaultTargetCoreVertices;

            int desiredTiles = Math.Max(
                1,
                (int)Math.Ceiling(inputVertices / (double)targetCoreVertices));
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
            // Mỗi breakline segment có ID duy nhất: một segment có thể được index
            // vào nhiều bucket để truy vấn nhanh, nhưng khi dựng một tile chỉ được
            // đưa vào PrepareRaw đúng một lần.
            int segmentId = 0;
            foreach (var e in model.Entities)
            {
                if (!e.IsEnabled) continue;
                foreach (var v in e.ActiveVertices())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var p = v.Position;
                    buckets[Id(Ix(p.X), Iy(p.Y))].Points.Add(new VertexRef(e, v));
                }

                foreach (var seg in e.ActiveBreaklineSegments())
                {
                    if (seg.Length2D <= options.XyTolerance) continue;
                    var segmentRef = new SegmentRef(segmentId++, seg);
                    int x0 = Ix(Math.Min(seg.A.X, seg.B.X));
                    int x1 = Ix(Math.Max(seg.A.X, seg.B.X));
                    int y0 = Iy(Math.Min(seg.A.Y, seg.B.Y));
                    int y1 = Iy(Math.Max(seg.A.Y, seg.B.Y));
                    for (int ix = x0; ix <= x1; ix++)
                    for (int iy = y0; iy <= y1; iy++)
                        buckets[Id(ix, iy)].Segments.Add(segmentRef);
                }
            }

            var allIssues = new List<ValidationIssue>();
            var tileInfos = new List<TinTileInfo>();
            var previewTriangles = new List<Triangle3>(200000);
            long prepareMs = 0, triangulateMs = 0;
            int completed = 0;
            int total = nx * ny;
            int totalTriangles = 0;
            int previewPerTile = Math.Max(1, 200000 / Math.Max(1, total));
            var preparer = new SurfaceInputPreparer();
            var localBuilder = new ConformingTinBuilder();

            string storeRoot = Path.Combine(
                Path.GetTempPath(),
                "IMSAT_MiningVolume",
                "TinStore");
            Directory.CreateDirectory(storeRoot);
            string storePath = Path.Combine(
                storeRoot,
                "tin_" + Guid.NewGuid().ToString("N") + ".bin");

            progress?.Invoke(
                0,
                total,
                $"TIN dữ liệu lớn: 0/{total:n0} ô • {inputVertices:n0} đỉnh • " +
                $"mục tiêu ~{targetCoreVertices:n0} đỉnh/ô • đang chuẩn bị...");

            try
            {
                using (var fs = new FileStream(
                    storePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    4 << 20,
                    FileOptions.SequentialScan))
                using (var bw = new BinaryWriter(fs))
                {
                    for (int iy = 0; iy < ny; iy++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        progress?.Invoke(
                            completed,
                            total,
                            $"TIN dữ liệu lớn: ô {completed + 1:n0}/{total:n0} • đang gom dữ liệu...");

                        double coreMinX = minX + ix * dx;
                        double coreMaxX = ix == nx - 1 ? maxX : coreMinX + dx;
                        double coreMinY = minY + iy * dy;
                        double coreMaxY = iy == ny - 1 ? maxY : coreMinY + dy;

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

                            var pointRefs = new List<VertexRef>();
                            var segments = new List<Segment3>();
                            var seenSegmentIds = new HashSet<int>();
                            for (int bx = wx0; bx <= wx1; bx++)
                            for (int by = wy0; by <= wy1; by++)
                            {
                                var b = buckets[Id(bx, by)];
                                foreach (var pointRef in b.Points)
                                {
                                    var p = pointRef.Vertex.Position;
                                    if (p.X >= workMinX && p.X <= workMaxX &&
                                        p.Y >= workMinY && p.Y <= workMaxY)
                                        pointRefs.Add(pointRef);
                                }

                                foreach (var segmentRef in b.Segments)
                                {
                                    if (!seenSegmentIds.Add(segmentRef.Id)) continue;
                                    Segment3 clipped;
                                    if (TryClip(
                                        segmentRef.Segment,
                                        workMinX, workMinY, workMaxX, workMaxY,
                                        out clipped))
                                        segments.Add(clipped);
                                }
                            }

                            progress?.Invoke(
                                completed,
                                total,
                                $"TIN dữ liệu lớn: ô {completed + 1:n0}/{total:n0} • halo {haloFactor:0.00} • " +
                                $"{pointRefs.Count:n0} đỉnh • {segments.Count:n0} breakline • đang chuẩn hóa...");

                            var conflictIssues = new List<ValidationIssue>();
                            Dictionary<XYKey, List<ResolvedSite>> resolvedIndex;
                            var points = ResolveDuplicateInputPoints(
                                pointRefs,
                                options,
                                conflictIssues,
                                out resolvedIndex);

                            if (conflictIssues.Any(x => x.Severity == ValidationSeverity.Error))
                            {
                                throw new DuplicateXYConflictException(
                                    name,
                                    conflictIssues
                                        .Where(x => x.Severity == ValidationSeverity.Error)
                                        .ToList());
                            }

                            if (points.Count < 3)
                            {
                                acceptedCore = new List<Triangle3>();
                                break;
                            }

                            // Nếu một đỉnh breakline trùng đúng XY với nguồn có độ ưu tiên
                            // cao hơn (ví dụ POINT đo thực tế), chỉ snap Z của đầu mút trong
                            // bản dựng tạm. Dữ liệu CAD gốc tuyệt đối không bị sửa.
                            segments = SnapSegmentEndpoints(
                                segments,
                                resolvedIndex,
                                options.XyTolerance);

                            var pw = Stopwatch.StartNew();
                            var prepared = preparer.PrepareRaw(points, segments, options);
                            foreach (var issue in conflictIssues)
                                prepared.Issues.Add(issue);
                            pw.Stop();
                            prepareMs += pw.ElapsedMilliseconds;

                            if (prepared.HasErrors)
                            {
                                var resolvable = prepared.Issues
                                    .Where(x => x.Severity == ValidationSeverity.Error &&
                                        (string.Equals(x.Code, "DUPLICATE_XY_CONFLICT_Z", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(x.Code, "POINT_ON_BREAKLINE_Z_CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(x.Code, "BREAKLINE_CROSSING_Z_CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(x.Code, "BREAKLINE_OVERLAP_Z_CONFLICT", StringComparison.OrdinalIgnoreCase)))
                                    .ToList();

                                if (resolvable.Count > 0)
                                    throw new DuplicateXYConflictException(name, resolvable);

                                var first = prepared.Issues
                                    .Where(x => x.Severity == ValidationSeverity.Error)
                                    .Take(10)
                                    .Select(x => x.Code + ": " + x.Message);
                                throw new InvalidOperationException(
                                    $"Ô TIN ({ix + 1},{iy + 1}) có lỗi dữ liệu:\r\n" +
                                    string.Join("\r\n", first));
                            }

                            progress?.Invoke(
                                completed,
                                total,
                                $"TIN dữ liệu lớn: ô {completed + 1:n0}/{total:n0} • " +
                                $"{prepared.Sites.Count:n0} site • {prepared.Breaklines.Count:n0} breakline • đang tam giác hóa...");

                            var tw = Stopwatch.StartNew();
                            var local = localBuilder.Build(
                                name + $" [{ix + 1},{iy + 1}]",
                                prepared,
                                options,
                                message => progress?.Invoke(
                                    completed,
                                    total,
                                    $"TIN dữ liệu lớn: ô {completed + 1:n0}/{total:n0} • {message}"),
                                cancellationToken);
                            tw.Stop();
                            triangulateMs += tw.ElapsedMilliseconds;

                            var core = local.Triangles
                                .Where(t => InCore(
                                    t.Centroid2D,
                                    coreMinX, coreMinY, coreMaxX, coreMaxY,
                                    ix == nx - 1, iy == ny - 1))
                                .ToList();

                            bool stable = true;
                            foreach (var t in core)
                            {
                                if (!CircumcircleInside(
                                    t,
                                    workMinX, workMinY, workMaxX, workMaxY,
                                    options.XyTolerance))
                                {
                                    stable = false;
                                    break;
                                }
                            }

                            acceptedInput = prepared;
                            acceptedCore = core;
                            if (stable || haloFactor == HaloFactors[HaloFactors.Length - 1])
                                break;
                        }

                        if (acceptedCore != null && acceptedCore.Count > 0)
                        {
                            long offset = fs.Position;
                            foreach (var t in acceptedCore)
                                FileBackedTiledTriangleList.WriteTriangle(bw, t);

                            int tileIndex = tileInfos.Count;
                            tileInfos.Add(new TinTileInfo(
                                tileIndex,
                                offset,
                                acceptedCore.Count,
                                coreMinX, coreMinY, coreMaxX, coreMaxY));
                            totalTriangles += acceptedCore.Count;

                            // AutoCAD chỉ cần bản xem trước đủ dày để kiểm tra trực quan.
                            // TIN tính toán đầy đủ vẫn nằm trong kho tile ngoài RAM.
                            int step = Math.Max(1, (int)Math.Ceiling(
                                acceptedCore.Count / (double)previewPerTile));
                            for (int i = 0; i < acceptedCore.Count &&
                                previewTriangles.Count < 200000; i += step)
                                previewTriangles.Add(acceptedCore[i]);
                        }

                        if (acceptedInput != null)
                        {
                            foreach (var issue in acceptedInput.Issues)
                            {
                                if (issue.Severity != ValidationSeverity.Info && allIssues.Count < 5000)
                                    allIssues.Add(issue);
                            }
                        }

                        completed++;
                        progress?.Invoke(
                            completed,
                            total,
                            $"TIN dữ liệu lớn: ô {completed:n0}/{total:n0} • tổng {totalTriangles:n0} tam giác • lưu ngoài RAM");
                    }
                    bw.Flush();
                    fs.Flush(true);
                }

                if (totalTriangles == 0)
                    throw new InvalidOperationException("TIN dữ liệu lớn không tạo được tam giác hợp lệ.");

                var fileBacked = new FileBackedTiledTriangleList(storePath, tileInfos, cacheTiles: 6);
                storePath = null; // Quyền sở hữu file đã chuyển sang fileBacked.

                return new Result
                {
                    Surface = new TinSurface(name, fileBacked, allIssues),
                    InputVertexCount = inputVertices,
                    InputBreaklineCount = inputBreaklines,
                    TileCount = tileInfos.Count,
                    WarningCount = allIssues.Count(x => x.Severity == ValidationSeverity.Warning),
                    MinZ = minZ,
                    MaxZ = maxZ,
                    PrepareMilliseconds = prepareMs,
                    TriangulationMilliseconds = triangulateMs,
                    PreviewTriangles = previewTriangles
                };
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(storePath))
                {
                    try { File.Delete(storePath); } catch { }
                }
            }
        }

        private readonly struct XYKey : IEquatable<XYKey>
        {
            public XYKey(long x, long y) { X = x; Y = y; }
            public long X { get; }
            public long Y { get; }
            public bool Equals(XYKey other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is XYKey && Equals((XYKey)obj);
            public override int GetHashCode()
            {
                unchecked { return (X.GetHashCode() * 397) ^ Y.GetHashCode(); }
            }
        }

        private sealed class ResolvedSite
        {
            public Vec3 Position;
            public VertexRef Winner;
        }

        private static List<Vec3> ResolveDuplicateInputPoints(
            IReadOnlyList<VertexRef> source,
            SurfaceBuildOptions options,
            List<ValidationIssue> issues,
            out Dictionary<XYKey, List<ResolvedSite>> index)
        {
            // 5 cm chỉ dùng để nhận diện sai khác rất nhỏ tại cùng XY; không trung
            // bình hay tạo cao độ mới. Luôn giữ Z của nguồn ưu tiên cao hơn.
            const double minorZTolerance = 0.05;
            double xyTol = Math.Max(options.XyTolerance, 1e-9);
            index = new Dictionary<XYKey, List<ResolvedSite>>();
            var ordered = new List<ResolvedSite>();

            foreach (var item in source)
            {
                var p = item.Vertex.Position;
                long ix = (long)Math.Floor(p.X / xyTol);
                long iy = (long)Math.Floor(p.Y / xyTol);

                ResolvedSite match = null;
                double bestD2 = double.PositiveInfinity;

                for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                {
                    var key = new XYKey(ix + dx, iy + dy);
                    List<ResolvedSite> candidates;
                    if (!index.TryGetValue(key, out candidates)) continue;
                    foreach (var candidate in candidates)
                    {
                        double px = candidate.Position.X - p.X;
                        double py = candidate.Position.Y - p.Y;
                        double d2 = px * px + py * py;
                        if (d2 <= xyTol * xyTol && d2 < bestD2)
                        {
                            match = candidate;
                            bestD2 = d2;
                        }
                    }
                }

                if (match == null)
                {
                    var key = new XYKey(ix, iy);
                    var site = new ResolvedSite { Position = p, Winner = item };
                    List<ResolvedSite> cell;
                    if (!index.TryGetValue(key, out cell))
                    {
                        cell = new List<ResolvedSite>();
                        index[key] = cell;
                    }
                    cell.Add(site);
                    ordered.Add(site);
                    continue;
                }

                double oldZ = match.Position.Z;
                double dz = Math.Abs(oldZ - p.Z);
                if (dz <= options.ZConflictTolerance)
                    continue;

                int oldPriority = SourcePriority(match.Winner.Entity.Type);
                int newPriority = SourcePriority(item.Entity.Type);

                if (dz <= minorZTolerance || oldPriority != newPriority)
                {
                    bool replace = newPriority > oldPriority;
                    if (replace)
                    {
                        match.Position = new Vec3(match.Position.X, match.Position.Y, p.Z);
                        match.Winner = item;
                    }

                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning,
                        dz <= minorZTolerance
                            ? "AUTO_RESOLVE_DUPLICATE_XY_MINOR"
                            : "AUTO_RESOLVE_DUPLICATE_XY_BY_PRIORITY",
                        $"Trùng XY ({p.X:0.###}, {p.Y:0.###}) có Z={oldZ:0.###} / {p.Z:0.###}. " +
                        $"Giữ nguồn ưu tiên {TypeLabel(match.Winner.Entity.Type)} " +
                        $"(handle {match.Winner.Entity.Handle}); dữ liệu CAD gốc không bị sửa.",
                        match.Winner.Entity.Id));
                    continue;
                }

                if (options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseUpper ||
                    options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseLower)
                {
                    bool useUpper = options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseUpper;
                    bool chooseNew = useUpper ? p.Z > oldZ : p.Z < oldZ;
                    double chosenZ = useUpper ? Math.Max(oldZ, p.Z) : Math.Min(oldZ, p.Z);
                    if (chooseNew) match.Winner = item;
                    match.Position = new Vec3(match.Position.X, match.Position.Y, chosenZ);

                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning,
                        useUpper ? "USER_RESOLVE_DUPLICATE_XY_UPPER" : "USER_RESOLVE_DUPLICATE_XY_LOWER",
                        $"XY ({p.X:0.###}, {p.Y:0.###}) có hai Z mâu thuẫn {oldZ:0.###} và {p.Z:0.###}; " +
                        $"theo lựa chọn người dùng, dùng đỉnh {(useUpper ? "trên" : "dưới")} Z={chosenZ:0.###}. " +
                        $"Handles: {match.Winner.Entity.Handle} / {item.Entity.Handle}. Dữ liệu CAD gốc không bị sửa.",
                        match.Winner.Entity.Id));
                    continue;
                }

                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "DUPLICATE_XY_CONFLICT_Z",
                    $"XY ({p.X:0.###}, {p.Y:0.###}) có hai Z mâu thuẫn " +
                    $"{oldZ:0.###} và {p.Z:0.###}; cùng mức ưu tiên " +
                    $"{TypeLabel(item.Entity.Type)}. Handles: " +
                    $"{match.Winner.Entity.Handle} / {item.Entity.Handle}.",
                    item.Entity.Id));
            }

            return ordered.Select(x => x.Position).ToList();
        }

        private static List<Segment3> SnapSegmentEndpoints(
            IReadOnlyList<Segment3> source,
            Dictionary<XYKey, List<ResolvedSite>> resolved,
            double xyTolerance)
        {
            var output = new List<Segment3>(source.Count);
            foreach (var s in source)
            {
                Vec3 a = SnapPoint(s.A, resolved, xyTolerance);
                Vec3 b = SnapPoint(s.B, resolved, xyTolerance);
                if (a.XY.DistanceTo(b.XY) > xyTolerance)
                    output.Add(new Segment3(a, b, s.SourceId));
            }
            return output;
        }

        private static Vec3 SnapPoint(
            Vec3 p,
            Dictionary<XYKey, List<ResolvedSite>> resolved,
            double xyTolerance)
        {
            double tol = Math.Max(xyTolerance, 1e-9);
            long ix = (long)Math.Floor(p.X / tol);
            long iy = (long)Math.Floor(p.Y / tol);
            for (long dx = -1; dx <= 1; dx++)
            for (long dy = -1; dy <= 1; dy++)
            {
                List<ResolvedSite> sites;
                if (!resolved.TryGetValue(new XYKey(ix + dx, iy + dy), out sites))
                    continue;
                foreach (var site in sites)
                    if (site.Position.XY.DistanceTo(p.XY) <= tol)
                        return new Vec3(p.X, p.Y, site.Position.Z);
            }
            return p;
        }

        private static int SourcePriority(SourceEntityType type)
        {
            switch (type)
            {
                case SourceEntityType.Point: return 600;       // điểm đo thực tế
                case SourceEntityType.Polyline3d: return 500;  // breakline 3D
                case SourceEntityType.Contour: return 400;     // đường đồng mức
                case SourceEntityType.LwPolyline: return 300;
                case SourceEntityType.Polyline2d: return 250;
                case SourceEntityType.Line: return 200;
                default: return 100;
            }
        }

        private static string TypeLabel(SourceEntityType type)
        {
            switch (type)
            {
                case SourceEntityType.Point: return "POINT";
                case SourceEntityType.Polyline3d: return "3D POLYLINE";
                case SourceEntityType.Contour: return "đường đồng mức";
                case SourceEntityType.LwPolyline: return "LWPOLYLINE";
                case SourceEntityType.Polyline2d: return "2D POLYLINE";
                case SourceEntityType.Line: return "LINE";
                default: return type.ToString();
            }
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
