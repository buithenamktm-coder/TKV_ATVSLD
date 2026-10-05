using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Surface;

namespace MiningVolume.Surface
{
    /// <summary>
    /// Dựng TIN ràng buộc không phụ thuộc thư viện hình học bên ngoài.
    /// Quy trình:
    /// 1) Bowyer-Watson tạo Delaunay từ các site XY.
    /// 2) Khôi phục từng cạnh breakline bằng edge-flip; breakline đã khôi phục được khóa.
    /// 3) Loại tam giác ngoài ClipBoundary (nếu có) và xuất Triangle3 với Z gốc.
    ///
    /// Dữ liệu đầu vào phải được SurfaceInputPreparer kiểm tra trước: không trùng XY khác Z,
    /// không có breakline cắt/chồng lấn và breakline đã được tách tại mọi site nằm trên tuyến.
    /// </summary>
    public sealed class ConformingTinBuilder : ITinBuilder
    {
        private struct Tri
        {
            public int A, B, C;
            public Tri(int a, int b, int c) { A = a; B = b; C = c; }
            public bool Has(int v) { return A == v || B == v || C == v; }
        }

        private struct EdgeKey : IEquatable<EdgeKey>
        {
            public readonly int A, B;
            public EdgeKey(int a, int b)
            {
                if (a < b) { A = a; B = b; }
                else { A = b; B = a; }
            }
            public bool Equals(EdgeKey other) { return A == other.A && B == other.B; }
            public override bool Equals(object obj) { return obj is EdgeKey && Equals((EdgeKey)obj); }
            public override int GetHashCode() { unchecked { return (A * 397) ^ B; } }
            public override string ToString() { return A + "-" + B; }
        }

        public TinSurface Build(string name, PreparedSurfaceInput input, SurfaceBuildOptions options)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            options = options ?? new SurfaceBuildOptions();
            if (input.HasErrors)
                throw new InvalidOperationException("Dữ liệu mô hình còn lỗi. Không dựng TIN trước khi xử lý các lỗi Error.");
            if (input.Sites.Count < 3)
                throw new InvalidOperationException("TIN cần tối thiểu 3 điểm hợp lệ.");

            var vertices = new List<Vec3>(input.Sites);
            int realCount = vertices.Count;
            var tris = BowyerWatson(vertices, realCount, options.XyTolerance);
            if (tris.Count == 0)
                throw new InvalidOperationException("Không tạo được tam giác Delaunay từ dữ liệu đầu vào.");

            var locked = new HashSet<EdgeKey>();
            foreach (var seg in input.Breaklines)
            {
                int a = FindSiteIndex(vertices, realCount, seg.A, options.XyTolerance);
                int b = FindSiteIndex(vertices, realCount, seg.B, options.XyTolerance);
                if (a < 0 || b < 0)
                    throw new InvalidOperationException("Không ánh xạ được đầu mút breakline vào site TIN: " + seg.SourceId);
                if (a == b) continue;

                RecoverConstraint(tris, vertices, new EdgeKey(a, b), locked, options.XyTolerance);
                locked.Add(new EdgeKey(a, b));
            }

            var output = new List<Triangle3>();
            foreach (var t in tris)
            {
                if (t.A >= realCount || t.B >= realCount || t.C >= realCount) continue;
                var tri = new Triangle3(vertices[t.A], vertices[t.B], vertices[t.C]);
                if (tri.Area2D <= options.MinimumTriangleArea) continue;
                if (options.ClipBoundary != null && options.ClipBoundary.Count >= 3 &&
                    !Geometry2D.PointInPolygon(tri.Centroid2D, options.ClipBoundary, options.XyTolerance))
                    continue;
                output.Add(tri);
            }

            if (output.Count == 0)
                throw new InvalidOperationException("TIN không tạo được tam giác hợp lệ trong phạm vi tính.");

            return new TinSurface(name, output, input.Issues.ToArray());
        }

