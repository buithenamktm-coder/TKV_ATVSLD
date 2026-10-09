using System;
using MiningVolume.Core.Model;
using MiningVolume.Core.Surface;
using MiningVolume.Core.Sections;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Volumes;
using System.Collections.Generic;
using System.Linq;

namespace MiningVolume2023.Services
{
    public enum ModelRole { Existing, Design }
    public enum SourceSelectionMode { Layer = 0, ManualSelection = 1 }

    public sealed class ModelSession
    {
        public ModelSession(ModelRole role, string name, string tinLayer)
        {
            Role = role;
            Name = name;
            TinLayer = tinLayer;
            Source = new SurfaceModel(name);
        }
        public ModelRole Role { get; }
        public string Name { get; }
        public string Layer { get; set; }
        public SourceSelectionMode SourceMode { get; set; } = SourceSelectionMode.Layer;
        public List<string> SelectedHandles { get; } = new List<string>();
        public string TinLayer { get; }
        public SurfaceModel Source { get; set; }
        public TinSurface Tin { get; set; }
        public DateTime? TinBuiltFromSourceUtc { get; set; }
        public long? TinBuiltFromSourceRevision { get; set; }
        public bool TinVisible { get; set; } = true;
        public int TinCadFaceCount { get; set; }
        public bool TinCadIsPreview { get; set; }
        public bool IsTinCurrent
        {
            get
            {
                return Tin != null &&
                       Tin.Triangles != null &&
                       Tin.Triangles.Count > 0 &&
                       TinBuiltFromSourceRevision.HasValue &&
                       Source != null &&
                       TinBuiltFromSourceRevision.Value == Source.Revision;
            }
        }
        public HashSet<SourceEntityType> AllowedTypes { get; } = new HashSet<SourceEntityType>();
        public DateTime? LastBuiltUtc { get; set; }
        public int EntityCount => Source?.Entities?.Count ?? 0;
        public int VertexCount
        {
            get
            {
                int n = 0;
                if (Source == null) return n;
                foreach (var e in Source.Entities) n += e.Vertices.Count;
                return n;
            }
        }
        public int ActiveVertexCount
        {
            get
            {
                int n = 0;
                if (Source == null) return n;
                foreach (var e in Source.Entities)
                    if (e.IsEnabled)
                        foreach (var v in e.Vertices) if (v.IsEnabled) n++;
                return n;
            }
        }
    }

    public sealed class ProjectState
    {
        private static readonly ProjectState _current = new ProjectState();
        public static ProjectState Current => _current;
        public ModelSession Existing { get; } = new ModelSession(ModelRole.Existing, "Hiện trạng", "MV_TIN_HIENTRANG");
        public ModelSession Design { get; } = new ModelSession(ModelRole.Design, "Thiết kế", "MV_TIN_THIETKE");
        public ModelRole ActiveRole { get; set; } = ModelRole.Existing;
        public string BoundaryHandle { get; set; }

        // V1.0: vùng tùy chọn dùng để chỉ dựng TIN trong khu vực cần tính.
        // Polygon được chụp tại thời điểm chọn để phần dựng core không gọi AutoCAD API
        // từ background thread.
        public string TinRegionHandle { get; set; }
        public List<Vec2> TinRegionPolygon { get; } = new List<Vec2>();
        public bool HasTinRegion => TinRegionPolygon.Count >= 3;

        public Vec2? SectionDirection { get; set; }
        public SectionSystem SectionSystem { get; set; }
        public List<SectionProfile> SectionProfiles { get; } = new List<SectionProfile>();
        public VolumeResult VolumeResult { get; set; }
        public double SectionSpacing { get; set; } = 20.0;
        public double LevelStep { get; set; } = 5.0;
        public double FromLevel { get; set; }
        public double ToLevel { get; set; }
        public double HorizontalScale { get; set; } = 1000.0;
        public double VerticalScale { get; set; } = 500.0;
        public string DeveloperName { get; set; } = "Bùi Thế Nam";
        public string DeveloperContact { get; set; } = string.Empty;
        public DateTime? LastProjectSavedUtc { get; set; }
        public event EventHandler Changed;

        public ModelSession Active => ActiveRole == ModelRole.Existing ? Existing : Design;
        public ModelSession Get(ModelRole role) => role == ModelRole.Existing ? Existing : Design;
        public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

        public void Reset()
        {
            ResetModel(Existing);
            ResetModel(Design);
            ActiveRole = ModelRole.Existing;
            BoundaryHandle = null;
            TinRegionHandle = null;
            TinRegionPolygon.Clear();
            SectionDirection = null;
            SectionSystem = null;
            SectionProfiles.Clear();
            VolumeResult = null;
            SectionSpacing = 20.0;
            LevelStep = 5.0;
            FromLevel = 0;
            ToLevel = 0;
            HorizontalScale = 1000.0;
            VerticalScale = 500.0;
            DeveloperName = "Bùi Thế Nam";
            DeveloperContact = string.Empty;
            LastProjectSavedUtc = null;
        }

        private static void ResetModel(ModelSession s)
        {
            try { (s.Tin?.Triangles as IDisposable)?.Dispose(); } catch { }
            s.Layer = null;
            s.SourceMode = SourceSelectionMode.Layer;
            s.SelectedHandles.Clear();
            s.Source = new SurfaceModel(s.Name);
            s.Tin = null;
            s.TinBuiltFromSourceUtc = null;
            s.TinBuiltFromSourceRevision = null;
            s.TinVisible = true;
            s.TinCadFaceCount = 0;
            s.TinCadIsPreview = false;
            s.LastBuiltUtc = null;
            s.AllowedTypes.Clear();
        }

        private ProjectState() { }
    }
}