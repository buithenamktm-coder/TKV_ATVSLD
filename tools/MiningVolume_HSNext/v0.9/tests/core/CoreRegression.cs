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
        var options=new SurfaceBuildOptions{DuplicateXYConflictPolicy=policy};
        Check(options.LegacyElevationPolicyIgnored&&options.DuplicateXYConflictPolicy==DuplicateXYConflictPolicy.Stop,"Legacy policy not migrated");
        bool rejected=false;
        try { var r=new TiledConformingTinBuilder().Build("policy",m,options); (r.Surface.Triangles as IDisposable)?.Dispose(); }
        catch(DuplicateXYConflictException ex) { rejected=ex.Report!=null&&ex.Report.HasBlockingConflicts; }
        Check(rejected,"Legacy policy bypassed manual review");
        Check(a.Vertices[0].Position.Z==10&&b.Vertices[0].Position.Z==0,"Policy modified source");
    }
    static void OverlapPolicy(DuplicateXYConflictPolicy policy, bool partial, bool reverse)
    {
        var segs=new[]{new Segment3(P(0,0),P(10,0),"low"),
            new Segment3(P(partial?2:0,0,4),P(partial?8:10,0,4),"high"),
            new Segment3(P(5,-5,7),P(5,5,7),"cross")};
        if(reverse) Array.Reverse(segs);
        var p=Prepare(new[]{P(-1,-6),P(11,-6),P(11,6),P(-1,6)},segs,policy);
        Check(p.HasErrors,"Legacy overlap policy bypassed manual review");
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
            ["legacy_upper_requires_review"] = ()=>Check(Prepare(Corners().Append(P(0,0,1)),null,DuplicateXYConflictPolicy.UseUpper).HasErrors,"Legacy upper bypass"),
            ["legacy_lower_requires_review"] = ()=>Check(Prepare(Corners().Append(P(0,0,1)),null,DuplicateXYConflictPolicy.UseLower).HasErrors,"Legacy lower bypass"),
            ["manual_report_all_sources_and_rescan"] = ()=>{
                var a=Entity("a",SourceEntityType.Point,P(2,3,1));var b=Entity("b",SourceEntityType.Point,P(2,3,1));
                var c=Entity("c",SourceEntityType.Point,P(2,3,8));var m=Model(a,b,c);var scanner=new ConflictScanner();
                var report=scanner.Scan(m,Options);Check(report.Groups.Count==1&&report.Groups[0].Sources.Count==3,"Missing contributing source");
                Check(report.Groups[0].DeltaZ==7&&m.Revision==0,"Wrong delta or mutated source");
                m.RemoveEntityFromModel("c");Check(!scanner.Scan(m,Options).HasBlockingConflicts,"Exclusion did not clear conflict");
                m.RestoreEntity("c");Check(scanner.Scan(m,Options).HasBlockingConflicts,"Restore bypassed scan");
            },
            ["manual_report_crossing_provenance"] = ()=>{
                var m=Model(Entity("a",SourceEntityType.Line,P(0,0,1),P(10,10,1)),Entity("b",SourceEntityType.Line,P(0,10,5),P(10,0,5)));
                var g=new ConflictScanner().Scan(m,Options).Groups.Single();
                Check(g.Position.X==5&&g.Position.Y==5&&g.Sources.Count==2,"Crossing report");
                Check(g.Sources.All(s=>s.Generated&&s.SegmentIndex==0&&s.Handle==s.EntityId),"Missing source mapping");
            },
            ["exact_concave_clip_area_plane_and_storage"] = ()=>{
                var points=new List<Vec3>();for(int x=0;x<=10;x++)for(int y=0;y<=10;y++)points.Add(P(x,y,2*x-3*y+7));
                var boundary=new[]{new Vec2(0.3,0.2),new Vec2(9.7,0.2),new Vec2(9.7,1.6),new Vec2(2.4,1.6),new Vec2(2.4,9.8),new Vec2(0.3,9.8)};
                var options=new SurfaceBuildOptions{ClipBoundary=boundary};
                var tin=Build(Prepare(points),options);double expected=9.4*1.4+2.1*8.2;
                Check(Math.Abs(tin.Triangles.Sum(t=>t.Area2D)-expected)<1e-9,"Concave clipping area");
                Check(tin.Triangles.SelectMany(t=>new[]{t.A,t.B,t.C}).All(p=>Math.Abs(p.Z-(2*p.X-3*p.Y+7))<1e-10),"Clipping changed plane Z");
                var r=new TiledConformingTinBuilder().Build("clip",Model(Entity("points",SourceEntityType.Point,points.ToArray())),options);
                try{Check(Signature(tin)==Signature(r.Surface),"In-memory and stored clip differ");}
                finally{(r.Surface.Triangles as IDisposable)?.Dispose();}
            },
            ["clip_self_intersection_rejected"] = ()=>{
                bool rejected=false;try{new TinPolygonClipper(new[]{new Vec2(0,0),new Vec2(10,10),new Vec2(0,10),new Vec2(10,0)});}
                catch(ArgumentException){rejected=true;}Check(rejected,"Invalid polygon accepted");
            },
            ["large_coordinates"] = ()=>{var p=Prepare(Corners().Select(v=>P(v.X+2300000,v.Y+400000,80)));var t=Build(p);Coverage(p,t);Check(Math.Abs(t.Triangles.Sum(x=>x.Area2D)-100)<1e-6,"Coordinate translation changed area");},
            ["deterministic_repeat"] = ()=>{var p=Prepare(Corners().Append(P(4,3)));Check(Signature(Build(p))==Signature(Build(p)),"Nondeterministic");},
            ["small_positive_triangle_retains_site"] = ()=>{
                var p=Prepare(new[]{P(0,0),P(1,0),P(0.5,0.00000000001)});
                var t=Build(p);Coverage(p,t);Check(t.Triangles.Count==1,"Small positive triangle discarded");
                Check(t.Issues.Any(i=>i.Code=="RETAIN_SMALL_TRIANGLES"),"Small triangle not reported");},
            ["native_collinear_chain_retained"] = ()=>{
                var points=Corners().Concat(Enumerable.Range(1,99).Select(i=>P(i/10.0,5))).ToArray();
                var p=Prepare(points,new[]{new Segment3(P(0,5),P(10,5),"chain")});
                var t=Build(p);Coverage(p,t);Constraints(p,t);
                Check(Math.Abs(t.Triangles.Sum(x=>x.Area2D)-100)<1e-8,"Chain left a hole");},
            ["file_backed_seams_area_and_query_bounds"] = ()=>{
                var random=new Random(722);
                var points=new List<Vec3>{P(0,0),P(100,0),P(100,100),P(0,100)};
                for(int i=0;i<20000;i++)points.Add(P(random.NextDouble()*100,random.NextDouble()*100));
                var m=Model(Entity("random",SourceEntityType.Point,points.ToArray()));
                var r=new TiledConformingTinBuilder().Build("seam",m,Options);
                try {
                    var store=(ITiledTriangleSource)r.Surface.Triangles;
                    var edges=new Dictionary<string,(int count,Vec3 a,Vec3 b)>();double area=0;
                    string Key(Vec3 v)=>$"{Math.Round(v.X,6):R},{Math.Round(v.Y,6):R}";
                    foreach(var tile in store.Tiles){
                        Check(tile.TriangleCount<=8192,"Storage leaf exceeds density cap");
                        foreach(var t in store.ReadTile(tile.Index)){
                            area+=t.Area2D;var p=new[]{t.A,t.B,t.C};
                            Check(p.All(v=>tile.Contains(v.XY)),"Tile bounds omit triangle vertices");
                            for(int i=0;i<3;i++){
                                var a=p[i];var b=p[(i+1)%3];var aa=Key(a);var bb=Key(b);
                                var key=String.CompareOrdinal(aa,bb)<0?aa+"/"+bb:bb+"/"+aa;
                                if(edges.TryGetValue(key,out var value))edges[key]=(value.count+1,a,b);else edges[key]=(1,a,b);
                            }
                        }
                    }
                    bool Hull(Vec3 a,Vec3 b)=>(a.X==0&&b.X==0)||(a.X==100&&b.X==100)||(a.Y==0&&b.Y==0)||(a.Y==100&&b.Y==100);
                    Check(edges.Values.All(e=>e.count==2||(e.count==1&&Hull(e.a,e.b))),"Unpaired interior or nonmanifold edge");
                    Check(Math.Abs(area-10000)<1e-6,"Storage seam changed area");
                    Check(r.PreviewTriangles.Count<=200000,"Unbounded preview");
                    var prepared=Prepare(points);Coverage(prepared,r.Surface);
                } finally {(r.Surface.Triangles as IDisposable)?.Dispose();}
            },
            ["collinear_rejected"] = ()=>{bool rejected=false;try{Build(Prepare(new[]{P(0,0),P(1,1),P(2,2)}));}catch(InvalidOperationException){rejected=true;}Check(rejected,"Collinear accepted");},
            ["builder_cancel"] = ()=>{using(var c=new CancellationTokenSource()){c.Cancel();bool cancelled=false;try{new ConformingTinBuilder().Build("cancel",Prepare(Corners()),Options,null,c.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Cancelled builder returned TIN");}},
            ["preparer_cancel_before_start"] = ()=>{using(var c=new CancellationTokenSource()){c.Cancel();bool cancelled=false;try{new SurfaceInputPreparer().PrepareRaw(Corners(),null,Options,null,c.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Cancelled preparer returned input");}},
            ["preparer_cancel_during_topology"] = ()=>{using(var c=new CancellationTokenSource()){bool cancelled=false;try{new SurfaceInputPreparer().PrepareRaw(Corners(),new[]{new Segment3(P(0,0),P(10,10))},Options,s=>{if(s.Contains("giao điểm"))c.Cancel();},c.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Topology ignored cancellation");}},
            ["source_immutable"] = ()=>{var e=Entity("p",SourceEntityType.Point,P(1,1));var m=Model(e);new SurfaceInputPreparer().Prepare(m,Options);Check(Same(e.Vertices[0].Position,P(1,1))&&m.Revision==0,"Source changed");},
            ["segment_clip_interpolates_z"] = ()=>{var s=new Segment3(P(-1,5,0),P(11,5,12));var r=SurfaceRegionClipper.ClipSegment(s,Corners().Select(v=>v.XY).ToArray());Check(r.Count==1&&Math.Abs(r[0].A.Z-1)<1e-9&&Math.Abs(r[0].B.Z-11)<1e-9,"Clipped Z");},
            ["tiled_duplicate_different_types_stop"] = ()=>{
                var m=Model(Entity("a",SourceEntityType.Point,P(0,0,10)),Entity("b",SourceEntityType.Line,P(0,0,0),P(10,0)),Entity("c",SourceEntityType.Point,P(10,10)),Entity("d",SourceEntityType.Point,P(0,10)));
                bool rejected=false;try{var r=new TiledConformingTinBuilder().Build("conflict",m,Options);(r.Surface.Triangles as IDisposable)?.Dispose();}catch(DuplicateXYConflictException){rejected=true;}Check(rejected,"Tiled builder silently chooses Z by entity type");},
            ["overlap_upper_full_forward"] = ()=>OverlapPolicy(DuplicateXYConflictPolicy.UseUpper,false,false),
            ["overlap_upper_full_reverse"] = ()=>OverlapPolicy(DuplicateXYConflictPolicy.UseUpper,false,true),
            ["overlap_upper_partial_forward"] = ()=>OverlapPolicy(DuplicateXYConflictPolicy.UseUpper,true,false),
            ["overlap_upper_partial_reverse"] = ()=>OverlapPolicy(DuplicateXYConflictPolicy.UseUpper,true,true),
            ["overlap_lower_full_forward"] = ()=>OverlapPolicy(DuplicateXYConflictPolicy.UseLower,false,false),
            ["overlap_lower_full_reverse"] = ()=>OverlapPolicy(DuplicateXYConflictPolicy.UseLower,false,true),
            ["overlap_lower_partial_forward"] = ()=>OverlapPolicy(DuplicateXYConflictPolicy.UseLower,true,false),
            ["overlap_lower_partial_reverse"] = ()=>OverlapPolicy(DuplicateXYConflictPolicy.UseLower,true,true),
            ["tiled_concave_region_empty_outer_tiles"] = ()=>{
                var m=new SurfaceModel("L-region");
                var pts=new List<Vec3>();for(int x=0;x<=128;x++)for(int y=0;y<=128;y++)pts.Add(P(x,y));
                m.Entities.Add(Entity("grid",SourceEntityType.Point,pts.ToArray()));
                var boundary=new[]{new Vec2(0,0),new Vec2(128,0),new Vec2(128,8),new Vec2(8,8),new Vec2(8,128),new Vec2(0,128)};
                var r=new TiledConformingTinBuilder().Build("L-region",m,new SurfaceBuildOptions{ClipBoundary=boundary});
                try {Check(r.Surface.Triangles.Count>0,"Empty region TIN");Check(r.Surface.Triangles.All(t=>Geometry2D.TriangleIntersectsPolygon(t,boundary,1e-6)),"Outside triangle retained");}
                finally {(r.Surface.Triangles as IDisposable)?.Dispose();}
            },
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
    static void Fixture(string path, string boundary, int seconds, DuplicateXYConflictPolicy policy)
    {
        var sw=Stopwatch.StartNew();var m=ReadModel(path);var o=new SurfaceBuildOptions {DuplicateXYConflictPolicy=policy};
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
    static void ClipOracle()
    {
        var boundaries=new[]{
            new[]{new Vec2(.3,.2),new Vec2(9.7,.2),new Vec2(9.7,1.6),new Vec2(2.4,1.6),new Vec2(2.4,9.8),new Vec2(.3,9.8)},
            new[]{new Vec2(.1,5.01),new Vec2(9.9,5.06),new Vec2(9.9,5.08),new Vec2(.1,5.03)},
            new[]{new Vec2(.5,4),new Vec2(6,.3),new Vec2(9.8,6),new Vec2(4,9.9)},
            new[]{new Vec2(-2,3),new Vec2(12,3),new Vec2(12,8),new Vec2(-2,8)}
        };
        for(int k=0;k<boundaries.Length;k++){
            var rng=new Random(822+k);var points=new List<Vec3>();
            foreach(var p in Corners())points.Add(P(p.X,p.Y,2*p.X-3*p.Y+7));
            for(int j=0;j<2000;j++){double x=rng.NextDouble()*10,y=rng.NextDouble()*10;points.Add(P(x,y,2*x-3*y+7));}
            var tin=Build(Prepare(points),new SurfaceBuildOptions{ClipBoundary=boundaries[k]});
            Console.WriteLine(JsonSerializer.Serialize(new{oracleCase=k,boundary=boundaries[k].Select(p=>new[]{p.X,p.Y}),
                triangles=tin.Triangles.Select(t=>new[]{t.A,t.B,t.C}.Select(p=>new[]{p.X,p.Y,p.Z}))}));
        }
    }
    static void Main(string[] args){if(args.Length==0)RunTests();else if(args[0]=="--clip-oracle")ClipOracle();else Fixture(args[0],args.Length>1?args[1]:"-",args.Length>2?int.Parse(args[2]):120,args.Length>3?(DuplicateXYConflictPolicy)Enum.Parse(typeof(DuplicateXYConflictPolicy),args[3]):DuplicateXYConflictPolicy.Stop);}
}
