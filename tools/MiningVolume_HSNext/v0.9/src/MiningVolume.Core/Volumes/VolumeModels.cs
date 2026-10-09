using System;
using System.Collections.Generic;

namespace MiningVolume.Core.Volumes
{
    public enum VolumeFormulaKind
    {
        AverageEndArea,
        Prismoidal,
        Frustum,
        Pyramid
    }

    public sealed class LevelBand
    {
        public LevelBand(int index, double startZ, double endZ)
        {
            Index = index;
            StartZ = startZ;
            EndZ = endZ;
            LowerZ = Math.Min(startZ, endZ);
            UpperZ = Math.Max(startZ, endZ);
            Name = FormatLevel(startZ) + " ÷ " + FormatLevel(endZ);
        }
        public int Index { get; }
        public double StartZ { get; }
        public double EndZ { get; }
        public double LowerZ { get; }
        public double UpperZ { get; }
        public string Name { get; }

        private static string FormatLevel(double z)
        {
            if (Math.Abs(z) < 5e-10) z = 0;
            return z > 0 ? "+" + z.ToString("0.###") : z.ToString("0.###");
        }
    }

    public sealed class BandArea
    {
        public BandArea(LevelBand band, double cutArea, double fillArea)
        {
            Band = band;
            CutArea = cutArea;
            FillArea = fillArea;
        }
        public LevelBand Band { get; }
        public double CutArea { get; }
        public double FillArea { get; }
    }

    public sealed class SectionBandAreas
    {
        public SectionBandAreas(string sectionName, double offset, IReadOnlyList<BandArea> bands)
        {
            SectionName = sectionName;
            Offset = offset;
            Bands = bands ?? Array.Empty<BandArea>();
            double c = 0, f = 0;
            foreach (var b in Bands) { c += b.CutArea; f += b.FillArea; }
            CutArea = c;
            FillArea = f;
        }
        public string SectionName { get; }
        public double Offset { get; }
        public IReadOnlyList<BandArea> Bands { get; }
        public double CutArea { get; }
        public double FillArea { get; }
    }

    public sealed class SectionIntervalVolume
    {
        public int Index { get; set; }
        public string StartSection { get; set; }
        public string EndSection { get; set; }
        public double Distance { get; set; }

        public double CutAreaStart { get; set; }
        public double CutAreaMid { get; set; }
        public double CutAreaEnd { get; set; }
        public double CutVolume { get; set; }
        public VolumeFormulaKind CutFormula { get; set; }

        public double FillAreaStart { get; set; }
        public double FillAreaMid { get; set; }
        public double FillAreaEnd { get; set; }
        public double FillVolume { get; set; }
        public VolumeFormulaKind FillFormula { get; set; }
        public string Note { get; set; }
    }

    public sealed class LevelVolumeSummary
    {
        public LevelBand Band { get; set; }
        public double CutVolume { get; set; }
        public double FillVolume { get; set; }
        public double NetVolume => CutVolume - FillVolume;
    }

    public sealed class VolumeCalculationOptions
    {
        public double FromLevel { get; set; }
        public double ToLevel { get; set; }
        public double LevelStep { get; set; } = 5.0;
        public double Tolerance { get; set; } = 1e-7;
        public bool PreferPrismoidal { get; set; } = true;
    }

    public sealed class VolumeResult
    {
        public VolumeResult(IReadOnlyList<LevelBand> bands,
            IReadOnlyList<SectionIntervalVolume> intervals,
            IReadOnlyList<LevelVolumeSummary> levels,
            IReadOnlyList<SectionBandAreas> sectionAreas,
            IReadOnlyList<string> warnings)
        {
            Bands = bands ?? Array.Empty<LevelBand>();
            Intervals = intervals ?? Array.Empty<SectionIntervalVolume>();
            Levels = levels ?? Array.Empty<LevelVolumeSummary>();
            SectionAreas = sectionAreas ?? Array.Empty<SectionBandAreas>();
            Warnings = warnings ?? Array.Empty<string>();
            double c = 0, f = 0;
            foreach (var x in Intervals) { c += x.CutVolume; f += x.FillVolume; }
            TotalCutVolume = c;
            TotalFillVolume = f;
        }

        public IReadOnlyList<LevelBand> Bands { get; }
        public IReadOnlyList<SectionIntervalVolume> Intervals { get; }
        public IReadOnlyList<LevelVolumeSummary> Levels { get; }
        public IReadOnlyList<SectionBandAreas> SectionAreas { get; }
        public IReadOnlyList<string> Warnings { get; }
        public double TotalCutVolume { get; }
        public double TotalFillVolume { get; }
        public double NetVolume => TotalCutVolume - TotalFillVolume;
    }
}