using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Surface;

namespace MiningVolume.Surface
{
    /// <summary>
    /// Robust constrained triangulation through the pinned CDT native adapter.
    /// Inputs are normalized first; no constraint is dropped and there is no legacy fallback.
    /// The old Bowyer-Watson/edge-flip implementation is retained only in tests/core/legacy.
    /// </summary>
    public sealed class ConformingTinBuilder : ITinBuilder
    {
        private const string NativeLibrary = "MiningVolume.Cdt.Native.dll";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CheckProgress(int phase, int done, int total);
        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mv_cdt_build(double[] xy, int count, int[] edges, int edgeCount,
            CheckProgress check, out IntPtr mesh, out int triangles, StringBuilder error, int capacity);
        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern int mv_cdt_copy(IntPtr mesh, int[] indices, int length);
        [DllImport(NativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void mv_cdt_free(IntPtr mesh);

        private readonly struct XY : IEquatable<XY>
        {
            private readonly double _x, _y;
            public XY(Vec3 p) { _x = p.X; _y = p.Y; }
            public bool Equals(XY other) => _x == other._x && _y == other._y;
            public override bool Equals(object obj) => obj is XY && Equals((XY)obj);
            public override int GetHashCode() { unchecked { return (_x.GetHashCode() * 397) ^ _y.GetHashCode(); } }
        }

        public TinSurface Build(string name, PreparedSurfaceInput input, SurfaceBuildOptions options)
            => Build(name, input, options, null, default(CancellationToken));

        public TinSurface Build(string name, PreparedSurfaceInput input, SurfaceBuildOptions options,
            Action<string> progress, CancellationToken cancellationToken)
            => BuildCore(name, input, options, progress, cancellationToken, false);

        internal TinSurface BuildFileBacked(string name, PreparedSurfaceInput input, SurfaceBuildOptions options,
            Action<string> progress, CancellationToken cancellationToken)
            => BuildCore(name, input, options, progress, cancellationToken, true);

        private TinSurface BuildCore(string name, PreparedSurfaceInput input, SurfaceBuildOptions options,
            Action<string> progress, CancellationToken cancellationToken, bool fileBacked)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            options = options ?? new SurfaceBuildOptions();
            cancellationToken.ThrowIfCancellationRequested();
            if (input.HasErrors) throw new InvalidOperationException("Dữ liệu mô hình còn lỗi; chưa dựng TIN.");
            if (input.Sites.Count < 3) throw new InvalidOperationException("TIN cần ít nhất 3 điểm hợp lệ.");
            int n = input.Sites.Count;
            var xy = new double[checked(n * 2)];
            var siteIndex = new Dictionary<XY, int>(n);
            for (int i = 0; i < n; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var p = input.Sites[i];
                if (double.IsNaN(p.X) || double.IsInfinity(p.X) || double.IsNaN(p.Y) ||
                    double.IsInfinity(p.Y) || double.IsNaN(p.Z) || double.IsInfinity(p.Z))
                    throw new InvalidOperationException("Điểm TIN có tọa độ không hữu hạn.");
                xy[2 * i] = p.X; xy[2 * i + 1] = p.Y;
                if (siteIndex.ContainsKey(new XY(p))) throw new InvalidOperationException("Điểm trùng XY chưa được chuẩn hóa.");
                siteIndex.Add(new XY(p), i);
            }
            var edges = new List<int>(checked(input.Breaklines.Count * 2));
            foreach (var segment in input.Breaklines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int a, b;
                if (!siteIndex.TryGetValue(new XY(segment.A), out a) ||
                    !siteIndex.TryGetValue(new XY(segment.B), out b))
                    throw new InvalidOperationException("Đầu mút breakline chưa đồng bộ với nút TIN: " + segment.SourceId);
                if (Math.Abs(segment.A.Z - input.Sites[a].Z) > options.ZConflictTolerance ||
                    Math.Abs(segment.B.Z - input.Sites[b].Z) > options.ZConflictTolerance)
                    throw new InvalidOperationException("Cao độ đầu mút breakline chưa được chuẩn hóa: " + segment.SourceId);
                if (a != b) { edges.Add(a); edges.Add(b); }
            }
            var watch = Stopwatch.StartNew();
            Exception callbackError = null;
            CheckProgress callback = (phase, done, total) =>
            {
                if (cancellationToken.IsCancellationRequested || callbackError != null) return 1;
                if (watch.ElapsedMilliseconds >= 200)
                {
                    watch.Restart();
                    try { progress?.Invoke((phase == 0 ? "Tam giác hóa điểm " : phase == 1 ? "Khôi phục breakline " : "Kiểm tra lưới ") + $"{done:n0}/{total:n0}..."); }
                    catch (Exception ex) { callbackError = ex; return 1; }
                }
                return 0;
            };
            IntPtr mesh = IntPtr.Zero;
            try
            {
                int count;
                var error = new StringBuilder(2048);
                int code = mv_cdt_build(xy, n, edges.ToArray(), edges.Count / 2, callback,
                    out mesh, out count, error, error.Capacity);
                GC.KeepAlive(callback);
                if (callbackError != null) throw callbackError;
                cancellationToken.ThrowIfCancellationRequested();
                if (code == 2) throw new OperationCanceledException(cancellationToken);
                if (code != 0) throw new InvalidOperationException("Không dựng được lưới ràng buộc: " + error);
                if (count <= 0) throw new InvalidOperationException("Không có tam giác; kiểm tra các điểm thẳng hàng.");
                var indices = new int[checked(count * 3)];
                if (mv_cdt_copy(mesh, indices, indices.Length) != 0)
                    throw new InvalidOperationException("Không đọc được kết quả lưới ràng buộc.");
                var verticalFaces = VerticalFaceBuilder.Build(
                    input.Sites, indices, input.VerticalConflictGroups,
                    options.XyTolerance, options.ZConflictTolerance, cancellationToken);
                if (fileBacked)
                {
                    int retainedSmall;
                    var store = AdaptiveTinStore.Create(input.Sites, indices, options, progress,
                        cancellationToken, out retainedSmall);
                    var storedIssues = input.Issues.ToList();
                    if (retainedSmall > 0) storedIssues.Add(new ValidationIssue(ValidationSeverity.Warning,
                        "RETAIN_SMALL_TRIANGLES", $"Giữ {retainedSmall:n0} tam giác diện tích nhỏ dương để bảo toàn điểm và breakline."));
                    return new TinSurface(name, store, storedIssues,
                        verticalFaces, input.VerticalConflictGroups,
                    input.ConflictReport?.SameXYZDuplicates ?? 0,
                        input.RelaxedBreaklineConstraints);
                }
                var output = new List<Triangle3>(count);
                var clipper = options.ClipBoundary == null ? null : new TinPolygonClipper(options.ClipBoundary, cancellationToken);
                int small = 0;
                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var t = new Triangle3(input.Sites[indices[3 * i]], input.Sites[indices[3 * i + 1]], input.Sites[indices[3 * i + 2]]);
                    if (t.Area2D <= 0 || double.IsInfinity(t.Area2D) || double.IsNaN(t.Area2D))
                        throw new InvalidOperationException("Lưới có tam giác suy biến; không xuất kết quả thiếu dữ liệu.");
                    // A small positive-area triangle can be the only owner of a survey vertex.
                    // Keep it, report it, and never silently remove its point or constraint.
                    if (t.Area2D <= options.MinimumTriangleArea) small++;
                    if (clipper == null) output.Add(t);
                    else output.AddRange(clipper.Clip(t, cancellationToken));
                }
                if (output.Count == 0) throw new InvalidOperationException("TIN không tạo được tam giác hợp lệ trong phạm vi tính.");
                var issues = input.Issues.ToList();
                if (small > 0) issues.Add(new ValidationIssue(ValidationSeverity.Warning, "RETAIN_SMALL_TRIANGLES",
                    $"Giữ {small:n0} tam giác diện tích nhỏ dương để bảo toàn điểm và breakline."));
                return new TinSurface(name, output, issues,
                    verticalFaces, input.VerticalConflictGroups,
                    input.ConflictReport?.SameXYZDuplicates ?? 0,
                        input.RelaxedBreaklineConstraints);
            }
            catch (DllNotFoundException ex) { throw new InvalidOperationException("Thiếu thư viện dựng TIN. Hãy cài lại đầy đủ bộ cài IMSAT VOLUME.", ex); }
            finally { if (mesh != IntPtr.Zero) mv_cdt_free(mesh); }
        }
    }
}
