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
        public long PrepareMilliseconds { get; set; }
        public long TriangulationMilliseconds { get; set; }
        internal SurfaceModel SourceModel { get; set; }
        public int TriangleCount => Tin?.Triangles?.Count ?? 0;
        public long CoreMilliseconds => PrepareMilliseconds + TriangulationMilliseconds;
        public bool IsLargeDataset { get; set; }
        public int TileCount { get; set; }
        public IReadOnlyList<MiningVolume.Core.Geometry.Triangle3> PreviewTriangles { get; set; }

        public string Summary =>
            $"{SiteCount:n0} điểm TIN • {BreaklineCount:n0} đoạn breakline • {TriangleCount:n0} tam giác" +
            (IsLargeDataset ? $" • TIN dữ liệu lớn {TileCount:n0} ô" : string.Empty) +
            $" • Z: {MinZ:0.###} → {MaxZ:0.###}" +
            $" • dựng {CoreMilliseconds / 1000.0:0.00}s" +
            (WarningCount > 0 ? $" • {WarningCount:n0} cảnh báo" : string.Empty);
    }

    public static class SurfaceWorkflowService
    {
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
                    "hoặc chỉ thuộc layer TIN đầu ra của MiningVolume.");

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
            var options = new SurfaceBuildOptions
            {
                XyTolerance = 1e-6,
                ZConflictTolerance = 1e-4,
                MinimumTriangleArea = 1e-10,
                DuplicateXYConflictPolicy = duplicateXYPolicy
            };

            cancellationToken.ThrowIfCancellationRequested();

            // Dữ liệu mỏ lớn không được đẩy vào một phép Bowyer-Watson toàn cục.
            // Từ ngưỡng này chuyển sang TIN phân ô có halo, giữ breakline và kiểm
            // tra ổn định biên. Kiến trúc này hướng tới cỡ ~10 triệu đỉnh/mô hình.
            if (session.ActiveVertexCount >= LargeDatasetVertexThreshold)
            {
                progress?.Invoke(
                    $"Dữ liệu lớn: {session.ActiveVertexCount:n0} đỉnh. " +
                    "Đang dựng TIN phân ô, không giảm điểm âm thầm...");

                var tiled = new TiledConformingTinBuilder().Build(
                    session.Name,
                    source,
                    options,
                    (done, total, message) => progress?.Invoke(message),
                    cancellationToken);

                if (tiled.Surface == null || tiled.Surface.Triangles == null ||
                    tiled.Surface.Triangles.Count == 0)
                    throw new InvalidOperationException($"TIN {session.Name} không tạo được tam giác hợp lệ.");

                if (!ReferenceEquals(session.Source, source) || source.Revision != sourceRevision)
                    throw new InvalidOperationException(
                        $"Dữ liệu {session.Name} đã thay đổi trong lúc đang dựng TIN. " +
                        "Kết quả tạm bị hủy; hãy tạo/cập nhật TIN lại.");

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
                    PrepareMilliseconds = tiled.PrepareMilliseconds,
                    TriangulationMilliseconds = tiled.TriangulationMilliseconds,
                    SourceModel = source,
                    IsLargeDataset = true,
                    TileCount = tiled.TileCount,
                    PreviewTriangles = tiled.PreviewTriangles
                };
            }

            var prepareWatch = Stopwatch.StartNew();
            var prepared = new SurfaceInputPreparer().Prepare(source, options);
            prepareWatch.Stop();
            if (prepared.HasErrors)
            {
                var duplicateIssues = prepared.Issues
                    .Where(x => x.Severity == ValidationSeverity.Error &&
                        (string.Equals(x.Code, "DUPLICATE_XY_CONFLICT_Z", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Code, "POINT_ON_BREAKLINE_Z_CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Code, "BREAKLINE_CROSSING_Z_CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Code, "BREAKLINE_OVERLAP_Z_CONFLICT", StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (duplicateIssues.Count > 0)
                    throw new DuplicateXYConflictException(session.Name, duplicateIssues);

                throw new SurfaceValidationException(session.Name, prepared.Issues);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var triangulationWatch = Stopwatch.StartNew();
            var tin = new ConformingTinBuilder().Build(session.Name, prepared, options);
            triangulationWatch.Stop();
            if (tin == null || tin.Triangles == null || tin.Triangles.Count == 0)
                throw new InvalidOperationException($"TIN {session.Name} không tạo được tam giác hợp lệ.");

            if (!ReferenceEquals(session.Source, source) || source.Revision != sourceRevision)
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
                MaxZ = prepared.Sites.Max(p => p.Z),
                SourceModifiedUtc = sourceModified,
                SourceRevision = sourceRevision,
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
            return new[]
            {
                BuildCoreDetailed(ModelRole.Existing,
                    message => progress?.Invoke("Hiện trạng: " + message),
                    cancellationToken,
                    existingPolicy),
                BuildCoreDetailed(ModelRole.Design,
                    message => progress?.Invoke("Thiết kế: " + message),
                    cancellationToken,
                    designPolicy)
            };
        }

        public static void DrawTinPair(SurfaceBuildResult[] builds, Action<string> progress = null)
        {
            if (builds == null || builds.Length != 2)
                throw new ArgumentException("Cặp kết quả TIN không hợp lệ.", nameof(builds));

            var existing = builds.FirstOrDefault(x => x.Role == ModelRole.Existing);
            var design = builds.FirstOrDefault(x => x.Role == ModelRole.Design);
            if (existing == null || design == null)
                throw new InvalidOperationException("Thiếu kết quả TIN hiện trạng hoặc TIN thiết kế.");

            try
            {
                progress?.Invoke($"Đang ghi TIN hiện trạng xuống AutoCAD: {existing.TriangleCount:n0} tam giác...");
                DrawTin(ModelRole.Existing, existing);
                progress?.Invoke($"Đang ghi TIN thiết kế xuống AutoCAD: {design.TriangleCount:n0} tam giác...");
                DrawTin(ModelRole.Design, design);

                // Both layers were just written under document lock by this workflow.
                // Re-scanning the whole ModelSpace immediately would be redundant and
                // very expensive on production mine drawings.
                EnsureBothTinsReady(synchronizeCadLayers: false);
                progress?.Invoke("Đã ghi xong cặp TIN và kiểm tra trạng thái nguồn.");
            }
            catch
            {
                // A pair is an atomic calculation prerequisite. Never leave one new TIN
                // paired with an old/missing counterpart after a write failure.
                InvalidateTin(ModelRole.Existing, clearCadLayer: true, notify: false);
                InvalidateTin(ModelRole.Design, clearCadLayer: true, notify: false);
                ProjectState.Current.NotifyChanged();
                throw;
            }
        }

        public static void DrawTin(ModelRole role, SurfaceBuildResult build)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            if (build.Role != role) throw new InvalidOperationException("Kết quả dựng TIN không đúng mô hình cần ghi.");
            DrawTinInternal(
                role,
                build.Tin,
                build.SourceModel,
                build.SourceRevision,
                build.SourceModifiedUtc,
                build.IsLargeDataset ? build.PreviewTriangles : null);
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

            var doc = Application.DocumentManager.MdiActiveDocument ??
                      throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");

            bool cadPreview = previewTriangles != null && previewTriangles.Count > 0 &&
                              previewTriangles.Count < tin.Triangles.Count;
            var cadTin = cadPreview
                ? new TinSurface(session.Name + " - CAD preview", previewTriangles, Array.Empty<ValidationIssue>())
                : tin;

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

            int expectedCadFaces = cadTin.Triangles.Count;
            if (written != expectedCadFaces)
                throw new InvalidOperationException(
                    $"Ghi TIN {session.Name} xuống AutoCAD không đầy đủ: cần {expectedCadFaces:n0} mặt CAD, " +
                    $"đã ghi {written:n0}.");

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
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            state.NotifyChanged();

            try { doc.Editor.Regen(); } catch { }
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

        public static void EnsureBothTinsReady(bool synchronizeCadLayers)
        {
            EnsureTinReady(ModelRole.Existing, synchronizeCadLayers);
            EnsureTinReady(ModelRole.Design, synchronizeCadLayers);
        }

        public static void EnsureTinReady(ModelRole role, bool synchronizeCadLayer)
        {
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

            int expectedCadFaces = session.TinCadFaceCount > 0
                ? session.TinCadFaceCount
                : session.Tin.Triangles.Count;
            if (exists && count == expectedCadFaces)
                return;

            // If the output layer was deleted/edited manually, rebuild its CAD
            // representation from the verified in-memory TIN before computation.
            IReadOnlyList<MiningVolume.Core.Geometry.Triangle3> preview = null;
            if (session.TinCadIsPreview && session.Tin.Triangles is ITiledTriangleSource tiled)
                preview = BuildCadPreview(tiled, 200000);

            DrawTinInternal(
                role,
                session.Tin,
                session.Source,
                session.TinBuiltFromSourceRevision.Value,
                session.TinBuiltFromSourceUtc ?? session.Source.LastModifiedUtc,
                preview);
        }

        private static IReadOnlyList<MiningVolume.Core.Geometry.Triangle3> BuildCadPreview(
            ITiledTriangleSource tiled,
            int maxFaces)
        {
            var result = new List<MiningVolume.Core.Geometry.Triangle3>(
                Math.Min(maxFaces, tiled.Count));
            int tileCount = Math.Max(1, tiled.Tiles.Count);
            int perTile = Math.Max(1, maxFaces / tileCount);

            foreach (var tile in tiled.Tiles)
            {
                var tris = tiled.ReadTile(tile.Index);
                if (tris.Count == 0) continue;
                int step = Math.Max(1, (int)Math.Ceiling(tris.Count / (double)perTile));
                for (int i = 0; i < tris.Count && result.Count < maxFaces; i += step)
                    result.Add(tris[i]);
                if (result.Count >= maxFaces) break;
            }
            return result;
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