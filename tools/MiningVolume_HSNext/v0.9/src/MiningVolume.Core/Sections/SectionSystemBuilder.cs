using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Geometry;

namespace MiningVolume.Core.Sections
{
    public sealed class SectionSystemBuilder
    {
        public SectionSystem Build(IReadOnlyList<Vec2> boundary, Vec2 directionInput, SectionGenerationOptions options)
        {
            if (boundary == null || boundary.Count < 3) throw new ArgumentException("Đường bao phải có ít nhất 3 đỉnh.", nameof(boundary));
            options = options ?? new SectionGenerationOptions();
            if (options.Spacing <= options.Tolerance) throw new ArgumentOutOfRangeException(nameof(options.Spacing), "Khoảng cách mặt cắt phải lớn hơn 0.");

            var d = Normalize(directionInput, options.Tolerance);
            var n = new Vec2(-d.Y, d.X);
            double minN = double.PositiveInfinity, maxN = double.NegativeInfinity;
            foreach (var p in boundary)
            {
                double q = Dot(p, n);
                minN = Math.Min(minN, q);
                maxN = Math.Max(maxN, q);
            }
            if (maxN - minN <= options.Tolerance) throw new InvalidOperationException("Đường bao không có bề rộng theo phương vuông góc hướng mặt cắt.");

            var offsets = new List<double>();
            double o = minN;
            int guard = 0;
            while (o <= maxN + options.Tolerance && guard++ < 1000000)
            {
                offsets.Add(Math.Min(o, maxN));
                o += options.Spacing;
            }
            if (options.AppendLastOffset && offsets.Count > 0 && maxN - offsets[offsets.Count - 1] > options.Tolerance * 10)
                offsets.Add(maxN);

            var lines = new List<SectionLine>();
            var warnings = new List<string>();
            int number = options.FirstNumber;
            foreach (var offset in offsets)
            {
                var line = BuildLineAtOffset(boundary, d, n, offset, number, options.NamePrefix, options.Tolerance, warnings);
                if (line == null) continue;
                lines.Add(line);
                number++;
            }

            if (lines.Count == 0) throw new InvalidOperationException("Không tạo được tuyến mặt cắt nào bên trong đường bao.");
            return new SectionSystem(d, n, boundary.ToArray(), lines, warnings);
        }

        public SectionLine BuildLineAtOffset(IReadOnlyList<Vec2> boundary, Vec2 directionInput, double offset, int index, string prefix = "MC-", double tol = 1e-7)
        {
            var d = Normalize(directionInput, tol);
            var n = new Vec2(-d.Y, d.X);
            return BuildLineAtOffset(boundary, d, n, offset, index, prefix, tol, null);
        }

        private static SectionLine BuildLineAtOffset(IReadOnlyList<Vec2> boundary, Vec2 d, Vec2 n, double offset, int index, string prefix, double tol, IList<string> warnings)
        {
            var ts = new List<double>();
            int count = boundary.Count;
            for (int i = 0; i < count; i++)
            {
                var a = boundary[i];
                var b = boundary[(i + 1) % count];
                double na = Dot(a, n) - offset;
                double nb = Dot(b, n) - offset;

                if (Math.Abs(na) <= tol && Math.Abs(nb) <= tol)
                {
                    ts.Add(Dot(a, d));
                    ts.Add(Dot(b, d));
                    continue;
                }
                if ((na > tol && nb > tol) || (na < -tol && nb < -tol)) continue;
                double den = na - nb;
                if (Math.Abs(den) <= tol) continue;
                double u = na / den;
                if (u < -tol || u > 1 + tol) continue;
                u = Math.Max(0, Math.Min(1, u));
                var p = new Vec2(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u);
                ts.Add(Dot(p, d));
            }

            ts.Sort();
            var unique = new List<double>();
            foreach (var t in ts)
                if (unique.Count == 0 || Math.Abs(t - unique[unique.Count - 1]) > tol * 10)
                    unique.Add(t);

            if (unique.Count < 2) return null;
            var spans = new List<SectionSpan>();
            for (int i = 0; i < unique.Count - 1; i++)
            {
                double t0 = unique[i], t1 = unique[i + 1];
                if (t1 - t0 <= tol) continue;
                double tm = (t0 + t1) * 0.5;
                var pm = new Vec2(d.X * tm + n.X * offset, d.Y * tm + n.Y * offset);
                if (Geometry2D.PointInPolygon(pm, boundary, tol * 10)) spans.Add(new SectionSpan(t0, t1));
            }
            if (spans.Count == 0) return null;
            if (spans.Count > 1 && warnings != null)
                warnings.Add($"{prefix}{index:00} cắt đường bao lõm thành {spans.Count} đoạn; phần mềm giữ đúng các đoạn nằm trong đường bao.");

            return new SectionLine(index, prefix + index.ToString("00"), offset, d, n, spans);
        }

        private static Vec2 Normalize(Vec2 v, double tol)
        {
            double len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            if (len <= tol) throw new ArgumentException("Hướng mặt cắt không hợp lệ (hai điểm chọn trùng nhau).", nameof(v));
            return new Vec2(v.X / len, v.Y / len);
        }
        internal static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
    }
}