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
        public void OverrideZ(double z) {
            if(double.IsNaN(z)||double.IsInfinity(z))throw new ArgumentOutOfRangeException(nameof(z));
            Position = new Vec3(Position.X, Position.Y, z);
        }
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
            OriginId = Id;
            Handle = handle ?? string.Empty;
            Layer = layer ?? string.Empty;
            Type = type;
            Closed = closed;
            Vertices = vertices.Select((p, i) => new ModelVertex(i, p)).ToList();
            IsEnabled = true;
        }

        public string Id { get; }
        public string OriginId { get; set; }
        public int? OriginSegmentIndex { get; set; }
        public bool IsGenerated { get; set; }
        public SourceEntityType? OriginType { get; set; }
        public IReadOnlyList<int> OriginVertexIndices { get; set; }
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

    public sealed class ModelEditRecord
    {
        public DateTime Utc { get; set; }
        public string User { get; set; }
        public string Action { get; set; }
        public string EntityId { get; set; }
        public string Handle { get; set; }
        public int VertexIndex { get; set; }
        public double OldZ { get; set; }
        public double NewZ { get; set; }
        public bool OldEnabled { get; set; }
        public bool NewEnabled { get; set; }
    }

    public sealed class SurfaceModel
    {
        public SurfaceModel(string name) { Name = name ?? "Mô hình"; }
        private long _revision;

        public string Name { get; set; }
        public List<SourceEntity> Entities { get; } = new List<SourceEntity>();
        public List<ModelEditRecord> EditHistory { get; } = new List<ModelEditRecord>();
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
        // Resolve every selected vertex BEFORE changing any source data.
        // An invalid row may not leave the model half-edited or pollute history.
        private sealed class SelectedVertex
        {
            public SourceEntity Entity;
            public ModelVertex Vertex;
        }

        private List<SelectedVertex> ResolveVertices(
            IEnumerable<KeyValuePair<string, int>> selection)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            var byId = Entities.ToDictionary(e => e.Id, StringComparer.Ordinal);
            var resolved = new List<SelectedVertex>();
            foreach (var item in selection.Distinct())
            {
                SourceEntity entity;
                if (item.Key == null || !byId.TryGetValue(item.Key, out entity))
                    throw new KeyNotFoundException("Không tìm thấy đối tượng nguồn: " + item.Key);
                if (item.Value < 0 || item.Value >= entity.Vertices.Count)
                    throw new ArgumentOutOfRangeException(nameof(selection),
                        "Chỉ số đỉnh nguồn không hợp lệ.");
                resolved.Add(new SelectedVertex
                {
                    Entity = entity,
                    Vertex = entity.Vertices[item.Value]
                });
            }
            return resolved;
        }

        public void ValidateVertexSelection(
            IEnumerable<KeyValuePair<string, int>> selection)
        {
            ResolveVertices(selection);
        }

        public void SetVerticesEnabled(
            IEnumerable<KeyValuePair<string, int>> selection, bool enabled)
        {
            var selected = ResolveVertices(selection);
            foreach (var item in selected)
            {
                var vertex = item.Vertex;
                Record(item.Entity, vertex, enabled ? "Enable vertex" : "Exclude vertex",
                    vertex.Position.Z, enabled);
                vertex.SetEnabled(enabled);
            }
            if (selected.Count != 0) Touch();
        }

        public void SetEntitiesEnabled(IEnumerable<string> ids, bool enabled)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            var selected = new HashSet<string>(ids, StringComparer.Ordinal);
            if (selected.Contains(null))
                throw new ArgumentException("Mã đối tượng nguồn không được rỗng.", nameof(ids));
            bool changed = false;
            foreach (var entity in Entities)
            {
                if (!selected.Contains(entity.Id)) continue;
                entity.SetEnabled(enabled);
                changed = true;
            }
            if (changed) Touch();
        }

        public void RestoreSelectedVertices(
            IEnumerable<KeyValuePair<string, int>> selection)
        {
            var selected = ResolveVertices(selection);
            foreach (var item in selected)
            {
                item.Entity.SetEnabled(true);
                var vertex = item.Vertex;
                Record(item.Entity, vertex, "Restore vertex", vertex.Original.Z, true);
                vertex.Reset();
            }
            if (selected.Count != 0) Touch();
        }

        public void SetVertexEnabled(string id, int vertexIndex, bool enabled)
        {
            var e = Entities.First(x => x.Id == id);
            var v=e.Vertices[vertexIndex];
            Record(e,v,enabled?"Enable vertex":"Exclude vertex",v.Position.Z,enabled);
            v.SetEnabled(enabled);
            Touch();
        }
        public void OverrideVertexZ(string id, int vertexIndex, double z)
        {
            var e = Entities.First(x => x.Id == id);
            if(double.IsNaN(z)||double.IsInfinity(z))throw new ArgumentOutOfRangeException(nameof(z));
            var v=e.Vertices[vertexIndex];
            Record(e,v,"Override Z",z,v.IsEnabled);
            v.OverrideZ(z);
            Touch();
        }
        public void RestoreVertex(string id,int vertexIndex)
        {
            var e=Entities.First(x=>x.Id==id);var v=e.Vertices[vertexIndex];
            Record(e,v,"Restore vertex",v.Original.Z,true);v.Reset();Touch();
        }
        public void RestoreVertices(IEnumerable<KeyValuePair<string,int>> selection)
        {
            var byId=Entities.ToDictionary(e=>e.Id,StringComparer.Ordinal);
            var vertices=selection.Distinct().Select(item=>new {Entity=byId[item.Key],Index=item.Value}).ToList();
            // Validate the complete selection before the first mutation.
            foreach(var item in vertices)if(item.Index<0||item.Index>=item.Entity.Vertices.Count)
                throw new ArgumentOutOfRangeException(nameof(selection));
            foreach(var item in vertices) {
                var v=item.Entity.Vertices[item.Index];Record(item.Entity,v,"Restore vertex",v.Original.Z,true);v.Reset();
            }
            if(vertices.Count>0)Touch();
        }
        private void Record(SourceEntity e,ModelVertex v,string action,double z,bool enabled)
        {
            EditHistory.Add(new ModelEditRecord {Utc=DateTime.UtcNow,User=Environment.UserName,
                Action=action,EntityId=e.Id,Handle=e.Handle,VertexIndex=v.Index,
                OldZ=v.Position.Z,NewZ=z,OldEnabled=v.IsEnabled,NewEnabled=enabled});
        }
    }
}
