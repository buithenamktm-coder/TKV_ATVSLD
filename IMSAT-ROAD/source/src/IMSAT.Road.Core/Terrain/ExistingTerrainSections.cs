using System;
using System.Collections.Generic;
using IMSAT.Road.Core.Geometry;

namespace IMSAT.Road.Core.Terrain
{
    /// <summary>ONE existing-terrain surface: design TIN is not an input.</summary>
    public interface IExistingTerrain
    {
        bool TryElevation(double x, double y, out double z);
    }

    public sealed class NaturalSectionPoint
    {
        public double OffsetMetres { get; set; } // positive is left of forward alignment
        public double X { get; set; }
        public double Y { get; set; }
        public double? ExistingElevation { get; set; }
    }

    public sealed class NaturalSection
    {
        public double Station { get; set; }
        public IReadOnlyList<NaturalSectionPoint> Points { get; set; }
        public IReadOnlyList<string> Warnings { get; set; }
    }

    public static class ExistingTerrainSections
    {
        public static NaturalSection Sample(
            PolylineAlignment alignment, IExistingTerrain terrain,
            double station, double leftWidth, double rightWidth, double step)
        {
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            if (leftWidth < 0 || rightWidth < 0 || step <= 0 ||
                !IsFinite(leftWidth) || !IsFinite(rightWidth) || !IsFinite(step))
                throw new ArgumentOutOfRangeException(nameof(step), "Be rong va buoc lay mau khong hop le.");
            // Limit accidental excessive allocations; requested large sections must be deliberate.
            if ((leftWidth + rightWidth) / step > 100000)
                throw new ArgumentException("So diem lay mau vuot 100000. Tang buoc lay mau.");

            var positions = alignment.PositionAt(station);
            var offsets = new SortedSet<double>();
            offsets.Add(-rightWidth);
            offsets.Add(0);
            offsets.Add(leftWidth);
            for (double t = step; t < leftWidth; t += step) offsets.Add(t);
            for (double t = step; t < rightWidth; t += step) offsets.Add(-t);
            var points = new List<NaturalSectionPoint>();
            var warnings = new List<string>();
            foreach (double offset in offsets)
            {
                double x = positions.Point.X + offset * positions.LeftNormal.X;
                double y = positions.Point.Y + offset * positions.LeftNormal.Y;
                double z;
                bool covered = terrain.TryElevation(x, y, out z) && IsFinite(z);
                points.Add(new NaturalSectionPoint
                {
                    OffsetMetres = offset,
                    X = x,
                    Y = y,
                    ExistingElevation = covered ? (double?)z : null
                });
                if (!covered)
                    warnings.Add("Thieu cao do TIN hien trang: LT=" + station.ToString("F3") +
                                 "m, offset=" + offset.ToString("F3") + "m.");
            }
            return new NaturalSection { Station = station, Points = points, Warnings = warnings };
        }

        private static bool IsFinite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
    }
}
