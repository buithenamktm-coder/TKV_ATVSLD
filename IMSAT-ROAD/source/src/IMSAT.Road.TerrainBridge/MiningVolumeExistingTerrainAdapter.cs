using System;
using IMSAT.Road.Core.Terrain;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Sections;
using MiningVolume.Core.Surface;

namespace IMSAT.Road.TerrainBridge
{
    /// <summary>
    /// Reuses an existing MiningVolume TIN and its indexed elevation sampler.
    /// No design TIN is provided or requested.
    /// </summary>
    public sealed class MiningVolumeExistingTerrainAdapter : IExistingTerrain
    {
        private readonly TinSectionSampler _sampler = new TinSectionSampler();
        private readonly TinSurface _existing;
        private readonly double _tolerance;

        public MiningVolumeExistingTerrainAdapter(TinSurface existing, double tolerance = 1e-7)
        {
            _existing = existing ?? throw new ArgumentNullException(nameof(existing));
            if (tolerance <= 0 || double.IsNaN(tolerance) || double.IsInfinity(tolerance))
                throw new ArgumentOutOfRangeException(nameof(tolerance));
            _tolerance = tolerance;
        }

        public bool TryElevation(double x, double y, out double z)
        {
            return _sampler.TryElevation(_existing, new Vec2(x, y), _tolerance, out z);
        }
    }
}
