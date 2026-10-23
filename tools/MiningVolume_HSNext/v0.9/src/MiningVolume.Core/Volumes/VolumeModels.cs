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
            Bands = bands ?? MiningVolume.Core.Compatibility.EmptyArray<BandArea>.Value;
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
        // One authoritative contribution per elevation band; summaries sum these values.
        public List<SectionIntervalVolume> BandVolumes { get; } = new List<SectionIntervalVolume>();
        public bool HasMidArea { get; set; } = true;
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

        // Ngưỡng chênh tương đối giữa hai diện tích liền kề.
        // 0.40 tương ứng 40% và được dùng bởi quy tắc công thức thích ứng V1.0.
        public double AreaDifferenceThreshold { get; set; } = 0.40;

        public double Tolerance { get; set; } = 1e-7;

        // Giữ thuộc tính cũ để đọc dữ liệu/nguồn cũ; V1.0 không ép mọi khoảng dùng prismoid.
        public bool PreferPrismoidal { get; set; } = false;
    }

    public sealed class VolumeResult
    {
        // Identity belongs to a calculation result, never to an export attempt.
        // Re-exporting this object preserves the ID; recalculation creates a new one.
        public string CalculationId { get; } = Guid.NewGuid().ToString("N");

        public VolumeResult(IReadOnlyList<LevelBand> bands,
            IReadOnlyList<SectionIntervalVolume> intervals,
            IReadOnlyList<LevelVolumeSummary> levels,
            IReadOnlyList<SectionBandAreas> sectionAreas,
            IReadOnlyList<string> warnings,
            int skippedIntervals = 0)
        {
            ExcludedIntervals = skippedIntervals;
            Bands = bands ?? MiningVolume.Core.Compatibility.EmptyArray<LevelBand>.Value;
            Intervals = intervals ?? MiningVolume.Core.Compatibility.EmptyArray<SectionIntervalVolume>.Value;
            Levels = levels ?? MiningVolume.Core.Compatibility.EmptyArray<LevelVolumeSummary>.Value;
            SectionAreas = sectionAreas ?? MiningVolume.Core.Compatibility.EmptyArray<SectionBandAreas>.Value;
            Warnings = warnings ?? MiningVolume.Core.Compatibility.EmptyArray<string>.Value;
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
        public int ExcludedIntervals { get; }
        public bool IsComplete => ExcludedIntervals == 0 && Intervals.Count > 0;
        // Partial valid intervals are informative, not a mine-wide certified total.
        public double TotalCutVolume { get; }
        public double TotalFillVolume { get; }
        public double NetVolume => TotalCutVolume - TotalFillVolume;
    }
}
