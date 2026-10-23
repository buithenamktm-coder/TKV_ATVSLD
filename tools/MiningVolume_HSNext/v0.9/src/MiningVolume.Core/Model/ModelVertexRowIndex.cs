using System;
using System.Collections.Generic;

namespace MiningVolume.Core.Model
{
    /// <summary>
    /// A compact row index for a virtual DataGridView. An entity with 100,000
    /// vertices occupies ONE prefix entry, not 100,000 UI row objects.
    /// Rebuild only when the source identity/revision changes. Each visible
    /// row and selected vertex is resolved by binary search on demand.
    /// </summary>
    public sealed class ModelVertexRowIndex
    {
        private readonly int[] _endOffsets;
        private readonly SourceEntity[] _entities;

        public SurfaceModel Source { get; }
        public long Revision { get; }
        public int EntityCount { get; }
        public int RowCount { get; }
        public int ActiveVertexCount { get; }

        public ModelVertexRowIndex(SurfaceModel source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Revision = source.Revision;
            EntityCount = source.Entities.Count;
            var ends = new List<int>(EntityCount);
            var entities = new List<SourceEntity>(EntityCount);
            int rows = 0, active = 0;
            foreach (var entity in source.Entities)
            {
                if (entity == null)
                    throw new InvalidOperationException("Dữ liệu mô hình chứa đối tượng rỗng.");
                int count = entity.Vertices.Count;
                if (count == 0) continue;
                if (rows > int.MaxValue - count)
                    throw new InvalidOperationException(
                        "Số đỉnh nguồn vượt khả năng hiển thị của bảng AutoCAD.");
                rows += count;
                ends.Add(rows);
                entities.Add(entity);
                if (entity.IsEnabled)
                {
                    foreach (var vertex in entity.Vertices)
                        if (vertex.IsEnabled) active++;
                }
            }
            _endOffsets = ends.ToArray();
            _entities = entities.ToArray();
            RowCount = rows;
            ActiveVertexCount = active;
        }

        public bool TryGet(int row, out SourceEntity entity, out ModelVertex vertex)
        {
            entity = null;
            vertex = null;
            if (row < 0 || row >= RowCount) return false;
            // End offsets are strictly increasing because empty entities
            // were skipped. The first end >= (row+1) owns this vertex.
            int pos = Array.BinarySearch(_endOffsets, row + 1);
            if (pos < 0) pos = ~pos;
            int offset = pos == 0 ? 0 : _endOffsets[pos - 1];
            entity = _entities[pos];
            vertex = entity.Vertices[row - offset];
            return true;
        }
    }
}
