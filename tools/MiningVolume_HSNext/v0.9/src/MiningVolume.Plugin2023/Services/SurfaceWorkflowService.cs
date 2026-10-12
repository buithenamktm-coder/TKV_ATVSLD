using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System.Globalization;
using MiningVolume.Cad2023;
using MiningVolume.Core.Model;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Surface;
using MiningVolume.Surface;

namespace MiningVolume2023.Services
{
    public sealed class SurfaceLoadResult
    {
        public int Entities { get; set; }
        public int Vertices { get; set; }
        public string OutputTinLayer { get; set; }
        public string Summary => $"{Entities:n0} đối tượng • {Vertices:n0} đỉnh";
    }

    public sealed class SurfaceBuildResult
    {
        public ModelRole Role { get; set; }
        public TinSurface Tin { get; set; }
        public int SiteCount { get; set; }
        public int BreaklineCount { get; set; }
        public int WarningCount { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
        public DateTime SourceModifiedUtc { get; set; }
        public long SourceRevision { get; set; }
        public long TinRegionRevision { get; set; }
        public long PrepareMilliseconds { get; set; }
        public long TriangulationMilliseconds { get; set; }
        internal SurfaceModel SourceModel { get; set; }
        public int TriangleCount => Tin?.Triangles?.Count ?? 0;
        public int VerticalFaceCount => Tin?.VerticalFaces?.Count ?? 0;
        public int DuplicateXYGroupCount => Tin?.VerticalConflicts?.Count ?? 0;
        public int SameXYZDuplicateVertices => Tin?.SameXYZDuplicateVertices ?? 0;
        public int RelaxedBreaklineConstraints => Tin?.RelaxedBreaklineConstraints ?? 0;
        public int DuplicateXYSourceVertices => Tin?.VerticalConflicts?.Sum(g =>
        {
            int originalCount = g.Sources.Count(x => !x.Generated && x.VertexIndex >= 0);
            // Synthetic raw-coordinate groups lack CAD handles/vertex indices.
            return originalCount > 0 ? originalCount : g.Sources.Count;
        }) ?? 0;
        public long CoreMilliseconds => PrepareMilliseconds + TriangulationMilliseconds;
        public bool IsLargeDataset { get; set; }
        public int TileCount { get; set; }
        public IReadOnlyList<MiningVolume.Core.Geometry.Triangle3> PreviewTriangles { get; set; }

        public string Summary =>
            $"{SiteCount:n0} điểm TIN • {BreaklineCount:n0} đoạn breakline • {TriangleCount:n0} tam giác" +
            (IsLargeDataset ? $" • TIN dữ liệu lớn {TileCount:n0} ô" : string.Empty) +
            (DuplicateXYGroupCount > 0 ? $" • {DuplicateXYGroupCount:n0} vị trí XY khác Z / {DuplicateXYSourceVertices:n0} đỉnh nguồn • {VerticalFaceCount:n0} mặt đứng" : "") +
            (SameXYZDuplicateVertices > 0 ? $" • {SameXYZDuplicateVertices:n0} đỉnh trùng cả XYZ" : "") +
            (RelaxedBreaklineConstraints > 0 ? $" • {RelaxedBreaklineConstraints:n0} ràng buộc đồng mức xung đột đã nới trên lưới hỗ trợ (nguồn giữ nguyên)" : "") +
            $" • Z: {MinZ:0.###} → {MaxZ:0.###}" +
            $" • dựng {CoreMilliseconds / 1000.0:0.00}s" +
            (WarningCount > 0 ? $" • {WarningCount:n0} cảnh báo" : string.Empty) +
            string.Concat((Tin?.Issues ?? Array.Empty<ValidationIssue>()).Where(x=>x.Code=="SKIPPED_DEGENERATE_TRIANGLES").Select(x=>" • "+x.Message));
    }

    public static class SurfaceWorkflowService
    {
        private static int _interactiveBuild;
        public static IDisposable EnterInteractiveBuild()
        {
            if (Interlocked.CompareExchange(ref _interactiveBuild, 1, 0) != 0)
                throw new InvalidOperationException("Đang có một lượt dựng TIN. Hãy đợi hoàn tất hoặc hủy trước khi tạo TIN tiếp theo.");
            return new BuildLease();
        }
        private sealed class BuildLease : IDisposable
        {
            private int _released;
            public void Dispose() {
                if (Interlocked.Exchange(ref _released, 1) == 0)
                    Interlocked.Exchange(ref _interactiveBuild, 0);
            }
        }

