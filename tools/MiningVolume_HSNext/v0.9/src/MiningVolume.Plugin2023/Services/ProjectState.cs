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

    /// <summary>
    /// Danh mục đơn vị tham khảo thuộc TKV, đối chiếu tên theo cổng thông tin
    /// TKV (vinacomin.vn / web.vinacomin.vn), cập nhật 13/10/2026.
    /// Các đơn vị không có trong danh mục vẫn có thể nhập trực tiếp;
    /// không lưu mã chỉ mục để bảo toàn tên pháp lý trên DWG/Excel.
    /// </summary>
    public static class MiningUnitCatalog
    {
        // Keep existing serialized category labels for older V2 DWG projects.
        public const string Coal = "Than lộ thiên";
        public const string Mineral = "Quặng/khoáng sản lộ thiên";
        public const string TkvOther = "Đơn vị TKV khác";
        public const string OutsideTkv = "Đơn vị khác (ngoài TKV)";

        public static readonly IReadOnlyList<string> CoalUnits = Array.AsReadOnly(new[]
        {
            "Công ty Cổ phần Than Cao Sơn - TKV",
            "Công ty Cổ phần Than Đèo Nai - Cọc Sáu - TKV",
            "Công ty Cổ phần Than Hà Tu - Vinacomin",
            "Công ty Than Na Dương - VVMI",
            "Công ty Than Khánh Hòa - VVMI",
            "Công ty Than Núi Hồng - VVMI",
            "Công ty Cổ phần Than - Điện Nông Sơn - TKV",
            "Tổng công ty Công nghiệp mỏ Việt Bắc TKV - CTCP"
        });

        public static readonly IReadOnlyList<string> MineralUnits = Array.AsReadOnly(new[]
        {
            "Tổng công ty Khoáng sản TKV - CTCP",
            "Chi nhánh Mỏ tuyển đồng Sin Quyền, Lào Cai - Vimico",
            "Công ty Cổ phần Đồng Tả Phời - Vinacomin",
            "Công ty TNHH MTV Nhôm Lâm Đồng - TKV",
            "Công ty Nhôm Đắk Nông - TKV",
            "Công ty Cổ phần Khoáng sản 3 - Vimico",
            "Công ty Cổ phần Đất hiếm Lai Châu - Vimico",
            "Công ty Cổ phần Khoáng sản và Luyện kim Cao Bằng - Vimico",
            "Công ty Cổ phần Kim loại màu Thái Nguyên - Vimico",
            "Công ty Cổ phần Cromit Cổ Định Thanh Hoá - TKV",
            "Công ty Cổ phần Sắt Thạch Khê - Vinacomin"
        });

        public static readonly IReadOnlyList<string> OtherTkvUnits = Array.AsReadOnly(new[]
        {
            "Tập đoàn Công nghiệp Than - Khoáng sản Việt Nam",
            "Viện Khoa học Công nghệ mỏ - Vinacomin",
            "Viện Cơ khí năng lượng và mỏ - Vinacomin",
            "Công ty Than Nam Mẫu - TKV",
            "Công ty Than Thống Nhất - TKV",
            "Công ty Than Dương Huy - TKV",
            "Công ty Than Quang Hanh - TKV",
            "Công ty Than Khe Chàm - TKV",
            "Công ty Than Mạo Khê - TKV",
            "Công ty Than Hòn Gai - TKV",
            "Công ty Than Hạ Long - TKV",
            "Công ty Than Uông Bí - TKV",
            "Công ty Cổ phần Than Hà Lầm - Vinacomin",
            "Công ty Cổ phần Than Mông Dương - Vinacomin",
            "Công ty Cổ phần Than Núi Béo - Vinacomin",
            "Công ty Cổ phần Than Vàng Danh - Vinacomin",
            "Công ty Tuyển than Cửa Ông - TKV",
            "Công ty Tuyển than Hòn Gai - Vinacomin",
            "Công ty Chế biến Than Quảng Ninh - TKV",
            "Công ty Kho vận và Cảng Cẩm Phả - Vinacomin",
            "Công ty Kho vận Đá Bạc - Vinacomin",
            "Công ty Xây lắp mỏ - TKV",
            "Công ty TNHH MTV Môi trường - TKV",
            "Công ty Cổ phần Địa chất mỏ - TKV",
            "Công ty Cổ phần Địa chất Việt Bắc - TKV",
            "Công ty Cổ phần Địa chất và Khoáng sản - Vinacomin",
            "Công ty Cổ phần Tư vấn đầu tư Mỏ và Công nghiệp - Vinacomin",
            "Công ty Cổ phần Tin học, Công nghệ và Môi trường - Vinacomin",
            "Công ty Cổ phần Giám định - Vinacomin",
            "Công ty Cổ phần Vật tư - TKV",
            "Công ty Cổ phần Chế tạo máy - Vinacomin",
            "Công ty Cổ phần Cơ điện Uông Bí - Vinacomin",
            "Công ty Cổ phần Cơ khí Mạo Khê - Vinacomin",
            "Công ty Cổ phần Công nghiệp Ô tô - Vinacomin",
            "Công ty Cổ phần Vận tải và đưa đón thợ mỏ - Vinacomin",
            "Công ty Cổ phần Kinh doanh than Cẩm Phả - Vinacomin",
            "Công ty Cổ phần Kinh doanh than Miền Bắc - Vinacomin",
            "Công ty Cổ phần Xuất nhập khẩu Than - Vinacomin",
            "Công ty Cổ phần Hoa tiêu Hàng hải - TKV",
            "Công ty Cổ phần Du lịch và Thương mại - Vinacomin",
            "Công ty Cổ phần Gang thép Cao Bằng",
            "Công ty Cổ phần Xi măng La Hiên - VVMI",
            "Công ty Cổ phần Xi măng Quán Triều - VVMI",
            "Công ty Cổ phần Xi măng Tân Quang - VVMI",
            "Tổng công ty Công nghiệp Hóa chất mỏ - Vinacomin",
            "Tổng công ty Điện lực TKV - CTCP",
            "Công ty Cổ phần Nhiệt điện Cẩm Phả - TKV",
            "Công ty Thủy điện Đồng Nai 5 - TKV",
            "Trung tâm Cấp cứu mỏ - Vinacomin",
            "Trường Cao đẳng Than - Khoáng sản Việt Nam",
            "Trường Quản trị kinh doanh - Vinacomin",
            "Bệnh viện Than - Khoáng sản",
            "Ban Quản lý dự án chuyên ngành mỏ than - TKV"
        });

        public static bool IsKnownGroup(string group) =>
            string.Equals(group, Coal, StringComparison.Ordinal) ||
            string.Equals(group, Mineral, StringComparison.Ordinal) ||
            string.Equals(group, TkvOther, StringComparison.Ordinal) ||
            string.Equals(group, OutsideTkv, StringComparison.Ordinal);

        public static IReadOnlyList<string> ForCategory(string group)
        {
            if (string.Equals(group, Coal, StringComparison.Ordinal)) return CoalUnits;
            if (string.Equals(group, Mineral, StringComparison.Ordinal)) return MineralUnits;
            if (string.Equals(group, TkvOther, StringComparison.Ordinal)) return OtherTkvUnits;
            return MiningVolume.Core.Compatibility.EmptyArray<string>.Value; // Outside TKV: user enters full name.
        }
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
        public long? TinBuiltFromRegionRevision { get; set; }
        public bool TinVisible { get; set; } = true;
        public int TinCadFaceCount { get; set; }
        public bool TinCadIsPreview { get; set; }
        // A reopened DWG may contain the full, validated 3DFACE TIN while
        // the in-memory source data has not been restored from a legacy file.
        // This flag applies only to a checked CAD surface, never an arbitrary
        // DWG layer or a synthetic empty mesh.
        public bool TinRestoredFromCad { get; set; }
        // Fail closed if the previously saved surveyed X/Y/Z fingerprint no
        // longer matches the DWG. TIN CAD still exists but is not certified.
        public bool CadRecoveryBlocked { get; set; }
        public bool IsTinCurrent
        {
            get
            {
                return Tin != null &&
                       Tin.Triangles != null &&
                       Tin.Triangles.Count > 0 &&
                       TinBuiltFromSourceRevision.HasValue &&
                       Source != null &&
                       TinBuiltFromSourceRevision.Value == Source.Revision &&
                       TinBuiltFromRegionRevision.HasValue &&
                       TinBuiltFromRegionRevision.Value == ProjectState.Current.TinRegionRevision;
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
        // Mặt cắt than lộ thiên: TT 34/2018/TT-BCT, Điều 10 và Điều 12.
        // Users can select Custom and change every parameter; never silently
        // represent a noncompliant configuration as legally approved.
        public string SectionParameterMode { get; set; } = "TT34/2018/TT-BCT";
        public int SectionMapScale { get; set; } = 1000; // 1:1000 default; Custom scales are stored separately.
        // Grid-line elevation spacing on the drawn section (not bench band size).
        public double SectionAxisElevationInterval { get; set; } = 20.0;
        // Khoảng trống xuất hình CAD, không ảnh hưởng chiều dài tính khối lượng.
        public double SectionLayoutGap { get; set; } = 90.0;
        public double LevelStep { get; set; } = 5.0;
        public double FromLevel { get; set; }
        public double ToLevel { get; set; }
        public double HorizontalScale { get; set; } = 1000.0;
        public double VerticalScale { get; set; } = 1000.0;
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
            // Explicit MV_PROJECT_LOAD also calls Reset() within the SAME
            // drawing. Discard its cached grid result before disposing any
            // accepted old TIN, not only on AutoCAD DocumentActivated.
            GridWorkflowService.Invalidate();
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
            SectionParameterMode = "TT34/2018/TT-BCT";
            SectionMapScale = 1000;
            SectionAxisElevationInterval = 20.0;
            SectionLayoutGap = 90.0;
            LevelStep = 5.0;
            FromLevel = 0;
            ToLevel = 0;
            HorizontalScale = 1000.0;
            VerticalScale = 1000.0;
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
            s.TinBuiltFromRegionRevision = null;
            s.TinVisible = true;
            s.TinCadFaceCount = 0;
            s.TinCadIsPreview = false;
            s.TinRestoredFromCad = false;
            s.CadRecoveryBlocked = false;
            s.LastBuiltUtc = null;
            s.AllowedTypes.Clear();
        }

        private ProjectState() { }
    }
}