using System;
using System.Collections.Generic;
using MiningVolume.Core.Geometry;

namespace MiningVolume.Core.Grid
{
    /// <summary>Khối lượng đào/đắp chính xác hình học trong một ô lưới.</summary>
    public sealed class GridCellVolume
    {
        public double CutVolume { get; internal set; }
        public double FillVolume { get; internal set; }
        public double NetVolume => CutVolume - FillVolume;
        public double CalculationArea { get; internal set; }
        public double CoveredArea { get; internal set; }
        public double NoDataArea => Math.Max(0, CalculationArea - CoveredArea);
        public bool IsComplete => NoDataArea <= Math.Max(1e-8, CalculationArea * 1e-8);
    }

    /// <summary>
    /// Tích phân thể tích giữa hai TIN trên từng ô lưới bằng giao hai tam giác,
    /// cắt theo ô, ranh giới lõm/lỗ và đường đẳng chênh cao 0.
    /// KHÔNG lấy trung bình 4 đỉnh ô; trong một ô có thể vừa đào vừa đắp.
    ///
    /// Đầu vào là các tam giác ứng viên đã lọc theo bbox/tile; dùng SpatialIndex
    /// của mô hình TIN ở lớp gọi để tránh quét mọi tam giác cho từng ô.
    /// </summary>
    public static class ExactGridCellCalculator
    {
        private const double Eps = 1e-9;

        public static GridCellVolume Calculate(
            IReadOnlyList<Triangle3> existing,
            IReadOnlyList<Triangle3> design,
            IReadOnlyList<Vec2> cellPolygon,
            IReadOnlyList<Vec2> outerBoundary = null,
            IReadOnlyList<IReadOnlyList<Vec2>> holes = null)
        {
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            if (design == null) throw new ArgumentNullException(nameof(design));
            var cell = Normalize(cellPolygon);
            if (cell.Count < 3 || !Convex(cell))
                throw new ArgumentException("Ô lưới phải là đa giác lồi hợp lệ, có diện tích dương.", nameof(cellPolygon));

            var outer = outerBoundary == null
                ? new List<List<Vec2>> { cell }
                : Triangulate(outerBoundary);
            var exclusions = new List<List<Vec2>>();
            if (holes != null)
                foreach (var hole in holes)
                    exclusions.AddRange(Triangulate(hole));

            var result = new GridCellVolume();
            foreach (var polygon in outer)
                result.CalculationArea += Area(Clip(cell, polygon));
            foreach (var polygon in exclusions)
                result.CalculationArea -= Area(Clip(cell, polygon));
            if (result.CalculationArea < -Eps)
                throw new InvalidOperationException("Diện tích lỗ vượt quá vùng tính; hãy kiểm tra đường bao.");
            result.CalculationArea = Math.Max(0, result.CalculationArea);
            if (result.CalculationArea <= Eps) return result;

            foreach (var tn in existing)
            {
                if (tn.Area2D <= Eps || !Overlaps(cell, tn)) continue;
                var inExisting = Clip(cell, Triangle(tn));
                if (Area(inExisting) <= Eps) continue;
                foreach (var tk in design)
                {
                    if (tk.Area2D <= Eps || !Overlaps(inExisting, tk)) continue;
                    var intersection = Clip(inExisting, Triangle(tk));
                    if (Area(intersection) <= Eps) continue;
                    double fullArea = 0, fullCut = 0, fullFill = 0;

                    // Các tam giác từ một polygon không chồng nội thất lên nhau.
                    foreach (var polygon in outer)
                        Accumulate(Clip(intersection, polygon), tn, tk,
                            ref fullArea, ref fullCut, ref fullFill);
                    foreach (var polygon in exclusions)
                    {
                        double a = 0, c = 0, f = 0;
                        Accumulate(Clip(intersection, polygon), tn, tk, ref a, ref c, ref f);
                        fullArea -= a; fullCut -= c; fullFill -= f;
                    }
                    result.CoveredArea += fullArea;
                    result.CutVolume += fullCut;
                    result.FillVolume += fullFill;
                }
            }

            double tol = Math.Max(1e-7, result.CalculationArea * 1e-7);
            if (result.CoveredArea > result.CalculationArea + tol)
                throw new InvalidOperationException("Hai TIN có tam giác chồng lấn hoặc đường bao không hợp lệ; diện tích tính bị đếm trùng.");
            if (result.CoveredArea < -tol || result.CutVolume < -tol || result.FillVolume < -tol)
                throw new InvalidOperationException("Kết quả thể tích âm bất thường; kiểm tra đường bao và lỗ.");
            result.CoveredArea = Math.Max(0, result.CoveredArea);
            result.CutVolume = Math.Max(0, result.CutVolume);
            result.FillVolume = Math.Max(0, result.FillVolume);
            return result;
        }