        private static List<Tri> BowyerWatson(List<Vec3> vertices, int realCount, double tol)
        {
            double minX = vertices[0].X, maxX = vertices[0].X;
            double minY = vertices[0].Y, maxY = vertices[0].Y;
            for (int i = 1; i < realCount; i++)
            {
                minX = Math.Min(minX, vertices[i].X); maxX = Math.Max(maxX, vertices[i].X);
                minY = Math.Min(minY, vertices[i].Y); maxY = Math.Max(maxY, vertices[i].Y);
            }
            double cx = (minX + maxX) * 0.5, cy = (minY + maxY) * 0.5;
            double span = Math.Max(maxX - minX, maxY - minY);
            if (span <= tol) throw new InvalidOperationException("Các điểm TIN gần như trùng nhau trong XY.");
            double r = span * 64.0 + 1.0;

            int s0 = vertices.Count; vertices.Add(new Vec3(cx - 2.0 * r, cy - r, 0));
            int s1 = vertices.Count; vertices.Add(new Vec3(cx + 2.0 * r, cy - r, 0));
            int s2 = vertices.Count; vertices.Add(new Vec3(cx, cy + 2.0 * r, 0));

            var tris = new List<Tri> { MakeCcw(s0, s1, s2, vertices) };
            for (int p = 0; p < realCount; p++)
            {
                var bad = new List<int>();
                for (int i = 0; i < tris.Count; i++)
                    if (InCircumcircle(vertices[p].XY, tris[i], vertices, tol)) bad.Add(i);

                var boundaryCount = new Dictionary<EdgeKey, int>();
                for (int k = 0; k < bad.Count; k++)
                {
                    var t = tris[bad[k]];
                    AddEdgeCount(boundaryCount, new EdgeKey(t.A, t.B));
                    AddEdgeCount(boundaryCount, new EdgeKey(t.B, t.C));
                    AddEdgeCount(boundaryCount, new EdgeKey(t.C, t.A));
                }

                var badSet = new HashSet<int>(bad);
                var kept = new List<Tri>(tris.Count + boundaryCount.Count);
                for (int i = 0; i < tris.Count; i++) if (!badSet.Contains(i)) kept.Add(tris[i]);
                foreach (var kv in boundaryCount)
                {
                    if (kv.Value != 1) continue;
                    var nt = MakeCcw(kv.Key.A, kv.Key.B, p, vertices);
                    if (TriangleArea2(nt, vertices) > tol * tol) kept.Add(nt);
                }
                tris = kept;
            }

            tris.RemoveAll(t => t.Has(s0) || t.Has(s1) || t.Has(s2));
            vertices.RemoveRange(realCount, vertices.Count - realCount);
            return tris;
        }

        private static void RecoverConstraint(List<Tri> tris, List<Vec3> vertices, EdgeKey constraint,
            HashSet<EdgeKey> locked, double tol)
        {
            int maxIter = Math.Max(200, tris.Count * 20);
            for (int iter = 0; iter < maxIter; iter++)
            {
                var adj = BuildAdjacency(tris);
                if (adj.ContainsKey(constraint)) return;

                EdgeKey candidate = default(EdgeKey);
                List<int> owners = null;
                bool found = false;
                Vec2 ca = vertices[constraint.A].XY, cb = vertices[constraint.B].XY;

                foreach (var kv in adj)
                {
                    var e = kv.Key;
                    if (kv.Value.Count != 2) continue;
                    if (locked.Contains(e)) continue;
                    if (e.A == constraint.A || e.A == constraint.B || e.B == constraint.A || e.B == constraint.B) continue;
                    if (!Geometry2D.ProperIntersection(ca, cb, vertices[e.A].XY, vertices[e.B].XY, tol)) continue;
                    candidate = e; owners = kv.Value; found = true; break;
                }

                if (!found)
                    throw new InvalidOperationException("Không thể khôi phục cạnh breakline " + constraint +
                        ". Kiểm tra điểm thẳng hàng, breakline giao nhau hoặc dữ liệu quá suy biến.");

                if (!TryFlip(tris, owners[0], owners[1], candidate, vertices, locked, tol))
                {
                    // Thử một cạnh giao khác trước khi kết luận bế tắc.
                    bool flipped = false;
                    foreach (var kv in adj)
                    {
                        var e = kv.Key;
                        if (kv.Value.Count != 2 || locked.Contains(e)) continue;
                        if (e.A == constraint.A || e.A == constraint.B || e.B == constraint.A || e.B == constraint.B) continue;
                        if (!Geometry2D.ProperIntersection(ca, cb, vertices[e.A].XY, vertices[e.B].XY, tol)) continue;
                        if (TryFlip(tris, kv.Value[0], kv.Value[1], e, vertices, locked, tol)) { flipped = true; break; }
                    }
                    if (!flipped)
                        throw new InvalidOperationException("Không thể edge-flip để khôi phục breakline " + constraint + ".");
                }
            }
            throw new InvalidOperationException("Vượt số vòng lặp khi khôi phục breakline " + constraint + ".");
        }

