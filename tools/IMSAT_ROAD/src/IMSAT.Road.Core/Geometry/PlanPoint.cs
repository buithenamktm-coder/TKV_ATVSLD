using System;

namespace IMSAT.Road.Core.Geometry
{
    public readonly struct PlanPoint
    {
        public PlanPoint(double x, double y)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(x), "Toa do phai la so huu han.");
            X = x;
            Y = y;
        }
        public double X { get; }
        public double Y { get; }
        public double DistanceTo(PlanPoint other)
        {
            double dx = other.X - X;
            double dy = other.Y - Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
