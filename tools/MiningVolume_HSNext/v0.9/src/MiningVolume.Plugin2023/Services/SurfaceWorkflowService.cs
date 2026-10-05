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
        public DateTime SourceModifiedUtc { get; set; }
        public int TriangleCount => Tin?.Triangles?.Count ?? 0;

        public string Summary =>
            $"{SiteCount:n0} điểm TIN • {BreaklineCount:n0} đoạn breakline • {TriangleCount:n0} tam giác" +
            (WarningCount > 0 ? $" • {WarningCount:n0} cảnh báo" : string.Empty);
    }

    public static class SurfaceWorkflowService
    {
        public static SurfaceLoadResult LoadLayer(ModelRole role, string layer, ISet<SourceEntityType> allowedTypes)
        {
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

            var sourceModified = session.Source.LastModifiedUtc;
            var options = new SurfaceBuildOptions
            {
                XyTolerance = 1e-6,
                ZConflictTolerance = 1e-4,
                MinimumTriangleArea = 1e-10
            };

            var prepared = new SurfaceInputPreparer().Prepare(session.Source, options);
            if (prepared.HasErrors)
                throw new SurfaceValidationException(prepared.Issues);

            var tin = new ConformingTinBuilder().Build(session.Name, prepared, options);
            if (tin == null || tin.Triangles == null || tin.Triangles.Count == 0)
                throw new InvalidOperationException($"TIN {session.Name} không tạo được tam giác hợp lệ.");

            return new SurfaceBuildResult
            {
                Role = role,
                Tin = tin,
                SiteCount = prepared.Sites.Count,
                BreaklineCount = prepared.Breaklines.Count,
                WarningCount = prepared.Issues.Count(x => x.Severity == ValidationSeverity.Warning),
                SourceModifiedUtc = sourceModified
            };
        }

        // Backward-compatible core entry used by project restore.
        public static TinSurface BuildCore(ModelRole role) => BuildCoreDetailed(role).Tin;

        public static void DrawTin(ModelRole role, SurfaceBuildResult build)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            if (build.Role != role) throw new InvalidOperationException("Kết quả dựng TIN không đúng mô hình cần ghi.");
            DrawTinInternal(role, build.Tin, build.SourceModifiedUtc);
        }

        public static void DrawTin(ModelRole role, TinSurface tin)
        {
            var session = ProjectState.Current.Get(role);
            var stamp = session.Source?.LastModifiedUtc ?? DateTime.UtcNow;
            DrawTinInternal(role, tin, stamp);
        }

        private static void DrawTinInternal(ModelRole role, TinSurface tin, DateTime sourceModifiedUtc)
        {
            if (tin == null) throw new ArgumentNullException(nameof(tin));
            if (tin.Triangles == null || tin.Triangles.Count == 0)
                throw new InvalidOperationException("TIN không có tam giác hợp lệ.");

            var state = ProjectState.Current;
            var session = state.Get(role);
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
            using (doc.LockDocument())
                count = TinCadRenderer.CountFaces(doc.Database, session.TinLayer);

            if (count == session.Tin.Triangles.Count)
                return;

            // If the output layer was deleted/edited manually, rebuild its CAD
            // representation from the verified in-memory TIN before computation.
            DrawTinInternal(role, session.Tin, session.TinBuiltFromSourceUtc.Value);
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
        public SurfaceValidationException(IReadOnlyList<ValidationIssue> issues)
            : base("Dữ liệu mô hình còn lỗi, chưa thể dựng TIN.") { Issues = issues; }
        public IReadOnlyList<ValidationIssue> Issues { get; }
    }
}