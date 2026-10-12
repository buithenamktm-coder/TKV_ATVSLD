using System;
using System.Collections.Generic;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;

namespace MiningVolume.Core.Surface
{
    public enum ValidationSeverity { Info, Warning, Error }

    public enum DuplicateXYConflictPolicy
    {
        Stop = 0,
        // Numeric values retained ONLY to read legacy projects/API requests.
        // SurfaceBuildOptions always migrates these to Stop; no automatic Z choice.
        UseUpper = 1,
        UseLower = 2
    }

    public sealed class DuplicateXYConflictException : Exception
    {
        public DuplicateXYConflictException(string modelName, IReadOnlyList<ValidationIssue> issues)
            : base($"Dữ liệu mô hình {modelName} có xung đột cao độ Z. Mở Điểm trùng tọa độ để kiểm tra và xử lý nguồn.")
        {
            ModelName = modelName ?? "Mô hình";
            Issues = issues ?? Array.Empty<ValidationIssue>();
        }

        public string ModelName { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }
        public ConflictReport Report { get; set; }
    }

    public sealed class ValidationIssue
    {
        public ValidationIssue(ValidationSeverity severity, string code, string message, string sourceId = null)
        {
            Severity = severity; Code = code; Message = message; SourceId = sourceId ?? string.Empty;
        }
        public ValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string SourceId { get; }
    }

    public sealed class PreparedSurfaceInput
    {
        public ConflictReport ConflictReport { get; set; }
        // Groups retain every original elevation/provenance; CDT receives one
        // planar XY site and generates separate zero-plan-area 3D faces.
        public List<ConflictGroup> VerticalConflictGroups { get; } = new List<ConflictGroup>();
        public List<Vec3> Sites { get; } = new List<Vec3>();
        public List<Segment3> Breaklines { get; } = new List<Segment3>();
        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();
        public bool HasErrors => Issues.Exists(x => x.Severity == ValidationSeverity.Error);
    }

    public sealed class TinTileInfo
    {
        public TinTileInfo(int index, long byteOffset, int triangleCount,
            double minX, double minY, double maxX, double maxY)
        {
            Index = index;
            ByteOffset = byteOffset;
            TriangleCount = triangleCount;
            MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
        }
        public int Index { get; }
        public long ByteOffset { get; }
        public int TriangleCount { get; }
        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }

        public bool Intersects(double minX, double minY, double maxX, double maxY)
            => !(MaxX < minX || MinX > maxX || MaxY < minY || MinY > maxY);

        public bool Contains(Vec2 p, double pad = 0.0)
            => p.X >= MinX - pad && p.X <= MaxX + pad &&
               p.Y >= MinY - pad && p.Y <= MaxY + pad;
    }

    /// <summary>
    /// Nguồn tam giác TIN phân ô có thể nằm ngoài RAM. Các phép tính mặt cắt
    /// nên truy vấn theo tile thay vì quét toàn bộ hàng triệu tam giác.
    /// </summary>
    public interface ITiledTriangleSource : IReadOnlyList<Triangle3>, IDisposable
    {
        IReadOnlyList<TinTileInfo> Tiles { get; }
        IReadOnlyList<Triangle3> ReadTile(int tileIndex);
        IEnumerable<int> QueryTiles(double minX, double minY, double maxX, double maxY);
        bool IsFileBacked { get; }
    }

    public sealed class TinSurface
    {
        public TinSurface(string name, IReadOnlyList<Triangle3> triangles, IReadOnlyList<ValidationIssue> issues,
            IReadOnlyList<Triangle3> verticalFaces = null, IReadOnlyList<ConflictGroup> verticalConflicts = null,
            int sameXYZDuplicates = 0)
        {
            Name = name;
            Triangles = triangles;
            Issues = issues;
            VerticalFaces = verticalFaces ?? Array.Empty<Triangle3>();
            VerticalConflicts = verticalConflicts ?? Array.Empty<ConflictGroup>();
            SameXYZDuplicateVertices = sameXYZDuplicates;
        }
        public string Name { get; }
        public IReadOnlyList<Triangle3> Triangles { get; }
        // Never pass vertical faces to a single-Z-at-XY elevation sampler.
        public IReadOnlyList<Triangle3> VerticalFaces { get; }
        public IReadOnlyList<ConflictGroup> VerticalConflicts { get; }
        public int SameXYZDuplicateVertices { get; }
        public bool RequiresSectionReview => VerticalConflicts.Count > 0;
        public IReadOnlyList<ValidationIssue> Issues { get; }
    }

    public sealed class SurfaceBuildOptions
    {
        public double XyTolerance { get; set; } = 1e-6;
        public double ZConflictTolerance { get; set; } = 1e-4;
        public double MinimumTriangleArea { get; set; } = 1e-10;
        // Opt-in workflow only: keeps all original Z in a separate 3D curtain;
        // ordinary 2.5D / strict unit callers remain fail-closed by default.
        public bool PreserveDuplicateXYVerticalFaces { get; set; }
        public bool LegacyElevationPolicyIgnored { get; private set; }
        public DuplicateXYConflictPolicy DuplicateXYConflictPolicy
        {
            get => DuplicateXYConflictPolicy.Stop;
            set { if (value != DuplicateXYConflictPolicy.Stop) LegacyElevationPolicyIgnored = true; }
        }
        public IReadOnlyList<Vec2> ClipBoundary { get; set; }
    }

    public interface ITinBuilder
    {
        TinSurface Build(string name, PreparedSurfaceInput input, SurfaceBuildOptions options);
    }
}
