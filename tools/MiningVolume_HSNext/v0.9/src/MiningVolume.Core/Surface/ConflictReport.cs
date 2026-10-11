using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;

namespace MiningVolume.Core.Surface
{
    public sealed class ConflictSource
    {
        public string EntityId { get; set; }
        public string Handle { get; set; }
        public string Layer { get; set; }
        public SourceEntityType Type { get; set; }
        public int VertexIndex { get; set; } = -1;
        public int SegmentIndex { get; set; } = -1;
        public double SegmentParameter { get; set; }
        public double Z { get; set; }
        public bool Generated { get; set; }
        public string Origin { get; set; }
        internal string Key => EntityId+":"+VertexIndex+":"+SegmentIndex+":"+SegmentParameter.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
    }

    public sealed class ConflictGroup
    {
        public string Id { get; internal set; }
        public Vec2 Position { get; internal set; }
        public List<ConflictSource> Sources { get; } = new List<ConflictSource>();
        internal HashSet<string> SourceKeys { get; } = new HashSet<string>(StringComparer.Ordinal);
        public double DeltaZ => Sources.Count==0?0:Sources.Max(s=>s.Z)-Sources.Min(s=>s.Z);
        public string Status => "Chưa xử lý — cần kiểm tra nguồn; TIN 2.5D chỉ có một Z tại mỗi XY";
    }

    public sealed class ConflictReport
    {
        public string ModelName { get; set; }
        public long SourceRevision { get; set; }
        public DateTime ScannedUtc { get; set; } = DateTime.UtcNow;
        public double XyTolerance { get; set; }
        public double ZTolerance { get; set; }
        public int SameXYZDuplicates { get; internal set; }
        public int InputVertices { get; internal set; }
        public int InputSegments { get; internal set; }
        public List<ConflictGroup> Groups { get; } = new List<ConflictGroup>();
        public bool HasBlockingConflicts => Groups.Count>0;
        public int SourceRecordCount => Groups.Sum(g=>g.Sources.Count);
        public DuplicateXYConflictException ToException() => new DuplicateXYConflictException(ModelName,
            Groups.Select(g=>new ValidationIssue(ValidationSeverity.Error,"DUPLICATE_XY_CONFLICT_Z",
                g.Id+": "+g.Sources.Count+" nguồn khác cao độ; kiểm tra bảng Điểm trùng tọa độ.")).ToList()){Report=this};
    }

    /// <summary>Read-only, per-surface scan of source and generated geometry. Never chooses Z.</summary>
    public sealed class ConflictScanner
    {
        private readonly struct Cell : IEquatable<Cell>
        {
            public readonly long X,Y;
            public Cell(long x,long y){X=x;Y=y;}
            public bool Equals(Cell p)=>X==p.X&&Y==p.Y;
            public override bool Equals(object p)=>p is Cell&&Equals((Cell)p);
            public override int GetHashCode(){unchecked{return X.GetHashCode()*397^Y.GetHashCode();}}
        }
        private sealed class PointRef { public Vec3 P; public SourceEntity Entity; public int Vertex; }
        private sealed class EdgeRef { public Segment3 Edge; public SourceEntity Entity; public int Segment; }

