using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Geometry;

namespace MiningVolume.Core.Sections
{
    /// <summary>
    /// One editable CAD polyline run in section (station, elevation) coordinates.
    /// A run represents only the common TIN coverage originally calculated.
    /// The CAD drawing is input for this operation, not merely an illustration.
    /// </summary>
    public sealed class EditedSectionVertex
    {
        public EditedSectionVertex(double station, double z) { Station = station; Z = z; }
        public double Station { get; }
        public double Z { get; }
    }

    public sealed class EditedSectionPath
    {
        public EditedSectionPath(int run, IReadOnlyList<EditedSectionVertex> vertices)
        {
            Run = run; Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        }
        public int Run { get; }
        public IReadOnlyList<EditedSectionVertex> Vertices { get; }
    }

    public static class EditedSectionProfileBuilder
    {
        public static SectionProfile Rebuild(SectionProfile original,
            IReadOnlyList<EditedSectionPath> existing,
            IReadOnlyList<EditedSectionPath> design,
            double tolerance = 1e-6)
        {
            if (original == null || !original.HasData)
                throw new InvalidOperationException("Mặt cắt gốc không có dữ liệu hợp lệ.");
            if (existing == null || design == null)
                throw new ArgumentNullException("Đường mặt cắt chỉnh sửa không được rỗng.");
            if (tolerance <= 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
            var ranges = new List<(double start, double end)>();
            foreach (var seg in original.Segments)
            {
                if (ranges.Count == 0 || Math.Abs(ranges[ranges.Count - 1].end - seg.S0) > tolerance)
                    ranges.Add((seg.S0, seg.S1));
                else
                {
                    var last = ranges[ranges.Count - 1];
                    ranges[ranges.Count - 1] = (last.start, seg.S1);
                }
            }
            if (existing.Count != ranges.Count || design.Count != ranges.Count)
                throw new InvalidOperationException("Số đoạn đường mặt cắt khác với dữ liệu gốc. Kiểm tra đường đã xóa hoặc bị tách.");
            var byExisting = existing.ToDictionary(x => x.Run);
            var byDesign = design.ToDictionary(x => x.Run);
            if (byExisting.Count != ranges.Count || byDesign.Count != ranges.Count)
                throw new InvalidOperationException("Mặt cắt có mã đoạn trùng hoặc thiếu.");
            var result = new List<SectionProfileSegment>();
            double cut = 0, fill = 0;
            for (int run = 0; run < ranges.Count; run++)
            {
                if (!byExisting.TryGetValue(run, out var e) || !byDesign.TryGetValue(run, out var d))
                    throw new InvalidOperationException("Thiếu đường hiện trạng hoặc thiết kế tại đoạn " + (run + 1));
                Validate(e, ranges[run], tolerance);
                Validate(d, ranges[run], tolerance);
                var stations = e.Vertices.Select(x => x.Station)
                    .Concat(d.Vertices.Select(x => x.Station)).OrderBy(x => x).ToList();
                var knots = new List<double>(stations.Count);
                foreach (var x in stations)
                    if (knots.Count == 0 || x - knots[knots.Count - 1] > tolerance)
                        knots.Add(x);
                knots[0] = ranges[run].start;
                knots[knots.Count - 1] = ranges[run].end;
                for (int i = 1; i < knots.Count; i++)
                {
                    double a = knots[i - 1], c = knots[i];
                    if (c - a <= tolerance) continue;
                    double e0 = At(e.Vertices, a, tolerance), e1 = At(e.Vertices, c, tolerance);
                    double d0 = At(d.Vertices, a, tolerance), d1 = At(d.Vertices, c, tolerance);
                    var line = original.Line;
                    result.Add(new SectionProfileSegment(a, c,
                        line.PointAt(line.MinT + a), line.PointAt(line.MinT + c),
                        e0, e1, d0, d1));
                    AddAreas(c - a, e0 - d0, e1 - d1, ref cut, ref fill);
                }
            }
            if (result.Count == 0)
                throw new InvalidOperationException("Mặt cắt chỉnh sửa không còn vùng hợp lệ để tính diện tích.");
            return new SectionProfile(original.Line, result, cut, fill,
                new[] { "Đã cập nhật từ đường mặt cắt do người dùng hiệu chỉnh trên CAD." },true);
        }

        public static bool SameGeometry(IReadOnlyList<SectionProfile> a,IReadOnlyList<SectionProfile> b)
        {
            if(a==null || b==null || a.Count!=b.Count)return false;
            for(int i=0;i<a.Count;i++) {
                if(a[i]==null || b[i]==null) {if(a[i]!=b[i])return false;continue;}
                if(a[i].Line.Name!=b[i].Line.Name || a[i].Segments.Count!=b[i].Segments.Count)return false;
                for(int j=0;j<a[i].Segments.Count;j++) {
                    var x=a[i].Segments[j];var y=b[i].Segments[j];
                    if(x.S0!=y.S0 || x.S1!=y.S1 || x.ExistingZ0!=y.ExistingZ0 || x.ExistingZ1!=y.ExistingZ1 ||
                        x.DesignZ0!=y.DesignZ0 || x.DesignZ1!=y.DesignZ1)return false;
                }
            }
            return true;
        }

        private static void Validate(EditedSectionPath path, (double start, double end) range, double tol)
        {
            if (path.Vertices.Count < 2)
                throw new InvalidOperationException("Đường mặt cắt phải có ít nhất hai đỉnh.");
            var a = path.Vertices[0];
            var z = path.Vertices[path.Vertices.Count - 1];
            if (Math.Abs(a.Station - range.start) > tol || Math.Abs(z.Station - range.end) > tol)
                throw new InvalidOperationException("Không được thay đổi hai đầu đoạn mặt cắt ngoài vùng TIN đã kiểm tra.");
            double prev = double.NegativeInfinity;
            foreach (var v in path.Vertices)
            {
                if (double.IsNaN(v.Station) || double.IsInfinity(v.Station) ||
                    double.IsNaN(v.Z) || double.IsInfinity(v.Z) ||
                    v.Station <= prev + tol || v.Station < range.start - tol ||
                    v.Station > range.end + tol)
                    throw new InvalidOperationException("Đường mặt cắt phải đi theo X tăng dần, không gấp ngược, trùng đỉnh hoặc vượt biên.");
                prev = v.Station;
            }
        }

        private static double At(IReadOnlyList<EditedSectionVertex> nodes, double station, double tol)
        {
            if (station <= nodes[0].Station + tol) return nodes[0].Z;
            if (station >= nodes[nodes.Count - 1].Station - tol) return nodes[nodes.Count - 1].Z;
            int lo = 0, hi = nodes.Count - 1;
            while (hi - lo > 1)
            {
                int mid = lo + (hi - lo) / 2;
                if (nodes[mid].Station <= station) lo = mid; else hi = mid;
            }
            var a = nodes[lo]; var b = nodes[hi];
            return a.Z + (b.Z - a.Z) * ((station - a.Station) / (b.Station - a.Station));
        }

        private static void AddAreas(double length, double delta0, double delta1, ref double cut, ref double fill)
        {
            if (delta0 >= 0 && delta1 >= 0) { cut += length * (delta0 + delta1) / 2; return; }
            if (delta0 <= 0 && delta1 <= 0) { fill -= length * (delta0 + delta1) / 2; return; }
            double a = Math.Abs(delta0), b = Math.Abs(delta1);
            double first = length * a / (a + b);
            if (delta0 > 0) { cut += a * first / 2; fill += b * (length - first) / 2; }
            else { fill += a * first / 2; cut += b * (length - first) / 2; }
        }
    }
}
