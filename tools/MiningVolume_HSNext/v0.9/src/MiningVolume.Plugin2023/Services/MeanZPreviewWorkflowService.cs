using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;
using MiningVolume.Core.Surface;
using MiningVolume.Surface;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// ISOLATED EXPERIMENT: all enabled source vertices, Z = arithmetic mean
    /// at coincident XY, no inherited breakline constraints, native CDT mesh.
    /// Never writes to ModelSession.Tin, existing/design official TIN layers,
    /// SectionProfiles, VolumeResult, or original CAD/source geometry.
    /// </summary>
    internal static class MeanZPreviewWorkflowService
    {
        internal const string ExistingTestLayer = "MV_TIN_ZMEAN_HT_TEST_ONLY";
        internal const string DesignTestLayer = "MV_TIN_ZMEAN_TK_TEST_ONLY";
        private const int MaxDrawnPreviewTriangles = 100000;

        internal sealed class PreviewResult
        {
            public ModelRole Role { get; internal set; }
            public MeanZPreviewPreparer.Result Analysis { get; internal set; }
            public TinSurface Surface { get; internal set; }
            public IReadOnlyList<Triangle3> DrawnTriangles { get; internal set; }
            public long SourceRevision { get; internal set; }
            public SurfaceModel Source { get; internal set; }
            public int TotalTriangles => Surface?.Triangles?.Count ?? 0;
            public bool DrawIsSampled => DrawnTriangles != null && DrawnTriangles.Count < TotalTriangles;
            public string Layer => LayerFor(Role);
            public void Dispose()
            {
                try { (Surface?.Triangles as IDisposable)?.Dispose(); } catch { }
            }
        }

        internal static string LayerFor(ModelRole role)
            => role == ModelRole.Existing ? ExistingTestLayer : DesignTestLayer;

        internal static PreviewResult Build(ModelRole role,
            Action<string> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var session = ProjectState.Current.Get(role);
            SurfaceModel source = session.Source;
            if (source == null || source.Entities.Count == 0)
                throw new InvalidOperationException("Chưa nạp dữ liệu nguồn TIN thử nghiệm.");

            long revision = source.Revision;
            // Honour the same optional CAD boundary as the official surface
            // workflow. Clip on the core worker; do not touch AutoCAD objects.
            var boundary=ProjectState.Current.HasTinRegion
                ? ProjectState.Current.TinRegionPolygon.ToArray()
                : null;
            progress?.Invoke("Kiểm tra các nhóm trùng XY, yêu cầu sửa nếu ΔZ > 3,000 m...");
            var prepared = MeanZPreviewPreparer.Prepare(source, 1e-6, 1e-4,
                cancellationToken, boundary);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(source,session.Source) || source.Revision != revision)
                throw new InvalidOperationException("Dữ liệu nguồn thay đổi khi kiểm tra Z; hãy quét lại.");
            // Hard gate before ANY triangulation: do not silently average a 79m
            // conflict or create an apparently usable surface from it.
            if (prepared.HasBlockingConflicts)
                throw new MeanZReviewRequiredException(prepared);

            if(prepared.Prepared.Sites.Count<3)
                throw new InvalidOperationException("TIN thử nghiệm cần tối thiểu 3 tọa độ XY khác nhau.");
            progress?.Invoke("Tam giác hóa các đỉnh đã kiểm tra, không ràng buộc breakline...");
            var opts = new SurfaceBuildOptions
            {
                XyTolerance = 1e-6, ZConflictTolerance = 1e-4,
                MinimumTriangleArea = 1e-10,
                ClipBoundary = boundary
            };
            var mesh = new TiledConformingTinBuilder().BuildPrepared(
                session.Name + " - Z TRUNG BINH THU NGHIEM",
                prepared.Prepared, opts,
                prepared.OriginalVertexCount,
                0, 0, (a,b,message) => progress?.Invoke(message), cancellationToken);
            bool ok = false;
            try
            {
                if (mesh.Surface?.Triangles == null || mesh.Surface.Triangles.Count == 0)
                    throw new InvalidOperationException("Lưới thử nghiệm không có tam giác.");
                if (!ReferenceEquals(source,session.Source) || source.Revision != revision)
                    throw new InvalidOperationException(
                        "Nguồn đã thay đổi trong quá trình dựng TIN thử nghiệm; kết quả không còn hợp lệ.");

                // Full mesh remains in the file-backed source for audit. CAD displays
                // a capped, regularly sampled set so very large input does not cause
                // AutoCAD to freeze while writing millions of 3DFACE entities.
                int n = mesh.Surface.Triangles.Count;
                int step = Math.Max(1,(int)Math.Ceiling(n / (double)MaxDrawnPreviewTriangles));
                var drawn = new List<Triangle3>(Math.Min(n,MaxDrawnPreviewTriangles));
                for (int i=0;i<n;i+=step)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    drawn.Add(mesh.Surface.Triangles[i]);
                }
                ok = true;
                return new PreviewResult
                {
                    Role=role, Analysis=prepared, Surface=mesh.Surface,
                    SourceRevision=revision, Source=source, DrawnTriangles=drawn
                };
            }
            finally
            {
                if (!ok) try { (mesh.Surface?.Triangles as IDisposable)?.Dispose(); } catch { }
            }
        }

        internal static int Draw(PreviewResult result)
        {
            if (result == null || result.Analysis == null || result.DrawnTriangles == null)
                throw new ArgumentNullException(nameof(result));
            var session = ProjectState.Current.Get(result.Role);
            if (!ReferenceEquals(session.Source,result.Source) ||
                session.Source.Revision != result.SourceRevision)
                throw new InvalidOperationException(
                    "Nguồn đã thay đổi; cần dựng lại TIN thử nghiệm. Không chèn lưới cũ.");
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                ?? throw new InvalidOperationException("Không có bản vẽ AutoCAD đang mở.");
            short color = result.Role == ModelRole.Existing ? (short)6 : (short)4;
            var visible = new TinSurface("TIN Z TB - THU NGHIEM - KHONG TINH KL",
                result.DrawnTriangles, new []
                {
                    new ValidationIssue(ValidationSeverity.Warning,"ZMEAN_PREVIEW_ONLY",
                        "TIN tham khảo, không dùng để tính khối lượng.")
                });
            int count;
            using(doc.LockDocument())
                count = TinCadRenderer.Replace(doc.Database,result.Layer,visible,color);
            try {doc.Editor.Regen();} catch { }
            return count;
        }

        internal static void ExportCsv(PreviewResult preview, string filename)
        {
            if (preview == null || preview.Analysis == null)
                throw new ArgumentNullException(nameof(preview));
            if (string.IsNullOrWhiteSpace(filename))throw new ArgumentException("Tên file CSV không hợp lệ.");
            var data=preview.Analysis;
            using (var writer=new StreamWriter(filename,false,new UTF8Encoding(true)))
            {
                writer.WriteLine("IMSAT MININGVOLUME;TIN THU NGHIEM TRUNG BINH Z;KHONG DUNG TINH KHOI LUONG");
                writer.WriteLine("Mo hinh;"+(preview.Role==ModelRole.Existing?"Hien trang":"Thiet ke"));
                writer.WriteLine("So dinh goc;"+data.OriginalVertexCount.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("So dinh sau gop;"+data.Prepared.Sites.Count.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("So nhom XY trung;"+data.DuplicateGroupCount.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("So nhom XY khac Z;"+data.Conflicts.Count.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("So nhom can kiem tra > 3m;"+data.ReviewGroups.Count.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("So breakline KHONG rang buoc;"+data.IgnoredBreaklineSegments.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("So tam giac CDT;"+preview.TotalTriangles.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("So mat CAD minh hoa;"+preview.DrawnTriangles.Count.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine();
                writer.WriteLine("X;Y;So dinh gop;Z nho nhat;Z lon nhat;Chenh Z;Z trung binh;Handle dau;Handle cuoi");
                foreach(var c in data.Conflicts.OrderByDescending(c=>c.DeltaZ))
                {
                    string N(double v)=>v.ToString("0.######",CultureInfo.InvariantCulture);
                    writer.WriteLine(string.Join(";",new [] {
                        N(c.X),N(c.Y),c.VertexCount.ToString(CultureInfo.InvariantCulture),
                        N(c.MinZ),N(c.MaxZ),N(c.DeltaZ),N(c.MeanZ),
                        Clean(c.FirstHandle),Clean(c.LastHandle)
                    }));
                }
            }
        }
        private static string Clean(string value)
        {
            value=value??"";
            // CSV formula injection prevention: CAD handles should be hex only.
            return new string(value.Where(c=>c>='0'&&c<='9'||c>='A'&&c<='F'||c>='a'&&c<='f').ToArray());
        }
    }
}
