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
        public string Summary => $"{Entities:n0} đối tượng • {Vertices:n0} đỉnh";
    }

    public static class SurfaceWorkflowService
    {
        public static SurfaceLoadResult LoadLayer(ModelRole role, string layer, ISet<SourceEntityType> allowedTypes)
        {
            var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
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
            var session = ProjectState.Current.Get(role);
            session.Layer = layer;
            session.AllowedTypes.Clear();
            if (allowedTypes != null) foreach (var t in allowedTypes) session.AllowedTypes.Add(t);
            session.Source = model;
            session.Tin = null;
            session.LastBuiltUtc = null;
            ProjectState.Current.SectionProfiles.Clear();
            ProjectState.Current.VolumeResult = null;
            ProjectState.Current.NotifyChanged();

            return new SurfaceLoadResult
            {
                Entities = entities.Count,
                Vertices = entities.Sum(e => e.Vertices.Count)
            };
        }

        public static TinSurface BuildCore(ModelRole role)
        {
            var state = ProjectState.Current;
            var session = state.Get(role);
            if (session.Source == null || session.Source.Entities.Count == 0)
                throw new InvalidOperationException($"Mô hình {session.Name} chưa có dữ liệu.");

            var options = new SurfaceBuildOptions
            {
                XyTolerance = 1e-6,
                ZConflictTolerance = 1e-4,
                MinimumTriangleArea = 1e-10
            };
            var prepared = new SurfaceInputPreparer().Prepare(session.Source, options);
            if (prepared.HasErrors)
                throw new SurfaceValidationException(prepared.Issues);

            return new ConformingTinBuilder().Build(session.Name, prepared, options);
        }


        public static void DrawTin(ModelRole role, TinSurface tin)
        {
            if (tin == null) throw new ArgumentNullException(nameof(tin));
            var state = ProjectState.Current;
            var session = state.Get(role);
            var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            using (doc.LockDocument())
            {
                TinCadRenderer.Replace(doc.Database, session.TinLayer, tin, role == ModelRole.Existing ? (short)1 : (short)3);
                TinCadRenderer.SetVisible(doc.Database, session.TinLayer, true);
            }
            session.Tin = tin;
            session.LastBuiltUtc = DateTime.UtcNow;
            session.TinVisible = true;
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            state.NotifyChanged();
        }

        public static void SetTinVisible(ModelRole role, bool visible)
        {
            var session = ProjectState.Current.Get(role);
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            using (doc.LockDocument()) TinCadRenderer.SetVisible(doc.Database, session.TinLayer, visible);
            session.TinVisible = visible;
            ProjectState.Current.NotifyChanged();
        }
    }

    public sealed class SurfaceValidationException : Exception
    {
        public SurfaceValidationException(IReadOnlyList<ValidationIssue> issues)
            : base("Dữ liệu mô hình còn lỗi, chưa thể dựng TIN.") { Issues = issues; }
        public IReadOnlyList<ValidationIssue> Issues { get; }
    }
}