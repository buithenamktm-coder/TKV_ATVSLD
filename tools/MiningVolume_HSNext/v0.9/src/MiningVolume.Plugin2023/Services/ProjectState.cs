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

    /// <summary>Starter list for TKV coal and mineral surface operations.
    /// Editable in UI; a project may contain another organizational unit.</summary>
    public static class MiningUnitCatalog
    {
        public const string Coal = "Than lộ thiên";
        public const string Mineral = "Quặng/khoáng sản lộ thiên";
        public static readonly IReadOnlyList<string> CoalUnits = new[]
        {
            "Công ty Cổ phần Than Cao Sơn - TKV",
            "Công ty Cổ phần Than Đèo Nai - Cọc Sáu - TKV",
            "Công ty Cổ phần Than Hà Tu - Vinacomin",
            "Công ty Than Na Dương - VVMI",
            "Công ty Than Khánh Hòa - VVMI",
            "Công ty Than Núi Hồng - VVMI",
            "Công ty Cổ phần Than - Điện Nông Sơn - TKV"
        };
        public static readonly IReadOnlyList<string> MineralUnits = new[]
        {
            "Chi nhánh Mỏ tuyển đồng Sin Quyền, Lào Cai - Vimico",
            "Công ty Cổ phần Đồng Tả Phời - Vinacomin",
            "Công ty TNHH MTV Nhôm Lâm Đồng - TKV",
            "Công ty Nhôm Đắk Nông - TKV"
        };
        public static IReadOnlyList<string> ForCategory(string group) =>
            string.Equals(group, Mineral, StringComparison.Ordinal) ? MineralUnits : CoalUnits;
    }

    /// <summary>A reporting period is not a calculation timestamp.</summary>
    public static class VolumePeriodCatalog
    {
        public const string Month = "Tháng";
        public const string Quarter = "Quý";
        public const string Year = "Năm";
        public static string Display(string type, int number, int year)
        {
            if (year < 1900 || year > 2200) throw new ArgumentOutOfRangeException(nameof(year));
            if (type == Year) return "Năm " + year;
            if (type == Quarter && number >= 1 && number <= 4)
                return "Quý " + number + "/" + year;
            if (type == Month && number >= 1 && number <= 12)
                return "Tháng " + number.ToString("00") + "/" + year;
            throw new ArgumentOutOfRangeException(nameof(number), "Kỳ khối lượng chưa hợp lệ.");
        }
    }

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
        // A source revision alone is insufficient when the TIN's clipping
        // polygon changes during a background build. Every scope mutation
        // increments this independent generation counter.
        private long _tinRegionRevision;
        public long TinRegionRevision => System.Threading.Interlocked.Read(ref _tinRegionRevision);
        public void MarkTinRegionChanged() =>
            System.Threading.Interlocked.Increment(ref _tinRegionRevision);

        public Vec2? SectionDirection { get; set; }
        public SectionSystem SectionSystem { get; set; }
        public List<SectionProfile> SectionProfiles { get; } = new List<SectionProfile>();
        private VolumeResult _volumeResult;
        public VolumeResult VolumeResult {
            get=>_volumeResult;
            set { _volumeResult=value;VolumeCalculatedUtc=null;VolumeCalculationSeconds=null; }
        }
        public List<MineralBody> MineralBodies { get; } = new List<MineralBody>();
        public bool HasMineralInput => MineralBodies.Any(b => b.HasPairedOutcrops);
        public string GridBoundaryHandle { get; set; }
        public double GridSizeX { get; set; } = 20.0;
        public double GridSizeY { get; set; } = 20.0;
        public double GridRotationDegrees { get; set; }
        public double SectionSpacing { get; set; } = 20.0;
        // Khoảng trống xuất hình CAD, không ảnh hưởng chiều dài tính khối lượng.
        public double SectionLayoutGap { get; set; } = 90.0;
        public double LevelStep { get; set; } = 5.0;
        public double FromLevel { get; set; }
        public double ToLevel { get; set; }
        public double HorizontalScale { get; set; } = 1000.0;
        public double VerticalScale { get; set; } = 500.0;
        public string DeveloperName { get; set; } = "Bùi Thế Nam";
        public string DeveloperContact { get; set; } = "ĐT/Zalo: 0967280686";
        public string MiningUnitGroup { get; set; } = MiningUnitCatalog.Coal;
        public string MiningUnitName { get; set; } = string.Empty;
        public string VolumePeriodType { get; set; } = VolumePeriodCatalog.Month;
        public int VolumePeriodNumber { get; set; } = DateTime.Now.Month;
        public int VolumePeriodYear { get; set; } = DateTime.Now.Year;
        public string VolumePeriodLabel => VolumePeriodCatalog.Display(
            VolumePeriodType, VolumePeriodNumber, VolumePeriodYear);
        public DateTime? VolumeCalculatedUtc { get; set; }
        public double? VolumeCalculationSeconds { get; set; }
        public DateTime? GridCalculatedUtc { get; set; }
        public double? GridCalculationSeconds { get; set; }
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
            MarkTinRegionChanged();
            SectionDirection = null;
            SectionSystem = null;
            SectionProfiles.Clear();
            VolumeResult = null;
            VolumeCalculatedUtc = null;
            VolumeCalculationSeconds = null;
            GridCalculatedUtc = null;
            GridCalculationSeconds = null;
            MiningUnitGroup = MiningUnitCatalog.Coal;
            MiningUnitName = string.Empty;
            VolumePeriodType = VolumePeriodCatalog.Month;
            VolumePeriodNumber = DateTime.Now.Month;
            VolumePeriodYear = DateTime.Now.Year;
            MineralBodies.Clear();
            GridBoundaryHandle = null;
            GridSizeX = 20.0;
            GridSizeY = 20.0;
            GridRotationDegrees = 0;
            SectionSpacing = 20.0;
            SectionLayoutGap = 90.0;
            LevelStep = 5.0;
            FromLevel = 0;
            ToLevel = 0;
            HorizontalScale = 1000.0;
            VerticalScale = 500.0;
            DeveloperName = "Bùi Thế Nam";
            DeveloperContact = "ĐT/Zalo: 0967280686";
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