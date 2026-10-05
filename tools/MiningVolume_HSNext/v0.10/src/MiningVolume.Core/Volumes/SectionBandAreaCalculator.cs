using System;
using System.Collections.Generic;
using MiningVolume.Core.Sections;

namespace MiningVolume.Core.Volumes
{
    /// <summary>
    /// Chia chính xác phần diện tích giữa đường hiện trạng và thiết kế trên mặt cắt
    /// theo các dải cao độ. Mỗi đoạn tuyến tính được tách tại vị trí đổi dấu đào/đắp,
    /// sau đó đa giác trong mặt phẳng (s,z) được clip bởi hai đường cao độ của tầng.
    /// </summary>
    public sealed class SectionBandAreaCalculator
    {
        private struct P
        {
            public P(double x, double z) { X = x; Z = z; }
            public double X, Z;
        }

        public SectionBandAreas Calculate(SectionProfile profile, IReadOnlyList<LevelBand> bands, double tol = 1e-9)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (bands == null) throw new ArgumentNullException(nameof(bands));

            var cut = new double[bands.Count];
            var fill = new double[bands.Count];
            foreach (var seg in profile.Segments)
            {
                double d0 = seg.ExistingZ0 - seg.DesignZ0;
                double d1 = seg.ExistingZ1 - seg.DesignZ1;
                if (seg.Length <= tol) continue;

                if (d0 * d1 < -tol * tol)
                {
                    double u = d0 / (d0 - d1);
                    u = Math.Max(0, Math.Min(1, u));
                    AddPiece(seg, 0, u, bands, cut, fill, tol);
                    AddPiece(seg, u, 1, bands, cut, fill, tol);
                }
                else AddPiece(seg, 0, 1, bands, cut, fill, tol);
            }

            var list = new List<BandArea>(bands.Count);
            for (int i = 0; i < bands.Count; i++) list.Add(new BandArea(bands[i], cut[i], fill[i]));
            return new SectionBandAreas(profile.Line.Name, profile.Line.Offset, list);
        }

        private static void AddPiece(SectionProfileSegment seg, double u0, double u1,
            IReadOnlyList<LevelBand> bands, double[] cut, double[] fill, double tol)
        {
            if (u1 - u0 <= tol) return;
            double s0 = Lerp(seg.S0, seg.S1, u0), s1 = Lerp(seg.S0, seg.S1, u1);
            double e0 = Lerp(seg.ExistingZ0, seg.ExistingZ1, u0), e1 = Lerp(seg.ExistingZ0, seg.ExistingZ1, u1);
            double d0 = Lerp(seg.DesignZ0, seg.DesignZ1, u0), d1 = Lerp(seg.DesignZ0, seg.DesignZ1, u1);
            double dm = ((e0 + e1) - (d0 + d1)) * 0.5;
            if (Math.Abs(dm) <= tol) return;

            var poly = new List<P>
            {
                new P(s0, e0), new P(s1, e1), new P(s1, d1), new P(s0, d0)
            };
            for (int i = 0; i < bands.Count; i++)
            {
                var clipped = ClipAbove(poly, bands[i].LowerZ, tol);
                clipped = ClipBelow(clipped, bands[i].UpperZ, tol);
                double a = Area(clipped);
                if (a <= tol) continue;
                if (dm > 0) cut[i] += a; else fill[i] += a;
            }
        }

        private static List<P> ClipAbove(List<P> input, double z, double tol)
        {
            return Clip(input, p => p.Z >= z - tol, (a, b) => IntersectHorizontal(a, b, z));
        }

        private static List<P> ClipBelow(List<P> input, double z, double tol)
        {
            return Clip(input, p => p.Z <= z + tol, (a, b) => IntersectHorizontal(a, b, z));
        }

        private static List<P> Clip(List<P> input, Func<P, bool> inside, Func<P, P, P> intersect)
        {
            var output = new List<P>();
            if (input == null || input.Count == 0) return output;
            P prev = input[input.Count - 1];
            bool prevIn = inside(prev);
            foreach (var cur in input)
            {
                bool curIn = inside(cur);
                if (curIn)
                {
                    if (!prevIn) output.Add(intersect(prev, cur));
                    output.Add(cur);
                }
                else if (prevIn) output.Add(intersect(prev, cur));
                prev = cur; prevIn = curIn;
            }
            return output;
        }

        private static P IntersectHorizontal(P a, P b, double z)
        {
            double dz = b.Z - a.Z;
            if (Math.Abs(dz) < 1e-30) return new P(a.X, z);
            double u = (z - a.Z) / dz;
            return new P(a.X + (b.X - a.X) * u, z);
        }

        private static double Area(List<P> poly)
        {
            if (poly == null || poly.Count < 3) return 0;
            double a = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                P p = poly[i], q = poly[(i + 1) % poly.Count];
                a += p.X * q.Z - q.X * p.Z;
            }
            return Math.Abs(a) * 0.5;
        }

        private static double Lerp(double a, double b, double u) => a + (b - a) * u;
    }
}