        public ConflictReport Scan(SurfaceModel model, SurfaceBuildOptions options,
            Action<string> progress=null,CancellationToken token=default(CancellationToken))
        {
            if(model==null)throw new ArgumentNullException(nameof(model));
            options=options??new SurfaceBuildOptions();
            double xy=options.XyTolerance,z=options.ZConflictTolerance;
            if(!(xy>0)||!Finite(xy)||!(z>=0)||!Finite(z))throw new ArgumentException("Dung sai XY/Z không hợp lệ.");
            var report=new ConflictReport{ModelName=model.Name,SourceRevision=model.Revision,XyTolerance=xy,ZTolerance=z};
            var points=new List<PointRef>();var edges=new List<EdgeRef>();
            var watch=Stopwatch.StartNew();
            void Pulse(string stage,int done,int total){if(watch.ElapsedMilliseconds<500)return;watch.Restart();progress?.Invoke(stage+$" {done:n0}/{total:n0}...");}
            foreach(var e in model.Entities){
                token.ThrowIfCancellationRequested();if(!e.IsEnabled)continue;
                foreach(var v in e.ActiveVertices()){
                    token.ThrowIfCancellationRequested();var p=v.Position;
                    if(!Finite(p.X)||!Finite(p.Y)||!Finite(p.Z))throw new InvalidOperationException("Nguồn "+e.Handle+" có tọa độ không hữu hạn.");
                    points.Add(new PointRef{P=p,Entity=e,Vertex=v.Index});
                }
                if(e.IsBreakline)for(int i=0;i<e.Vertices.Count-(e.Closed?0:1);i++){
                    var a=e.Vertices[i];var b=e.Vertices[(i+1)%e.Vertices.Count];
                    if(a.IsEnabled&&b.IsEnabled){var seg=new Segment3(a.Position,b.Position,e.Id);if(seg.Length2D>xy)edges.Add(new EdgeRef{Edge=seg,Entity=e,Segment=i});}
                }
            }
            report.InputVertices=points.Count;report.InputSegments=edges.Count;
            var groupCells=new Dictionary<Cell,List<ConflictGroup>>();
            Cell At(Vec2 p)=>new Cell((long)Math.Floor(p.X/xy),(long)Math.Floor(p.Y/xy));
            ConflictSource Vertex(PointRef p) {
                int original=p.Entity.OriginVertexIndices!=null?p.Entity.OriginVertexIndices[p.Vertex]:
                    p.Entity.IsGenerated?-1:p.Vertex;
                return new ConflictSource {EntityId=p.Entity.OriginId,Handle=p.Entity.Handle,Layer=p.Entity.Layer,
                    Type=p.Entity.OriginType??p.Entity.Type,VertexIndex=original,Z=p.P.Z,Generated=original<0,
                    Origin=original<0?"Đỉnh cắt phạm vi; xem đối tượng gốc":"Đỉnh mô hình nguồn"};
            }
            ConflictSource Edge(EdgeRef e,Vec2 p,string origin)=>new ConflictSource{EntityId=e.Entity.OriginId,Handle=e.Entity.Handle,
                Layer=e.Entity.Layer,Type=e.Entity.OriginType??e.Entity.Type,SegmentIndex=e.Entity.OriginSegmentIndex??e.Segment,
                SegmentParameter=SurfaceInputPreparer.ParameterOnSegment(p,e.Edge),Z=Geometry2D.InterpolateZOnSegment(p,e.Edge),Generated=true,Origin=origin};
            void Add(Vec2 p,params ConflictSource[] sources){
                var cell=At(p);ConflictGroup group=null;
                for(long dx=-1;dx<=1&&group==null;dx++)for(long dy=-1;dy<=1&&group==null;dy++)
                    if(groupCells.TryGetValue(new Cell(cell.X+dx,cell.Y+dy),out var list))
                        group=list.FirstOrDefault(g=>g.Position.DistanceTo(p)<=xy);
                if(group==null){group=new ConflictGroup{Position=p};report.Groups.Add(group);
                    if(!groupCells.TryGetValue(cell,out var list))groupCells[cell]=list=new List<ConflictGroup>();list.Add(group);}
                foreach(var source in sources)if(group.SourceKeys.Add(source.Key))group.Sources.Add(source);
            }
            // Only duplicate buckets allocate a list of contributing vertices.
            var pointCells=new Dictionary<Cell,List<int>>();var duplicates=new Dictionary<int,List<int>>();
            for(int i=0;i<points.Count;i++){
                token.ThrowIfCancellationRequested();Pulse("Quét trùng XY",i,points.Count);
                var p=points[i].P;var cell=At(p.XY);int match=-1;
                for(long dx=-1;dx<=1&&match<0;dx++)for(long dy=-1;dy<=1&&match<0;dy++)
                    if(pointCells.TryGetValue(new Cell(cell.X+dx,cell.Y+dy),out var ids))
                        foreach(int id in ids)if(points[id].P.XY.DistanceTo(p.XY)<=xy){match=id;break;}
                if(match<0){if(!pointCells.TryGetValue(cell,out var ids))pointCells[cell]=ids=new List<int>();ids.Add(i);}
                else{if(!duplicates.TryGetValue(match,out var ids))duplicates[match]=ids=new List<int>{match};ids.Add(i);}
            }
            foreach(var pair in duplicates){
                token.ThrowIfCancellationRequested();var ids=pair.Value;
                if(ids.Max(i=>points[i].P.Z)-ids.Min(i=>points[i].P.Z)>z)
                    Add(points[pair.Key].P.XY,ids.Select(i=>Vertex(points[i])).ToArray());
                else report.SameXYZDuplicates+=ids.Count-1;
            }
            if(points.Count>0&&edges.Count>0){
                var index=new SurfaceInputPreparer.PointGridIndex(points.Select(p=>p.P).ToArray(),xy,token);
                for(int i=0;i<edges.Count;i++){
                    token.ThrowIfCancellationRequested();Pulse("Kiểm tra điểm trên breakline",i,edges.Count);var e=edges[i];var s=e.Edge;
                    foreach(int j in index.Query(s.A.X,s.A.Y,s.B.X,s.B.Y,xy)){
                        token.ThrowIfCancellationRequested();var p=points[j];
                        if(!Geometry2D.PointOnSegment(p.P.XY,s.A.XY,s.B.XY,xy))continue;
                        var edgeSource=Edge(e,p.P.XY,"Nội suy trên breakline");
                        if(Math.Abs(edgeSource.Z-p.P.Z)>z)Add(p.P.XY,Vertex(p),edgeSource);
                    }
                }
            }
            if(edges.Count>1){
                var index=new SurfaceInputPreparer.SegmentGridIndex(edges.Select(e=>e.Edge).ToArray(),xy,token);
                for(int i=0;i<edges.Count;i++){
                    token.ThrowIfCancellationRequested();Pulse("Kiểm tra giao/chồng breakline",i,edges.Count);
                    foreach(int j in index.Query(i)){
                        token.ThrowIfCancellationRequested();if(j<=i)continue;var a=edges[i];var b=edges[j];
                        if(Geometry2D.ProperIntersection(a.Edge.A.XY,a.Edge.B.XY,b.Edge.A.XY,b.Edge.B.XY,xy)){
                            if(SurfaceInputPreparer.TryLineIntersection(a.Edge.A.XY,a.Edge.B.XY,b.Edge.A.XY,b.Edge.B.XY,out var p)){
                                var sa=Edge(a,p,"Giao điểm hai breakline");var sb=Edge(b,p,"Giao điểm hai breakline");
                                if(Math.Abs(sa.Z-sb.Z)>z)Add(p,sa,sb);
                            }
                        }else if(SurfaceInputPreparer.CollinearOverlap(a.Edge,b.Edge,xy)){
                            foreach(var p in new[]{a.Edge.A.XY,a.Edge.B.XY,b.Edge.A.XY,b.Edge.B.XY}){
                                if(!Geometry2D.PointOnSegment(p,a.Edge.A.XY,a.Edge.B.XY,xy)||!Geometry2D.PointOnSegment(p,b.Edge.A.XY,b.Edge.B.XY,xy))continue;
                                var sa=Edge(a,p,"Đoạn breakline chồng");var sb=Edge(b,p,"Đoạn breakline chồng");if(Math.Abs(sa.Z-sb.Z)>z)Add(p,sa,sb);
                            }
                        }
                    }
                }
            }
            report.Groups.Sort((a,b)=>{int c=a.Position.X.CompareTo(b.Position.X);return c!=0?c:a.Position.Y.CompareTo(b.Position.Y);});
            for(int i=0;i<report.Groups.Count;i++)report.Groups[i].Id="XY-"+(i+1).ToString("000");
            progress?.Invoke($"Kiểm tra xong: {report.Groups.Count:n0} nhóm chặn • {report.SourceRecordCount:n0} bản ghi nguồn.");
            return report;
        }
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
