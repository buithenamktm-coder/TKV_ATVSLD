using System.Collections.Generic;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;

namespace MiningVolume.Core.Surface
{
    public enum ValidationSeverity { Info, Warning, Error }

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
        public List<Vec3> Sites { get; } = new List<Vec3>();
        public List<Segment3> Breaklines { get; } = new List<Segment3>();
        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();
        public bool HasErrors => Issues.Exists(x => x.Severity == ValidationSeverity.Error);
    }

    public sealed class TinSurface
    {
        public TinSurface(string name, IReadOnlyList<Triangle3> triangles, IReadOnlyList<ValidationIssue> issues)
        {
            Name = name;
            Triangles = triangles;
            Issues = issues;
        }
        public string Name { get; }
        public IReadOnlyList<Triangle3> Triangles { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }
    }

    public sealed class SurfaceBuildOptions
    {
        public double XyTolerance { get; set; } = 1e-6;
        public double ZConflictTolerance { get; set; } = 1e-4;
        public double MinimumTriangleArea { get; set; } = 1e-10;
        public IReadOnlyList<Vec2> ClipBoundary { get; set; }
    }

    public interface ITinBuilder
    {
        TinSurface Build(string name, PreparedSurfaceInput input, SurfaceBuildOptions options);
    }
}