using System;
using System.Collections.Generic;
using MiningVolume.Core.Geometry;

namespace MiningVolume.Core.Sections
{
    public sealed class SectionSpan
    {
        public SectionSpan(double startT, double endT)
        {
            if (endT < startT) { var t = startT; startT = endT; endT = t; }
            StartT = startT;
            EndT = endT;
        }
        public double StartT { get; }
        public double EndT { get; }
        public double Length => EndT - StartT;
    }

    public sealed class SectionLine
    {
        public SectionLine(int index, string name, double offset, Vec2 direction, Vec2 normal, IReadOnlyList<SectionSpan> spans)
        {
            Index = index;
            Name = name ?? ("MC-" + index.ToString("00"));
            Offset = offset;
            Direction = direction;
            Normal = normal;
            Spans = spans ?? Array.Empty<SectionSpan>();
        }
        public int Index { get; }
        public string Name { get; }
        public double Offset { get; }
        public Vec2 Direction { get; }
        public Vec2 Normal { get; }
        public IReadOnlyList<SectionSpan> Spans { get; }
        public double TotalLength
        {
            get
            {
                double s = 0;
                foreach (var x in Spans) s += x.Length;
                return s;
            }
        }
        public double MinT => Spans.Count == 0 ? 0 : Spans[0].StartT;
        public double MaxT => Spans.Count == 0 ? 0 : Spans[Spans.Count - 1].EndT;
        public Vec2 PointAt(double t) => new Vec2(Direction.X * t + Normal.X * Offset, Direction.Y * t + Normal.Y * Offset);
    }

    public sealed class SectionSystem
    {
        public SectionSystem(Vec2 direction, Vec2 normal, IReadOnlyList<Vec2> boundary, IReadOnlyList<SectionLine> lines, IReadOnlyList<string> warnings)
        {
            Direction = direction;
            Normal = normal;
            Boundary = boundary ?? Array.Empty<Vec2>();
            Lines = lines ?? Array.Empty<SectionLine>();
            Warnings = warnings ?? Array.Empty<string>();
        }
        public Vec2 Direction { get; }
        public Vec2 Normal { get; }
        public IReadOnlyList<Vec2> Boundary { get; }
        public IReadOnlyList<SectionLine> Lines { get; }
        public IReadOnlyList<string> Warnings { get; }
    }

    public sealed class SectionGenerationOptions
    {
        public double Spacing { get; set; } = 20.0;
        public double Tolerance { get; set; } = 1e-7;
        public string NamePrefix { get; set; } = "MC-";
        public int FirstNumber { get; set; } = 1;
        public bool AppendLastOffset { get; set; } = true;
    }

    public sealed class SectionProfileSegment
    {
        public SectionProfileSegment(double s0, double s1, Vec2 map0, Vec2 map1,
            double existingZ0, double existingZ1, double designZ0, double designZ1)
        {
            S0 = s0; S1 = s1; Map0 = map0; Map1 = map1;
            ExistingZ0 = existingZ0; ExistingZ1 = existingZ1;
            DesignZ0 = designZ0; DesignZ1 = designZ1;
        }
        public double S0 { get; }
        public double S1 { get; }
        public Vec2 Map0 { get; }
        public Vec2 Map1 { get; }
        public double ExistingZ0 { get; }
        public double ExistingZ1 { get; }
        public double DesignZ0 { get; }
        public double DesignZ1 { get; }
        public double Length => S1 - S0;
    }

    public sealed class SectionProfile
    {
        public SectionProfile(SectionLine line, IReadOnlyList<SectionProfileSegment> segments, double cutArea, double fillArea, IReadOnlyList<string> warnings)
        {
            Line = line;
            Segments = segments ?? Array.Empty<SectionProfileSegment>();
            CutArea = cutArea;
            FillArea = fillArea;
            Warnings = warnings ?? Array.Empty<string>();
            MinZ = double.PositiveInfinity;
            MaxZ = double.NegativeInfinity;
            foreach (var s in Segments)
            {
                MinZ = Math.Min(MinZ, Math.Min(Math.Min(s.ExistingZ0, s.ExistingZ1), Math.Min(s.DesignZ0, s.DesignZ1)));
                MaxZ = Math.Max(MaxZ, Math.Max(Math.Max(s.ExistingZ0, s.ExistingZ1), Math.Max(s.DesignZ0, s.DesignZ1)));
            }
            if (double.IsInfinity(MinZ)) { MinZ = 0; MaxZ = 0; }
        }
        public SectionLine Line { get; }
        public IReadOnlyList<SectionProfileSegment> Segments { get; }
        public double CutArea { get; }
        public double FillArea { get; }
        public IReadOnlyList<string> Warnings { get; }
        public double MinZ { get; private set; }
        public double MaxZ { get; private set; }
        public bool HasData => Segments.Count > 0;
    }
}