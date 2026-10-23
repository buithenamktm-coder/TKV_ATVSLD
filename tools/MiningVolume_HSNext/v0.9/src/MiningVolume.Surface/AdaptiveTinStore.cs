using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Surface;

namespace MiningVolume.Surface
{
    /// <summary>
    /// Spatially partitions ONE verified mesh for storage, not for triangulation.
    /// No new geometry is introduced at storage boundaries. Every accepted
    /// triangle is written once; metadata bounds cover the whole triangle.
    /// </summary>
    internal static class AdaptiveTinStore
    {
        private const int MaxTrianglesPerLeaf = 8192;
        private struct Record { public int Index; public double X, Y; }

        public static FileBackedTiledTriangleList Create(IReadOnlyList<Vec3> sites,
            int[] indices, SurfaceBuildOptions options, Action<string> progress,
            CancellationToken token, out int small, out int degenerate)
        {
            int triangles = indices.Length / 3, count = 0;
            var recordList = new List<Record>(triangles);
            var clipped = new List<Triangle3>();
            var clipper = options.ClipBoundary == null ? null : new TinPolygonClipper(options.ClipBoundary, token);
            small = 0;
            degenerate = 0;
            Triangle3 Triangle(int i) => i < 0 ? clipped[-i-1] : new Triangle3(sites[indices[3*i]],
                sites[indices[3*i+1]], sites[indices[3*i+2]]);
            for (int i = 0; i < triangles; i++)
            {
                token.ThrowIfCancellationRequested();
                var t = Triangle(i);
                if (TinTriangleQuality.CheckedArea(t) == 0)
                {
                    // Zero-area XY faces cannot represent a surface. Ignore only the face,
                    // never remove source vertices or constrained breakline segments.
                    // The caller reports the count; a fully invalid mesh still fails below.
                    degenerate++;
                    continue;
                }
                if (t.Area2D <= options.MinimumTriangleArea) small++;
                if (clipper == null) {
                    var c=t.Centroid2D; recordList.Add(new Record{Index=i,X=c.X,Y=c.Y});
                } else foreach(var part in clipper.Clip(t,token)) {
                    clipped.Add(part);var c=part.Centroid2D;
                    recordList.Add(new Record{Index=-clipped.Count,X=c.X,Y=c.Y});
                }
            }
            count=recordList.Count;
            var records=recordList.ToArray();
            if (count == 0) throw new InvalidOperationException("TIN không tạo được tam giác hợp lệ trong phạm vi tính.");

            string directory = Path.Combine(Path.GetTempPath(), "IMSAT_MiningVolume", "TinStore");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "tin_" + Guid.NewGuid().ToString("N") + ".bin");
            var tiles = new List<TinTileInfo>();
            int written = 0, comparisons = 0;
            IComparer<Record> Comparer(bool x) => System.Collections.Generic.Comparer<Record>.Create((a,b) =>
            {
                if ((++comparisons & 4095) == 0) token.ThrowIfCancellationRequested();
                int order = x ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y);
                return order != 0 ? order : a.Index.CompareTo(b.Index);
            });
            try
            {
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                    FileShare.Read, 1 << 20, FileOptions.SequentialScan))
                using (var writer = new BinaryWriter(stream))
                {
                    void Partition(int start, int length)
                    {
                        token.ThrowIfCancellationRequested();
                        double minX=double.PositiveInfinity,minY=double.PositiveInfinity;
                        double maxX=double.NegativeInfinity,maxY=double.NegativeInfinity;
                        if (length > MaxTrianglesPerLeaf)
                        {
                            for(int i=start;i<start+length;i++)
                            {
                                minX=Math.Min(minX,records[i].X); maxX=Math.Max(maxX,records[i].X);
                                minY=Math.Min(minY,records[i].Y); maxY=Math.Max(maxY,records[i].Y);
                            }
                            progress?.Invoke($"Phân vùng lưu TIN: {written:n0}/{count:n0} tam giác...");
                            try { Array.Sort(records,start,length,Comparer(maxX-minX >= maxY-minY)); }
                            catch (InvalidOperationException ex) when (ex.InnerException is OperationCanceledException)
                            { token.ThrowIfCancellationRequested(); throw; }
                            int half=length/2;
                            Partition(start,half); Partition(start+half,length-half);
                            return;
                        }
                        long offset=stream.Position;
                        for(int i=start;i<start+length;i++)
                        {
                            token.ThrowIfCancellationRequested();
                            var t=Triangle(records[i].Index);
                            FileBackedTiledTriangleList.WriteTriangle(writer,t);
                            minX=Math.Min(minX,Math.Min(t.A.X,Math.Min(t.B.X,t.C.X)));
                            minY=Math.Min(minY,Math.Min(t.A.Y,Math.Min(t.B.Y,t.C.Y)));
                            maxX=Math.Max(maxX,Math.Max(t.A.X,Math.Max(t.B.X,t.C.X)));
                            maxY=Math.Max(maxY,Math.Max(t.A.Y,Math.Max(t.B.Y,t.C.Y)));
                        }
                        tiles.Add(new TinTileInfo(tiles.Count,offset,length,minX,minY,maxX,maxY));
                        written+=length;
                    }
                    Partition(0,count);
                    writer.Flush(); stream.Flush(true);
                }
                var result=new FileBackedTiledTriangleList(path,tiles,cacheTiles:6);
                path=null;
                return result;
            }
            finally { if(path!=null) { try { File.Delete(path); } catch { } } }
        }
    }
}
