using System;
using System.Collections.Generic;
using System.Linq;
using IMSAT.Road.Core;
using IMSAT.Road.Core.Geometry;
using IMSAT.Road.Core.Standards;
using IMSAT.Road.Core.Terrain;

namespace IMSAT.Road.Smoke
{
    internal static class Program
    {
        private sealed class FlatTerrain : IExistingTerrain
        {
            public bool TryElevation(double x, double y, out double z)
            {
                z = 100;
                return true;
            }
        }

        private sealed class NoCoverageTerrain : IExistingTerrain
        {
            public bool TryElevation(double x, double y, out double z)
            {
                z = 0;
                return false;
            }
        }

        private static int _passed;
        private static void Assert(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL: " + description);
            _passed++;
            Console.WriteLine("PASS: " + description);
        }

        private static void Nearly(double actual, double expected, string description)
            => Assert(Math.Abs(actual - expected) < 1e-9, description);

        private static void Main()
        {
            var options = new RoadSettings();
            options.Validate();
            Assert(options.StandardCode == "TCVN 4054:2005" && options.RoadClass == "III"
                && options.TerrainType == "Dong bang" && options.CheckTechnicalCriteria,
                "Default TCVN 4054:2005 / cap III / dong bang / checks ON");

            options.IsMineRoad = true;
            bool mineRejected = false;
            try { options.Validate(); } catch (ArgumentException) { mineRejected = true; }
            Assert(mineRejected, "Mine road does not silently inherit TCVN 4054");

            var alignment = new PolylineAlignment(new[]
            {
                new PlanPoint(0, 0), new PlanPoint(3, 4), new PlanPoint(13, 4)
            }, 1000);
            Nearly(alignment.Length, 15, "Polyline 2D station length");
            Nearly(alignment.PointAt(1002.5).X, 1.5, "First segment interpolation X");
            Nearly(alignment.PointAt(1002.5).Y, 2, "First segment interpolation Y");
            Nearly(alignment.PointAt(1010).X, 8, "Second segment interpolation X");

            var section = ExistingTerrainSections.Sample(alignment, new FlatTerrain(),
                1010, 2, 2, 2);
            Assert(section.Points.Count == 3 && section.Warnings.Count == 0,
                "One-TIN existing profile has three sampled points");
            Nearly(section.Points[0].Y, 2, "Right offset for east-facing centreline");
            Nearly(section.Points[2].Y, 6, "Left offset for east-facing centreline");
            Nearly(section.Points[1].ExistingElevation.Value, 100, "Existing elevation read");

            var missing = ExistingTerrainSections.Sample(alignment, new NoCoverageTerrain(),
                1010, 1, 1, 1);
            Assert(missing.Warnings.Count == 3 &&
                missing.Points.All(p => !p.ExistingElevation.HasValue),
                "Missing terrain is reported; no fake Z=0");

            var uncheckedRule = DesignCriterion.CheckMin("R", 1000, 80, null, "");
            Assert(uncheckedRule.Status == CriterionStatus.NotEvaluated,
                "Unknown standard limit never shows false PASS");
            var fails = DesignCriterion.CheckMin("R", 1000, 80, 100, "TEST SOURCE");
            Assert(fails.Status == CriterionStatus.Fail, "Radius fails known test limit");
            var passes = DesignCriterion.CheckMax("i", 1000, 4, 5, "TEST SOURCE");
            Assert(passes.Status == CriterionStatus.Pass, "Grade passes known test limit");
            Console.WriteLine("IMSAT ROAD CORE SMOKE PASS (" + _passed + " assertions)");
        }
    }
}
