using System;
using System.Collections.Generic;

namespace MiningVolume.Core.Geometry
{
    public static class Geometry2D
    {
        public static bool AlmostSame(Vec2 a, Vec2 b, double tol)
            => Math.Abs(a.X - b.X) <= tol && Math.Abs(a.Y - b.Y) <= tol;

        public static bool PointOnSegment(Vec2 p, Vec2 a, Vec2 b, double tol)
        {
            double cross = Math.Abs(Vec2.Cross(a, b, p));
            double len = a.DistanceTo(b);
            if (len <= tol) return p.DistanceTo(a) <= tol;
            if (cross / len > tol) return false;
            double dot = (p.X - a.X) * (p.X - b.X) + (p.Y - a.Y) * (p.Y - b.Y);
            return dot <= tol * tol;
        }

        // True only for a proper interior/interior crossing. Shared endpoints are not crossings.
        public static bool ProperIntersection(Vec2 a, Vec2 b, Vec2 c, Vec2 d, double tol)
        {
            if (AlmostSame(a, c, tol) || AlmostSame(a, d, tol) ||
                AlmostSame(b, c, tol) || AlmostSame(b, d, tol)) return false;

            double abC = Vec2.Cross(a, b, c);
            double abD = Vec2.Cross(a, b, d);
            double cdA = Vec2.Cross(c, d, a);
            double cdB = Vec2.Cross(c, d, b);

            // Cross product has units length^2, while tol has units length.
            // Comparing |cross| directly with tol makes the effective geometric
            // tolerance depend on segment length and misses real crossings on
            // very short TIN edges. Convert tol to an area tolerance using the
            // corresponding segment length so it represents perpendicular
            // distance consistently for dense and sparse mine meshes.
            double abLen = a.DistanceTo(b);
            double cdLen = c.DistanceTo(d);
            if (abLen <= tol || cdLen <= tol) return false;

            double abEps = Math.Max(1e-24, tol * abLen);
            double cdEps = Math.Max(1e-24, tol * cdLen);

            return ((abC > abEps && abD < -abEps) || (abC < -abEps && abD > abEps)) &&
                   ((cdA > cdEps && cdB < -cdEps) || (cdA < -cdEps && cdB > cdEps));
        }

        public static bool PointInPolygon(Vec2 p, IReadOnlyList<Vec2> poly, double tol = 1e-9)
        {
            if (poly == null || poly.Count < 3) return false;
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vec2 a = poly[j], b = poly[i];
                if (PointOnSegment(p, a, b, tol)) return true;
                bool hit = ((a.Y > p.Y) != (b.Y > p.Y)) &&
                           (p.X < (b.X - a.X) * (p.Y - a.Y) / ((b.Y - a.Y) == 0 ? 1e-300 : (b.Y - a.Y)) + a.X);
                if (hit) inside = !inside;
            }
            return inside;
        }

        public static bool TriangleIntersectsPolygon(
            Triangle3 triangle,
            IReadOnlyList<Vec2> polygon,
            double tol = 1e-9)
        {
            if (polygon == null || polygon.Count < 3) return false;

            var tv = new[] { triangle.A.XY, triangle.B.XY, triangle.C.XY };
            for (int i = 0; i < tv.Length; i++)
                if (PointInPolygon(tv[i], polygon, tol))
                    return true;

            for (int i = 0; i < polygon.Count; i++)
                if (PointInTriangle(polygon[i], tv[0], tv[1], tv[2], tol))
                    return true;

            for (int i = 0; i < 3; i++)
            {
                Vec2 a = tv[i];
                Vec2 b = tv[(i + 1) % 3];
                for (int j = 0; j < polygon.Count; j++)
                {
                    Vec2 c = polygon[j];
                    Vec2 d = polygon[(j + 1) % polygon.Count];
                    if (SegmentsTouchOrCross(a, b, c, d, tol))
                        return true;
                }
            }

            return false;
        }

        public static bool PointInTriangle(
            Vec2 p,
            Vec2 a,
            Vec2 b,
            Vec2 c,
            double tol = 1e-9)
        {
            if (PointOnSegment(p, a, b, tol) ||
                PointOnSegment(p, b, c, tol) ||
                PointOnSegment(p, c, a, tol))
                return true;

            double c1 = Vec2.Cross(a, b, p);
            double c2 = Vec2.Cross(b, c, p);
            double c3 = Vec2.Cross(c, a, p);
            double maxLen = Math.Max(
                a.DistanceTo(b),
                Math.Max(b.DistanceTo(c), c.DistanceTo(a)));
            double eps = Math.Max(1e-24, tol * Math.Max(maxLen, 1.0));

            bool hasNeg = c1 < -eps || c2 < -eps || c3 < -eps;
            bool hasPos = c1 > eps || c2 > eps || c3 > eps;
            return !(hasNeg && hasPos);
        }

        private static bool SegmentsTouchOrCross(
            Vec2 a,
            Vec2 b,
            Vec2 c,
            Vec2 d,
            double tol)
        {
            if (ProperIntersection(a, b, c, d, tol)) return true;
            return PointOnSegment(a, c, d, tol) ||
                   PointOnSegment(b, c, d, tol) ||
                   PointOnSegment(c, a, b, tol) ||
                   PointOnSegment(d, a, b, tol);
        }

        public static double InterpolateZOnSegment(Vec2 p, Segment3 s)
        {
            double dx = s.B.X - s.A.X, dy = s.B.Y - s.A.Y;
            double den = dx * dx + dy * dy;
            if (den <= 1e-24) return s.A.Z;
            double t = ((p.X - s.A.X) * dx + (p.Y - s.A.Y) * dy) / den;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return s.A.Z + t * (s.B.Z - s.A.Z);
        }
    }
}