using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;

namespace MiningVolume.Core.Surface
{
    /// <summary>
    /// Chuẩn hóa dữ liệu trước khi dựng TIN.
    ///
    /// Quy tắc v0.10.4:
    /// - trùng XY khác Z: lỗi thật, không tự đoán;
    /// - điểm nằm trên breakline: tự chia đoạn nếu Z phù hợp;
    /// - hai breakline cắt nhau: tự tạo nút nếu Z nội suy hai tuyến trùng nhau;
    /// - hai breakline chồng lấn: tự chia tại các đầu mút và loại đoạn trùng nếu Z phù hợp;
    /// - chỉ chặn khi hình học cùng XY nhưng cao độ mâu thuẫn.
    ///
    /// Như vậy dữ liệu CAD thực tế có linework trùng/cắt nhưng cùng bề mặt không còn
    /// bắt người dùng sửa thủ công từng đoạn trước khi dựng TIN.
    /// </summary>
    public sealed class SurfaceInputPreparer
    {
        public PreparedSurfaceInput Prepare(SurfaceModel model, SurfaceBuildOptions options,
            Action<string> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            options = options ?? new SurfaceBuildOptions();
            if (options.XyTolerance <= 0) throw new ArgumentOutOfRangeException(nameof(options.XyTolerance));

            var result = new PreparedSurfaceInput();
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke("Đang đọc điểm và breakline nguồn...");
            var active = model.Entities.Where(e => e.IsEnabled);

            foreach (var e in active)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var v in e.ActiveVertices()) { cancellationToken.ThrowIfCancellationRequested(); result.Sites.Add(v.Position); }
                foreach (var s in e.ActiveBreaklineSegments())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (s.Length2D <= options.XyTolerance)
                    {
                        result.Issues.Add(new ValidationIssue(
                            ValidationSeverity.Warning,
                            "ZERO_LENGTH_BREAKLINE",
                            "Bỏ qua đoạn breakline có chiều dài XY gần bằng 0.",
                            s.SourceId));
                    }
                    else result.Breaklines.Add(s);
                }
            }

            return NormalizePreparedInput(result, options, progress, cancellationToken);
        }

        /// <summary>
        /// Chuẩn hóa dữ liệu thô đã được phân vùng trước. Đường này dùng cho
        /// large-dataset/tiled TIN để không phải dựng hàng triệu SourceEntity tạm.
        /// </summary>
        public PreparedSurfaceInput PrepareRaw(
            IEnumerable<Vec3> sites,
            IEnumerable<Segment3> breaklines,
            SurfaceBuildOptions options,
            Action<string> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            options = options ?? new SurfaceBuildOptions();
            if (options.XyTolerance <= 0) throw new ArgumentOutOfRangeException(nameof(options.XyTolerance));

            var result = new PreparedSurfaceInput();
            if (sites != null) foreach (var p in sites) { cancellationToken.ThrowIfCancellationRequested(); result.Sites.Add(p); }

            if (breaklines != null)
            {
                foreach (var s in breaklines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (s.Length2D <= options.XyTolerance)
                    {
                        result.Issues.Add(new ValidationIssue(
                            ValidationSeverity.Warning,
                            "ZERO_LENGTH_BREAKLINE",
                            "Bỏ qua đoạn breakline có chiều dài XY gần bằng 0.",
                            s.SourceId));
                    }
                    else result.Breaklines.Add(s);
                }
            }

            return NormalizePreparedInput(result, options, progress, cancellationToken);
        }

        /// <summary>
        /// Đường chuẩn hóa dành riêng cho TIN phân ô. TiledConformingTinBuilder đã
        /// giải quyết trùng XY trước khi gọi vào đây, vì vậy không lặp lại vòng
        /// ResolveDuplicateSites đầu tiên. Sau khi NormalizeBreaklineTopology chạy,
        /// không quét lại toàn bộ cặp breakline lần thứ hai; nếu còn topology suy
        /// biến, ConformingTinBuilder sẽ báo đúng ràng buộc không khôi phục được.
        /// Điều này loại hai lượt quét lớn trên mỗi halo của dữ liệu mỏ dày.
        /// </summary>
        public PreparedSurfaceInput PrepareRawForTiledTin(
            IEnumerable<Vec3> uniqueSites,
            IEnumerable<Segment3> breaklines,
            SurfaceBuildOptions options,
            Action<string> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            options = options ?? new SurfaceBuildOptions();
            if (options.XyTolerance <= 0) throw new ArgumentOutOfRangeException(nameof(options.XyTolerance));

            var result = new PreparedSurfaceInput();
            if (uniqueSites != null) foreach (var p in uniqueSites) { cancellationToken.ThrowIfCancellationRequested(); result.Sites.Add(p); }
            if (breaklines != null)
            {
                foreach (var seg in breaklines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (seg.Length2D <= options.XyTolerance)
                    {
                        result.Issues.Add(new ValidationIssue(
                            ValidationSeverity.Warning,
                            "ZERO_LENGTH_BREAKLINE",
                            "Bỏ qua đoạn breakline có chiều dài XY gần bằng 0.",
                            seg.SourceId));
                    }
                    else result.Breaklines.Add(seg);
                }
            }

            NormalizeBreaklineTopology(result, options, progress, cancellationToken);
            ResolveDuplicateSites(result, options, progress, cancellationToken);

            if (result.Sites.Count < 3)
                result.Issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "TOO_FEW_POINTS",
                    "Mô hình cần ít nhất 3 điểm XY hợp lệ để dựng TIN."));

            return result;
        }

        private static PreparedSurfaceInput NormalizePreparedInput(
            PreparedSurfaceInput result,
            SurfaceBuildOptions options, Action<string> progress, CancellationToken cancellationToken)
        {
            ResolveDuplicateSites(result, options, progress, cancellationToken);
            // Conflicting elevations require a user decision before expensive topology work.
            if (result.HasErrors) return result;
            NormalizeBreaklineTopology(result, options, progress, cancellationToken);
            ResolveDuplicateSites(result, options, progress, cancellationToken);
            ValidateNormalizedTopology(result, options, progress, cancellationToken);

            if (result.Sites.Count < 3)
                result.Issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "TOO_FEW_POINTS",
                    "Mô hình cần ít nhất 3 điểm XY hợp lệ để dựng TIN."));

            return result;
        }

        private sealed class SplitMark
        {
            public SplitMark(double t, Vec3 p) { T = t; Point = p; }
            public double T { get; }
            public Vec3 Point { get; set; }
        }

        private static void NormalizeBreaklineTopology(PreparedSurfaceInput result, SurfaceBuildOptions options, Action<string> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke("Đang chuẩn hóa giao điểm và breakline chồng lấn...");
            if (result.Breaklines.Count == 0) return;

            var source = result.Breaklines.ToList();
            var marks = new List<List<SplitMark>>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                marks.Add(new List<SplitMark>
                {
                    new SplitMark(0.0, source[i].A),
                    new SplitMark(1.0, source[i].B)
                });
            }

            // 1) Split a breakline at every already-existing site lying on its interior.
            var pointIndex = new PointGridIndex(result.Sites, options.XyTolerance, cancellationToken);
            for (int i = 0; i < source.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var seg = source[i];
                foreach (int siteIndex in pointIndex.Query(
                    seg.A.X, seg.A.Y, seg.B.X, seg.B.Y, options.XyTolerance))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var site = result.Sites[siteIndex];
                    if (!Geometry2D.PointOnSegment(site.XY, seg.A.XY, seg.B.XY, options.XyTolerance))
                        continue;

                    double t = ParameterOnSegment(site.XY, seg);
                    if (t <= ParamTolerance(seg, options.XyTolerance) ||
                        t >= 1.0 - ParamTolerance(seg, options.XyTolerance))
                        continue;

                    double expected = Geometry2D.InterpolateZOnSegment(site.XY, seg);
                    if (Math.Abs(expected - site.Z) > options.ZConflictTolerance)
                    {
                        if (options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseUpper ||
                            options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseLower)
                        {
                            bool useUpper = options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseUpper;
                            double chosenZ = useUpper ? Math.Max(site.Z, expected) : Math.Min(site.Z, expected);
                            var chosen = new Vec3(site.X, site.Y, chosenZ);

                            // Chỉ thay trong dữ liệu chuẩn hóa của lần dựng TIN hiện tại.
                            // Không sửa POINT hay breakline nguồn trong bản vẽ CAD.
                            result.Sites[siteIndex] = chosen;
                            AddMark(marks[i], t, chosen, options.XyTolerance);
                            result.Issues.Add(new ValidationIssue(
                                ValidationSeverity.Warning,
                                useUpper
                                    ? "USER_RESOLVE_POINT_ON_BREAKLINE_UPPER"
                                    : "USER_RESOLVE_POINT_ON_BREAKLINE_LOWER",
                                $"Điểm tại ({site.X:0.###}, {site.Y:0.###}) có Z={site.Z:0.###}, " +
                                $"breakline {seg.SourceId} nội suy Z={expected:0.###}; theo lựa chọn người dùng, " +
                                $"dùng đỉnh {(useUpper ? "trên" : "dưới")} Z={chosenZ:0.###}. " +
                                "Dữ liệu CAD gốc không bị sửa.",
                                seg.SourceId));
                            continue;
                        }

                        result.Issues.Add(new ValidationIssue(
                            ValidationSeverity.Error,
                            "POINT_ON_BREAKLINE_Z_CONFLICT",
                            $"Điểm tại ({site.X:0.###}, {site.Y:0.###}) nằm trên breakline {seg.SourceId} " +
                            $"nhưng Z={site.Z:0.###} khác Z nội suy {expected:0.###}.",
                            seg.SourceId));
                        continue;
                    }

                    AddMark(marks[i], t, new Vec3(site.X, site.Y, (site.Z + expected) * 0.5),
                        options.XyTolerance);
                }
            }

            // 2) Normalize crossings/overlaps pairwise using the spatial index.
            var segIndex = new SegmentGridIndex(source, options.XyTolerance, cancellationToken);
            var generatedSites = new List<Vec3>();
            int autoCrossings = 0;
            int overlapPairs = 0;

            for (int i = 0; i < source.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (int j in segIndex.Query(i))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (j <= i) continue;

                    var a = source[i];
                    var b = source[j];

                    if (Geometry2D.ProperIntersection(
                        a.A.XY, a.B.XY, b.A.XY, b.B.XY, options.XyTolerance))
                    {
                        Vec2 xy;
                        if (!TryLineIntersection(a.A.XY, a.B.XY, b.A.XY, b.B.XY, out xy))
                            continue;

                        double za = Geometry2D.InterpolateZOnSegment(xy, a);
                        double zb = Geometry2D.InterpolateZOnSegment(xy, b);
                        if (Math.Abs(za - zb) > options.ZConflictTolerance)
                        {
                            if (options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseUpper ||
                                options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseLower)
                            {
                                bool useUpper = options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseUpper;
                                double chosenZ = useUpper ? Math.Max(za, zb) : Math.Min(za, zb);
                                var chosen = new Vec3(xy.X, xy.Y, chosenZ);
                                AddMark(marks[i], ParameterOnSegment(xy, a), chosen, options.XyTolerance);
                                AddMark(marks[j], ParameterOnSegment(xy, b), chosen, options.XyTolerance);
                                generatedSites.Add(chosen);
                                result.Issues.Add(new ValidationIssue(
                                    ValidationSeverity.Warning,
                                    useUpper
                                        ? "USER_RESOLVE_BREAKLINE_CROSSING_UPPER"
                                        : "USER_RESOLVE_BREAKLINE_CROSSING_LOWER",
                                    $"Hai breakline {a.SourceId} / {b.SourceId} cắt tại ({xy.X:0.###}, {xy.Y:0.###}) " +
                                    $"có Z1={za:0.###}, Z2={zb:0.###}; theo lựa chọn người dùng, dùng đỉnh " +
                                    $"{(useUpper ? "trên" : "dưới")} Z={chosenZ:0.###}. Dữ liệu CAD gốc không bị sửa."));
                                autoCrossings++;
                                continue;
                            }

                            result.Issues.Add(new ValidationIssue(
                                ValidationSeverity.Error,
                                "BREAKLINE_CROSSING_Z_CONFLICT",
                                $"Hai breakline {a.SourceId} / {b.SourceId} cắt tại " +
                                $"({xy.X:0.###}, {xy.Y:0.###}) nhưng cao độ nội suy mâu thuẫn " +
                                $"Z1={za:0.###}, Z2={zb:0.###}. Đây là xung đột bề mặt thật cần kiểm tra dữ liệu."));
                            continue;
                        }

                        var p = new Vec3(xy.X, xy.Y, (za + zb) * 0.5);
                        AddMark(marks[i], ParameterOnSegment(xy, a), p, options.XyTolerance);
                        AddMark(marks[j], ParameterOnSegment(xy, b), p, options.XyTolerance);
                        generatedSites.Add(p);
                        autoCrossings++;
                        continue;
                    }

                    if (!CollinearOverlap(a, b, options.XyTolerance))
                        continue;

                    overlapPairs++;
                    var candidates = new[] { a.A, a.B, b.A, b.B };
                    foreach (var q in candidates)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!Geometry2D.PointOnSegment(q.XY, a.A.XY, a.B.XY, options.XyTolerance) ||
                            !Geometry2D.PointOnSegment(q.XY, b.A.XY, b.B.XY, options.XyTolerance))
                            continue;

                        double za = Geometry2D.InterpolateZOnSegment(q.XY, a);
                        double zb = Geometry2D.InterpolateZOnSegment(q.XY, b);
                        if (Math.Abs(za - zb) > options.ZConflictTolerance)
                        {
                            if (options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseUpper ||
                                options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseLower)
                            {
                                bool useUpper = options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseUpper;
                                double chosenZ = useUpper ? Math.Max(za, zb) : Math.Min(za, zb);
                                var chosen = new Vec3(q.X, q.Y, chosenZ);
                                AddMark(marks[i], ParameterOnSegment(q.XY, a), chosen, options.XyTolerance);
                                AddMark(marks[j], ParameterOnSegment(q.XY, b), chosen, options.XyTolerance);
                                generatedSites.Add(chosen);
                                result.Issues.Add(new ValidationIssue(
                                    ValidationSeverity.Warning,
                                    useUpper
                                        ? "USER_RESOLVE_BREAKLINE_OVERLAP_UPPER"
                                        : "USER_RESOLVE_BREAKLINE_OVERLAP_LOWER",
                                    $"Hai breakline chồng lấn {a.SourceId} / {b.SourceId} tại " +
                                    $"({q.X:0.###}, {q.Y:0.###}) có Z1={za:0.###}, Z2={zb:0.###}; " +
                                    $"theo lựa chọn người dùng, dùng đỉnh {(useUpper ? "trên" : "dưới")} " +
                                    $"Z={chosenZ:0.###}. Dữ liệu CAD gốc không bị sửa."));
                                continue;
                            }

                            result.Issues.Add(new ValidationIssue(
                                ValidationSeverity.Error,
                                "BREAKLINE_OVERLAP_Z_CONFLICT",
                                $"Hai breakline chồng lấn {a.SourceId} / {b.SourceId} nhưng tại " +
                                $"({q.X:0.###}, {q.Y:0.###}) có Z1={za:0.###}, Z2={zb:0.###}."));
                            continue;
                        }

                        var p = new Vec3(q.X, q.Y, (za + zb) * 0.5);
                        AddMark(marks[i], ParameterOnSegment(q.XY, a), p, options.XyTolerance);
                        AddMark(marks[j], ParameterOnSegment(q.XY, b), p, options.XyTolerance);
                        generatedSites.Add(p);
                    }
                }
            }

            if (autoCrossings > 0)
                result.Issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "AUTO_NODE_BREAKLINE_CROSSINGS",
                    $"Đã tự tạo {autoCrossings:n0} nút tại giao điểm breakline có cao độ phù hợp."));

            if (overlapPairs > 0)
                result.Issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "AUTO_NORMALIZE_BREAKLINE_OVERLAPS",
                    $"Đã tự chuẩn hóa {overlapPairs:n0} cặp breakline chồng lấn có cao độ phù hợp."));

            // Resolve the complete set of nodes before rebuilding/deduplicating edges.
            // Pairwise split marks can otherwise retain an earlier/lower Z even after
            // the user selected Upper (or vice versa), especially at shared endpoints.
            result.Sites.AddRange(generatedSites);
            if (options.DuplicateXYConflictPolicy != DuplicateXYConflictPolicy.Stop)
                ResolveDuplicateSites(result, options, progress, cancellationToken, marks);

            // 3) Rebuild split segments.
            var rebuilt = new List<Segment3>();
            for (int i = 0; i < source.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var seg = source[i];
                var ordered = marks[i]
                    .Where(x => x.T >= -1e-10 && x.T <= 1.0 + 1e-10)
                    .OrderBy(x => x.T)
                    .ToList();

                var unique = new List<SplitMark>();
                foreach (var m in ordered)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (unique.Count == 0)
                    {
                        unique.Add(m);
                        continue;
                    }

                    var last = unique[unique.Count - 1];
                    if (Math.Abs(last.T - m.T) <= ParamTolerance(seg, options.XyTolerance) ||
                        last.Point.XY.DistanceTo(m.Point.XY) <= options.XyTolerance)
                    {
                        if (Math.Abs(last.Point.Z - m.Point.Z) > options.ZConflictTolerance)
                        {
                            result.Issues.Add(new ValidationIssue(
                                ValidationSeverity.Error,
                                "BREAKLINE_NODE_Z_CONFLICT",
                                $"Breakline {seg.SourceId} có hai nút gần trùng XY nhưng khác Z."));
                        }
                        continue;
                    }
                    unique.Add(m);
                }

                for (int k = 0; k < unique.Count - 1; k++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var p0 = unique[k].Point;
                    var p1 = unique[k + 1].Point;
                    if (p0.XY.DistanceTo(p1.XY) <= options.XyTolerance) continue;
                    rebuilt.Add(new Segment3(p0, p1, seg.SourceId));
                }
            }

            // 4) Remove exact/near-exact duplicate subsegments created by overlaps.
            int duplicateSegments = 0;
            var dedup = new List<Segment3>();
            var buckets = new Dictionary<string, List<int>>();
            foreach (var s in rebuilt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string key = SegmentBucketKey(s, options.XyTolerance);
                List<int> ids;
                if (!buckets.TryGetValue(key, out ids))
                {
                    ids = new List<int>();
                    buckets[key] = ids;
                }

                int match = -1;
                foreach (int id in ids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (SameUndirectedXY(dedup[id], s, options.XyTolerance))
                    {
                        match = id;
                        break;
                    }
                }

                if (match < 0)
                {
                    ids.Add(dedup.Count);
                    dedup.Add(s);
                    continue;
                }

                var old = dedup[match];
                if (!SameUndirectedXYZ(old, s, options.XyTolerance, options.ZConflictTolerance))
                {
                    result.Issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "BREAKLINE_DUPLICATE_Z_CONFLICT",
                        $"Hai đoạn breakline trùng XY ({old.SourceId} / {s.SourceId}) nhưng khác cao độ."));
                    continue;
                }

                duplicateSegments++;
            }

            if (duplicateSegments > 0)
                result.Issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "AUTO_REMOVE_DUPLICATE_BREAKLINES",
                    $"Đã tự loại {duplicateSegments:n0} đoạn breakline trùng nhau sau khi chuẩn hóa."));

            result.Breaklines.Clear();
            result.Breaklines.AddRange(dedup);


            foreach (var s in dedup)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Sites.Add(s.A);
                result.Sites.Add(s.B);
            }
        }

        private static void ValidateNormalizedTopology(PreparedSurfaceInput result, SurfaceBuildOptions options, Action<string> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke("Đang kiểm tra topology sau chuẩn hóa...");
            int count = result.Breaklines.Count;
            if (count < 2) return;

            var index = new SegmentGridIndex(result.Breaklines, options.XyTolerance, cancellationToken);
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var a = result.Breaklines[i];
                foreach (int j in index.Query(i))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (j <= i) continue;
                    var b = result.Breaklines[j];

                    if (Geometry2D.ProperIntersection(
                        a.A.XY, a.B.XY, b.A.XY, b.B.XY, options.XyTolerance))
                    {
                        result.Issues.Add(new ValidationIssue(
                            ValidationSeverity.Error,
                            "BREAKLINE_NORMALIZE_CROSSING_FAILED",
                            $"Sau chuẩn hóa vẫn còn breakline cắt nhau ({a.SourceId} / {b.SourceId})."));
                    }
                    else if (CollinearOverlap(a, b, options.XyTolerance))
                    {
                        result.Issues.Add(new ValidationIssue(
                            ValidationSeverity.Error,
                            "BREAKLINE_NORMALIZE_OVERLAP_FAILED",
                            $"Sau chuẩn hóa vẫn còn breakline chồng lấn ({a.SourceId} / {b.SourceId})."));
                    }
                }
            }
        }

        private static void ResolveDuplicateSites(PreparedSurfaceInput result, SurfaceBuildOptions options, Action<string> progress, CancellationToken cancellationToken,
            List<List<SplitMark>> nodes = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Invoke("Đang kiểm tra điểm trùng XY và cao độ...");
            if (nodes != null)
                foreach (var segmentNodes in nodes)
                    foreach (var node in segmentNodes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        result.Sites.Add(node.Point);
                    }
            var unique = new List<Vec3>();
            var grid = new Dictionary<string, List<int>>();

            foreach (var p in result.Sites)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long ix = (long)Math.Floor(p.X / options.XyTolerance);
                long iy = (long)Math.Floor(p.Y / options.XyTolerance);
                int match = -1;

                for (long dx = -1; dx <= 1 && match < 0; dx++)
                for (long dy = -1; dy <= 1 && match < 0; dy++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string k = (ix + dx) + ":" + (iy + dy);
                    List<int> ids;
                    if (!grid.TryGetValue(k, out ids)) continue;
                    foreach (int id in ids)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (unique[id].XY.DistanceTo(p.XY) <= options.XyTolerance)
                        {
                            match = id;
                            break;
                        }
                    }
                }

                if (match < 0)
                {
                    string key = ix + ":" + iy;
                    List<int> ids;
                    if (!grid.TryGetValue(key, out ids))
                    {
                        ids = new List<int>();
                        grid[key] = ids;
                    }
                    ids.Add(unique.Count);
                    unique.Add(p);
                }
                else
                {
                    var old = unique[match];
                    if (Math.Abs(old.Z - p.Z) > options.ZConflictTolerance)
                    {
                        if (options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseUpper)
                        {
                            double chosen = Math.Max(old.Z, p.Z);
                            unique[match] = new Vec3(old.X, old.Y, chosen);
                            result.Issues.Add(new ValidationIssue(
                                ValidationSeverity.Warning,
                                "USER_RESOLVE_DUPLICATE_XY_UPPER",
                                $"Hai điểm trùng XY ({p.X:0.###}, {p.Y:0.###}) có Z={old.Z:0.###} và Z={p.Z:0.###}; " +
                                $"theo lựa chọn người dùng, dùng đỉnh trên Z={chosen:0.###}."));
                        }
                        else if (options.DuplicateXYConflictPolicy == DuplicateXYConflictPolicy.UseLower)
                        {
                            double chosen = Math.Min(old.Z, p.Z);
                            unique[match] = new Vec3(old.X, old.Y, chosen);
                            result.Issues.Add(new ValidationIssue(
                                ValidationSeverity.Warning,
                                "USER_RESOLVE_DUPLICATE_XY_LOWER",
                                $"Hai điểm trùng XY ({p.X:0.###}, {p.Y:0.###}) có Z={old.Z:0.###} và Z={p.Z:0.###}; " +
                                $"theo lựa chọn người dùng, dùng đỉnh dưới Z={chosen:0.###}."));
                        }
                        else
                        {
                            result.Issues.Add(new ValidationIssue(
                                ValidationSeverity.Error,
                                "DUPLICATE_XY_CONFLICT_Z",
                                $"Hai điểm trùng XY nhưng khác Z: ({p.X:0.###}, {p.Y:0.###}) " +
                                $"có Z={old.Z:0.###} và Z={p.Z:0.###}."));
                        }
                    }
                }
            }

            if (nodes != null)
                foreach (var segmentNodes in nodes)
                    foreach (var node in segmentNodes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        node.Point = FindCanonicalNode(node.Point, unique, grid, options.XyTolerance);
                    }
            result.Sites.Clear();
            result.Sites.AddRange(unique);
        }

        private static Vec3 FindCanonicalNode(Vec3 p, IReadOnlyList<Vec3> sites,
            Dictionary<string, List<int>> grid, double tolerance)
        {
            long ix = (long)Math.Floor(p.X / tolerance);
            long iy = (long)Math.Floor(p.Y / tolerance);
            // Use the same neighbourhood and match order as ResolveDuplicateSites.
            for (long dx = -1; dx <= 1; dx++)
            for (long dy = -1; dy <= 1; dy++)
            {
                List<int> ids;
                if (!grid.TryGetValue((ix + dx) + ":" + (iy + dy), out ids)) continue;
                foreach (int id in ids)
                    if (sites[id].XY.DistanceTo(p.XY) <= tolerance) return sites[id];
            }
            throw new InvalidOperationException("Không ánh xạ được nút breakline đã chuẩn hóa.");
        }

        private static void AddMark(List<SplitMark> marks, double t, Vec3 p, double xyTol)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            foreach (var m in marks)
                if (Math.Abs(m.T - t) <= 1e-12 || m.Point.XY.DistanceTo(p.XY) <= xyTol)
                    return;
            marks.Add(new SplitMark(t, p));
        }

        private static double ParameterOnSegment(Vec2 p, Segment3 s)
        {
            double dx = s.B.X - s.A.X;
            double dy = s.B.Y - s.A.Y;
            double den = dx * dx + dy * dy;
            if (den <= 1e-30) return 0.0;
            return ((p.X - s.A.X) * dx + (p.Y - s.A.Y) * dy) / den;
        }

        private static double ParamTolerance(Segment3 s, double xyTol)
        {
            return Math.Min(1e-6, xyTol / Math.Max(s.Length2D, xyTol));
        }

        private static bool TryLineIntersection(Vec2 a, Vec2 b, Vec2 c, Vec2 d, out Vec2 p)
        {
            double rX = b.X - a.X, rY = b.Y - a.Y;
            double sX = d.X - c.X, sY = d.Y - c.Y;
            double den = rX * sY - rY * sX;
            if (Math.Abs(den) <= 1e-24)
            {
                p = default(Vec2);
                return false;
            }

            double qX = c.X - a.X, qY = c.Y - a.Y;
            double t = (qX * sY - qY * sX) / den;
            p = new Vec2(a.X + t * rX, a.Y + t * rY);
            return true;
        }

        private static bool CollinearOverlap(Segment3 a, Segment3 b, double tol)
        {
            double la = a.Length2D;
            if (la <= tol || b.Length2D <= tol) return false;
            if (Math.Abs(Vec2.Cross(a.A.XY, a.B.XY, b.A.XY)) / la > tol) return false;
            if (Math.Abs(Vec2.Cross(a.A.XY, a.B.XY, b.B.XY)) / la > tol) return false;

            bool useX = Math.Abs(a.B.X - a.A.X) >= Math.Abs(a.B.Y - a.A.Y);
            double a0 = useX ? a.A.X : a.A.Y;
            double a1 = useX ? a.B.X : a.B.Y;
            double b0 = useX ? b.A.X : b.A.Y;
            double b1 = useX ? b.B.X : b.B.Y;
            if (a0 > a1) { double t = a0; a0 = a1; a1 = t; }
            if (b0 > b1) { double t = b0; b0 = b1; b1 = t; }
            return Math.Min(a1, b1) - Math.Max(a0, b0) > tol;
        }

        private static bool SameUndirectedXY(Segment3 a, Segment3 b, double tol)
        {
            return (Geometry2D.AlmostSame(a.A.XY, b.A.XY, tol) &&
                    Geometry2D.AlmostSame(a.B.XY, b.B.XY, tol)) ||
                   (Geometry2D.AlmostSame(a.A.XY, b.B.XY, tol) &&
                    Geometry2D.AlmostSame(a.B.XY, b.A.XY, tol));
        }

        private static bool SameUndirectedXYZ(Segment3 a, Segment3 b, double xyTol, double zTol)
        {
            if (Geometry2D.AlmostSame(a.A.XY, b.A.XY, xyTol) &&
                Geometry2D.AlmostSame(a.B.XY, b.B.XY, xyTol))
                return Math.Abs(a.A.Z - b.A.Z) <= zTol &&
                       Math.Abs(a.B.Z - b.B.Z) <= zTol;

            if (Geometry2D.AlmostSame(a.A.XY, b.B.XY, xyTol) &&
                Geometry2D.AlmostSame(a.B.XY, b.A.XY, xyTol))
                return Math.Abs(a.A.Z - b.B.Z) <= zTol &&
                       Math.Abs(a.B.Z - b.A.Z) <= zTol;

            return false;
        }

        private static string SegmentBucketKey(Segment3 s, double tol)
        {
            string a = PointBucketKey(s.A.X, s.A.Y, tol);
            string b = PointBucketKey(s.B.X, s.B.Y, tol);
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        private static string PointBucketKey(double x, double y, double tol)
        {
            long ix = (long)Math.Round(x / tol);
            long iy = (long)Math.Round(y / tol);
            return ix + ":" + iy;
        }

        private sealed class PointGridIndex
        {
            private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
            private readonly CancellationToken _cancellationToken;
            private readonly double _cell;
            private readonly double _minX, _minY;

            public PointGridIndex(IReadOnlyList<Vec3> points, double tol, CancellationToken cancellationToken)
            {
                _cancellationToken = cancellationToken;
                double maxX, maxY;
                Bounds(points.Select(p => p.X), points.Select(p => p.Y),
                    out _minX, out _minY, out maxX, out maxY);
                double span = Math.Max(maxX - _minX, maxY - _minY);
                _cell = Math.Max(tol * 100.0,
                    span / Math.Max(8.0, Math.Sqrt(Math.Max(1, points.Count))));
                for (int i = 0; i < points.Count; i++) { cancellationToken.ThrowIfCancellationRequested(); Add(i, points[i].X, points[i].Y); }
            }

            public IEnumerable<int> Query(double x0, double y0, double x1, double y1, double pad)
            {
                int ix0 = Ix(Math.Min(x0, x1) - pad);
                int ix1 = Ix(Math.Max(x0, x1) + pad);
                int iy0 = Iy(Math.Min(y0, y1) - pad);
                int iy1 = Iy(Math.Max(y0, y1) + pad);

                // Mỗi site chỉ nằm trong đúng một cell, nên không cần cấp phát
                // HashSet cho từng breakline query.
                for (int ix = ix0; ix <= ix1; ix++)
                for (int iy = iy0; iy <= iy1; iy++)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    List<int> ids;
                    if (!_cells.TryGetValue(Key(ix, iy), out ids)) continue;
                    foreach (var id in ids) yield return id;
                }
            }

            private void Add(int i, double x, double y)
            {
                long key = Key(Ix(x), Iy(y));
                List<int> ids;
                if (!_cells.TryGetValue(key, out ids))
                {
                    ids = new List<int>();
                    _cells[key] = ids;
                }
                ids.Add(i);
            }

            private int Ix(double x) => (int)Math.Floor((x - _minX) / _cell);
            private int Iy(double y) => (int)Math.Floor((y - _minY) / _cell);
            private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
        }

        private sealed class SegmentGridIndex
        {
            private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
            private readonly List<List<long>> _segmentCells = new List<List<long>>();
            private readonly int[] _visited;
            private int _visitToken;
            private readonly CancellationToken _cancellationToken;
            private readonly double _cell, _minX, _minY;

            public SegmentGridIndex(IReadOnlyList<Segment3> segments, double tol, CancellationToken cancellationToken)
            {
                _cancellationToken = cancellationToken;
                double maxX, maxY;
                Bounds(
                    segments.SelectMany(seg => new[] { seg.A.X, seg.B.X }),
                    segments.SelectMany(seg => new[] { seg.A.Y, seg.B.Y }),
                    out _minX, out _minY, out maxX, out maxY);

                double span = Math.Max(maxX - _minX, maxY - _minY);
                _cell = Math.Max(tol * 100.0,
                    span / Math.Max(8.0, Math.Sqrt(Math.Max(1, segments.Count))));
                _visited = new int[Math.Max(1, segments.Count)];
                for (int i = 0; i < segments.Count; i++)
                {
                    _cancellationToken.ThrowIfCancellationRequested(); cancellationToken.ThrowIfCancellationRequested(); IndexSegment(i, segments[i], tol); }
            }

            public IEnumerable<int> Query(int segmentIndex)
            {
                int token = NextVisitToken();
                foreach (var key in _segmentCells[segmentIndex])
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    List<int> ids;
                    if (!_cells.TryGetValue(key, out ids)) continue;
                    foreach (var id in ids)
                    {
                        _cancellationToken.ThrowIfCancellationRequested();
                        if (_visited[id] == token) continue;
                        _visited[id] = token;
                        yield return id;
                    }
                }
            }

            private int NextVisitToken()
            {
                if (_visitToken == int.MaxValue)
                {
                    Array.Clear(_visited, 0, _visited.Length);
                    _visitToken = 1;
                }
                else _visitToken++;
                return _visitToken;
            }

            private void IndexSegment(int i, Segment3 seg, double pad)
            {
                int ix0 = Ix(Math.Min(seg.A.X, seg.B.X) - pad);
                int ix1 = Ix(Math.Max(seg.A.X, seg.B.X) + pad);
                int iy0 = Iy(Math.Min(seg.A.Y, seg.B.Y) - pad);
                int iy1 = Iy(Math.Max(seg.A.Y, seg.B.Y) + pad);
                var keys = new List<long>();

                for (int ix = ix0; ix <= ix1; ix++)
                for (int iy = iy0; iy <= iy1; iy++)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    long key = Key(ix, iy);
                    keys.Add(key);
                    List<int> ids;
                    if (!_cells.TryGetValue(key, out ids))
                    {
                        ids = new List<int>();
                        _cells[key] = ids;
                    }
                    ids.Add(i);
                }
                _segmentCells.Add(keys);
            }

            private int Ix(double x) => (int)Math.Floor((x - _minX) / _cell);
            private int Iy(double y) => (int)Math.Floor((y - _minY) / _cell);
            private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
        }

        private static void Bounds(IEnumerable<double> xs, IEnumerable<double> ys,
            out double minX, out double minY, out double maxX, out double maxY)
        {
            var xa = xs.ToArray();
            var ya = ys.ToArray();

            if (xa.Length == 0 || ya.Length == 0)
            {
                minX = minY = 0;
                maxX = maxY = 1;
                return;
            }

            minX = xa.Min();
            maxX = xa.Max();
            minY = ya.Min();
            maxY = ya.Max();
            if (maxX <= minX) maxX = minX + 1.0;
            if (maxY <= minY) maxY = minY + 1.0;
        }
    }
}
