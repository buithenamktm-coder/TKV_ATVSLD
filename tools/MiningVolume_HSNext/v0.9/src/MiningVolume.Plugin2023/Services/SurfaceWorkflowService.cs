using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
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
        internal SurfaceModel SourceModel { get; set; }
        public int TriangleCount => Tin?.Triangles?.Count ?? 0;

        public string Summary =>
            $"{SiteCount:n0} điểm TIN • {BreaklineCount:n0} đoạn breakline • {TriangleCount:n0} tam giác" +
            $" • Z: {MinZ:0.###} → {MaxZ:0.###}" +
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

            var model = new SurfaceModel(role == ModelRole.Existing ? "Hiện trạng" : "Thiết kế");
            model.Entities.AddRange(entities);
            model.Touch();

            var session = ProjectState.Current.Get(role);
            session.Layer = layer;
            session.AllowedTypes.Clear();
            if (allowedTypes != null)
                foreach (var t in allowedTypes) session.AllowedTypes.Add(t);
            session.Source = model;

            // Loading/reloading source data invalidates the previous TIN completely.
            // Clear the dedicated output layer so stale faces can never be mistaken for
            // the current surface.
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

        public static SurfaceBuildResult BuildCoreDetailed(ModelRole role)
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
                MinimumTriangleArea = 1e-10
            };

            var prepared = new SurfaceInputPreparer().Prepare(source, options);
            if (prepared.HasErrors)
                throw new SurfaceValidationException(session.Name, prepared.Issues);

            var tin = new ConformingTinBuilder().Build(session.Name, prepared, options);
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
                SourceModel = source
            };
        }

        // Backward-compatible core entry used by project restore.
        public static TinSurface BuildCore(ModelRole role) => BuildCoreDetailed(role).Tin;

        public static SurfaceBuildResult[] BuildPairCoreDetailed()
        {
            // Build both pure-core surfaces before touching the DWG. If either fails,
            // neither CAD TIN is replaced.
            return new[]
            {
                BuildCoreDetailed(ModelRole.Existing),
                BuildCoreDetailed(ModelRole.Design)
            };
        }

        public static void DrawTinPair(SurfaceBuildResult[] builds)
        {
            if (builds == null || builds.Length != 2)
                throw new ArgumentException("Cặp kết quả TIN không hợp lệ.", nameof(builds));

            var existing = builds.FirstOrDefault(x => x.Role == ModelRole.Existing);
            var design = builds.FirstOrDefault(x => x.Role == ModelRole.Design);
            if (existing == null || design == null)
                throw new InvalidOperationException("Thiếu kết quả TIN hiện trạng hoặc TIN thiết kế.");

            try
            {
                DrawTin(ModelRole.Existing, existing);
                DrawTin(ModelRole.Design, design);
                EnsureBothTinsReady(synchronizeCadLayers: true);
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
            DrawTinInternal(role, build.Tin, build.SourceModel, build.SourceRevision, build.SourceModifiedUtc);
        }

        public static void DrawTin(ModelRole role, TinSurface tin)
        {
            var session = ProjectState.Current.Get(role);
            var source = session.Source ?? throw new InvalidOperationException("Mô hình chưa có dữ liệu nguồn.");
            DrawTinInternal(role, tin, source, source.Revision, source.LastModifiedUtc);
        }

        private static void DrawTinInternal(ModelRole role, TinSurface tin, SurfaceModel sourceModel, long sourceRevision, DateTime sourceModifiedUtc)
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

            int written;
            int verified;
            using (doc.LockDocument())
            {
                written = TinCadRenderer.Replace(
                    doc.Database,
                    session.TinLayer,
                    tin,
                    role == ModelRole.Existing ? (short)1 : (short)3);
                TinCadRenderer.SetVisible(doc.Database, session.TinLayer, true);
                verified = TinCadRenderer.CountFaces(doc.Database, session.TinLayer);
            }

            if (written != tin.Triangles.Count || verified != tin.Triangles.Count)
                throw new InvalidOperationException(
                    $"Ghi TIN {session.Name} xuống AutoCAD không đầy đủ: lõi có {tin.Triangles.Count:n0} tam giác, " +
                    $"đã ghi {written:n0}, kiểm tra trên layer còn {verified:n0}.");

            session.Tin = tin;
            session.TinBuiltFromSourceUtc = sourceModifiedUtc;
            session.TinBuiltFromSourceRevision = sourceRevision;
            session.LastBuiltUtc = DateTime.UtcNow;
            session.TinVisible = true;
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
            session.Tin = null;
            session.TinBuiltFromSourceUtc = null;
            session.TinBuiltFromSourceRevision = null;
            session.LastBuiltUtc = null;
            session.TinVisible = false;

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

            if (exists && count == session.Tin.Triangles.Count)
                return;

            // If the output layer was deleted/edited manually, rebuild its CAD
            // representation from the verified in-memory TIN before computation.
            DrawTinInternal(
                role,
                session.Tin,
                session.Source,
                session.TinBuiltFromSourceRevision.Value,
                session.TinBuiltFromSourceUtc ?? session.Source.LastModifiedUtc);
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