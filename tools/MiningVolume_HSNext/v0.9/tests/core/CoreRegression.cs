using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;
using MiningVolume.Core.Surface;
using MiningVolume.Surface;

// Compiled against the actual Core + Surface sources, with no CAD mock or geometry reimplementation.
internal static class CoreRegression
{
    static Vec3 P(double x, double y, double z=0) => new Vec3(x,y,z);
    static readonly SurfaceBuildOptions Options = new SurfaceBuildOptions();
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static PreparedSurfaceInput Prepare(IEnumerable<Vec3> points, IEnumerable<Segment3> segments=null,
        DuplicateXYConflictPolicy policy=DuplicateXYConflictPolicy.Stop) =>
        new SurfaceInputPreparer().PrepareRaw(points,segments,new SurfaceBuildOptions { DuplicateXYConflictPolicy=policy });
    static TinSurface Build(PreparedSurfaceInput p, SurfaceBuildOptions o=null) => new ConformingTinBuilder().Build("regression",p,o??Options);
    static bool Same(Vec3 a, Vec3 b) => a.X==b.X && a.Y==b.Y && a.Z==b.Z;
    static IEnumerable<Vec3> Corners() => new[]{P(0,0),P(10,0),P(10,10),P(0,10)};
    static void Coverage(PreparedSurfaceInput p, TinSurface t)
    {
        var used=new HashSet<(double,double)>();
        foreach(var tri in t.Triangles) foreach(var v in new[]{tri.A,tri.B,tri.C}) used.Add((v.X,v.Y));
        Check(p.Sites.All(v=>used.Contains((v.X,v.Y))),"Input site omitted from TIN");
    }
    static void Constraints(PreparedSurfaceInput p, TinSurface t)
    {
        foreach(var s in p.Breaklines)
            Check(t.Triangles.Any(tr=>new[]{(tr.A,tr.B),(tr.B,tr.C),(tr.C,tr.A)}.Any(e=>
                (Same(e.Item1,s.A)&&Same(e.Item2,s.B))||(Same(e.Item1,s.B)&&Same(e.Item2,s.A)))),"Missing constraint "+s.SourceId);
    }
    static string Signature(TinSurface t) => string.Join(";",t.Triangles.Select(x=>
        string.Join("/",new[]{x.A,x.B,x.C}.Select(v=>$"{v.X:R},{v.Y:R},{v.Z:R}").OrderBy(v=>v,StringComparer.Ordinal))).OrderBy(x=>x,StringComparer.Ordinal));
    static SurfaceModel Model(params SourceEntity[] entities) { var m=new SurfaceModel("test"); m.Entities.AddRange(entities); return m; }
    static SourceEntity Entity(string id, SourceEntityType type, params Vec3[] p) => new SourceEntity(id,id,"test",type,p);
    static void TiledPolicy(DuplicateXYConflictPolicy policy, double expected)
    {
        var a=Entity("a",SourceEntityType.Point,P(0,0,10));
        var b=Entity("b",SourceEntityType.Line,P(0,0),P(10,0));
        var m=Model(a,b,Entity("c",SourceEntityType.Point,P(10,10)),Entity("d",SourceEntityType.Point,P(0,10)));
        var r=new TiledConformingTinBuilder().Build("policy",m,new SurfaceBuildOptions{DuplicateXYConflictPolicy=policy});
        try { var points=r.Surface.Triangles.SelectMany(t=>new[]{t.A,t.B,t.C}).Where(v=>v.X==0&&v.Y==0).ToArray();
            Check(points.Length>0&&points.All(v=>v.Z==expected),"Tiled explicit Z policy ignored");
            Check(a.Vertices[0].Position.Z==10&&b.Vertices[0].Position.Z==0,"Policy modified source");
        } finally { (r.Surface.Triangles as IDisposable)?.Dispose(); }
    }
    static void RunTests()
    {
        var tests=new Dictionary<string,Action> {
            ["square_area_no_holes"] = ()=> {var t=Build(Prepare(Corners()));Check(t.Triangles.Count==2&&Math.Abs(t.Triangles.Sum(x=>x.Area2D)-100)<1e-9,"Square area");},
            ["all_sites_referenced"] = ()=>{var p=Prepare(Corners().Concat(new[]{P(2,3),P(6,7),P(5,5)}));Coverage(p,Build(p));},
            ["single_constraint"] = ()=>{var p=Prepare(Corners(),new[]{new Segment3(P(0,0),P(10,10))});Constraints(p,Build(p));},
            ["crossing_same_z"] = ()=>{var p=Prepare(Corners(),new[]{new Segment3(P(0,0),P(10,10)),new Segment3(P(0,10),P(10,0))});Check(!p.HasErrors&&p.Sites.Any(v=>v.X==5&&v.Y==5),"Crossing not noded");Constraints(p,Build(p));},
            ["crossing_conflicting_z"] = ()=>{var p=Prepare(Corners(),new[]{new Segment3(P(0,0),P(10,10)),new Segment3(P(0,10,2),P(10,0,2))});Check(p.HasErrors,"Z conflict accepted");},
            ["overlapping_constraints"] = ()=>{var p=Prepare(Corners(),new[]{new Segment3(P(0,0),P(10,0)),new Segment3(P(2,0),P(8,0))});Check(!p.HasErrors,"Overlap rejected");Constraints(p,Build(p));},
            ["site_on_constraint"] = ()=>{var p=Prepare(Corners().Append(P(5,0)),new[]{new Segment3(P(0,0),P(10,0))});Check(p.Breaklines.Count==2,"Site not split");Coverage(p,Build(p));},
            ["duplicate_xy_same_z"] = ()=>{var p=Prepare(Corners().Append(P(0,0)));Check(p.Sites.Count==4&&!p.HasErrors,"Duplicate handling");},
            ["duplicate_xy_stop"] = ()=>Check(Prepare(Corners().Append(P(0,0,1))).HasErrors,"Conflicting Z accepted"),
            ["duplicate_xy_upper"] = ()=>{var p=Prepare(Corners().Append(P(0,0,1)),null,DuplicateXYConflictPolicy.UseUpper);Check(!p.HasErrors&&p.Sites.Single(v=>v.X==0&&v.Y==0).Z==1,"Upper policy");},
            ["duplicate_xy_lower"] = ()=>{var p=Prepare(Corners().Append(P(0,0,1)),null,DuplicateXYConflictPolicy.UseLower);Check(!p.HasErrors&&p.Sites.Single(v=>v.X==0&&v.Y==0).Z==0,"Lower policy");},
            ["large_coordinates"] = ()=>{var p=Prepare(Corners().Select(v=>P(v.X+2300000,v.Y+400000,80)));var t=Build(p);Coverage(p,t);Check(Math.Abs(t.Triangles.Sum(x=>x.Area2D)-100)<1e-6,"Coordinate translation changed area");},
            ["deterministic_repeat"] = ()=>{var p=Prepare(Corners().Append(P(4,3)));Check(Signature(Build(p))==Signature(Build(p)),"Nondeterministic");},
            ["collinear_rejected"] = ()=>{bool rejected=false;try{Build(Prepare(new[]{P(0,0),P(1,1),P(2,2)}));}catch(InvalidOperationException){rejected=true;}Check(rejected,"Collinear accepted");},
            ["builder_cancel"] = ()=>{using(var c=new CancellationTokenSource()){c.Cancel();bool cancelled=false;try{new ConformingTinBuilder().Build("cancel",Prepare(Corners()),Options,null,c.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Cancelled builder returned TIN");}},
            ["preparer_cancel_before_start"] = ()=>{using(var c=new CancellationTokenSource()){c.Cancel();bool cancelled=false;try{new SurfaceInputPreparer().PrepareRaw(Corners(),null,Options,null,c.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Cancelled preparer returned input");}},
            ["preparer_cancel_during_topology"] = ()=>{using(var c=new CancellationTokenSource()){bool cancelled=false;try{new SurfaceInputPreparer().PrepareRaw(Corners(),new[]{new Segment3(P(0,0),P(10,10))},Options,s=>{if(s.Contains("giao điểm"))c.Cancel();},c.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Topology ignored cancellation");}},
            ["source_immutable"] = ()=>{var e=Entity("p",SourceEntityType.Point,P(1,1));var m=Model(e);new SurfaceInputPreparer().Prepare(m,Options);Check(Same(e.Vertices[0].Position,P(1,1))&&m.Revision==0,"Source changed");},
            ["segment_clip_interpolates_z"] = ()=>{var s=new Segment3(P(-1,5,0),P(11,5,12));var r=SurfaceRegionClipper.ClipSegment(s,Corners().Select(v=>v.XY).ToArray());Check(r.Count==1&&Math.Abs(r[0].A.Z-1)<1e-9&&Math.Abs(r[0].B.Z-11)<1e-9,"Clipped Z");},
            ["tiled_duplicate_different_types_stop"] = ()=>{
                var m=Model(Entity("a",SourceEntityType.Point,P(0,0,10)),Entity("b",SourceEntityType.Line,P(0,0,0),P(10,0)),Entity("c",SourceEntityType.Point,P(10,10)),Entity("d",SourceEntityType.Point,P(0,10)));
                bool rejected=false;try{var r=new TiledConformingTinBuilder().Build("conflict",m,Options);(r.Surface.Triangles as IDisposable)?.Dispose();}catch(DuplicateXYConflictException){rejected=true;}Check(rejected,"Tiled builder silently chooses Z by entity type");},
            ["tiled_explicit_upper"] = ()=>TiledPolicy(DuplicateXYConflictPolicy.UseUpper,10),
            ["tiled_explicit_lower"] = ()=>TiledPolicy(DuplicateXYConflictPolicy.UseLower,0),
            ["tiled_minor_z_conflict_stop"] = ()=>{
                var m=Model(Entity("a",SourceEntityType.Point,P(0,0,0.01)),Entity("b",SourceEntityType.Point,P(0,0)),Entity("c",SourceEntityType.Point,P(10,10)),Entity("d",SourceEntityType.Point,P(0,10)),Entity("e",SourceEntityType.Point,P(10,0)));
                bool rejected=false;try{var r=new TiledConformingTinBuilder().Build("conflict",m,Options);(r.Surface.Triangles as IDisposable)?.Dispose();}catch(DuplicateXYConflictException){rejected=true;}Check(rejected,"Tiled builder silently ignores small Z conflict");}
        };
        int failed=0; foreach(var test in tests){var sw=Stopwatch.StartNew();try{test.Value();Console.WriteLine(JsonSerializer.Serialize(new{name=test.Key,status="PASS",ms=sw.ElapsedMilliseconds}));}catch(Exception ex){failed++;Console.WriteLine(JsonSerializer.Serialize(new{name=test.Key,status="FAIL",ms=sw.ElapsedMilliseconds,error=ex.Message}));}}
        Console.WriteLine(JsonSerializer.Serialize(new{tests=tests.Count,failed})); Environment.ExitCode=failed==0?0:1;
    }
    static SurfaceModel ReadModel(string path)
    {
        var m=new SurfaceModel(Path.GetFileNameWithoutExtension(path));
        using(var r=new BinaryReader(File.OpenRead(path)))while(r.BaseStream.Position<r.BaseStream.Length){
            var type=(SourceEntityType)r.ReadByte();bool closed=r.ReadByte()!=0;int hn=r.ReadInt32();int n=r.ReadInt32();
            string handle=Encoding.ASCII.GetString(r.ReadBytes(hn));var p=new Vec3[n];for(int i=0;i<n;i++)p[i]=P(r.ReadDouble(),r.ReadDouble(),r.ReadDouble());
            m.Entities.Add(new SourceEntity(handle,handle,m.Name,type,p,closed));
        }
        return m;
    }
    static void Fixture(string path, string boundary, int seconds)
    {
        var sw=Stopwatch.StartNew();var m=ReadModel(path);var o=new SurfaceBuildOptions();
        Console.WriteLine(JsonSerializer.Serialize(new{phase="loaded",entities=m.Entities.Count,vertices=m.Entities.Sum(x=>x.Vertices.Count),ms=sw.ElapsedMilliseconds}));
        using(var c=new CancellationTokenSource(TimeSpan.FromSeconds(seconds)))try {
            if(boundary!="-") {using(var r=new BinaryReader(File.OpenRead(boundary))){int n=r.ReadInt32();var b=new Vec2[n];for(int i=0;i<n;i++)b[i]=new Vec2(r.ReadDouble(),r.ReadDouble());o.ClipBoundary=b;}m=SurfaceRegionClipper.Clip(m,o.ClipBoundary,o.XyTolerance,c.Token);}
            int vertices=m.Entities.Sum(x=>x.Vertices.Count);
            Console.WriteLine(JsonSerializer.Serialize(new{phase="region",entities=m.Entities.Count,vertices,ms=sw.ElapsedMilliseconds}));
            TinSurface tin;
            if(vertices>=200000){var r=new TiledConformingTinBuilder().Build(m.Name,m,o,(d,t,s)=>Console.WriteLine(JsonSerializer.Serialize(new{phase="progress",done=d,total=t,message=s})),c.Token);tin=r.Surface;}
            else {var p=new SurfaceInputPreparer().Prepare(m,o,s=>Console.WriteLine(s),c.Token);Console.WriteLine(JsonSerializer.Serialize(new{phase="prepared",sites=p.Sites.Count,breaklines=p.Breaklines.Count,errors=p.Issues.Count(x=>x.Severity==ValidationSeverity.Error),issues=p.Issues.GroupBy(x=>x.Code).ToDictionary(x=>x.Key,x=>x.Count()),ms=sw.ElapsedMilliseconds}));if(p.HasErrors){Environment.ExitCode=2;return;}tin=new ConformingTinBuilder().Build(m.Name,p,o,s=>Console.WriteLine(s),c.Token);}
            Console.WriteLine(JsonSerializer.Serialize(new{phase="complete",triangles=tin.Triangles.Count,ms=sw.ElapsedMilliseconds,managedBytes=GC.GetTotalMemory(false)}));(tin.Triangles as IDisposable)?.Dispose();
        } catch(Exception ex){Console.WriteLine(JsonSerializer.Serialize(new{phase="error",kind=ex.GetType().Name,message=ex.Message,issues=(ex as DuplicateXYConflictException)?.Issues.GroupBy(x=>x.Code).ToDictionary(x=>x.Key,x=>x.Count()),ms=sw.ElapsedMilliseconds}));Environment.ExitCode=2;}
    }
    static void Main(string[] args){if(args.Length==0)RunTests();else Fixture(args[0],args.Length>1?args[1]:"-",args.Length>2?int.Parse(args[2]):120);}
}