        public static SurfaceLoadResult[] LoadVia4Preset(ISet<SourceEntityType> allowedTypes)
        {
            // Validate both inputs and the unique boundary before replacing either session.
            string boundaryHandle = BoundaryGeometryService.FindUniqueBoundaryOnLayer("LO_TINHKL");
            var polygon = BoundaryGeometryService.ReadBoundary(boundaryHandle);
            if (polygon.Count < 3) throw new InvalidOperationException("LO_TINHKL không có đủ đỉnh hợp lệ.");
            var doc = Application.DocumentManager.MdiActiveDocument ??
                throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            var layers = new[] { "ht", "- nam4" };
            var sources = new IReadOnlyList<SourceEntity>[2];
            using (doc.LockDocument())
            {
                var reader = new CadLayerSurfaceReader();
                for (int i = 0; i < layers.Length; i++)
                {
                    sources[i] = reader.Read(doc.Database, layers[i], 0.50)
                        .Where(e => allowedTypes == null || allowedTypes.Count == 0 || allowedTypes.Contains(e.Type)).ToList();
                    if (sources[i].Count == 0)
                        throw new InvalidOperationException($"Layer '{layers[i]}' không có dữ liệu hợp lệ theo loại đang chọn.");
                }
            }
            var result = new[]
            {
                CommitLoadedSource(ModelRole.Existing, layers[0], SourceSelectionMode.Layer, sources[0], allowedTypes),
                CommitLoadedSource(ModelRole.Design, layers[1], SourceSelectionMode.Layer, sources[1], allowedTypes)
            };
            var state = ProjectState.Current;
            state.TinRegionHandle = boundaryHandle;
            state.TinRegionPolygon.Clear();
            state.TinRegionPolygon.AddRange(polygon);
            state.MarkTinRegionChanged();
            // Use the same boundary for section-based volume calculation.
            state.BoundaryHandle = boundaryHandle;
            state.SectionSystem = null;
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            state.VolumeCalculatedUtc = null;
            state.VolumeCalculationSeconds = null;
            GridWorkflowService.Invalidate();
            state.NotifyChanged();
            return result;
        }

