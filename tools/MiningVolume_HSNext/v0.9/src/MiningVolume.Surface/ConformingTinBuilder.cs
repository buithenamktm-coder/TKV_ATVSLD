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

        // Cached circumcircle used by the X-sweep Bowyer-Watson pass. Once the
        // current X is to the right of RightX, this triangle can never be part
        // of a later cavity and is moved permanently to the completed set.
        private struct WorkTri
        {
            public Tri T;
            public double Cx, Cy, R2, RightX;
            public WorkTri(Tri t, double cx, double cy, double r2)
            {
                T = t;
                Cx = cx;
                Cy = cy;
                R2 = r2;
                RightX = cx + Math.Sqrt(Math.Max(0.0, r2));
            }
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
            var siteIndex = new SiteIndex(vertices, realCount, options.XyTolerance);

            // Adjacency and the spatial edge index are built once for the whole
            // constraint pass. The v0.10.4 implementation rebuilt adjacency for
            // every breakline (and often for every edge-flip), which becomes
            // prohibitive on contour models with tens of thousands of segments.
            var adjacency = BuildAdjacency(tris);
            var edgeIndex = new EdgeGridIndex(vertices, realCount, adjacency.Keys, options.XyTolerance);

            foreach (var seg in input.Breaklines)
            {
                int a = siteIndex.Find(seg.A);
                int b = siteIndex.Find(seg.B);
                if (a < 0 || b < 0)
                    throw new InvalidOperationException("Không ánh xạ được đầu mút breakline vào site TIN: " + seg.SourceId);
                if (a == b) continue;

                var constraint = new EdgeKey(a, b);
                if (!adjacency.ContainsKey(constraint))
                    RecoverConstraint(tris, vertices, constraint, locked, adjacency, edgeIndex, options.XyTolerance);

                // RecoverConstraint có thể biểu diễn một breakline bằng chuỗi cạnh
                // collinear qua các site trung gian. Chỉ khóa cạnh gốc khi cạnh đó
                // thực sự tồn tại; các cạnh con đã được khóa ngay trong fallback.
                if (adjacency.ContainsKey(constraint))
                    locked.Add(constraint);
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

            var seed = MakeCcw(s0, s1, s2, vertices);
            WorkTri seedWork;
            if (!TryWorkTri(seed, vertices, out seedWork))
                throw new InvalidOperationException("Không khởi tạo được tam giác bao Delaunay.");

            // X-order is critical: it lets completed circumcircles leave the
            // active set permanently instead of every new point scanning the
            // entire triangulation.
            var order = Enumerable.Range(0, realCount)
                .OrderBy(i => vertices[i].X)
                .ThenBy(i => vertices[i].Y)
                .ToArray();

            var open = new List<WorkTri> { seedWork };
            var completed = new List<Tri>(Math.Max(4, realCount * 2));

            foreach (int p in order)
            {
                var point = vertices[p];
                var boundaryCount = new Dictionary<EdgeKey, int>();
                var next = new List<WorkTri>(open.Count + 8);

                for (int i = 0; i < open.Count; i++)
                {
                    var wt = open[i];

                    // Sorted-X sweep: no future point can lie in this circle.
                    if (wt.RightX < point.X - tol)
                    {
                        completed.Add(wt.T);
                        continue;
                    }

                    double dx = point.X - wt.Cx;
                    double dy = point.Y - wt.Cy;
                    double d2 = dx * dx + dy * dy;
                    double eps = Math.Max(tol * tol, Math.Abs(wt.R2) * 1e-12);
                    if (d2 <= wt.R2 + eps)
                    {
                        AddEdgeCount(boundaryCount, new EdgeKey(wt.T.A, wt.T.B));
                        AddEdgeCount(boundaryCount, new EdgeKey(wt.T.B, wt.T.C));
                        AddEdgeCount(boundaryCount, new EdgeKey(wt.T.C, wt.T.A));
                    }
                    else
                    {
                        next.Add(wt);
                    }
                }

                foreach (var kv in boundaryCount)
                {
                    if (kv.Value != 1) continue;
                    var nt = MakeCcw(kv.Key.A, kv.Key.B, p, vertices);
                    if (TriangleArea2(nt, vertices) <= tol * tol) continue;
                    WorkTri nwt;
                    if (TryWorkTri(nt, vertices, out nwt))
                        next.Add(nwt);
                }

                open = next;
            }

            for (int i = 0; i < open.Count; i++) completed.Add(open[i].T);
            completed.RemoveAll(t => t.Has(s0) || t.Has(s1) || t.Has(s2));
            vertices.RemoveRange(realCount, vertices.Count - realCount);
            return completed;
        }

        private static bool TryWorkTri(Tri t, List<Vec3> v, out WorkTri work)
        {
            var a = v[t.A];
            var b = v[t.B];
            var c = v[t.C];

            // Local-coordinate circumcenter avoids squaring large mine-grid
            // coordinates directly and is much more stable numerically.
            double bx = b.X - a.X, by = b.Y - a.Y;
            double cx = c.X - a.X, cy = c.Y - a.Y;
            double d = 2.0 * (bx * cy - by * cx);
            if (Math.Abs(d) <= 1e-30)
            {
                work = default(WorkTri);
                return false;
            }

            double b2 = bx * bx + by * by;
            double c2 = cx * cx + cy * cy;
            double ux = (cy * b2 - by * c2) / d;
            double uy = (bx * c2 - cx * b2) / d;
            double ccx = a.X + ux;
            double ccy = a.Y + uy;
            double r2 = ux * ux + uy * uy;
            if (double.IsNaN(r2) || double.IsInfinity(r2))
            {
                work = default(WorkTri);
                return false;
            }

            work = new WorkTri(t, ccx, ccy, r2);
            return true;
        }

        private static void RecoverConstraint(
            List<Tri> tris,
            List<Vec3> vertices,
            EdgeKey constraint,
            HashSet<EdgeKey> locked,
            Dictionary<EdgeKey, List<int>> adjacency,
            EdgeGridIndex edgeIndex,
            double tol)
        {
            if (adjacency.ContainsKey(constraint)) return;

            Vec2 ca = vertices[constraint.A].XY;
            Vec2 cb = vertices[constraint.B].XY;

            // Chèn cạnh ràng buộc theo hàng đợi các cạnh đang cắt breakline.
            // Cách cũ mỗi vòng lại lấy một cạnh bất kỳ từ HashSet nên có thể
            // flip qua lại cùng một vùng và chạm giới hạn vòng lặp trên dữ liệu
            // mỏ dày. Hàng đợi có thứ tự dọc theo breakline ổn định hơn và chỉ
            // thử lại cạnh chưa flip được sau khi topo xung quanh đã thay đổi.
            var pending = new Queue<EdgeKey>();
            var queued = new HashSet<EdgeKey>();
            foreach (var crossingEdge in CrossingEdgesOrdered(
                ca, cb, constraint, vertices, locked, adjacency, edgeIndex, tol))
            {
                pending.Enqueue(crossingEdge);
                queued.Add(crossingEdge);
            }

            if (pending.Count == 0)
            {
                if (TryRecoverConstraintThroughInteriorSites(
                    tris, vertices, constraint, locked, adjacency, edgeIndex, tol))
                    return;

                throw new InvalidOperationException(
                    "Không tìm thấy dãy cạnh tam giác cắt breakline " + constraint +
                    ". Không có cạnh cắt và cũng không tìm thấy chuỗi site trung gian " +
                    "nằm trên breakline để tách ràng buộc. Dữ liệu có thể suy biến cục bộ.");
            }

            int initialCrossings = pending.Count;
            int successfulFlips = 0;
            int failuresSinceProgress = 0;
            int maxSuccessfulFlips = Math.Max(4096, initialCrossings * 128 + 2048);

            while (!adjacency.ContainsKey(constraint))
            {
                if (pending.Count == 0)
                {
                    foreach (var crossingEdge in CrossingEdgesOrdered(
                        ca, cb, constraint, vertices, locked, adjacency, edgeIndex, tol))
                    {
                        if (queued.Add(crossingEdge)) pending.Enqueue(crossingEdge);
                    }

                    if (pending.Count == 0) break;
                }

                var currentEdge = pending.Dequeue();
                queued.Remove(currentEdge);

                List<int> owners;
                if (!adjacency.TryGetValue(currentEdge, out owners) || owners.Count != 2)
                    continue;
                if (locked.Contains(currentEdge))
                    continue;
                if (!Geometry2D.ProperIntersection(
                    ca, cb, vertices[currentEdge.A].XY, vertices[currentEdge.B].XY, tol))
                    continue;

                EdgeKey replacement;
                if (TryFlip(
                    tris, owners[0], owners[1], currentEdge, vertices, locked,
                    adjacency, edgeIndex, tol, out replacement))
                {
                    successfulFlips++;
                    failuresSinceProgress = 0;

                    if (Geometry2D.ProperIntersection(
                        ca, cb,
                        vertices[replacement.A].XY,
                        vertices[replacement.B].XY,
                        tol) &&
                        queued.Add(replacement))
                    {
                        pending.Enqueue(replacement);
                    }

                    if (successfulFlips > maxSuccessfulFlips)
                        throw new InvalidOperationException(
                            "Không hội tụ khi khôi phục breakline " + constraint +
                            " sau " + successfulFlips.ToString("n0") +
                            " lần edge-flip. Cần kiểm tra hình học suy biến cục bộ.");
                }
                else
                {
                    if (queued.Add(currentEdge)) pending.Enqueue(currentEdge);
                    failuresSinceProgress++;

                    // Nếu đã đi hết một vòng hàng đợi mà không flip được cạnh nào
                    // thì topology hiện tại không thể tiến thêm bằng edge-flip.
                    if (failuresSinceProgress >= Math.Max(1, pending.Count))
                        throw new InvalidOperationException(
                            "Không thể edge-flip để khôi phục breakline " + constraint +
                            ". Các tam giác lân cận không còn cạnh hợp lệ để flip; " +
                            "kiểm tra điểm thẳng hàng hoặc hình học suy biến cục bộ.");
                }
            }

            if (!adjacency.ContainsKey(constraint))
                throw new InvalidOperationException(
                    "Không khôi phục được breakline " + constraint +
                    " sau khi xử lý " + successfulFlips.ToString("n0") +
                    " lần edge-flip.");
        }

        private static bool TryRecoverConstraintThroughInteriorSites(
            List<Tri> tris,
            List<Vec3> vertices,
            EdgeKey constraint,
            HashSet<EdgeKey> locked,
            Dictionary<EdgeKey, List<int>> adjacency,
            EdgeGridIndex edgeIndex,
            double tol)
        {
            Vec2 a = vertices[constraint.A].XY;
            Vec2 b = vertices[constraint.B].XY;
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 <= tol * tol) return false;

            // Fallback này chỉ dùng khi không có cạnh nào cắt "properly". Trường
            // hợp thường gặp là breakline đi chính xác qua một hay nhiều site TIN,
            // nên ràng buộc hợp lệ thực chất là một chuỗi cạnh collinear thay vì
            // một cạnh duy nhất. Dùng tolerance hơi nới để hấp thụ sai số số học
            // do clip tile, nhưng vẫn ở mức rất nhỏ so với đơn vị bản vẽ mỏ.
            double onLineTol = Math.Max(tol * 10.0, 1e-8);
            double paramTol = Math.Min(1e-6, onLineTol / Math.Max(Math.Sqrt(len2), onLineTol));

            var chain = new List<Tuple<double, int>>
            {
                Tuple.Create(0.0, constraint.A),
                Tuple.Create(1.0, constraint.B)
            };

            for (int i = 0; i < vertices.Count; i++)
            {
                if (i == constraint.A || i == constraint.B) continue;
                Vec2 p = vertices[i].XY;
                if (!Geometry2D.PointOnSegment(p, a, b, onLineTol)) continue;

                double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
                if (t <= paramTol || t >= 1.0 - paramTol) continue;
                chain.Add(Tuple.Create(t, i));
            }

            if (chain.Count <= 2) return false;

            chain = chain
                .OrderBy(x => x.Item1)
                .ThenBy(x => x.Item2)
                .ToList();

            // Gộp các site gần trùng tham số để tránh tạo đoạn gần zero.
            var ordered = new List<int>();
            double lastT = double.NegativeInfinity;
            foreach (var item in chain)
            {
                if (ordered.Count > 0 && Math.Abs(item.Item1 - lastT) <= paramTol)
                    continue;
                ordered.Add(item.Item2);
                lastT = item.Item1;
            }

            if (ordered.Count <= 2) return false;

            for (int i = 0; i < ordered.Count - 1; i++)
            {
                int u = ordered[i];
                int v = ordered[i + 1];
                if (u == v) continue;

                var sub = new EdgeKey(u, v);
                if (!adjacency.ContainsKey(sub))
                    RecoverConstraint(tris, vertices, sub, locked, adjacency, edgeIndex, tol);

                if (adjacency.ContainsKey(sub))
                    locked.Add(sub);
            }

            // Thành công nếu mỗi khoảng liên tiếp đã trở thành cạnh, hoặc đã được
            // RecoverConstraint tách tiếp thành các cạnh con collinear và khóa chúng.
            // Không yêu cầu cạnh constraint gốc phải tồn tại.
            return true;
        }

        private static IEnumerable<EdgeKey> CrossingEdgesOrdered(
            Vec2 ca,
            Vec2 cb,
            EdgeKey constraint,
            List<Vec3> vertices,
            HashSet<EdgeKey> locked,
            Dictionary<EdgeKey, List<int>> adjacency,
            EdgeGridIndex edgeIndex,
            double tol)
        {
            var candidates = CollectCrossingEdges(
                edgeIndex.Query(ca, cb),
                ca, cb, constraint, vertices, locked, adjacency, tol);

            // Safety net: the spatial edge index is an accelerator, not a source
            // of truth. On rare dense/near-grid-boundary cases, if it returns no
            // crossing candidates, scan current adjacency once so a stale/missed
            // index bucket cannot falsely report that the breakline is impossible.
            if (candidates.Count == 0)
            {
                candidates = CollectCrossingEdges(
                    adjacency.Keys,
                    ca, cb, constraint, vertices, locked, adjacency, tol);
            }

            return candidates
                .OrderBy(x => x.Item1)
                .ThenBy(x => x.Item2.A)
                .ThenBy(x => x.Item2.B)
                .Select(x => x.Item2);
        }

        private static List<Tuple<double, EdgeKey>> CollectCrossingEdges(
            IEnumerable<EdgeKey> edges,
            Vec2 ca,
            Vec2 cb,
            EdgeKey constraint,
            List<Vec3> vertices,
            HashSet<EdgeKey> locked,
            Dictionary<EdgeKey, List<int>> adjacency,
            double tol)
        {
            var candidates = new List<Tuple<double, EdgeKey>>();
            foreach (var e in edges)
            {
                List<int> owners;
                if (!adjacency.TryGetValue(e, out owners) || owners.Count != 2) continue;
                if (locked.Contains(e)) continue;
                if (e.A == constraint.A || e.A == constraint.B ||
                    e.B == constraint.A || e.B == constraint.B) continue;
                if (!Geometry2D.ProperIntersection(
                    ca, cb, vertices[e.A].XY, vertices[e.B].XY, tol)) continue;

                double t = IntersectionParameterAlongFirst(
                    ca, cb, vertices[e.A].XY, vertices[e.B].XY);
                candidates.Add(Tuple.Create(t, e));
            }
            return candidates;
        }

        private static double IntersectionParameterAlongFirst(
            Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            double rx = b.X - a.X, ry = b.Y - a.Y;
            double sx = d.X - c.X, sy = d.Y - c.Y;
            double den = rx * sy - ry * sx;
            if (Math.Abs(den) <= 1e-30) return double.PositiveInfinity;
            double qx = c.X - a.X, qy = c.Y - a.Y;
            return (qx * sy - qy * sx) / den;
        }

        private static bool TryFlip(
            List<Tri> tris,
            int i1,
            int i2,
            EdgeKey shared,
            List<Vec3> vertices,
            HashSet<EdgeKey> locked,
            Dictionary<EdgeKey, List<int>> adjacency,
            EdgeGridIndex edgeIndex,
            double tol,
            out EdgeKey replacement)
        {
            replacement = default(EdgeKey);
            var t1 = tris[i1];
            var t2 = tris[i2];
            int x = OppositeVertex(t1, shared);
            int y = OppositeVertex(t2, shared);
            if (x < 0 || y < 0 || x == y) return false;

            // A valid diagonal flip stays inside the union of these two adjacent
            // triangles. Therefore the new diagonal cannot cross any unrelated
            // triangulation edge; checking every previously locked breakline
            // here is unnecessary O(B) work.
            if (!Geometry2D.ProperIntersection(
                vertices[shared.A].XY, vertices[shared.B].XY,
                vertices[x].XY, vertices[y].XY, tol)) return false;

            var newEdge = new EdgeKey(x, y);
            if (locked.Contains(newEdge)) return false;
            replacement = newEdge;

            var n1 = MakeCcw(x, y, shared.A, vertices);
            var n2 = MakeCcw(y, x, shared.B, vertices);
            if (TriangleArea2(n1, vertices) <= tol * tol ||
                TriangleArea2(n2, vertices) <= tol * tol) return false;

            RemoveTriangleOwners(adjacency, t1, i1);
            RemoveTriangleOwners(adjacency, t2, i2);

            tris[i1] = n1;
            tris[i2] = n2;

            AddTriangleOwners(adjacency, n1, i1);
            AddTriangleOwners(adjacency, n2, i2);

            edgeIndex.Add(new EdgeKey(n1.A, n1.B));
            edgeIndex.Add(new EdgeKey(n1.B, n1.C));
            edgeIndex.Add(new EdgeKey(n1.C, n1.A));
            edgeIndex.Add(new EdgeKey(n2.A, n2.B));
            edgeIndex.Add(new EdgeKey(n2.B, n2.C));
            edgeIndex.Add(new EdgeKey(n2.C, n2.A));
            return true;
        }

        private static void RemoveTriangleOwners(
            Dictionary<EdgeKey, List<int>> adjacency, Tri t, int triIndex)
        {
            RemoveOwner(adjacency, new EdgeKey(t.A, t.B), triIndex);
            RemoveOwner(adjacency, new EdgeKey(t.B, t.C), triIndex);
            RemoveOwner(adjacency, new EdgeKey(t.C, t.A), triIndex);
        }

        private static void AddTriangleOwners(
            Dictionary<EdgeKey, List<int>> adjacency, Tri t, int triIndex)
        {
            AddOwner(adjacency, new EdgeKey(t.A, t.B), triIndex);
            AddOwner(adjacency, new EdgeKey(t.B, t.C), triIndex);
            AddOwner(adjacency, new EdgeKey(t.C, t.A), triIndex);
        }

        private static void RemoveOwner(
            Dictionary<EdgeKey, List<int>> adjacency, EdgeKey edge, int triIndex)
        {
            List<int> owners;
            if (!adjacency.TryGetValue(edge, out owners)) return;
            owners.Remove(triIndex);
            if (owners.Count == 0) adjacency.Remove(edge);
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

        private sealed class EdgeGridIndex
        {
            private readonly Dictionary<string, List<EdgeKey>> _cells =
                new Dictionary<string, List<EdgeKey>>();
            private readonly HashSet<EdgeKey> _indexed = new HashSet<EdgeKey>();
            private readonly List<Vec3> _vertices;
            private readonly double _cell;
            private readonly double _minX;
            private readonly double _minY;

            public EdgeGridIndex(
                List<Vec3> vertices,
                int realCount,
                IEnumerable<EdgeKey> edges,
                double tol)
            {
                _vertices = vertices;
                double maxX = vertices[0].X, maxY = vertices[0].Y;
                _minX = vertices[0].X;
                _minY = vertices[0].Y;
                for (int i = 1; i < realCount; i++)
                {
                    _minX = Math.Min(_minX, vertices[i].X);
                    _minY = Math.Min(_minY, vertices[i].Y);
                    maxX = Math.Max(maxX, vertices[i].X);
                    maxY = Math.Max(maxY, vertices[i].Y);
                }

                var edgeList = edges.ToList();
                double span = Math.Max(maxX - _minX, maxY - _minY);
                _cell = Math.Max(
                    Math.Max(tol * 100.0, 1e-9),
                    span / Math.Max(32.0, Math.Sqrt(Math.Max(1, edgeList.Count))));

                foreach (var edge in edgeList) Add(edge);
            }

            public void Add(EdgeKey edge)
            {
                if (!_indexed.Add(edge)) return;
                foreach (var key in SegmentCells(
                    _vertices[edge.A].XY, _vertices[edge.B].XY))
                {
                    List<EdgeKey> edges;
                    if (!_cells.TryGetValue(key, out edges))
                    {
                        edges = new List<EdgeKey>();
                        _cells[key] = edges;
                    }
                    edges.Add(edge);
                }
            }

            public HashSet<EdgeKey> Query(Vec2 a, Vec2 b)
            {
                var result = new HashSet<EdgeKey>();
                foreach (var cell in SegmentCellCoords(a, b))
                {
                    // Neighbor cells make the DDA robust when an intersection
                    // lies exactly on a grid boundary.
                    for (long dx = -1; dx <= 1; dx++)
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        List<EdgeKey> edges;
                        if (!_cells.TryGetValue(Key(cell.Item1 + dx, cell.Item2 + dy), out edges))
                            continue;
                        foreach (var edge in edges) result.Add(edge);
                    }
                }
                return result;
            }

            private IEnumerable<string> SegmentCells(Vec2 a, Vec2 b)
            {
                foreach (var cell in SegmentCellCoords(a, b))
                    yield return Key(cell.Item1, cell.Item2);
            }

            private IEnumerable<Tuple<long, long>> SegmentCellCoords(Vec2 a, Vec2 b)
            {
                long x0 = Ix(a.X), y0 = Iy(a.Y);
                long x1 = Ix(b.X), y1 = Iy(b.Y);
                long steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
                if (steps == 0)
                {
                    yield return Tuple.Create(x0, y0);
                    yield break;
                }

                long lastX = long.MinValue, lastY = long.MinValue;
                for (long i = 0; i <= steps; i++)
                {
                    double t = i / (double)steps;
                    long x = (long)Math.Floor(x0 + (x1 - x0) * t);
                    long y = (long)Math.Floor(y0 + (y1 - y0) * t);
                    if (x == lastX && y == lastY) continue;
                    lastX = x; lastY = y;
                    yield return Tuple.Create(x, y);
                }
            }

            private long Ix(double x) => (long)Math.Floor((x - _minX) / _cell);
            private long Iy(double y) => (long)Math.Floor((y - _minY) / _cell);
            private static string Key(long x, long y) => x + ":" + y;
        }

        private sealed class SiteIndex
        {
            private readonly Dictionary<string, List<int>> _cells = new Dictionary<string, List<int>>();
            private readonly List<Vec3> _vertices;
            private readonly double _tol;
            private readonly double _tol2;

            public SiteIndex(List<Vec3> vertices, int realCount, double tolerance)
            {
                _vertices = vertices;
                _tol = Math.Max(tolerance, 1e-12);
                _tol2 = _tol * _tol;

                for (int i = 0; i < realCount; i++)
                {
                    var p = vertices[i];
                    string key = Key(Ix(p.X), Iy(p.Y));
                    List<int> ids;
                    if (!_cells.TryGetValue(key, out ids))
                    {
                        ids = new List<int>();
                        _cells[key] = ids;
                    }
                    ids.Add(i);
                }
            }

            public int Find(Vec3 p)
            {
                long ix = Ix(p.X);
                long iy = Iy(p.Y);
                int best = -1;
                double bestD2 = double.PositiveInfinity;

                for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                {
                    List<int> ids;
                    if (!_cells.TryGetValue(Key(ix + dx, iy + dy), out ids)) continue;
                    foreach (int id in ids)
                    {
                        double px = _vertices[id].X - p.X;
                        double py = _vertices[id].Y - p.Y;
                        double d2 = px * px + py * py;
                        if (d2 <= _tol2 && d2 < bestD2)
                        {
                            best = id;
                            bestD2 = d2;
                        }
                    }
                }
                return best;
            }

            private long Ix(double x) => (long)Math.Floor(x / _tol);
            private long Iy(double y) => (long)Math.Floor(y / _tol);
            private static string Key(long x, long y) => x + ":" + y;
        }
    }
}