        private static void Accumulate(List<Vec2> poly, Triangle3 tn, Triangle3 tk,
            ref double area, ref double cut, ref double fill)
        {
            if (poly.Count < 3) return;
            double a = Area(poly);
            if (a <= Eps) return;
            area += a;
            var signed = new List<SignedPoint>(poly.Count);
            foreach (var p in poly)
                signed.Add(new SignedPoint(p, Elevation(tn, p) - Elevation(tk, p)));
            cut += IntegrateSigned(ClipSign(signed, true), true);
            fill += IntegrateSigned(ClipSign(signed, false), false);
        }

        private readonly struct SignedPoint
        {
            public readonly Vec2 P;
            public readonly double Delta;
            public SignedPoint(Vec2 p, double delta) { P = p; Delta = delta; }
        }

        private static List<SignedPoint> ClipSign(List<SignedPoint> vertices, bool positive)
        {
            var output = new List<SignedPoint>();
            if (vertices.Count == 0) return output;
            var previous = vertices[vertices.Count - 1];
            bool previousIn = positive ? previous.Delta >= -Eps : previous.Delta <= Eps;
            foreach (var next in vertices)
            {
                bool nextIn = positive ? next.Delta >= -Eps : next.Delta <= Eps;
                if (nextIn != previousIn)
                {
                    double den = previous.Delta - next.Delta;
                    if (Math.Abs(den) > 1e-15)
                    {
                        double alpha = previous.Delta / den;
                        alpha = Math.Max(0, Math.Min(1, alpha));
                        var p = new Vec2(previous.P.X + alpha * (next.P.X - previous.P.X),
                            previous.P.Y + alpha * (next.P.Y - previous.P.Y));
                        output.Add(new SignedPoint(p, 0));
                    }
                }
                if (nextIn) output.Add(next);
                previous = next;
                previousIn = nextIn;
            }
            return output;
        }

        private static double IntegrateSigned(List<SignedPoint> p, bool positive)
        {
            if (p.Count < 3) return 0;
            double volume = 0;
            for (int i = 1; i + 1 < p.Count; i++)
            {
                double a = Math.Abs(Vec2.Cross(p[0].P, p[i].P, p[i + 1].P)) * 0.5;
                double mean = (p[0].Delta + p[i].Delta + p[i + 1].Delta) / 3.0;
                volume += a * (positive ? mean : -mean);
            }
            return Math.Max(0, volume);
        }

        private static double Elevation(Triangle3 t, Vec2 p)
        {
            double det = Vec2.Cross(t.A.XY, t.B.XY, t.C.XY);
            if (Math.Abs(det) <= 1e-15)
                throw new InvalidOperationException("Tam giác TIN bị suy biến.");
            double a = Vec2.Cross(p, t.B.XY, t.C.XY) / det;
            double b = Vec2.Cross(t.A.XY, p, t.C.XY) / det;
            double c = 1.0 - a - b;
            return a * t.A.Z + b * t.B.Z + c * t.C.Z;
        }

        private static List<Vec2> Triangle(Triangle3 t) => new List<Vec2> { t.A.XY, t.B.XY, t.C.XY };

        private static bool Overlaps(IReadOnlyList<Vec2> poly, Triangle3 tri)
        {
            if (poly.Count == 0) return false;
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            foreach (var p in poly)
            {
                minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
            }
            double ta = Math.Min(tri.A.X, Math.Min(tri.B.X, tri.C.X));
            double tb = Math.Max(tri.A.X, Math.Max(tri.B.X, tri.C.X));
            double tc = Math.Min(tri.A.Y, Math.Min(tri.B.Y, tri.C.Y));
            double td = Math.Max(tri.A.Y, Math.Max(tri.B.Y, tri.C.Y));
            return !(tb < minX - Eps || ta > maxX + Eps ||
                     td < minY - Eps || tc > maxY + Eps);
        }

        private static List<Vec2> Clip(IReadOnlyList<Vec2> subject, IReadOnlyList<Vec2> convexClip)
        {
            var poly = new List<Vec2>(subject);
            if (poly.Count < 3 || convexClip.Count < 3) return new List<Vec2>();
            double sign = SignedArea(convexClip) >= 0 ? 1 : -1;
            for (int i = 0; i < convexClip.Count; i++)
            {
                if (poly.Count == 0) break;
                var edgeStart = convexClip[i];
                var edgeEnd = convexClip[(i + 1) % convexClip.Count];
                var next = new List<Vec2>();
                var previous = poly[poly.Count - 1];
                double prevSide = sign * Vec2.Cross(edgeStart, edgeEnd, previous);
                foreach (var point in poly)
                {
                    double side = sign * Vec2.Cross(edgeStart, edgeEnd, point);
                    bool isInside = side >= -Eps, wasInside = prevSide >= -Eps;
                    if (isInside != wasInside)
                    {
                        double denominator = prevSide - side;
                        if (Math.Abs(denominator) > 1e-15)
                        {
                            double t = Math.Max(0, Math.Min(1, prevSide / denominator));
                            next.Add(new Vec2(previous.X + t * (point.X - previous.X),
                                previous.Y + t * (point.Y - previous.Y)));
                        }
                    }
                    if (isInside) next.Add(point);
                    previous = point; prevSide = side;
                }
                poly = RemoveAdjacentDuplicates(next);
            }
            return poly;
        }

