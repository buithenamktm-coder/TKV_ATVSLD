using System;
using System.Collections.Generic;
namespace MiningVolume.Core.Sections
{
    /// <summary>Bounded CAD annotations only; never changes volume bands.</summary>
    public static class SectionAxisTicks
    {
        public static IReadOnlyList<double> Create(double start, double end, double step, int maximum)
        {
            if (!Finite(start) || !Finite(end) || !Finite(step) || step <= 0 || end < start || maximum < 1)
                throw new ArgumentOutOfRangeException(nameof(step), "Invalid elevation axis range or step.");
            double intervals = (end - start) / step;
            if (!Finite(intervals) || intervals + 1 > maximum + 0.000001)
                throw new ArgumentOutOfRangeException(nameof(step), "Elevation axis exceeds annotation budget.");
            // A positive step can round back to the same double: never loop or silently coarsen.
            if (!Finite(start + step) || start + step <= start || end + step <= end)
                throw new ArgumentOutOfRangeException(nameof(step), "Elevation step is below coordinate precision.");
            int count = (int)Math.Floor(intervals + 0.000001) + 1;
            var values = new double[count];
            for (int i = 0; i < count; i++)
            {
                double value = start + i * step;
                if (!Finite(value) || (i > 0 && value <= values[i - 1]))
                    throw new ArgumentOutOfRangeException(nameof(step), "Elevation ticks are not strictly increasing.");
                values[i] = value;
            }
            return values;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
