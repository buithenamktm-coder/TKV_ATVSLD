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

        /// <summary>
        /// The 2.5D CDT triangulates a single representative Z at conflicting
        /// XY. All support triangles INCIDENT to that XY can inherit this
        /// arbitrary branch, even cells not directly crossed by the upright
        /// curtain. This spatial hash lets callers quarantine those triangles
        /// without an O(number of conflicts) scan per grid cell.
        /// </summary>
        public sealed class ConflictRootIndex
        {
            private readonly Dictionary<(long,long), List<Vec2>> _sites =
                new Dictionary<(long,long), List<Vec2>>();
            private readonly double _epsilon;
            private readonly HashSet<(long,long)> _neighborCells =
                new HashSet<(long,long)>();
            public bool IsEmpty => _sites.Count == 0;

            public ConflictRootIndex(TinSurface existing, TinSurface design,
                double epsilon = 1e-6)
            {
                if (!(epsilon > 0) || !Finite(epsilon))
                    throw new ArgumentOutOfRangeException(nameof(epsilon));
                _epsilon = epsilon;
                foreach (var root in Roots(existing, design))
                {
                    var key = Key(root);
                    if (!_sites.TryGetValue(key, out var list))
                        _sites.Add(key, list = new List<Vec2>());
                    list.Add(root);
                    // Usually less than 0.1% of terrain vertices are near a
                    // multi-Z root; one lookup eliminates 9 unnecessary probes.
                    for(long dx=-1;dx<=1;dx++)
                        for(long dy=-1;dy<=1;dy++)
                            _neighborCells.Add((
                                checked(key.Item1+dx),checked(key.Item2+dy)));
                }
            }

            private (long,long) Key(Vec2 p)
            {
                if (!Finite(p.X) || !Finite(p.Y))
                    throw new InvalidOperationException("XY mặt đứng không hữu hạn.");
                return (checked((long)Math.Floor(p.X / _epsilon)),
                    checked((long)Math.Floor(p.Y / _epsilon)));
            }

            public bool Contains(Vec2 p)
            {
                if (IsEmpty) return false;
                var cell = Key(p);
                if (!_neighborCells.Contains(cell)) return false;
                for (long dx=-1;dx<=1;dx++)
                    for (long dy=-1;dy<=1;dy++)
                    {
                        var key=(checked(cell.Item1+dx),checked(cell.Item2+dy));
                        if (!_sites.TryGetValue(key,out var values)) continue;
                        foreach (var v in values)
                        {
                            double ax=v.X-p.X, ay=v.Y-p.Y;
                            if (ax*ax+ay*ay<=_epsilon*_epsilon) return true;
                        }
                    }
                return false;
            }

            public bool Incident(Triangle3 t) =>
                Contains(t.A.XY) || Contains(t.B.XY) || Contains(t.C.XY);

            public bool AnyIncident(IReadOnlyList<Triangle3> triangles)
            {
                if (IsEmpty || triangles == null) return false;
                foreach (var t in triangles)
                    if (Incident(t)) return true;
                return false;
            }
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
        /// A section interval spans a two-dimensional strip. If a vertical
        /// curtain touches that strip, its interpolated volume is not yet
        /// certified even if neither end section passes through the root.
        /// A reviewed/edited system can still explicitly accept the profile.
        /// </summary>
        public static bool TouchesStrip(SectionLine first, SectionLine second,
            TinSurface existing, TinSurface design, double tolerance=1e-7)
        {
            if(first==null||second==null)return false;
            double minN=Math.Min(first.Offset,second.Offset)-Math.Max(tolerance,1e-7);
            double maxN=Math.Max(first.Offset,second.Offset)+Math.Max(tolerance,1e-7);
            double minT=double.PositiveInfinity,maxT=double.NegativeInfinity;
            foreach(var line in new[]{first,second})
                foreach(var span in line.Spans)
                {
                    minT=Math.Min(minT,span.StartT);
                    maxT=Math.Max(maxT,span.EndT);
                }
            if(!Finite(minT)||!Finite(maxT))return false;
            // The builder uses parallel section lines sharing Direction/Normal.
            Vec2 Project(Vec2 point) =>
                new Vec2(Dot(point,first.Normal),Dot(point,first.Direction));
            foreach(var root in Roots(existing,design))
            {
                var p=Project(root);
                if(p.X>=minN&&p.X<=maxN&&p.Y>=minT&&p.Y<=maxT)return true;
            }
            foreach(var face in Faces(existing,design))
            {
                if(!Projection(face,out Vec2 a,out Vec2 c))continue;
                if(SegmentHitsRect(Project(a),Project(c),minN,minT,maxN,maxT))
                    return true;
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
                // Rasterize by grid COLUMN, then only the rows crossed in
                // that column. Previous rectangle scan was O(width*height)
                // per diagonal curtain and froze 5,000+ cell grid work.
                // Intersect exact padded cell rectangles before marking.
                for(long i=loI;i<=hiI;i++)
                {
                    double dt=q.X-p.X, t0=0, t1=1;
                    if(Math.Abs(dt)<=1e-15)
                    {
                        if(p.X<i-1e-8 || p.X>i+1+1e-8) continue;
                    }
                    else
                    {
                        double tx0=(i-1e-8-p.X)/dt;
                        double tx1=(i+1+1e-8-p.X)/dt;
                        if(tx0>tx1){double swap=tx0;tx0=tx1;tx1=swap;}
                        t0=Math.Max(0,tx0);t1=Math.Min(1,tx1);
                        if(t0>t1)continue;
                    }
                    double ya=p.Y+(q.Y-p.Y)*t0;
                    double yb=p.Y+(q.Y-p.Y)*t1;
                    long firstJ=Math.Max(loJ,(long)Math.Floor(Math.Min(ya,yb)-1e-8));
                    long lastJ=Math.Min(hiJ,(long)Math.Floor(Math.Max(ya,yb)+1e-8));
                    for(long j=firstJ;j<=lastJ;j++)
                        if(SegmentHitsRect(p,q,i-1e-8,j-1e-8,i+1+1e-8,j+1+1e-8))
                            affected.Add((checked((int)i),checked((int)j)));
                }
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