        public static SurfaceLoadResult LoadLayer(ModelRole role, string layer, ISet<SourceEntityType> allowedTypes)
        {
            if (string.IsNullOrWhiteSpace(layer))
                throw new InvalidOperationException("Chưa chọn layer dữ liệu nguồn.");

            var state = ProjectState.Current;
            if (string.Equals(layer, state.Existing.TinLayer, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layer, state.Design.TinLayer, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Không được dùng layer TIN đầu ra làm dữ liệu nguồn. Hãy chọn layer POINT/LINE/POLYLINE/đồng mức gốc.");

            var doc = Application.DocumentManager.MdiActiveDocument ??
                      throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            IReadOnlyList<SourceEntity> entities;
            using (doc.LockDocument())
            {
                var reader = new CadLayerSurfaceReader();
                entities = reader.Read(doc.Database, layer, 0.50)
                    .Where(e => allowedTypes == null || allowedTypes.Count == 0 || allowedTypes.Contains(e.Type))
                    .ToList();
            }

            if (entities.Count == 0)
                throw new InvalidOperationException(
                    $"Layer '{layer}' không có đối tượng POINT/LINE/POLYLINE hợp lệ theo loại dữ liệu đang chọn.");

            return CommitLoadedSource(role, layer, SourceSelectionMode.Layer, entities, allowedTypes);
        }

        public static SurfaceLoadResult LoadSelection(ModelRole role, IReadOnlyList<ObjectId> objectIds, ISet<SourceEntityType> allowedTypes)
        {
            if (objectIds == null || objectIds.Count == 0)
                throw new InvalidOperationException("Chưa chọn đối tượng CAD nào.");

            EnsureTinRegionMatchesCad();
            var state = ProjectState.Current;
            var doc = Application.DocumentManager.MdiActiveDocument ??
                      throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");

            IReadOnlyList<SourceEntity> entities;
            using (doc.LockDocument())
            {
                var reader = new CadLayerSurfaceReader();
                entities = reader.Read(doc.Database, objectIds, 0.50)
                    .Where(e =>
                        !string.Equals(e.Layer, state.Existing.TinLayer, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(e.Layer, state.Design.TinLayer, StringComparison.OrdinalIgnoreCase))
                    .Where(e => allowedTypes == null || allowedTypes.Count == 0 || allowedTypes.Contains(e.Type))
                    .ToList();
            }

            if (entities.Count == 0)
                throw new InvalidOperationException(
                    "Các đối tượng đã chọn không có POINT/LINE/POLYLINE hợp lệ theo loại dữ liệu đang bật, " +
                    "hoặc chỉ thuộc layer TIN đầu ra của IMSAT VOLUME.");

            return CommitLoadedSource(role, null, SourceSelectionMode.ManualSelection, entities, allowedTypes);
        }

        public static SurfaceLoadResult LoadSelectedHandles(ModelRole role, IEnumerable<string> handles, ISet<SourceEntityType> allowedTypes)
        {
            var doc = Application.DocumentManager.MdiActiveDocument ??
                      throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            var ids = new List<ObjectId>();
            foreach (var text in handles ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                try
                {
                    long value = long.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    ObjectId id = doc.Database.GetObjectId(false, new Handle(value), 0);
                    if (!id.IsNull && id.IsValid && !id.IsErased) ids.Add(id);
                }
                catch
                {
                    // Một handle có thể đã bị xóa/sửa trong DWG; bỏ qua và để bước kiểm tra
                    // phía dưới quyết định còn đủ dữ liệu để khôi phục mô hình hay không.
                }
            }
            if (ids.Count == 0)
                throw new InvalidOperationException("Không còn đối tượng CAD đã chọn trước đây để khôi phục mô hình.");
            return LoadSelection(role, ids, allowedTypes);
        }

        private static SurfaceLoadResult CommitLoadedSource(
            ModelRole role,
            string layer,
            SourceSelectionMode sourceMode,
            IReadOnlyList<SourceEntity> entities,
            ISet<SourceEntityType> allowedTypes)
        {
            var model = new SurfaceModel(role == ModelRole.Existing ? "Hiện trạng" : "Thiết kế");
            model.Entities.AddRange(entities);
            model.Touch();

            var session = ProjectState.Current.Get(role);
            session.Layer = sourceMode == SourceSelectionMode.Layer ? layer : null;
            session.SourceMode = sourceMode;
            session.SelectedHandles.Clear();
            if (sourceMode == SourceSelectionMode.ManualSelection)
                foreach (var handle in entities.Select(e => e.Handle).Distinct(StringComparer.OrdinalIgnoreCase))
                    session.SelectedHandles.Add(handle);

            session.AllowedTypes.Clear();
            if (allowedTypes != null)
                foreach (var t in allowedTypes) session.AllowedTypes.Add(t);
            session.Source = model;

            // Bất kỳ thay đổi nguồn nào cũng làm TIN trước đó mất hiệu lực.
            InvalidateTin(role, clearCadLayer: true, notify: false);
            EnsureOutputLayer(role);

            ProjectState.Current.SectionProfiles.Clear();
            ProjectState.Current.VolumeResult = null;
            ProjectState.Current.NotifyChanged();

            return new SurfaceLoadResult
            {
                Entities = entities.Count,
                Vertices = entities.Sum(e => e.Vertices.Count),
                OutputTinLayer = session.TinLayer
            };
        }

        private const int LargeDatasetVertexThreshold = 200000;

        public static SurfaceBuildResult BuildCoreDetailed(
            ModelRole role,
            Action<string> progress = null,
            CancellationToken cancellationToken = default(CancellationToken),
            DuplicateXYConflictPolicy duplicateXYPolicy = DuplicateXYConflictPolicy.Stop)
        {
            var state = ProjectState.Current;
            var session = state.Get(role);
            if (session.Source == null || session.Source.Entities.Count == 0)
                throw new InvalidOperationException($"Mô hình {session.Name} chưa có dữ liệu.");

            var source = session.Source;
            var sourceModified = source.LastModifiedUtc;
            var sourceRevision = source.Revision;
            var regionRevision = state.TinRegionRevision;
            var clipBoundary = state.HasTinRegion
                ? state.TinRegionPolygon.ToArray()
                : null;

            var options = new SurfaceBuildOptions
            {
                XyTolerance = 1e-6,
                ZConflictTolerance = 1e-4,
                MinimumTriangleArea = 1e-10,
                // User-confirmed HSMo-like workflow: do not stop on duplicate
                // survey points with different Z; retain extra 3D faces and
                // require editable section acceptance before official volumes.
                PreserveDuplicateXYVerticalFaces = true,
                DuplicateXYConflictPolicy = duplicateXYPolicy,
                ClipBoundary = clipBoundary
            };

            cancellationToken.ThrowIfCancellationRequested();

            // V1.0 cho phép chỉ dựng TIN trong vùng người dùng chọn trên CAD.
            // Việc lọc/cắt nguồn diễn ra trước triangulation để giảm mạnh số điểm
            // phải xử lý; nguồn gốc trong ProjectState không bị sửa.
            var buildSource = source;
            if (clipBoundary != null && clipBoundary.Length >= 3)
            {
                progress?.Invoke("Đang lọc dữ liệu theo vùng TIN đã chọn trên CAD...");
                buildSource = SurfaceRegionClipper.Clip(
                    source,
                    clipBoundary,
                    options.XyTolerance,
                    cancellationToken);
            }

            int buildVertexCount = CountActiveVertices(buildSource);
            if (buildVertexCount < 3)
                throw new InvalidOperationException(
                    $"Vùng tạo TIN của {session.Name} không có đủ 3 đỉnh hợp lệ. " +
                    "Hãy chọn vùng lớn hơn hoặc kiểm tra dữ liệu nguồn.");

            cancellationToken.ThrowIfCancellationRequested();

            // Dữ liệu mỏ lớn không được đẩy vào một phép Bowyer-Watson toàn cục.
            // Từ ngưỡng này chuyển sang TIN phân ô có halo, giữ breakline và kiểm
            // tra ổn định biên. Kiến trúc này hướng tới cỡ ~10 triệu đỉnh/mô hình.
            if (buildVertexCount >= LargeDatasetVertexThreshold)
            {
                progress?.Invoke(
                    $"Dữ liệu cần dựng: {buildVertexCount:n0} đỉnh" +
                    (clipBoundary != null ? " trong vùng chọn. " : ". ") +
                    "Đang dựng TIN phân ô, không giảm điểm âm thầm...");

                var tiled = new TiledConformingTinBuilder().Build(
                    session.Name,
                    buildSource,
                    options,
                    (done, total, message) => progress?.Invoke(message),
                    cancellationToken);

                if (tiled.Surface == null || tiled.Surface.Triangles == null ||
                    tiled.Surface.Triangles.Count == 0)
                    throw new InvalidOperationException($"TIN {session.Name} không tạo được tam giác hợp lệ.");

                if (!ReferenceEquals(session.Source, source) ||
                    source.Revision != sourceRevision ||
                    state.TinRegionRevision != regionRevision)
                {
                    (tiled.Surface.Triangles as IDisposable)?.Dispose();
                    throw new InvalidOperationException(
                        $"Dữ liệu {session.Name} đã thay đổi trong lúc đang dựng TIN. " +
                        "Kết quả tạm bị hủy; hãy tạo/cập nhật TIN lại.");
                }

                return new SurfaceBuildResult
                {
                    Role = role,
                    Tin = tiled.Surface,
                    SiteCount = tiled.InputVertexCount,
                    BreaklineCount = tiled.InputBreaklineCount,
                    WarningCount = tiled.WarningCount,
                    MinZ = tiled.MinZ,
                    MaxZ = tiled.MaxZ,
                    SourceModifiedUtc = sourceModified,
                    SourceRevision = sourceRevision,
                    TinRegionRevision = regionRevision,
                    PrepareMilliseconds = tiled.PrepareMilliseconds,
                    TriangulationMilliseconds = tiled.TriangulationMilliseconds,
                    SourceModel = source,
                    IsLargeDataset = true,
                    TileCount = tiled.TileCount,
                    PreviewTriangles = tiled.PreviewTriangles
                };
            }

            var prepareWatch = Stopwatch.StartNew();
            var prepared = new SurfaceInputPreparer().Prepare(buildSource, options, progress, cancellationToken);
            prepareWatch.Stop();
            if (prepared.HasErrors)
            {
                if(prepared.ConflictReport?.HasBlockingConflicts==true)throw prepared.ConflictReport.ToException();
                var duplicateIssues = prepared.Issues
                    .Where(x => x.Severity == ValidationSeverity.Error &&
                        (string.Equals(x.Code, "DUPLICATE_XY_CONFLICT_Z", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Code, "POINT_ON_BREAKLINE_Z_CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Code, "BREAKLINE_CROSSING_Z_CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Code, "BREAKLINE_OVERLAP_Z_CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Code, "BREAKLINE_DUPLICATE_Z_CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Code, "BREAKLINE_NODE_Z_CONFLICT", StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (duplicateIssues.Count > 0)
                    throw new DuplicateXYConflictException(session.Name, duplicateIssues);

                throw new SurfaceValidationException(session.Name, prepared.Issues);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var triangulationWatch = Stopwatch.StartNew();
            var tin = new ConformingTinBuilder().Build(session.Name, prepared, options, progress, cancellationToken);
            triangulationWatch.Stop();
            if (tin == null || tin.Triangles == null || tin.Triangles.Count == 0)
                throw new InvalidOperationException($"TIN {session.Name} không tạo được tam giác hợp lệ.");

            if (!ReferenceEquals(session.Source, source) ||
                source.Revision != sourceRevision ||
                state.TinRegionRevision != regionRevision)
                throw new InvalidOperationException(
                    $"Dữ liệu {session.Name} đã thay đổi trong lúc đang dựng TIN. " +
                    "Kết quả tạm bị hủy; hãy tạo/cập nhật TIN lại.");

            return new SurfaceBuildResult
            {
                Role = role,
                Tin = tin,
                SiteCount = prepared.Sites.Count,
                BreaklineCount = prepared.Breaklines.Count,
                WarningCount = prepared.Issues.Count(x => x.Severity == ValidationSeverity.Warning),
                MinZ = prepared.Sites.Min(p => p.Z),
                MaxZ = Math.Max(prepared.Sites.Max(p => p.Z),
                    prepared.VerticalConflictGroups.Count == 0 ? double.NegativeInfinity :
                    prepared.VerticalConflictGroups.Max(g => g.Sources.Max(v => v.Z))),
                SourceModifiedUtc = sourceModified,
                SourceRevision = sourceRevision,
                TinRegionRevision = regionRevision,
                PrepareMilliseconds = prepareWatch.ElapsedMilliseconds,
                TriangulationMilliseconds = triangulationWatch.ElapsedMilliseconds,
                SourceModel = source,
                IsLargeDataset = false,
                TileCount = 1
            };
        }

        // Backward-compatible core entry used by project restore.
        public static TinSurface BuildCore(ModelRole role) => BuildCoreDetailed(role).Tin;

        public static SurfaceBuildResult[] BuildPairCoreDetailed(
            Action<string> progress = null,
            CancellationToken cancellationToken = default(CancellationToken),
            DuplicateXYConflictPolicy existingPolicy = DuplicateXYConflictPolicy.Stop,
            DuplicateXYConflictPolicy designPolicy = DuplicateXYConflictPolicy.Stop)
        {
            // Build both pure-core surfaces before touching the DWG. If either fails,
            // neither CAD TIN is replaced.
            var existing = BuildCoreDetailed(ModelRole.Existing,
                message => progress?.Invoke("Hiện trạng: " + message), cancellationToken, existingPolicy);
            try {
                var design = BuildCoreDetailed(ModelRole.Design,
                    message => progress?.Invoke("Thiết kế: " + message), cancellationToken, designPolicy);
                return new[] { existing, design };
            } catch {
                (existing.Tin.Triangles as IDisposable)?.Dispose();
                throw;
            }
        }

        public static void DrawTinPair(SurfaceBuildResult[] builds, Action<string> progress = null)
        {
            if (builds == null || builds.Length != 2)
                throw new ArgumentException("Cặp kết quả TIN không hợp lệ.", nameof(builds));

            var existing = builds.FirstOrDefault(x => x.Role == ModelRole.Existing);
            var design = builds.FirstOrDefault(x => x.Role == ModelRole.Design);
            if (existing == null || design == null)
                throw new InvalidOperationException("Thiếu kết quả TIN hiện trạng hoặc TIN thiết kế.");

            var state = ProjectState.Current;
            var doc = Application.DocumentManager.MdiActiveDocument ??
                throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            foreach (var build in builds)
                if (!ReferenceEquals(state.Get(build.Role).Source, build.SourceModel) ||
                    build.SourceModel.Revision != build.SourceRevision ||
                    build.TinRegionRevision != state.TinRegionRevision)
                    throw new InvalidOperationException("Nguồn TIN đã đổi; không ghi kết quả cũ.");
            // The CAD layer is authoritative geometry, never a sampled preview.
            var ht = existing.Tin;
            var tk = design.Tin;
            progress?.Invoke("Đang ghi TIN hiện trạng xuống AutoCAD, tiếp đến thiết kế; Esc để hủy cả thao tác...");
            int[] written;
            using (doc.LockDocument())
                written = TinCadRenderer.ReplacePair(doc.Database, state.Existing.TinLayer, ht,
                    state.Design.TinLayer, tk);
            // Both CAD layers committed together. Only now transfer ownership of stores.
            AdoptTin(state.Existing, existing.Tin, existing.SourceRevision, existing.SourceModifiedUtc,
                written[0], false);
            AdoptTin(state.Design, design.Tin, design.SourceRevision, design.SourceModifiedUtc,
                written[1], false);
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            state.VolumeCalculatedUtc = null;
            state.VolumeCalculationSeconds = null;
            GridWorkflowService.Invalidate();
            EnsureBothTinsReady(synchronizeCadLayers: false);
            state.NotifyChanged();
            try { doc.Editor.Regen(); } catch { }
        }

        public static void DrawTin(ModelRole role, SurfaceBuildResult build)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            EnsureTinRegionMatchesCad();
            if (build.Role != role) throw new InvalidOperationException("Kết quả dựng TIN không đúng mô hình cần ghi.");
            if (build.TinRegionRevision != ProjectState.Current.TinRegionRevision)
                throw new InvalidOperationException(
                    "Vùng tạo TIN đã thay đổi trong khi tính. Không ghi TIN cũ; hãy dựng lại theo vùng mới.");
            DrawTinInternal(
                role,
                build.Tin,
                build.SourceModel,
                build.SourceRevision,
                build.SourceModifiedUtc,
                null);
        }

        public static void DrawTin(ModelRole role, TinSurface tin)
        {
            var session = ProjectState.Current.Get(role);
            var source = session.Source ?? throw new InvalidOperationException("Mô hình chưa có dữ liệu nguồn.");
            DrawTinInternal(role, tin, source, source.Revision, source.LastModifiedUtc, null);
        }

        private static void DrawTinInternal(
            ModelRole role,
            TinSurface tin,
            SurfaceModel sourceModel,
            long sourceRevision,
            DateTime sourceModifiedUtc,
            IReadOnlyList<MiningVolume.Core.Geometry.Triangle3> previewTriangles)
        {
            if (tin == null) throw new ArgumentNullException(nameof(tin));
            if (tin.Triangles == null || tin.Triangles.Count == 0)
                throw new InvalidOperationException("TIN không có tam giác hợp lệ.");

            var state = ProjectState.Current;
            var session = state.Get(role);
            if (sourceModel == null || !ReferenceEquals(session.Source, sourceModel) || sourceModel.Revision != sourceRevision)
                throw new InvalidOperationException(
                    $"Dữ liệu {session.Name} đã thay đổi sau khi dựng TIN. " +
                    "Không ghi TIN cũ xuống AutoCAD; hãy tạo/cập nhật lại TIN.");

            EnsureTinRegionMatchesCad();
            var doc = Application.DocumentManager.MdiActiveDocument ??
                      throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");

            // All original support triangles and the vertical faces are written to DWG.
            // Never replace the official surface with a stride-sampled subset.
            var cadTin = tin;
            bool cadPreview = false;

            int written;
            using (doc.LockDocument())
            {
                written = TinCadRenderer.Replace(
                    doc.Database,
                    session.TinLayer,
                    cadTin,
                    role == ModelRole.Existing ? (short)1 : (short)3);
                TinCadRenderer.SetVisible(doc.Database, session.TinLayer, true);
            }

            int expectedCadFaces = cadTin.Triangles.Count + cadTin.VerticalFaces.Count;
            if (written != expectedCadFaces)
                throw new InvalidOperationException(
                    $"Ghi TIN {session.Name} xuống AutoCAD không đầy đủ: cần {expectedCadFaces:n0} mặt CAD, " +
                    $"đã ghi {written:n0}.");

            AdoptTin(session, tin, sourceRevision, sourceModifiedUtc, written, cadPreview);
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            state.VolumeCalculatedUtc = null;
            state.VolumeCalculationSeconds = null;
            GridWorkflowService.Invalidate();
            state.NotifyChanged();

            try { doc.Editor.Regen(); } catch { }
        }

        private static void AdoptTin(ModelSession session, TinSurface tin, long sourceRevision,
            DateTime sourceModifiedUtc, int written, bool cadPreview)
        {
            var previousTin = session.Tin;
            session.Tin = tin;
            if (previousTin != null && !ReferenceEquals(previousTin, tin))
            {
                try { (previousTin.Triangles as IDisposable)?.Dispose(); } catch { }
            }
            session.TinBuiltFromSourceUtc = sourceModifiedUtc;
            session.TinBuiltFromSourceRevision = sourceRevision;
            session.LastBuiltUtc = DateTime.UtcNow;
            session.TinVisible = true;
            session.TinCadFaceCount = written;
            session.TinCadIsPreview = cadPreview;
        }

        public static void EnsureOutputLayer(ModelRole role)
        {
            var session = ProjectState.Current.Get(role);
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            using (doc.LockDocument())
                TinCadRenderer.PrepareLayer(
                    doc.Database,
                    session.TinLayer,
                    role == ModelRole.Existing ? (short)1 : (short)3);
        }

        public static void InvalidateTin(ModelRole role, bool clearCadLayer = true, bool notify = true)
        {
            var state = ProjectState.Current;
            var session = state.Get(role);
            var oldTin = session.Tin;
            session.Tin = null;
            if (oldTin != null)
            {
                try { (oldTin.Triangles as IDisposable)?.Dispose(); } catch { }
            }
            session.TinBuiltFromSourceUtc = null;
            session.TinBuiltFromSourceRevision = null;
            session.LastBuiltUtc = null;
            session.TinVisible = false;
            session.TinCadFaceCount = 0;
            session.TinCadIsPreview = false;

            if (clearCadLayer)
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    using (doc.LockDocument())
                    {
                        TinCadRenderer.PrepareLayer(
                            doc.Database,
                            session.TinLayer,
                            role == ModelRole.Existing ? (short)1 : (short)3);
                        TinCadRenderer.Clear(doc.Database, session.TinLayer);
                    }
                    try { doc.Editor.Regen(); } catch { }
                }
            }

            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            if (notify) state.NotifyChanged();
        }

        // A grip edit changes the CAD boundary geometry without changing its
        // handle or the model's source revision. Never accept results clipped
        // to a stale region under that circumstance.
        private static void EnsureTinRegionMatchesCad()
        {
            var state = ProjectState.Current;
            if (!state.HasTinRegion) return;
            if (string.IsNullOrWhiteSpace(state.TinRegionHandle))
                throw new InvalidOperationException(
                    "Vùng tạo TIN chưa có đường bao CAD để kiểm chứng. Hãy chọn lại vùng.");

            IReadOnlyList<Vec2> actual;
            try { actual = BoundaryGeometryService.ReadBoundary(state.TinRegionHandle); }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Không xác minh được vùng tạo TIN trên CAD. Hãy chọn lại đường bao và dựng TIN trước khi tính.",
                    ex);
            }

            var expected = state.TinRegionPolygon;
            if (actual.Count != expected.Count)
                throw new InvalidOperationException(
                    "Đường bao tạo TIN trên CAD đã thay đổi số đỉnh. Hãy chọn lại vùng và dựng TIN.");
            for (int i = 0; i < actual.Count; i++)
                if (!Geometry2D.AlmostSame(actual[i], expected[i], 1e-7))
                    throw new InvalidOperationException(
                        "Đường bao tạo TIN trên CAD đã bị chỉnh Grip. " +
                        "TIN đang lưu thuộc vùng cũ; hãy chọn lại vùng và dựng TIN trước khi tính.");
        }

        public static void EnsureBothTinsReady(bool synchronizeCadLayers)
        {
            EnsureTinRegionMatchesCad();
            EnsureTinReady(ModelRole.Existing, synchronizeCadLayers);
            EnsureTinReady(ModelRole.Design, synchronizeCadLayers);
        }

        public static void EnsureTinReady(ModelRole role, bool synchronizeCadLayer)
        {
            EnsureTinRegionMatchesCad();
            var session = ProjectState.Current.Get(role);
            if (session.Source == null || session.Source.Entities.Count == 0)
                throw new InvalidOperationException($"Chưa nạp dữ liệu {session.Name}.");
            if (session.Tin == null || session.Tin.Triangles == null || session.Tin.Triangles.Count == 0)
                throw new InvalidOperationException(
                    $"Chưa tạo TIN {session.Name}. Vào mục Mô hình và bấm TẠO / CẬP NHẬT TIN.");
            if (!session.IsTinCurrent)
                throw new InvalidOperationException(
                    $"TIN {session.Name} đã cũ so với dữ liệu X-Y-Z hiện tại. Phải cập nhật lại TIN trước khi tính.");

            if (synchronizeCadLayer)
                EnsureTinLayerSynchronized(role);
        }

        public static void EnsureTinLayerSynchronized(ModelRole role)
        {
            var session = ProjectState.Current.Get(role);
            if (!session.IsTinCurrent) return;

            var doc = Application.DocumentManager.MdiActiveDocument ??
                      throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            int count;
            int unexpected;
            bool exists;
            using (doc.LockDocument())
            {
                exists = TinCadRenderer.LayerExists(doc.Database, session.TinLayer);
                count = TinCadRenderer.CountFaces(doc.Database, session.TinLayer);
                unexpected = TinCadRenderer.CountUnexpectedEntities(doc.Database, session.TinLayer);
            }

            if (unexpected > 0)
                throw new InvalidOperationException(
                    $"Layer {session.TinLayer} có {unexpected:n0} đối tượng không phải 3DFACE. " +
                    "Không dùng layer này để tính nhằm tránh xóa nhầm dữ liệu CAD.");

            // Always validate against FULL official geometry. Old V2 QA sessions
            // could carry a 200k-face sampled layer and a matching sampled count;
            // that legacy layer must never be accepted as a complete CAD TIN.
            int expectedCadFaces = checked(session.Tin.Triangles.Count +
                session.Tin.VerticalFaces.Count);
            if (exists && count == expectedCadFaces && !session.TinCadIsPreview)
                return;

            // Restore only the CAD drawing representation of the existing,
            // unchanged TIN. Calling DrawTinInternal here would incorrectly
            // invalidate previously reviewed section profiles, earthwork volume
            // and grid volume even though source data and geometry are identical.
            // Do not adopt/dispose the current TIN or alter calculation timestamps.
            int written;
            using (doc.LockDocument())
            {
                written = TinCadRenderer.Replace(doc.Database,
                    session.TinLayer, session.Tin,
                    role == ModelRole.Existing ? (short)1 : (short)3);
                if (!session.TinVisible)
                    TinCadRenderer.SetVisible(doc.Database, session.TinLayer, false);
            }
            if (written != expectedCadFaces)
                throw new InvalidOperationException(
                    $"Khôi phục layer TIN không đầy đủ: cần {expectedCadFaces:n0} mặt, " +
                    $"đã tạo {written:n0}. Không cập nhật trạng thái mô hình.");
            session.TinCadFaceCount = written;
            session.TinCadIsPreview = false;
            try { doc.Editor.Regen(); } catch { }
        }

        public static string TinStatusText(ModelRole role)
        {
            var s = ProjectState.Current.Get(role);
            if (s.Source == null || s.Source.Entities.Count == 0)
                return $"{s.Name}: chưa nạp dữ liệu → {s.TinLayer}";
            if (s.Tin == null)
                return $"{s.Name}: CHƯA TẠO TIN → {s.TinLayer}";
            if (!s.IsTinCurrent)
                return $"{s.Name}: TIN CẦN CẬP NHẬT → {s.TinLayer}";
            return $"{s.Name}: TIN HỢP LỆ {s.Tin.Triangles.Count:n0} tam giác → {s.TinLayer}";
        }

        private static int CountActiveVertices(SurfaceModel model)
        {
            if (model == null) return 0;
            int count = 0;
            foreach (var entity in model.Entities)
                if (entity.IsEnabled)
                    foreach (var vertex in entity.Vertices)
                        if (vertex.IsEnabled) count++;
            return count;
        }

        public static void SetTinVisible(ModelRole role, bool visible)
        {
            var session = ProjectState.Current.Get(role);
            if (session.Tin == null) return;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            using (doc.LockDocument())
                TinCadRenderer.SetVisible(doc.Database, session.TinLayer, visible);
            session.TinVisible = visible;
            ProjectState.Current.NotifyChanged();
            try { doc.Editor.Regen(); } catch { }
        }
    }

    public sealed class SurfaceValidationException : Exception
    {
        public SurfaceValidationException(string modelName, IReadOnlyList<ValidationIssue> issues)
            : base($"Dữ liệu mô hình {modelName} còn lỗi, chưa thể dựng TIN.")
        {
            ModelName = modelName;
            Issues = issues;
        }

        public string ModelName { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }
    }
}
