using System;
using System.Collections.Generic;
using System.Threading;
using MiningVolume.Core.Geometry;

namespace MiningVolume.Core.Surface
{
    public static class TinTriangleQuality
    {
        public static double CheckedArea(Triangle3 t) {
            if(!Finite(t.A.X)||!Finite(t.A.Y)||!Finite(t.A.Z)||
                !Finite(t.B.X)||!Finite(t.B.Y)||!Finite(t.B.Z)||
                !Finite(t.C.X)||!Finite(t.C.Y)||!Finite(t.C.Z))throw new InvalidOperationException("TIN có tam giác chứa tọa độ NaN/Infinity; không được bỏ để tiếp tục.");
            double area=t.Area2D;
            if(!Finite(area))throw new InvalidOperationException("Diện tích TIN không hữu hạn.");
            return area;
        }
        // Called by the real CAD renderer as well as Core/native regression tests.
        public static void ValidateForCad(Triangle3 t) {
            if(!(CheckedArea(t)>0))throw new InvalidOperationException("TIN có tam giác XY thẳng hàng chính xác; chưa được chứng minh an toàn để ghi CAD.");
        }
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        static long Edge(int a,int b)=>((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);

        // Before any face is discarded, prove exact zero XY area AND retention
        // of its vertices and constrained edges in the positive-area mesh.
        // No positive face is removed, so removed projected area is exactly 0 m².
        public static int VerifyZeroAreaRemoval(IReadOnlyList<Vec3> sites,int[] indices,IReadOnlyList<int> edges,CancellationToken token)
        {
            var removed=new HashSet<int>();var candidateEdges=new HashSet<long>();int count=0;
            for(int i=0;i<indices.Length;i+=3) {
                if((i&4095)==0)token.ThrowIfCancellationRequested();
                int a=indices[i],b=indices[i+1],c=indices[i+2];
                double area=CheckedArea(new Triangle3(sites[a],sites[b],sites[c]));
                if(area>0)continue;
                count++;removed.Add(a);removed.Add(b);removed.Add(c);
                candidateEdges.Add(Edge(a,b));candidateEdges.Add(Edge(b,c));candidateEdges.Add(Edge(c,a));
            }
            if(count==0)return 0;
            var required=new HashSet<long>();
            for(int i=0;i<edges.Count;i+=2)if(candidateEdges.Contains(Edge(edges[i],edges[i+1])))required.Add(Edge(edges[i],edges[i+1]));
            for(int i=0;i<indices.Length;i+=3) {
                if((i&4095)==0)token.ThrowIfCancellationRequested();
                int a=indices[i],b=indices[i+1],c=indices[i+2];
                if(CheckedArea(new Triangle3(sites[a],sites[b],sites[c]))==0)continue;
                removed.Remove(a);removed.Remove(b);removed.Remove(c);
                required.Remove(Edge(a,b));required.Remove(Edge(b,c));required.Remove(Edge(c,a));
            }
            if(removed.Count>0||required.Count>0)throw new InvalidOperationException(
                "Tam giác thẳng hàng còn giữ đỉnh/breakline độc lập; chưa thể bỏ an toàn. Không xuất mô hình thiếu dữ liệu.");
            return count;
        }
    }
}
