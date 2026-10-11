using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Geometry;

namespace MiningVolume.Core.Sections
{
    /// <summary>
    /// Kiểm tra vùng than/quặng do người dùng hiệu chỉnh trên mặt cắt.
    /// Đầu vào (S,Z) là kích thước THỰC, không phải tọa độ CAD đã phóng đứng.
    /// Vùng chỉ hợp lệ nếu nằm TRỌN trong tập hợp các miền đào (Ztk..Ztn)
    /// của hai mặt địa hình; tích phân diện tích bằng giao đa giác chính xác.
    /// </summary>
    public static class MineralSectionPolygonCalculator
    {
        private const double Eps = 1e-9;

        public static double ValidateAndMeasure(SectionProfile profile, IReadOnlyList<Vec2> vertices)
        {
            if (profile == null || !profile.HasData)
                throw new ArgumentException("Mặt cắt chưa có đủ hai TIN.", nameof(profile));
            var ring = Normalize(vertices);
            if (ring.Count < 3 || !IsSimple(ring))
                throw new ArgumentException("Vùng than/quặng phải là đa giác kín đơn, không tự cắt.");
            double requested = Area(ring);
            if (requested <= Eps)
                throw new ArgumentException("Diện tích vùng khoáng sản bằng 0.");
            var pieces = Triangulate(ring);
            double inside = 0;
            foreach (var segment in profile.Segments)
            {
                foreach (var cut in MakeCutPolygons(segment))
                {
                    if (Area(cut) <= Eps) continue;
                    foreach (var polygon in pieces)
                        inside += Area(ClipConvex(polygon, cut));
                }
            }

            double tolerance = Math.Max(1e-7, requested * 1e-6);
            if (inside < requested - tolerance)
                throw new InvalidOperationException(
                    "Vùng khoáng sản vượt ngoài phần đào hiện trạng–thiết kế hoặc đi qua vị trí không có dữ liệu. " +
                    "Hãy chỉnh Polyline và bấm CẬP NHẬT MẶT CẮT.");
            if (inside > requested + tolerance)
                throw new InvalidOperationException(
                    "Các đoạn dữ liệu mặt cắt bị chồng lấn, diện tích khoáng sản bị đếm trùng.");
            return requested;
        }

        public static double OverlapArea(IReadOnlyList<Vec2> first, IReadOnlyList<Vec2> second)
        {
            var a = Normalize(first);
            var b = Normalize(second);
            if (!IsSimple(a) || !IsSimple(b))
                throw new ArgumentException("Đường bao khoáng sản không hợp lệ.");
            var trianglesA = Triangulate(a);
            var trianglesB = Triangulate(b);
            double sum = 0;
            foreach (var x in trianglesA)
                foreach (var y in trianglesB)
                    sum += Area(ClipConvex(x, y));
            return sum;
        }

        public static double Area(IReadOnlyList<Vec2> p)
        {
            if (p == null || p.Count < 3) return 0;
            double sum = 0;
            for (int i = 1; i + 1 < p.Count; i++)
                sum += Vec2.Cross(p[0], p[i], p[i+1]);
            return Math.Abs(sum) * .5;
        }

        private static List<List<Vec2>> MakeCutPolygons(SectionProfileSegment s)
        {
            var result = new List<List<Vec2>>();
            if (s.S1 <= s.S0 + Eps) return result;
            double d0 = s.ExistingZ0 - s.DesignZ0;
            double d1 = s.ExistingZ1 - s.DesignZ1;
            if (d0 <= Eps && d1 <= Eps) return result;
            if (d0 > Eps && d1 > Eps)
            {
                result.Add(new List<Vec2>
                {
                    new Vec2(s.S0, s.DesignZ0), new Vec2(s.S1, s.DesignZ1),
                    new Vec2(s.S1, s.ExistingZ1), new Vec2(s.S0, s.ExistingZ0)
                });
                return result;
            }
            if (Math.Abs(d0-d1) <= Eps) return result;
            double alpha = d0 / (d0-d1);
            alpha = Math.Max(0,Math.Min(1,alpha));
            double crossS=s.S0+alpha*(s.S1-s.S0);
            double crossZ=s.DesignZ0+alpha*(s.DesignZ1-s.DesignZ0);
            var zero=new Vec2(crossS,crossZ);
            if(d0>Eps)
                result.Add(new List<Vec2> {new Vec2(s.S0,s.DesignZ0),zero,
                    new Vec2(s.S0,s.ExistingZ0)});
            else
                result.Add(new List<Vec2> {zero,new Vec2(s.S1,s.DesignZ1),
                    new Vec2(s.S1,s.ExistingZ1)});
            return result;
        }