        private static bool TryFlip(List<Tri> tris, int i1, int i2, EdgeKey shared,
            List<Vec3> vertices, HashSet<EdgeKey> locked, double tol)
        {
            var t1 = tris[i1]; var t2 = tris[i2];
            int x = OppositeVertex(t1, shared); int y = OppositeVertex(t2, shared);
            if (x < 0 || y < 0 || x == y) return false;

            // Hai đường chéo phải cắt nhau trong nội bộ tứ giác => tứ giác lồi và flip hợp lệ.
            if (!Geometry2D.ProperIntersection(vertices[shared.A].XY, vertices[shared.B].XY,
                                               vertices[x].XY, vertices[y].XY, tol)) return false;
            var newEdge = new EdgeKey(x, y);
            if (locked.Contains(newEdge)) return false;

            // Không tạo cạnh mới cắt một breakline đã khóa.
            foreach (var le in locked)
            {
                if (le.A == x || le.A == y || le.B == x || le.B == y) continue;
                if (Geometry2D.ProperIntersection(vertices[x].XY, vertices[y].XY,
                                                  vertices[le.A].XY, vertices[le.B].XY, tol)) return false;
            }

            var n1 = MakeCcw(x, y, shared.A, vertices);
            var n2 = MakeCcw(y, x, shared.B, vertices);
            if (TriangleArea2(n1, vertices) <= tol * tol || TriangleArea2(n2, vertices) <= tol * tol) return false;
            tris[i1] = n1; tris[i2] = n2;
            return true;
        }

        private static Dictionary<EdgeKey, List<int>> BuildAdjacency(List<Tri> tris)
        {
            var d = new Dictionary<EdgeKey, List<int>>();
            for (int i = 0; i < tris.Count; i++)
            {
                AddOwner(d, new EdgeKey(tris[i].A, tris[i].B), i);
                AddOwner(d, new EdgeKey(tris[i].B, tris[i].C), i);
                AddOwner(d, new EdgeKey(tris[i].C, tris[i].A), i);
            }
            return d;
        }

        private static int OppositeVertex(Tri t, EdgeKey e)
        {
            if (t.A != e.A && t.A != e.B) return t.A;
            if (t.B != e.A && t.B != e.B) return t.B;
            if (t.C != e.A && t.C != e.B) return t.C;
            return -1;
        }

        private static void AddOwner(Dictionary<EdgeKey, List<int>> d, EdgeKey e, int triIndex)
        {
            List<int> a;
            if (!d.TryGetValue(e, out a)) { a = new List<int>(2); d.Add(e, a); }
            a.Add(triIndex);
        }

        private static void AddEdgeCount(Dictionary<EdgeKey, int> d, EdgeKey e)
        {
            int n; d[e] = d.TryGetValue(e, out n) ? n + 1 : 1;
        }

        private static Tri MakeCcw(int a, int b, int c, List<Vec3> v)
        {
            if (Vec2.Cross(v[a].XY, v[b].XY, v[c].XY) >= 0) return new Tri(a, b, c);
            return new Tri(a, c, b);
        }

        private static double TriangleArea2(Tri t, List<Vec3> v)
        {
            return Math.Abs(Vec2.Cross(v[t.A].XY, v[t.B].XY, v[t.C].XY));
        }

        private static bool InCircumcircle(Vec2 p, Tri t, List<Vec3> v, double tol)
        {
            Vec2 a = v[t.A].XY, b = v[t.B].XY, c = v[t.C].XY;
            double ax = a.X - p.X, ay = a.Y - p.Y;
            double bx = b.X - p.X, by = b.Y - p.Y;
            double cx = c.X - p.X, cy = c.Y - p.Y;
            double det = (ax * ax + ay * ay) * (bx * cy - cx * by)
                       - (bx * bx + by * by) * (ax * cy - cx * ay)
                       + (cx * cx + cy * cy) * (ax * by - bx * ay);
            double orient = Vec2.Cross(a, b, c);
            double eps = Math.Max(1e-14, tol * tol * tol * tol);
            return orient > 0 ? det > eps : det < -eps;
        }

        private static int FindSiteIndex(List<Vec3> vertices, int realCount, Vec3 p, double tol)
        {
            int best = -1; double bestD = double.PositiveInfinity;
            for (int i = 0; i < realCount; i++)
            {
                double d = vertices[i].XY.DistanceTo(p.XY);
                if (d <= tol && d < bestD) { best = i; bestD = d; }
            }
            return best;
        }
    }
}