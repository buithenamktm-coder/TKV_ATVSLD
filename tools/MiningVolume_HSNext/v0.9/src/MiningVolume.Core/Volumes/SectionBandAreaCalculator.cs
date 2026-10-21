using System;
using System.Threading;
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

        public SectionBandAreas Calculate(SectionProfile profile, IReadOnlyList<LevelBand> bands, double tol = 1e-9, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (bands == null) throw new ArgumentNullException(nameof(bands));

            cancellationToken.ThrowIfCancellationRequested();
            var cut = new double[bands.Count];
            var fill = new double[bands.Count];
            // Per-call scratch buffers: never shared between parallel profiles.
            // Clear/reuse preserves vertex order and clipping arithmetic while
            // avoiding two lists and four delegates for every intersected band.
            var poly = new List<P>(4);
            var above = new List<P>(8);
            var clipped = new List<P>(8);
            foreach (var seg in profile.Segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double d0 = seg.ExistingZ0 - seg.DesignZ0;
                double d1 = seg.ExistingZ1 - seg.DesignZ1;
                if (seg.Length <= tol) continue;

                if (d0 * d1 < -tol * tol)
                {
                    double u = d0 / (d0 - d1);
                    u = Math.Max(0, Math.Min(1, u));
                    AddPiece(seg, 0, u, bands, cut, fill, tol, cancellationToken, poly, above, clipped);
                    AddPiece(seg, u, 1, bands, cut, fill, tol, cancellationToken, poly, above, clipped);
                }
                else AddPiece(seg, 0, 1, bands, cut, fill, tol, cancellationToken, poly, above, clipped);
            }

            var list = new List<BandArea>(bands.Count);
            for (int i = 0; i < bands.Count; i++) list.Add(new BandArea(bands[i], cut[i], fill[i]));
            cancellationToken.ThrowIfCancellationRequested();
            return new SectionBandAreas(profile.Line.Name, profile.Line.Offset, list);
        }

        private static void AddPiece(SectionProfileSegment seg, double u0, double u1,
            IReadOnlyList<LevelBand> bands, double[] cut, double[] fill, double tol, CancellationToken cancellationToken,
            List<P> poly, List<P> above, List<P> clipped)
        {
            if (u1 - u0 <= tol) return;
            double s0 = Lerp(seg.S0, seg.S1, u0), s1 = Lerp(seg.S0, seg.S1, u1);
            double e0 = Lerp(seg.ExistingZ0, seg.ExistingZ1, u0), e1 = Lerp(seg.ExistingZ0, seg.ExistingZ1, u1);
            double d0 = Lerp(seg.DesignZ0, seg.DesignZ1, u0), d1 = Lerp(seg.DesignZ0, seg.DesignZ1, u1);
            double dm = ((e0 + e1) - (d0 + d1)) * 0.5;
            if (Math.Abs(dm) <= tol) return;

            poly.Clear();
            poly.Add(new P(s0, e0)); poly.Add(new P(s1, e1));
            poly.Add(new P(s1, d1)); poly.Add(new P(s0, d0));
            // Clipping cannot produce area outside this piece's elevation
            // extent. Keep the same tolerance as the two clipping predicates:
            // near-boundary pieces must still take the exact clipping path.
            double minZ = Math.Min(Math.Min(e0, e1), Math.Min(d0, d1));
            double maxZ = Math.Max(Math.Max(e0, e1), Math.Max(d0, d1));
            for (int i = 0; i < bands.Count; i++)
            {
                if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (maxZ < bands[i].LowerZ - tol || minZ > bands[i].UpperZ + tol)
                    continue;
                ClipAbove(poly, bands[i].LowerZ, tol, above);
                ClipBelow(above, bands[i].UpperZ, tol, clipped);
                double a = Area(clipped);
                if (a <= tol) continue;
                if (dm > 0) cut[i] += a; else fill[i] += a;
            }
        }

        private static void ClipAbove(List<P> input, double z, double tol, List<P> output)
        {
            Clip(input, z, tol, true, output);
        }

        private static void ClipBelow(List<P> input, double z, double tol, List<P> output)
        {
            Clip(input, z, tol, false, output);
        }

        private static void Clip(List<P> input, double z, double tol, bool keepAbove, List<P> output)
        {
            output.Clear();
            if (input.Count == 0) return;
            P prev = input[input.Count - 1];
            bool prevIn = keepAbove ? prev.Z >= z - tol : prev.Z <= z + tol;
            foreach (var cur in input)
            {
                bool curIn = keepAbove ? cur.Z >= z - tol : cur.Z <= z + tol;
                if (curIn)
                {
                    if (!prevIn) output.Add(IntersectHorizontal(prev, cur, z));
                    output.Add(cur);
                }
                else if (prevIn) output.Add(IntersectHorizontal(prev, cur, z));
                prev = cur; prevIn = curIn;
            }
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
