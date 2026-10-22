using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MiningVolume.Core.Geometry;

namespace MiningVolume.Core.Surface
{
    /// <summary>Clips surface triangles to one simple polygon. Z is interpolated on the source face.
    /// Holes/multiple rings are not supported by this contract and must not be flattened into it.</summary>
    public sealed class TinPolygonClipper
    {
        private readonly List<Vec2[]> _ears = new List<Vec2[]>();
        private readonly double _minX, _minY, _maxX, _maxY;
        public TinPolygonClipper(IReadOnlyList<Vec2> boundary, CancellationToken token = default(CancellationToken))
        {
            if(boundary==null||boundary.Count<3)throw new ArgumentException("Đường bao cần ít nhất ba đỉnh.");
            var p=boundary.ToList();
            if(p.Count>3&&p[0].DistanceTo(p[p.Count-1])==0)p.RemoveAt(p.Count-1);
            foreach(var v in p)if(!Finite(v.X)||!Finite(v.Y))throw new ArgumentException("Đường bao có tọa độ không hữu hạn.");
            _minX=p.Min(v=>v.X);_maxX=p.Max(v=>v.X);_minY=p.Min(v=>v.Y);_maxY=p.Max(v=>v.Y);
            for(int i=0;i<p.Count;i++)for(int j=i+1;j<p.Count;j++){
                token.ThrowIfCancellationRequested();
                if(p[i].DistanceTo(p[j])==0)throw new ArgumentException("Đường bao có đỉnh lặp hoặc nhiều vòng; chưa hỗ trợ lỗ.");
                if(j==i+1||(i==0&&j==p.Count-1))continue;
                var a=p[i];var b=p[(i+1)%p.Count];var c=p[j];var d=p[(j+1)%p.Count];
                if(Intersects(a,b,c,d))throw new ArgumentException("Đường bao tự giao hoặc tự tiếp xúc.");
            }
            double signed=0;for(int i=1;i+1<p.Count;i++)signed+=Vec2.Cross(p[0],p[i],p[i+1]);
            if(signed==0||!Finite(signed))throw new ArgumentException("Đường bao suy biến.");
            if(signed<0)p.Reverse();
            while(p.Count>3){
                token.ThrowIfCancellationRequested();bool found=false;
                for(int i=0;i<p.Count;i++){
                    var a=p[(i+p.Count-1)%p.Count];var b=p[i];var c=p[(i+1)%p.Count];
                    double cross=Vec2.Cross(a,b,c);
                    if(cross==0){p.RemoveAt(i);found=true;break;}
                    if(cross<0)continue;
                    bool occupied=false;
                    for(int j=0;j<p.Count;j++)if(j!=i&&j!=(i+p.Count-1)%p.Count&&j!=(i+1)%p.Count&&Inside(p[j],a,b,c)){occupied=true;break;}
                    if(occupied)continue;
                    _ears.Add(new[]{a,b,c});p.RemoveAt(i);found=true;break;
                }
                if(!found)throw new ArgumentException("Không phân tích được đường bao hợp lệ; chưa dựng TIN.");
            }
            if(p.Count==3&&Vec2.Cross(p[0],p[1],p[2])>0)_ears.Add(p.ToArray());
            if(_ears.Count==0)throw new ArgumentException("Đường bao không có diện tích.");
        }
        public IEnumerable<Triangle3> Clip(Triangle3 triangle, CancellationToken token=default(CancellationToken))
        {
            if(Math.Max(triangle.A.X,Math.Max(triangle.B.X,triangle.C.X))<_minX||
                Math.Min(triangle.A.X,Math.Min(triangle.B.X,triangle.C.X))>_maxX||
                Math.Max(triangle.A.Y,Math.Max(triangle.B.Y,triangle.C.Y))<_minY||
                Math.Min(triangle.A.Y,Math.Min(triangle.B.Y,triangle.C.Y))>_maxY)yield break;
            // Every face is clipped against the same polygon decomposition, including interior
            // faces: skipping a split on one side would create T-junctions along shared edges.
            foreach(var ear in _ears){
                token.ThrowIfCancellationRequested();
                var polygon=new List<Vec3>{triangle.A,triangle.B,triangle.C};
                for(int k=0;k<3&&polygon.Count>0;k++){
                    var output=new List<Vec3>();var a=ear[k];var b=ear[(k+1)%3];
                    var prev=polygon[polygon.Count-1];double dp=Vec2.Cross(a,b,prev.XY);
                    foreach(var current in polygon){
                        double dc=Vec2.Cross(a,b,current.XY);
                        if((dp>=0)!=(dc>=0)){
                            // Canonical endpoint ordering makes intersections independent of
                            // traversal orientation on adjacent triangles.
                            var lo=prev;var hi=current;
                            if(lo.X>hi.X||(lo.X==hi.X&&lo.Y>hi.Y)){lo=current;hi=prev;}
                            double dl=Vec2.Cross(a,b,lo.XY),dh=Vec2.Cross(a,b,hi.XY);
                            double t=dl/(dl-dh);
                            Append(output,new Vec3(lo.X+(hi.X-lo.X)*t,lo.Y+(hi.Y-lo.Y)*t,lo.Z+(hi.Z-lo.Z)*t));
                        }
                        if(dc>=0)Append(output,current);
                        prev=current;dp=dc;
                    }
                    if(output.Count>1&&Equal(output[0],output[output.Count-1]))output.RemoveAt(output.Count-1);
                    polygon=output;
                }
                for(int i=1;i+1<polygon.Count;i++){
                    var t=new Triangle3(polygon[0],polygon[i],polygon[i+1]);
                    if(t.Area2D>0)yield return t;
                }
            }
        }
        private static bool Equal(Vec3 a,Vec3 b)=>a.X==b.X&&a.Y==b.Y;
        private static void Append(List<Vec3> p,Vec3 v){if(p.Count==0||!Equal(p[p.Count-1],v))p.Add(v);}
        private static bool Inside(Vec2 p,Vec2 a,Vec2 b,Vec2 c)=>Vec2.Cross(a,b,p)>=0&&Vec2.Cross(b,c,p)>=0&&Vec2.Cross(c,a,p)>=0;
        private static bool Finite(double d)=>!double.IsNaN(d)&&!double.IsInfinity(d);
        private static bool On(Vec2 p,Vec2 a,Vec2 b)=>Vec2.Cross(a,b,p)==0&&p.X>=Math.Min(a.X,b.X)&&p.X<=Math.Max(a.X,b.X)&&p.Y>=Math.Min(a.Y,b.Y)&&p.Y<=Math.Max(a.Y,b.Y);
        private static bool Intersects(Vec2 a,Vec2 b,Vec2 c,Vec2 d)=>
            (Math.Sign(Vec2.Cross(a,b,c))*Math.Sign(Vec2.Cross(a,b,d))<0&&Math.Sign(Vec2.Cross(c,d,a))*Math.Sign(Vec2.Cross(c,d,b))<0)||
            On(a,c,d)||On(b,c,d)||On(c,a,b)||On(d,a,b);
    }
}
