using System;
using System.Collections.Generic;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Sections;

namespace MiningVolume.Core.Surface
{
    /// <summary>
    /// Project the preserved 3D vertical curtains onto the map. They have zero
    /// PLAN AREA, but their adjacent 2.5D support triangles are not necessarily
    /// an authoritative choice of the correct Z branch. Do not stop a WHOLE mine
    /// because of one double-Z survey point: quarantine only intersected cells
    /// and section routes. Never silently assign an average/upper/lower Z.
    /// </summary>
    public static class VerticalConflictFootprint
    {
        private static bool Finite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private static double Dot(Vec2 a, Vec2 b) => a.X*b.X+a.Y*b.Y;

        private static IEnumerable<Triangle3> Faces(TinSurface existing, TinSurface design)
        {
            if (existing?.VerticalFaces != null)
                foreach (var f in existing.VerticalFaces) yield return f;
            if (design?.VerticalFaces != null)
                foreach (var f in design.VerticalFaces) yield return f;
        }

        private static IEnumerable<Vec2> Roots(TinSurface existing, TinSurface design)
        {
            if (existing?.VerticalConflicts != null)
                foreach (var g in existing.VerticalConflicts) yield return g.Position;
            if (design?.VerticalConflicts != null)
                foreach (var g in design.VerticalConflicts) yield return g.Position;
        }

        // Every preserved face projects to a segment (possibly repeated in a
        // two-triangle curtain). Choose its most separated XY vertices.
        private static bool Projection(Triangle3 f, out Vec2 a, out Vec2 b)
        {
            Vec2[] p={f.A.XY,f.B.XY,f.C.XY};
            double best=0;int x=0,y=1;
            for(int i=0;i<3;i++)for(int j=i+1;j<3;j++)
            {
                double dx=p[i].X-p[j].X,dy=p[i].Y-p[j].Y;
                double d=dx*dx+dy*dy;
                if(d>best){best=d;x=i;y=j;}
            }
            a=p[x];b=p[y];
            return best>1e-20;
        }

        public static bool TouchesSection(SectionLine line,
            TinSurface existing, TinSurface design, double tolerance=1e-7)
        {
            if(line==null)return false;
            double tol=Math.Max(1e-7,tolerance*20);
            bool At(Vec2 p)
            {
                double side=Dot(p,line.Normal)-line.Offset;
                if(Math.Abs(side)>tol)return false;
                double along=Dot(p,line.Direction);
                foreach(var span in line.Spans)
                    if(along>=span.StartT-tol && along<=span.EndT+tol)return true;
                return false;
            }
            foreach(var p in Roots(existing,design))if(At(p))return true;
            foreach(var face in Faces(existing,design))
            {
                if(!Projection(face,out Vec2 a,out Vec2 b))continue;
                double da=Dot(a,line.Normal)-line.Offset;
                double db=Dot(b,line.Normal)-line.Offset;
                if(da>tol && db>tol || da< -tol && db< -tol)continue;
                double den=da-db;
                if(Math.Abs(den)<=tol)
                {
                    if(At(a)||At(b))return true;
                    if(Math.Abs(da)>tol)continue;
                    double ta=Dot(a,line.Direction),tb=Dot(b,line.Direction);
                    foreach(var span in line.Spans)
                        if(Math.Max(ta,tb)>=span.StartT-tol &&
                           Math.Min(ta,tb)<=span.EndT+tol)return true;
                }
                else
                {
                    double t=da/den;
                    if(t< -1e-8||t>1+1e-8)continue;
                    var p=new Vec2(a.X+t*(b.X-a.X),a.Y+t*(b.Y-a.Y));
                    if(At(p))return true;
                }
            }
            return false;
        }

        /// <summary>
        /// In a rotated grid, rasterize true projected curtain segments into
        /// affected cells. This is bounded by the requested grid dimensions.
        /// Mark a whole cell as needing review, rather than fabricate a number.
        /// </summary>
        public static HashSet<(int,int)> AffectedGridCells(
            TinSurface existing,TinSurface design,Vec2 origin,
            double sizeX,double sizeY,double cosine,double sine,
            long startI,long endI,long startJ,long endJ)
        {
            var affected=new HashSet<(int,int)>();
            if(sizeX<=0||sizeY<=0||!Finite(sizeX)||!Finite(sizeY))
                throw new ArgumentOutOfRangeException(nameof(sizeX));
            Vec2 Uv(Vec2 p)
            {
                double dx=p.X-origin.X,dy=p.Y-origin.Y;
                return new Vec2((cosine*dx+sine*dy)/sizeX,
                                (-sine*dx+cosine*dy)/sizeY);
            }
            void Mark(double u,double v)
            {
                long i=(long)Math.Floor(u),j=(long)Math.Floor(v);
                // Adjacent cells also own a root exactly on their edge.
                for(long di=-1;di<=1;di++)for(long dj=-1;dj<=1;dj++)
                {
                    long x=i+di,y=j+dj;
                    if(x<startI||x>=endI||y<startJ||y>=endJ)continue;
                    if(u<x-1e-8||u>x+1+1e-8||
                       v<y-1e-8||v>y+1+1e-8)continue;
                    affected.Add((checked((int)x),checked((int)y)));
                }
            }
            foreach(var p in Roots(existing,design)) Mark(Uv(p).X,Uv(p).Y);
            foreach(var face in Faces(existing,design))
            {
                if(!Projection(face,out Vec2 a,out Vec2 b))continue;
                var p=Uv(a);var q=Uv(b);
                if(!Finite(p.X)||!Finite(p.Y)||!Finite(q.X)||!Finite(q.Y))
                    throw new InvalidOperationException("Mặt đứng có tọa độ chiếu không hợp lệ.");
                // Include adjacent grid lines without marking the full 2D
                // bounding rectangle of an oblique face unnecessarily.
                long loI=Math.Max(startI,(long)Math.Floor(Math.Min(p.X,q.X)-1e-8));
                long hiI=Math.Min(endI-1,(long)Math.Floor(Math.Max(p.X,q.X)+1e-8));
                long loJ=Math.Max(startJ,(long)Math.Floor(Math.Min(p.Y,q.Y)-1e-8));
                long hiJ=Math.Min(endJ-1,(long)Math.Floor(Math.Max(p.Y,q.Y)+1e-8));
                for(long i=loI;i<=hiI;i++)for(long j=loJ;j<=hiJ;j++)
                    if(SegmentHitsRect(p,q,i-1e-8,j-1e-8,i+1+1e-8,j+1+1e-8))
                        affected.Add((checked((int)i),checked((int)j)));
            }
            return affected;
        }

        private static bool SegmentHitsRect(Vec2 a,Vec2 b,
            double left,double bottom,double right,double top)
        {
            double dx=b.X-a.X,dy=b.Y-a.Y,lo=0,hi=1;
            bool Cut(double p,double q)
            {
                if(Math.Abs(p)<1e-16)return q>=0;
                double r=q/p;
                if(p<0){if(r>hi)return false;if(r>lo)lo=r;}
                else {if(r<lo)return false;if(r<hi)hi=r;}
                return true;
            }
            return Cut(-dx,a.X-left)&&Cut(dx,right-a.X)&&
                   Cut(-dy,a.Y-bottom)&&Cut(dy,top-a.Y)&&lo<=hi;
        }
    }
}
