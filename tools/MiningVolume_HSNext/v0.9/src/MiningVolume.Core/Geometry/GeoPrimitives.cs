using System;

namespace MiningVolume.Core.Geometry
{
    public readonly struct Vec2
    {
        public Vec2(double x, double y) { X = x; Y = y; }
        public double X { get; }
        public double Y { get; }

        public double DistanceTo(Vec2 p)
        {
            double dx = X - p.X, dy = Y - p.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static double Cross(Vec2 a, Vec2 b, Vec2 c)
            => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    }

    public readonly struct Vec3
    {
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public Vec2 XY => new Vec2(X, Y);
    }

    public readonly struct Segment3
    {
        public Segment3(Vec3 a, Vec3 b, string sourceId = null)
        {
            A = a; B = b; SourceId = sourceId ?? string.Empty;
        }
        public Vec3 A { get; }
        public Vec3 B { get; }
        public string SourceId { get; }
        public double Length2D => A.XY.DistanceTo(B.XY);
    }

    public readonly struct Triangle3
    {
        public Triangle3(Vec3 a, Vec3 b, Vec3 c) { A = a; B = b; C = c; }
        public Vec3 A { get; }
        public Vec3 B { get; }
        public Vec3 C { get; }
        public double Area2D => Math.Abs(Vec2.Cross(A.XY, B.XY, C.XY)) * 0.5;
        public Vec2 Centroid2D => new Vec2((A.X + B.X + C.X) / 3.0, (A.Y + B.Y + C.Y) / 3.0);
    }
}