        private static List<Vec2> Normalize(IReadOnlyList<Vec2> raw)
        {
            if(raw==null)throw new ArgumentNullException(nameof(raw));
            var p=new List<Vec2>();
            foreach(var v in raw)
            {
                if(double.IsNaN(v.X)||double.IsNaN(v.Y)||
                   double.IsInfinity(v.X)||double.IsInfinity(v.Y))
                    throw new ArgumentException("Đỉnh khoáng sản chứa tọa độ không hữu hạn.");
                if(p.Count==0 || p[p.Count-1].DistanceTo(v)>Eps)p.Add(v);
            }
            if(p.Count>1&&p[0].DistanceTo(p[p.Count-1])<=Eps)
                p.RemoveAt(p.Count-1);
            bool removed=true;
            while(removed&&p.Count>3)
            {
                removed=false;
                for(int i=0;i<p.Count;i++)
                {
                    var a=p[(i+p.Count-1)%p.Count],b=p[i],c=p[(i+1)%p.Count];
                    if(b.DistanceTo(a)<=Eps || Math.Abs(Vec2.Cross(a,b,c)) <=
                       Eps*Math.Max(1.0,a.DistanceTo(c)))
                    {
                        p.RemoveAt(i);removed=true;break;
                    }
                }
            }
            return p;
        }

        private static bool IsSimple(IReadOnlyList<Vec2> p)
        {
            if(p.Count<3||Area(p)<=Eps)return false;
            for(int i=0;i<p.Count;i++)
                for(int j=i+1;j<p.Count;j++)
                {
                    if(j==i+1 || (i==0&&j==p.Count-1))continue;
                    var a=p[i],b=p[(i+1)%p.Count];
                    var c=p[j],d=p[(j+1)%p.Count];
                    if(Geometry2D.ProperIntersection(a,b,c,d,Eps) ||
                       Geometry2D.PointOnSegment(a,c,d,Eps) ||
                       Geometry2D.PointOnSegment(b,c,d,Eps) ||
                       Geometry2D.PointOnSegment(c,a,b,Eps) ||
                       Geometry2D.PointOnSegment(d,a,b,Eps))
                       return false;
                }
            return true;
        }

        private static List<List<Vec2>> Triangulate(List<Vec2> input)
        {
            var p=new List<Vec2>(input);
            if(SignedArea(p)<0)p.Reverse();
            var outTris=new List<List<Vec2>>();
            int iterations=0;
            while(p.Count>3)
            {
                if(++iterations>10000)throw new ArgumentException("Vùng khoáng sản quá phức tạp.");
                bool found=false;
                for(int i=0;i<p.Count;i++)
                {
                    int prev=(i+p.Count-1)%p.Count,next=(i+1)%p.Count;
                    var a=p[prev];var b=p[i];var c=p[next];
                    if(Vec2.Cross(a,b,c)<=Eps)continue;
                    bool occupied=false;
                    for(int j=0;j<p.Count;j++)
                    {
                        if(j==prev||j==i||j==next)continue;
                        if(Geometry2D.PointInTriangle(p[j],a,b,c,Eps))
                        {occupied=true;break;}
                    }
                    if(occupied)continue;
                    outTris.Add(new List<Vec2>{a,b,c});
                    p.RemoveAt(i);found=true;break;
                }
                if(!found)throw new ArgumentException(
                    "Không phân giải được hình học Polyline; kiểm tra các đỉnh trùng, tự cắt hoặc cạnh suy biến.");
            }
            outTris.Add(p);
            return outTris;
        }

        private static double SignedArea(IReadOnlyList<Vec2> p)
        {
            if(p.Count<3)return 0;
            double twice=0;
            for(int i=1;i+1<p.Count;i++)
                twice+=Vec2.Cross(p[0],p[i],p[i+1]);
            return .5*twice;
        }

        private static List<Vec2> ClipConvex(IReadOnlyList<Vec2> subject,IReadOnlyList<Vec2> clip)
        {
            var poly=new List<Vec2>(subject);
            if(poly.Count<3||clip.Count<3)return new List<Vec2>();
            double sign=SignedArea(clip)>=0 ? 1:-1;
            for(int i=0;i<clip.Count;i++)
            {
                var a=clip[i];var b=clip[(i+1)%clip.Count];
                if(poly.Count==0)break;
                var next=new List<Vec2>();
                var prev=poly[poly.Count-1];
                double prevSide=sign*Vec2.Cross(a,b,prev);
                foreach(var p in poly)
                {
                    double side=sign*Vec2.Cross(a,b,p);
                    bool inside=side>=-Eps,wasInside=prevSide>=-Eps;
                    if(inside!=wasInside)
                    {
                        double den=prevSide-side;
                        if(Math.Abs(den)>1e-15)
                        {
                            double t=Math.Max(0,Math.Min(1,prevSide/den));
                            next.Add(new Vec2(prev.X+t*(p.X-prev.X),prev.Y+t*(p.Y-prev.Y)));
                        }
                    }
                    if(inside)next.Add(p);
                    prev=p;prevSide=side;
                }
                poly=next;
            }
            return poly;
        }
    }
}
