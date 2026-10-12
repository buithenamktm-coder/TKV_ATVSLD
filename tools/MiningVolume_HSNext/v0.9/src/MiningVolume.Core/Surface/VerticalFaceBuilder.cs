using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MiningVolume.Core.Geometry;

namespace MiningVolume.Core.Surface
{
    /// <summary>
    /// Builds actual 3D triangles with a vertical XY edge for coincident XY
    /// measurements that have different heights. CDT still triangulates one
    /// support node per XY; other Z samples are NEVER discarded from the source
    /// or silently averaged. The curtain is separate from the 2.5D sampler.
    /// Affected sections need review/acceptance before volume calculations.
    /// </summary>
    public static class VerticalFaceBuilder
    {
        private readonly struct Cell : IEquatable<Cell>
        {
            public readonly long X, Y;
            public Cell(long x, long y) { X = x; Y = y; }
            public bool Equals(Cell other) => X == other.X && Y == other.Y;
            public override bool Equals(object value) => value is Cell && Equals((Cell)value);
            public override int GetHashCode() { unchecked { return X.GetHashCode() * 397 ^ Y.GetHashCode(); } }
        }

        public static IReadOnlyList<Triangle3> Build(
            IReadOnlyList<Vec3> sites,
            IReadOnlyList<int> meshIndices,
            IReadOnlyList<ConflictGroup> conflicts,
            double xyTolerance,
            double zTolerance,
            CancellationToken token = default(CancellationToken))
        {
            if (sites == null || meshIndices == null || conflicts == null)
                throw new ArgumentNullException("sites/meshIndices/conflicts");
            if (xyTolerance <= 0 || zTolerance < 0)
                throw new ArgumentOutOfRangeException(nameof(xyTolerance));
            if (conflicts.Count == 0) return Array.Empty<Triangle3>();

            Cell At(Vec2 p) => new Cell(
                checked((long)Math.Floor(p.X / xyTolerance)),
                checked((long)Math.Floor(p.Y / xyTolerance)));
            var spatial = new Dictionary<Cell, List<int>>();
            for (int i = 0; i < sites.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var cell = At(sites[i].XY);
                if (!spatial.TryGetValue(cell, out var list))
                    spatial.Add(cell, list = new List<int>(1));
                list.Add(i);
            }

            // Distinct vertical groups are much fewer than ordinary survey
            // points. Index by normalized CDT site for one-pass mesh traversal.
            var elevations = new Dictionary<int, List<double>>();
            foreach (var group in conflicts)
            {
                token.ThrowIfCancellationRequested();
                var cell = At(group.Position);
                int site = -1;
                for (long dx = -1; dx <= 1 && site < 0; dx++)
                for (long dy = -1; dy <= 1 && site < 0; dy++)
                    if (spatial.TryGetValue(new Cell(cell.X + dx, cell.Y + dy), out var candidates))
                        foreach (int j in candidates)
                            if (sites[j].XY.DistanceTo(group.Position) <= xyTolerance)
                            { site = j; break; }
                if (site < 0)
                    throw new InvalidOperationException("Không tìm thấy nút CDT tại XY trùng Z; không được làm mất mặt đứng.");

                var levels = group.Sources.Select(x => x.Z).Concat(new[] { sites[site].Z })
                    .OrderBy(z => z).ToList();
                var distinct = new List<double>();
                foreach (double z in levels)
                    if (distinct.Count == 0 || z - distinct[distinct.Count - 1] > zTolerance)
                        distinct.Add(z);
                if (distinct.Count >= 2) elevations[site] = distinct;
            }

            var faces = new List<Triangle3>();
            var created = new HashSet<(int site, int neighbor, int level)>();
            var completedSites = new HashSet<int>();
            for (int i = 0; i + 2 < meshIndices.Count; i += 3)
            {
                token.ThrowIfCancellationRequested();
                int a = meshIndices[i], b = meshIndices[i + 1], c = meshIndices[i + 2];
                Add(a, b); Add(a, c);
                Add(b, a); Add(b, c);
                Add(c, a); Add(c, b);
            }
            foreach (int site in elevations.Keys)
                if (!completedSites.Contains(site))
                    throw new InvalidOperationException("Nút trùng XY không có tam giác kề; không thể dựng mặt đứng 3D.");

            return faces;

            void Add(int center, int neighbor)
            {
                if (!elevations.TryGetValue(center, out var levels)) return;
                var root = sites[center];
                var other = sites[neighbor];
                if (root.XY.DistanceTo(other.XY) <= xyTolerance) return;
                for (int j = 1; j < levels.Count; j++)
                {
                    if (!created.Add((center, neighbor, j))) continue;
                    completedSites.Add(center);
                    faces.Add(new Triangle3(
                        new Vec3(root.X, root.Y, levels[j - 1]),
                        new Vec3(root.X, root.Y, levels[j]),
                        other));
                }
            }
        }
    }
}