        private static List<List<Vec2>> Triangulate(IReadOnlyList<Vec2> boundary)
        {
            var poly = Normalize(boundary);
            if (poly.Count < 3 || Math.Abs(SignedArea(poly)) <= Eps)
                throw new ArgumentException("Đường bao khoáng sản/vùng tính không hợp lệ.");
            if (HasSelfIntersection(poly))
                throw new ArgumentException("Đường bao tự cắt; không thể dùng để tính thể tích.");
            if (SignedArea(poly) < 0) poly.Reverse();
            var output = new List<List<Vec2>>();
            while (poly.Count > 3)
            {
                bool found = false;
                for (int i = 0; i < poly.Count; i++)
                {
                    var prev = poly[(i + poly.Count - 1) % poly.Count];
                    var curr = poly[i];
                    var next = poly[(i + 1) % poly.Count];
                    if (Vec2.Cross(prev, curr, next) <= Eps) continue;
                    bool contains = false;
                    for (int j = 0; j < poly.Count; j++)
                        if (j != i && j != (i + poly.Count - 1) % poly.Count && j != (i + 1) % poly.Count &&
                            InTriangle(poly[j], prev, curr, next))
                        { contains = true; break; }
                    if (contains) continue;
                    output.Add(new List<Vec2> { prev, curr, next });
                    poly.RemoveAt(i);
                    found = true;
                    break;
                }
                if (!found) throw new ArgumentException("Không thể chia đường bao thành tam giác; kiểm tra trùng đỉnh/đường biên.");
            }
            output.Add(poly);
            return output;
        }

        private static bool InTriangle(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
        {
            return Vec2.Cross(a, b, p) >= -Eps && Vec2.Cross(b, c, p) >= -Eps &&
                   Vec2.Cross(c, a, p) >= -Eps;
        }

        private static bool HasSelfIntersection(IReadOnlyList<Vec2> p)
        {
            for (int i = 0; i < p.Count; i++)
                for (int j = i + 1; j < p.Count; j++)
                {
                    if (j == i + 1 || (i == 0 && j == p.Count - 1)) continue;
                    if (SegmentsCross(p[i], p[(i + 1) % p.Count], p[j], p[(j + 1) % p.Count]))
                        return true;
                }
            return false;
        }

        private static bool SegmentsCross(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            double x = Vec2.Cross(a, b, c), y = Vec2.Cross(a, b, d);
            double z = Vec2.Cross(c, d, a), w = Vec2.Cross(c, d, b);
            return ((x > Eps && y < -Eps) || (x < -Eps && y > Eps)) &&
                   ((z > Eps && w < -Eps) || (z < -Eps && w > Eps));
        }

        private static List<Vec2> Normalize(IReadOnlyList<Vec2> input)
        {
            if (input == null) return new List<Vec2>();
            var poly = RemoveAdjacentDuplicates(input);
            if (poly.Count > 1 && Distance2(poly[0], poly[poly.Count - 1]) <= Eps * Eps)
                poly.RemoveAt(poly.Count - 1);
            bool changed = true;
            while (changed && poly.Count >= 3)
            {
                changed = false;
                for (int i = 0; i < poly.Count; i++)
                    if (Math.Abs(Vec2.Cross(poly[(i + poly.Count - 1) % poly.Count], poly[i],
                            poly[(i + 1) % poly.Count])) <= Eps)
                    { poly.RemoveAt(i); changed = true; break; }
            }
            return poly;
        }

        private static List<Vec2> RemoveAdjacentDuplicates(IReadOnlyList<Vec2> points)
        {
            var r = new List<Vec2>();
            foreach (var p in points)
                if (r.Count == 0 || Distance2(r[r.Count - 1], p) > Eps * Eps)
                    r.Add(p);
            if (r.Count > 1 && Distance2(r[0], r[r.Count - 1]) <= Eps * Eps)
                r.RemoveAt(r.Count - 1);
            return r;
        }

        private static double Distance2(Vec2 a, Vec2 b)
        {
            double x = a.X - b.X, y = a.Y - b.Y;
            return x * x + y * y;
        }

        private static bool Convex(IReadOnlyList<Vec2> p)
        {
            if (p.Count < 3 || Area(p) <= Eps) return false;
            int sign = 0;
            for (int i = 0; i < p.Count; i++)
            {
                double v = Vec2.Cross(p[i], p[(i + 1) % p.Count], p[(i + 2) % p.Count]);
                if (Math.Abs(v) <= Eps) continue;
                int s = v > 0 ? 1 : -1;
                if (sign != 0 && sign != s) return false;
                sign = s;
            }
            return sign != 0;
        }

        private static double Area(IReadOnlyList<Vec2> polygon) => Math.Abs(SignedArea(polygon));

        private static double SignedArea(IReadOnlyList<Vec2> polygon)
        {
            if (polygon.Count < 3) return 0;
            double twice = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                twice += a.X * b.Y - b.X * a.Y;
            }
            return twice * 0.5;
        }
    }
}
