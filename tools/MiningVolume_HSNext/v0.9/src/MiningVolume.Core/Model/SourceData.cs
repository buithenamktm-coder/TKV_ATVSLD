using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MiningVolume.Core.Geometry;

namespace MiningVolume.Core.Model
{
    public enum SourceEntityType
    {
        Point,
        Line,
        LwPolyline,
        Polyline2d,
        Polyline3d,
        Contour
    }

    public sealed class ModelVertex
    {
        public ModelVertex(int index, Vec3 position)
        {
            Index = index;
            Original = position;
            Position = position;
            IsEnabled = true;
        }

        public int Index { get; }
        public Vec3 Original { get; }
        public Vec3 Position { get; private set; }
        public bool IsEnabled { get; private set; }

        public void SetEnabled(bool enabled) => IsEnabled = enabled;
        public void OverrideZ(double z) => Position = new Vec3(Position.X, Position.Y, z);
        public void Reset()
        {
            Position = Original;
            IsEnabled = true;
        }
    }

    public sealed class SourceEntity
    {
        public SourceEntity(string id, string handle, string layer, SourceEntityType type, IEnumerable<Vec3> vertices, bool closed = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Handle = handle ?? string.Empty;
            Layer = layer ?? string.Empty;
            Type = type;
            Closed = closed;
            Vertices = vertices.Select((p, i) => new ModelVertex(i, p)).ToList();
            IsEnabled = true;
        }

        public string Id { get; }
        public string Handle { get; }
        public string Layer { get; }
        public SourceEntityType Type { get; }
        public bool Closed { get; }
        public bool IsEnabled { get; private set; }
        public List<ModelVertex> Vertices { get; }

        public bool IsBreakline => Type == SourceEntityType.Line || Type == SourceEntityType.LwPolyline ||
                                   Type == SourceEntityType.Polyline2d || Type == SourceEntityType.Polyline3d ||
                                   Type == SourceEntityType.Contour;

        public void SetEnabled(bool enabled) => IsEnabled = enabled;

        public IEnumerable<ModelVertex> ActiveVertices()
            => IsEnabled ? Vertices.Where(v => v.IsEnabled) : Enumerable.Empty<ModelVertex>();

        public IEnumerable<Segment3> ActiveBreaklineSegments()
        {
            if (!IsEnabled || !IsBreakline || Vertices.Count < 2) yield break;
            for (int i = 0; i < Vertices.Count - 1; i++)
            {
                var a = Vertices[i];
                var b = Vertices[i + 1];
                if (a.IsEnabled && b.IsEnabled)
                    yield return new Segment3(a.Position, b.Position, Id);
            }
            if (Closed && Vertices.Count > 2)
            {
                var a = Vertices[Vertices.Count - 1];
                var b = Vertices[0];
                if (a.IsEnabled && b.IsEnabled)
                    yield return new Segment3(a.Position, b.Position, Id);
            }
        }
    }

    public sealed class SurfaceModel
    {
        public SurfaceModel(string name) { Name = name ?? "Mô hình"; }
        private long _revision;

        public string Name { get; set; }
        public List<SourceEntity> Entities { get; } = new List<SourceEntity>();
        public DateTime LastModifiedUtc { get; private set; } = DateTime.UtcNow;
        public long Revision => Interlocked.Read(ref _revision);

        public void Touch()
        {
            LastModifiedUtc = DateTime.UtcNow;
            Interlocked.Increment(ref _revision);
        }
        public void RemoveEntityFromModel(string id)
        {
            var e = Entities.FirstOrDefault(x => x.Id == id);
            if (e != null) { e.SetEnabled(false); Touch(); }
        }
        public void RestoreEntity(string id)
        {
            var e = Entities.FirstOrDefault(x => x.Id == id);
            if (e != null) { e.SetEnabled(true); Touch(); }
        }
        public void SetVertexEnabled(string id, int vertexIndex, bool enabled)
        {
            var e = Entities.First(x => x.Id == id);
            e.Vertices[vertexIndex].SetEnabled(enabled);
            Touch();
        }
        public void OverrideVertexZ(string id, int vertexIndex, double z)
        {
            var e = Entities.First(x => x.Id == id);
            e.Vertices[vertexIndex].OverrideZ(z);
            Touch();
        }
    }
}