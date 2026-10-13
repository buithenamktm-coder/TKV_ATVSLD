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
        /// <summary>
        /// Clip the 3D upright triangles to the SAME polygon used for the 2.5D
        /// support mesh. These triangles have zero projected XY area, so the
        /// ordinary planar triangle clipper would silently discard them.
        /// Instead clip the root-to-neighbor XY segment, interpolate BOTH
        /// sloping Z edges and split each remaining trapezoid into triangles.
        /// No Z measurement is averaged, and faces never extend past the
        /// selected CAD border.
        /// </summary>
        public static IReadOnlyList<Triangle3> ClipToBoundary(
            IReadOnlyList<Triangle3> faces, IReadOnlyList<Vec2> boundary,
            double tolerance = 1e-7, CancellationToken token = default(CancellationToken))
        {
            if (faces == null) throw new ArgumentNullException(nameof(faces));
            if (boundary == null || boundary.Count < 3)
                throw new ArgumentException("Vùng tạo TIN phải có tối thiểu 3 đỉnh.", nameof(boundary));
            if (faces.Count == 0) return faces;
            var output = new List<Triangle3>(faces.Count);
            foreach (var face in faces)
            {
                token.ThrowIfCancellationRequested();
                var a = face.A;
                var b = face.B;
                var c = face.C;
                // VerticalFaceBuilder always places the duplicated XY root at
                // A and B. Reject unknown geometry instead of clipping wrongly.
                if (a.XY.DistanceTo(b.XY) > tolerance)
                    throw new InvalidOperationException(
                        "Mặt đứng không có cạnh AB cùng XY; không thể cắt vùng an toàn.");
                double dx = c.X - a.X, dy = c.Y - a.Y;
                double lengthSq = dx * dx + dy * dy;
                if (lengthSq <= tolerance * tolerance)
                    throw new InvalidOperationException(
                        "Mặt đứng suy biến trong XY; không thể cắt theo đường bao.");

                var horizontal = new Segment3(a, c, "VERTICAL_TIN_FACE");
                foreach (var kept in SurfaceRegionClipper.ClipSegment(
                    horizontal, boundary, tolerance))
                {
                    double t0 = Math.Max(0, Math.Min(1,
                        ((kept.A.X - a.X) * dx + (kept.A.Y - a.Y) * dy) / lengthSq));
                    double t1 = Math.Max(0, Math.Min(1,
                        ((kept.B.X - a.X) * dx + (kept.B.Y - a.Y) * dy) / lengthSq));
                    if (t1 - t0 <= 1e-12) continue;
                    Vec3 Low(double t) => new Vec3(
                        a.X + t * dx, a.Y + t * dy, a.Z + t * (c.Z - a.Z));
                    Vec3 High(double t) => new Vec3(
                        a.X + t * dx, a.Y + t * dy, b.Z + t * (c.Z - b.Z));
                    var lo0 = Low(t0); var hi0 = High(t0);
                    var lo1 = Low(t1); var hi1 = High(t1);
                    if (HasThreeDimensionalArea(lo0, hi0, lo1))
                        output.Add(new Triangle3(lo0, hi0, lo1));
                    if (HasThreeDimensionalArea(hi0, hi1, lo1))
                        output.Add(new Triangle3(hi0, hi1, lo1));
                }
            }
            return output;
        }

        // Native AutoCAD Face accepts all triangle vertex permutations.
        // Polygon clipping deliberately produces (AB) and (BC) upright edges.
        public static bool IsValidCadFace(Triangle3 face)
        {
            bool Finite(Vec3 p) =>
                !double.IsNaN(p.X) && !double.IsInfinity(p.X) &&
                !double.IsNaN(p.Y) && !double.IsInfinity(p.Y) &&
                !double.IsNaN(p.Z) && !double.IsInfinity(p.Z);
            if (!Finite(face.A) || !Finite(face.B) || !Finite(face.C))
                return false;
            bool VerticalPair(Vec3 x, Vec3 y, Vec3 other) =>
                x.XY.DistanceTo(y.XY) <= 1e-6 &&
                Math.Abs(x.Z - y.Z) > 1e-8 &&
                x.XY.DistanceTo(other.XY) > 1e-8;
            return HasThreeDimensionalArea(face.A, face.B, face.C) &&
                   (VerticalPair(face.A, face.B, face.C) ||
                    VerticalPair(face.B, face.C, face.A) ||
                    VerticalPair(face.C, face.A, face.B));
        }

        private static bool HasThreeDimensionalArea(Vec3 a, Vec3 b, Vec3 c)
        {
            double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
            double vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
            double x = uy * vz - uz * vy;
            double y = uz * vx - ux * vz;
            double z = ux * vy - uy * vx;
            return x * x + y * y + z * z > 1e-24;
        }

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
            // Do not allocate a List<int> for every ordinary survey point.
            // Large mine drawings can contain millions of unique XY cells.
            var first = new Dictionary<Cell, int>();
            var overflow = new Dictionary<Cell, List<int>>();
            for (int i = 0; i < sites.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var cell = At(sites[i].XY);
                if (!first.ContainsKey(cell)) first.Add(cell, i);
                else
                {
                    if (!overflow.TryGetValue(cell, out var extra))
                        overflow.Add(cell, extra = new List<int>(2));
                    extra.Add(i);
                }
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
                {
                    var key = new Cell(cell.X + dx, cell.Y + dy);
                    if (!first.TryGetValue(key, out int candidate)) continue;
                    if (sites[candidate].XY.DistanceTo(group.Position) <= xyTolerance)
                    { site = candidate; break; }
                    if (!overflow.TryGetValue(key, out var extra)) continue;
                    foreach (int j in extra)
                        if (sites[j].XY.DistanceTo(group.Position) <= xyTolerance)
                        { site = j; break; }
                }
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
