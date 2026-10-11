using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;
using MiningVolume.Core.Surface;

namespace MiningVolume.Surface
{
    /// <summary>
    /// One constrained mesh, adaptively partitioned only for file-backed storage.
    /// Independent halo triangulations cannot certify shared edges. We therefore
    /// normalize once and triangulate once; storage leaves never alter geometry.
    /// The public contract remains unchanged for the CAD/UI consumers.
    /// </summary>
    public sealed class TiledConformingTinBuilder
    {
        public sealed class Result
        {
            public TinSurface Surface { get; set; }
            public int InputVertexCount { get; set; }
            public int InputBreaklineCount { get; set; }
            public int TileCount { get; set; }
            public int WarningCount { get; set; }
            public double MinZ { get; set; }
            public double MaxZ { get; set; }
            public long PrepareMilliseconds { get; set; }
            public long TriangulationMilliseconds { get; set; }
            public IReadOnlyList<Triangle3> PreviewTriangles { get; set; }
        }

        public Result Build(string name, SurfaceModel model, SurfaceBuildOptions options,
            Action<int,int,string> progress=null, CancellationToken cancellationToken=default(CancellationToken))
        {
            if(model==null) throw new ArgumentNullException(nameof(model));
            options=options??new SurfaceBuildOptions();
            cancellationToken.ThrowIfCancellationRequested();
            int inputVertices=0,inputBreaklines=0;
            foreach(var entity in model.Entities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(!entity.IsEnabled)continue;
                foreach(var vertex in entity.ActiveVertices()) { cancellationToken.ThrowIfCancellationRequested(); inputVertices++; }
                foreach(var edge in entity.ActiveBreaklineSegments()) { cancellationToken.ThrowIfCancellationRequested(); if(edge.Length2D>options.XyTolerance)inputBreaklines++; }
            }
            var watch=Stopwatch.StartNew();
            var prepared=new SurfaceInputPreparer().Prepare(model,options,
                message=>progress?.Invoke(0,3,message),cancellationToken);
            long prepareMs=watch.ElapsedMilliseconds;
            if(prepared.HasErrors)
            {
                if(prepared.ConflictReport?.HasBlockingConflicts==true)throw prepared.ConflictReport.ToException();
                var errors=prepared.Issues.Where(i=>i.Severity==ValidationSeverity.Error).ToList();
                var zCodes=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                    "DUPLICATE_XY_CONFLICT_Z","POINT_ON_BREAKLINE_Z_CONFLICT",
                    "BREAKLINE_CROSSING_Z_CONFLICT","BREAKLINE_OVERLAP_Z_CONFLICT",
                    "BREAKLINE_DUPLICATE_Z_CONFLICT","BREAKLINE_NODE_Z_CONFLICT"
                };
                if(errors.Any(i=>zCodes.Contains(i.Code)))
                    throw new DuplicateXYConflictException(name,errors);
                throw new InvalidOperationException("Dữ liệu TIN chưa hợp lệ:\r\n"+
                    string.Join("\r\n",errors.Take(10).Select(i=>i.Code+": "+i.Message)));
            }
            progress?.Invoke(1,3,$"Dựng lưới thống nhất: {prepared.Sites.Count:n0} đỉnh • {prepared.Breaklines.Count:n0} breakline...");
            watch.Restart();
            var surface=new ConformingTinBuilder().BuildFileBacked(name,prepared,options,
                message=>progress?.Invoke(1,3,message),cancellationToken);
            bool transferred=false;
            try
            {
                long triangulationMs=watch.ElapsedMilliseconds;
                var tiles=(ITiledTriangleSource)surface.Triangles;
                var preview=new List<Triangle3>(Math.Min(200000,tiles.Count));
                int stride=Math.Max(1,(int)Math.Ceiling(tiles.Count/200000.0)),index=0;
                progress?.Invoke(2,3,"Chuẩn bị bản xem trước; tính toán vẫn dùng toàn bộ TIN...");
                foreach(var triangle in tiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if(index++%stride==0)preview.Add(triangle);
                }
                var result=new Result {
                    Surface=surface,InputVertexCount=inputVertices,InputBreaklineCount=inputBreaklines,
                    TileCount=tiles.Tiles.Count,WarningCount=surface.Issues.Count(i=>i.Severity==ValidationSeverity.Warning),
                    MinZ=prepared.Sites.Min(p=>p.Z),MaxZ=prepared.Sites.Max(p=>p.Z),
                    PrepareMilliseconds=prepareMs,TriangulationMilliseconds=triangulationMs,
                    PreviewTriangles=preview
                };
                progress?.Invoke(3,3,$"TIN hoàn thành: {tiles.Count:n0} tam giác • {tiles.Tiles.Count:n0} vùng lưu.");
                transferred=true;
                return result;
            }
            finally { if(!transferred)(surface.Triangles as IDisposable)?.Dispose(); }
        }
    }
}
