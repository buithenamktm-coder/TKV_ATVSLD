using System;
using System.Threading;
using System.Collections.Generic;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Surface;

namespace MiningVolume.Core.Sections
{
    /// <summary>
    /// Independent survey section from one or more user-selected CAD LINE /
    /// POLYLINE routes. Stations follow cumulative route distance, including
    /// bends, rather than the parallel section system used to compute volumes.
    /// Does not touch SectionSystem, official profiles or quantity calculations.
    /// </summary>
    public sealed class QuickSectionProfileBuilder
    {
        public SectionProfile Build(int index, string name, IReadOnlyList<Vec2> route,
            TinSurface existing, TinSurface design, TinSectionSampler sampler = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (route == null || route.Count < 2)
                throw new ArgumentException("Tuyến nhanh phải có ít nhất hai đỉnh.", nameof(route));
            if (existing == null || design == null)
                throw new InvalidOperationException("Cần có TIN hiện trạng và TIN thiết kế để vẽ mặt cắt nhanh.");
            cancellationToken.ThrowIfCancellationRequested();
            sampler = sampler ?? new TinSectionSampler();
            var segments = new List<SectionProfileSegment>();
            var warnings = new List<string>();
            double total = 0, cut = 0, fill = 0;
            for (int i = 1; i < route.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var from = route[i - 1];
                var to = route[i];
                double dx = to.X - from.X, dy = to.Y - from.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (double.IsNaN(len) || double.IsInfinity(len) || len < 1e-7)
                {
                    warnings.Add(name + ": bỏ qua đoạn tuyến trùng đỉnh.");
                    continue;
                }
                var d = new Vec2(dx / len, dy / len);
                var n = new Vec2(-d.Y, d.X);
                double offset = from.X * n.X + from.Y * n.Y;
                double start = from.X * d.X + from.Y * d.Y;
                var line = new SectionLine(i, name + "-seg" + i, offset,
                    d, n, new[] { new SectionSpan(start, start + len) });
                var piece = sampler.BuildProfile(line, existing, design, cancellationToken: cancellationToken);
                foreach (var segment in piece.Segments)
                    segments.Add(new SectionProfileSegment(
                        total + segment.S0, total + segment.S1,
                        segment.Map0, segment.Map1,
                        segment.ExistingZ0, segment.ExistingZ1,
                        segment.DesignZ0, segment.DesignZ1));
                cut += piece.CutArea; fill += piece.FillArea;
                foreach (var note in piece.Warnings) warnings.Add(note);
                total += len;
            }
            if (total < 1e-7)
                throw new InvalidOperationException(name + ": tuyến không có chiều dài hợp lệ.");
            // This synthetic station line describes ONLY the continuous
            // profile drawing coordinate. The real route is retained on each
            // segment's Map0/Map1, including all original CAD bends.
            var chainage = new SectionLine(index, name, 0,
                new Vec2(1, 0), new Vec2(0, 1),
                new[] { new SectionSpan(0, total) });
            if (segments.Count == 0)
                warnings.Add(name + ": không xác định được giao tuyến đồng thời với hai TIN.");
            cancellationToken.ThrowIfCancellationRequested();
            return new SectionProfile(chainage, segments, cut, fill, warnings);
        }
    }
}
