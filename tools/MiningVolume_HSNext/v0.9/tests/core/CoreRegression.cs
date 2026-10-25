using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Grid;
using MiningVolume.Core.Sections;
using MiningVolume.Core.Volumes;
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
    // Exercises the REAL tiled index without writing a giant temporary TIN
    // to disk. Both sources expose the same exact triangles.
    private sealed class RegressionTile : ITiledTriangleSource
    {
        private readonly Triangle3[] _items;
        private readonly TinTileInfo _tile;
        internal RegressionTile(Triangle3[] items, double max)
        {
            _items = items;
            _tile = new TinTileInfo(0, 0, items.Length, 0, 0, max, max);
        }
        public IReadOnlyList<TinTileInfo> Tiles => new[] { _tile };
        public bool IsFileBacked => true;
        public int Count => _items.Length;
        public Triangle3 this[int i] => _items[i];
        public IReadOnlyList<Triangle3> ReadTile(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _items;
        }
        public IEnumerable<int> QueryTiles(double minX, double minY, double maxX, double maxY)
        {
            if (_tile.Intersects(minX, minY, maxX, maxY)) yield return 0;
        }
        public IEnumerator<Triangle3> GetEnumerator() => ((IEnumerable<Triangle3>)_items).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public void Dispose() { }
    }

    static void CheckTiledSectionParity()
    {
        const int n = 40;
        var existing = new List<Triangle3>();
        var design = new List<Triangle3>();
        Func<double,double,double> height = (x,y) => 11 + 0.1*x + 0.2*y;
        for (int ix = 0; ix < n; ix++)
        for (int iy = 0; iy < n; iy++)
        {
            Vec3 A(double x,double y) => P(x,y,height(x,y));
            Vec3 B(double x,double y) => P(x,y,height(x,y)-5);
            var points = new[] { (x:(double)ix,y:(double)iy),
                (x:(double)ix+1,y:(double)iy),
                (x:(double)ix+1,y:(double)iy+1),
                (x:(double)ix,y:(double)iy+1) };
            foreach (int[] face in new[]{new[]{0,1,2},new[]{0,2,3}})
            {
                existing.Add(new Triangle3(
                    A(points[face[0]].x,points[face[0]].y),
                    A(points[face[1]].x,points[face[1]].y),
                    A(points[face[2]].x,points[face[2]].y)));
                design.Add(new Triangle3(
                    B(points[face[0]].x,points[face[0]].y),
                    B(points[face[1]].x,points[face[1]].y),
                    B(points[face[2]].x,points[face[2]].y)));
            }
        }
        var issues = Array.Empty<ValidationIssue>();
        var referenceExisting = new TinSurface("reference HT", existing.ToArray(), issues);
        var referenceDesign = new TinSurface("reference TK", design.ToArray(), issues);
        var tiledExisting = new TinSurface("tiled HT", new RegressionTile(existing.ToArray(),n),issues);
        var tiledDesign = new TinSurface("tiled TK", new RegressionTile(design.ToArray(),n),issues);
        var lines = new [] {
            new SectionLine(1,"horizontal",0,new Vec2(1,0),new Vec2(0,1),
                new[]{new SectionSpan(0,n)}),
            new SectionLine(2,"horizontal-edge",n,new Vec2(1,0),new Vec2(0,1),
                new[]{new SectionSpan(0,n)}),
            new SectionLine(3,"horizontal-near",0.0000001,new Vec2(1,0),new Vec2(0,1),
                new[]{new SectionSpan(0,n)}),
            new SectionLine(4,"vertical",0,new Vec2(0,1),new Vec2(1,0),
                new[]{new SectionSpan(0,n)}),
            new SectionLine(5,"vertical-middle",n/2.0,new Vec2(0,1),new Vec2(1,0),
                new[]{new SectionSpan(0,n)}),
            new SectionLine(6,"diagonal",0,new Vec2(Math.Sqrt(.5),Math.Sqrt(.5)),
                new Vec2(-Math.Sqrt(.5),Math.Sqrt(.5)),
                new[]{new SectionSpan(0,n*Math.Sqrt(2.0))})
        };
        var standard = new TinSectionSampler();
        var tiled = new TinSectionSampler();
        foreach(var line in lines)
        {
            var a = standard.BuildProfile(line,referenceExisting,referenceDesign);
            var b = tiled.BuildProfile(line,tiledExisting,tiledDesign);
            Check(Math.Abs(a.CutArea-b.CutArea)<1e-6,
                "Tiled spatial candidate query changed cut area for "+line.Name);
            Check(Math.Abs(a.FillArea-b.FillArea)<1e-6,
                "Tiled spatial candidate query changed fill area for "+line.Name);
            Check(b.Segments.Count==a.Segments.Count,
                "Tiled spatial candidate query missed a segment for "+line.Name);
            double expectedLength = line.Name == "diagonal" ? n*Math.Sqrt(2.0) : n;
            Check(Math.Abs(b.CutArea - expectedLength*5)<1e-5,
                "Tiled spatial candidate query lost terrain coverage for "+line.Name);
        }
        // The pair of very wide triangles MUST still be discovered through
        // the broad-bin fallback (each triangle covers all 32x32 cells).
        var wide = new [] {
            new Triangle3(P(0,0,10),P(n,0,10),P(n,n,10)),
            new Triangle3(P(0,0,10),P(n,n,10),P(0,n,10))
        };
        var wideTiled = new TinSurface("wide",new RegressionTile(wide,n),issues);
        var wideReference = new TinSurface("wide-reference",wide,issues);
        var aWide = standard.BuildProfile(lines[5],wideReference,referenceDesign);
        var bWide = tiled.BuildProfile(lines[5],wideTiled,tiledDesign);
        Check(Math.Abs(aWide.CutArea-bWide.CutArea)<1e-6 &&
            Math.Abs(aWide.FillArea-bWide.FillArea)<1e-6,
            "Broad triangles omitted from tiled section spatial query");
    }

    static void RunTests()
    {
        var tests=new Dictionary<string,Action> {
            ["batch_source_edit_6000_entities_single_revision_and_no_partial_changes"] = ()=>{
                var entities=Enumerable.Range(0,6000).Select(i =>
                    Entity("E"+i,SourceEntityType.Point,P(i,0,i))).ToArray();
                var model=Model(entities);
                var keys=Enumerable.Range(0,2000).Select(i =>
                    new KeyValuePair<string,int>("E"+(i*3),0)).ToList();
                keys.Add(keys[0]); // duplicate selection must not log/edit twice
                long old=model.Revision;
                model.ValidateVertexSelection(keys);
                model.SetVerticesEnabled(keys,false);
                Check(model.Revision==old+1 && model.EditHistory.Count==2000,
                    "Batch exclusion should record per vertex but touch source only once");
                Check(!entities[0].Vertices[0].IsEnabled &&
                    entities[1].Vertices[0].IsEnabled &&
                    !entities[5997].Vertices[0].IsEnabled,
                    "Batch exclusion changed wrong source vertex");
                Check(entities[5997].Vertices[0].Original.Z==5997,
                    "Source edits must retain original CAD Z");

                long revision=model.Revision;int history=model.EditHistory.Count;
                var invalid=keys.Take(3).Concat(new[]{
                    new KeyValuePair<string,int>("E10",3) }).ToArray();
                bool rejected=false;
                try{model.SetVerticesEnabled(invalid,true);}
                catch(ArgumentOutOfRangeException){rejected=true;}
                Check(rejected && model.Revision==revision &&
                    model.EditHistory.Count==history &&
                    !entities[0].Vertices[0].IsEnabled,
                    "Bad row must reject complete edit batch without partial mutations");
                rejected=false;
                try{model.RestoreSelectedVertices(new[]{
                    keys[0],new KeyValuePair<string,int>("MISSING",0)});}
                catch(KeyNotFoundException){rejected=true;}
                Check(rejected && model.Revision==revision &&
                    model.EditHistory.Count==history &&
                    !entities[0].Vertices[0].IsEnabled,
                    "Unknown source entity must reject complete restore batch");

                model.SetEntitiesEnabled(keys.Select(x=>x.Key),false);
                Check(model.Revision==revision+1 &&
                    !entities[0].IsEnabled && entities[1].IsEnabled,
                    "Batch entity exclusion changed wrong entities or revision");
                revision=model.Revision;
                model.RestoreSelectedVertices(keys);
                Check(model.Revision==revision+1 &&
                    model.EditHistory.Count==4000 &&
                    entities[0].IsEnabled && entities[5997].IsEnabled &&
                    entities[0].Vertices[0].IsEnabled &&
                    entities[5997].Vertices[0].IsEnabled &&
                    entities[1].IsEnabled,
                    "Batch restore must enable entities/vertices and keep complete history");
                revision=model.Revision;
                model.SetVerticesEnabled(Array.Empty<KeyValuePair<string,int>>(),true);
                model.SetEntitiesEnabled(Array.Empty<string>(),false);
                model.RestoreSelectedVertices(Array.Empty<KeyValuePair<string,int>>());
                Check(model.Revision==revision,
                    "Empty batch must not change source revision");
            },
            ["model_grid_virtual_index_maps_vertices_and_revisions"] = ()=>{
                var longLine=Entity("long",SourceEntityType.Polyline3d,
                    Enumerable.Range(0,30000).Select(i=>P(i,0,i*0.1)).ToArray());
                var singleton=Entity("single",SourceEntityType.Point,P(-1,-2,7));
                var tail=Entity("tail",SourceEntityType.Line,P(1,1,1),P(2,2,2));
                var source=Model(singleton,
                    Entity("empty",SourceEntityType.Point,Array.Empty<Vec3>()),
                    longLine,tail);
                var index=new ModelVertexRowIndex(source);
                Check(index.RowCount==30003 && index.ActiveVertexCount==30003,
                    "Virtual row index missed a source vertex");
                foreach(int row in new[]{0,1,15000,30000,30001,30002})
                {
                    SourceEntity entity;
                    ModelVertex vertex;
                    Check(index.TryGet(row,out entity,out vertex),
                        "Virtual row lookup missed row "+row);
                    var expectedEntity=row==0?singleton:row<=30000?longLine:tail;
                    int expectedLocal=row==0?0:row<=30000?row-1:row-30001;
                    Check(ReferenceEquals(entity,expectedEntity) &&
                        ReferenceEquals(vertex,expectedEntity.Vertices[expectedLocal]),
                        "Virtual row index changed object/vertex mapping");
                }
                foreach(int row in new[]{-1,30003,int.MaxValue})
                {
                    SourceEntity entity;
                    ModelVertex vertex;
                    Check(!index.TryGet(row,out entity,out vertex) &&
                        entity==null && vertex==null,
                        "Virtual row index returned a phantom vertex");
                }
                source.SetVertexEnabled("long",29999,false);
                var renewed=new ModelVertexRowIndex(source);
                Check(renewed.Revision!=index.Revision &&
                    renewed.RowCount==index.RowCount &&
                    renewed.ActiveVertexCount==30002,
                    "Revision rebuild changed row offsets or missed excluded vertex");
                SourceEntity currentEntity;
                ModelVertex currentVertex;
                Check(renewed.TryGet(30000,out currentEntity,out currentVertex) &&
                    !currentVertex.IsEnabled,
                    "Virtual grid failed to show excluded vertex without changing row order");
            },
            ["tiled_section_spatial_query_matches_exact_reference"] = CheckTiledSectionParity,
            ["clipped_upright_vertical_faces_all_valid_ABC_permutations"] = ()=>{
                var ring=new[]{new Vec2(0,0),new Vec2(8,0),new Vec2(8,10),new Vec2(0,10)};
                var source=new Triangle3(P(-2,5,10),P(-2,5,20),P(10,5,12));
                var clipped=VerticalFaceBuilder.ClipToBoundary(new[]{source},ring);
                Check(clipped.Count==2,"Expected both triangles of clipped upright face");
                foreach(var face in clipped)
                    Check(VerticalFaceBuilder.IsValidCadFace(face),
                        "Clipped upright face is rejected by real CAD quality gate");
                Check(VerticalFaceBuilder.IsValidCadFace(source),
                    "Original AB vertical edge rejected");
                Check(VerticalFaceBuilder.IsValidCadFace(new Triangle3(
                    source.B,source.C,source.A)), "CA vertical edge rejected");
                Check(VerticalFaceBuilder.IsValidCadFace(new Triangle3(
                    source.C,source.A,source.B)), "BC vertical edge rejected");
                Check(!VerticalFaceBuilder.IsValidCadFace(new Triangle3(
                    P(0,0,0),P(1,0,0),P(0,1,0))),
                    "Horizontal triangle must not be treated as vertical curtain");
                Check(!VerticalFaceBuilder.IsValidCadFace(new Triangle3(
                    P(0,0,0),P(0,0,0),P(1,0,0))),
                    "Degenerate face accepted");
            },
            ["quick_section_bent_route_uses_true_cumulative_chainage_without_system"] = ()=>{
                var ht=new TinSurface("ht",new[]{
                    new Triangle3(P(0,0,10),P(10,0,10),P(10,10,10)),
                    new Triangle3(P(0,0,10),P(10,10,10),P(0,10,10))},
                    Array.Empty<ValidationIssue>());
                var tk=new TinSurface("tk",new[]{
                    new Triangle3(P(0,0,2),P(10,0,2),P(10,10,2)),
                    new Triangle3(P(0,0,2),P(10,10,2),P(0,10,2))},
                    Array.Empty<ValidationIssue>());
                var route=new[]{new Vec2(1,1),new Vec2(9,1),new Vec2(9,9)};
                var result=new QuickSectionProfileBuilder().Build(1,"MCN-01",
                    route,ht,tk,new TinSectionSampler());
                Check(result.HasData,"Bent quick route produced no sampled segments");
                Check(Math.Abs(result.Line.MaxT-16)<1e-8 &&
                    Math.Abs(result.Line.MinT)<1e-8,
                    "Quick route must use 8m + 8m chainage (not 11.314m chord)");
                Check(Math.Abs(result.CutArea-128)<1e-5 &&
                    Math.Abs(result.FillArea)<1e-5,
                    "TIN 10/2 elevation difference across 16m route not preserved");
                Check(result.Segments.All(x=>x.S0>=-1e-6 && x.S1<=16+1e-6),
                    "Quick section stations exceed actual selected CAD route");
                Check(Math.Abs(result.Segments.First().Map0.X-1)<1e-6 &&
                    Math.Abs(result.Segments.Last().Map1.Y-9)<1e-6,
                    "Bent route's original CAD XY geometry was discarded");
            },
            ["vertical_3d_face_accepts_tiny_true_height_and_narrow_clip_without_missing_triangle_error"] = ()=>{
                // Both triangles have exactly ZERO XY projected area but valid
                // nonzero 3D areas. A prior absolute 1e-8 XY/Z threshold
                // wrongly made the CAD renderer reject narrow boundary clips.
                var tinyHeight=new Triangle3(P(2269184.0,2936930.0,10),
                    P(2269184.0,2936930.0,10+1e-9),
                    P(2269234.0,2936930.0,10));
                Check(tinyHeight.Area2D == 0 &&
                    VerticalFaceBuilder.IsValidCadFace(tinyHeight),
                    "Positive 3D area with tiny Z height rejected");
                var narrow=new Triangle3(P(2269184.0,2936930.0,10),
                    P(2269184.0,2936930.0,12),
                    P(2269184.0+1e-9,2936930.0,11));
                Check(narrow.Area2D == 0 &&
                    VerticalFaceBuilder.IsValidCadFace(narrow),
                    "Positive 3D area with very short XY span rejected");
                // Test the second diagonal of a clipped curtain (BC upright).
                var bc=new Triangle3(P(1,2,10),P(2,2,10),
                    P(2,2,12));
                Check(VerticalFaceBuilder.IsValidCadFace(bc),
                    "Valid clipped curtain B-C upright diagonal rejected");
                var invalid=new Triangle3(P(1,2,10),P(1,2,10),P(2,2,10));
                Check(!VerticalFaceBuilder.IsValidCadFace(invalid),
                    "Exactly zero-area face incorrectly accepted");
            },
            ["cad_validation_1681755_faces_retains_tiny_positive"] = ()=>{
                const double x=2500000,y=450000;
                var tiny=new Triangle3(P(x,y,1),P(x+0.000001,y,1),P(x,y+0.000001,1));
                Check(tiny.Area2D>0&&tiny.Area2D<=1e-12,"Fixture does not reproduce old CAD cutoff");
                var normal=new Triangle3(P(x,y),P(x+1,y),P(x,y+1));
                for(int i=0;i<1681755;i++)TinTriangleQuality.ValidateForCad(i==1681754?tiny:normal);
                var surface=new TinSurface("tiny",new[]{tiny},Array.Empty<ValidationIssue>());
                Check(new TinSectionSampler().TryElevation(surface,tiny.A.XY,1e-7,out var z)&&z==1,"Sampler silently lost tiny face");
                Check(tiny.Area2D==Math.Abs((tiny.B.X-tiny.A.X)*(tiny.C.Y-tiny.A.Y))/2,"Area oracle mismatch");
            },
            ["exact_orientation_cancellation_fallback"] = ()=>{
                // Exact determinant is -1; the two 2^54-scale products round to the same double.
                var a=new Vec2(0,0);var b=new Vec2(134217729,134217728);var c=new Vec2(134217728,134217727);
                Check(Vec2.Cross(a,b,c)==0,"Fixture no longer exercises cancellation");
                Check(RobustOrientation.Determinant(a,b,c)==-1,"Exact sign/area lost");
                TinTriangleQuality.ValidateForCad(new Triangle3(P(a.X,a.Y),P(b.X,b.Y),P(c.X,c.Y)));
            },
            ["zero_face_removal_proves_coverage_and_breaklines"] = ()=>{
                var p=new[]{P(0,0),P(1,0),P(2,0),P(1,1)};
                Check(TinTriangleQuality.VerifyZeroAreaRemoval(p,new[]{0,1,2,0,1,3,1,2,3},new[]{0,1,1,2},CancellationToken.None)==1,"Safe zero face not classified");
                bool rejected=false;try{TinTriangleQuality.VerifyZeroAreaRemoval(p,new[]{0,1,2,0,2,3},new[]{0,1},CancellationToken.None);}catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Removing sole owner of site/breakline accepted");
                rejected=false;try{TinTriangleQuality.ValidateForCad(new Triangle3(P(0,0),P(1,1),P(2,2)));}catch(InvalidOperationException){rejected=true;}
                Check(rejected,"True zero face accepted as support TIN");
                rejected=false;try{TinTriangleQuality.CheckedArea(new Triangle3(P(0,0,double.NaN),P(1,0),P(0,1)));}catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Nonfinite face silently skipped");
            },

            ["raw_duplicate_xy_preserves_all_elevations_without_entity_handles"] = ()=>{
                var opt=new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true};
                var input=new SurfaceInputPreparer().PrepareRaw(new[]{
                    P(0,0,10), P(10,0,0), P(10,10,0), P(0,10,0),
                    P(0,0,20)},null,opt);
                Check(!input.HasErrors&&input.VerticalConflictGroups.Count==1,
                    "Raw XY repeat silently dropped high Z without 3D provenance");
                var tin=Build(input,opt);
                Check(tin.VerticalFaces.Count>0&&tin.RequiresSectionReview,
                    "Raw duplicate Z did not create vertical 3D faces / section review gate");
                Check(tin.VerticalConflicts[0].Sources.Any(x=>x.Z==10)&&
                      tin.VerticalConflicts[0].Sources.Any(x=>x.Z==20),
                    "Raw high/low Z were not retained separately");
            },
            ["same_xyz_duplicates_are_counted_but_do_not_block_tin"] = ()=>{
                var options=new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true};
                var model=Model(Entity("a",SourceEntityType.Point,P(0,0,5)),
                    Entity("duplicate",SourceEntityType.Point,P(0,0,5)),
                    Entity("b",SourceEntityType.Point,P(10,0,5)),
                    Entity("c",SourceEntityType.Point,P(0,10,5)));
                var input=new SurfaceInputPreparer().Prepare(model,options);
                Check(!input.HasErrors && input.ConflictReport.SameXYZDuplicates==1,
                    "Same XYZ repeat should be counted without requiring manual deletion");
                var tin=Build(input,options);
                Check(tin.SameXYZDuplicateVertices==1 && !tin.RequiresSectionReview,
                    "Duplicate XYZ count was lost in TIN or wrongly marked as different-Z face");
                Check(tin.VerticalFaces.Count==0,
                    "Same XYZ repeat should not create spurious vertical face");
            },
            ["vertical_xy_auto_tin_preserves_both_original_z_and_renders_3d_faces"] = ()=>{
                var low=Entity("low",SourceEntityType.Point,P(5,5,10));
                var high=Entity("high",SourceEntityType.Point,P(5,5,20));
                var model=Model(
                    Entity("a",SourceEntityType.Point,P(0,0)),
                    Entity("b",SourceEntityType.Point,P(10,0)),
                    Entity("c",SourceEntityType.Point,P(10,10)),
                    Entity("d",SourceEntityType.Point,P(0,10)),
                    low,high);
                var strict=new SurfaceInputPreparer().Prepare(model,new SurfaceBuildOptions());
                Check(strict.HasErrors&&strict.ConflictReport.HasBlockingConflicts,
                    "Strict 2.5D policy unexpectedly lost its explicit error behavior");
                var opt=new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true};
                var prepared=new SurfaceInputPreparer().Prepare(model,opt);
                Check(!prepared.HasErrors,"Auto vertical mode still blocks duplicate XY survey points");
                Check(prepared.VerticalConflictGroups.Count==1,
                    "Expected exactly one retained XY group with two original Z elevations");
                Check(prepared.Sites.Count==5,"CDT support XY sites should be unique");
                var tin=Build(prepared,opt);
                Check(tin.Triangles.Count>0&&tin.VerticalFaces.Count>0,
                    "TIN did not include both regular facets and non-degenerate 3D vertical facets");
                Check(tin.VerticalConflicts.Count==1&&tin.RequiresSectionReview,
                    "Vertical conflict was not retained as an explicit section-review obligation");
                Check(tin.VerticalFaces.Any(t=>
                    t.A.X==5&&t.A.Y==5&&t.B.X==5&&t.B.Y==5&&
                    Math.Abs(t.A.Z-10)<1e-7&&Math.Abs(t.B.Z-20)<1e-7&&
                    t.Area2D<1e-15),
                    "Vertical CAD triangle does not contain the original two elevations");
                Check(low.Vertices[0].Position.Z==10&&high.Vertices[0].Position.Z==20,
                    "Source CAD data changed or discarded");
            },
            ["vertical_xy_contours_and_section_warning_without_point_cleanup"] = ()=>{
                var low=Entity("contourL",SourceEntityType.Contour,P(5,5,10),P(10,5,10));
                var high=Entity("contourH",SourceEntityType.Contour,P(5,5,20),P(5,10,20));
                var model=Model(
                    Entity("a",SourceEntityType.Point,P(0,0)),
                    Entity("b",SourceEntityType.Point,P(10,0)),
                    Entity("c",SourceEntityType.Point,P(10,10)),
                    Entity("d",SourceEntityType.Point,P(0,10)), low, high);
                var opt=new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true};
                var input=new SurfaceInputPreparer().Prepare(model,opt);
                Check(!input.HasErrors,"Doubled contour start forced manual point removal");
                Check(input.RelaxedBreaklineConstraints==2,
                    "Conflicting contour edges were not explicitly counted and relaxed");
                var tin=Build(input,opt);
                Check(tin.VerticalFaces.Count>0 && tin.VerticalConflicts.Count==1,
                    "Contour duplicate Z not represented by vertical CAD faces");
                var flat=new SurfaceInputPreparer().PrepareRaw(
                    new[]{P(0,0,0),P(10,0,0),P(10,10,0),P(0,10,0)},null,new SurfaceBuildOptions());
                var design=Build(flat);
                var line=new SectionLine(1,"MC-01",5,new Vec2(1,0),new Vec2(0,1),
                    new[]{new SectionSpan(0,10)});
                var section=new TinSectionSampler().BuildProfile(line,tin,design);
                Check(section.HasData&&section.Warnings.Any(w=>w.Contains("MẶT ĐỨNG NHIỀU Z:")),
                    "Section crossing double-Z position has no review warning");
                Check(low.Vertices[0].Position.Z==10&&high.Vertices[0].Position.Z==20,
                    "Contour source elevations were modified");
            },
            ["hsm0_1297_generated_breakline_conflicts_build_in_one_pass"] = ()=>{
                const int count=1297;
                var items=new List<SourceEntity>{
                    Entity("corner0",SourceEntityType.Point,P(0,0,1)),
                    Entity("corner1",SourceEntityType.Point,P(count*3+3,0,1)),
                    Entity("corner2",SourceEntityType.Point,P(count*3+3,10,1)),
                    Entity("corner3",SourceEntityType.Point,P(0,10,1))
                };
                for(int i=0;i<count;i++){
                    double x=2+i*3;
                    items.Add(Entity("h"+i,SourceEntityType.Contour,
                        P(x-.4,5,10),P(x+.4,5,10)));
                    items.Add(Entity("v"+i,SourceEntityType.Contour,
                        P(x,4.6,20),P(x,5.4,20)));
                }
                var model=Model(items.ToArray());
                var options=new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true};
                var input=new SurfaceInputPreparer().Prepare(model,options);
                Check(!input.HasErrors,"Synthetic 1297-generated-crossing contour model still blocks TIN");
                Check(input.VerticalConflictGroups.Count==count,
                    "Generated crossing group count mismatch in 1297-event geometry");
                Check(input.RelaxedBreaklineConstraints==2*count,
                    "Auto normalization should relax only the 2594 contradictory contour segments");
                var tin=Build(input,options);
                Check(tin.Triangles.Count>0 && tin.VerticalFaces.Count>=count &&
                    tin.RelaxedBreaklineConstraints==2*count && tin.RequiresSectionReview,
                    "TIN build failed to retain 1297 vertical conflict locations");
                Check(model.Entities.Count==4+2*count &&
                    model.Entities[4].Vertices[0].Position.Z==10 &&
                    model.Entities[5].Vertices[0].Position.Z==20,
                    "Synthetic conflict processing deleted or edited source CAD objects");
            },
            ["hsm0_generated_crossing_different_z_builds_without_manual_deletion"] = ()=>{
                var a=Entity("crossA",SourceEntityType.Contour,P(0,5,10),P(10,5,10));
                var b=Entity("crossB",SourceEntityType.Contour,P(5,0,20),P(5,10,20));
                var model=Model(Entity("p0",SourceEntityType.Point,P(0,0,1)),
                    Entity("p1",SourceEntityType.Point,P(10,0,1)),
                    Entity("p2",SourceEntityType.Point,P(10,10,1)),
                    Entity("p3",SourceEntityType.Point,P(0,10,1)),a,b);
                var opt=new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true};
                var prepared=new SurfaceInputPreparer().Prepare(model,opt);
                Check(!prepared.HasErrors,
                    "Generated crossing from two incompatible contours still blocks the automatic workflow");
                Check(prepared.VerticalConflictGroups.Count==1,
                    "Generated XY crossing is not carried into vertical-face source groups");
                Check(prepared.RelaxedBreaklineConstraints==2 && prepared.Breaklines.Count==0,
                    "Crossing contour constraints were not safely isolated from planar CDT");
                var tin=Build(prepared,opt);
                Check(tin.VerticalFaces.Count>0 && tin.RequiresSectionReview,
                    "Generated crossing failed to create vertical faces and mandatory section review");
                Check(tin.VerticalConflicts[0].Sources.Any(x=>Math.Abs(x.Z-10)<1e-8) &&
                      tin.VerticalConflicts[0].Sources.Any(x=>Math.Abs(x.Z-20)<1e-8),
                    "Breakline-crossing original Z values were lost");
                Check(a.Vertices[0].Original.Z==10&&b.Vertices[0].Original.Z==20 &&
                    model.Entities.Count==6,"Original CAD contours were changed or deleted");
            },
            ["hsm0_point_on_contour_different_z_builds_without_manual_deletion"] = ()=>{
                var contour=Entity("contour",SourceEntityType.Contour,P(0,5,10),P(10,5,10));
                var model=Model(
                    Entity("p0",SourceEntityType.Point,P(0,0,1)),
                    Entity("p1",SourceEntityType.Point,P(10,0,1)),
                    Entity("p2",SourceEntityType.Point,P(10,10,1)),
                    Entity("p3",SourceEntityType.Point,P(0,10,1)),
                    Entity("high",SourceEntityType.Point,P(5,5,20)),contour);
                var opt=new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true};
                var prepared=new SurfaceInputPreparer().Prepare(model,opt);
                Check(!prepared.HasErrors && prepared.RelaxedBreaklineConstraints==1,
                    "Point above contour still requires manual deletion");
                var tin=Build(prepared,opt);
                Check(tin.VerticalFaces.Count>0 && tin.RequiresSectionReview,
                    "Point above contour did not create vertical 3D face");
                Check(contour.Vertices[0].Position.Z==10,
                    "CAD contour was altered to resolve point-on-contour conflict");
            },
            ["hsm0_overlapping_contours_different_z_builds_without_manual_deletion"] = ()=>{
                var lo=Entity("lo",SourceEntityType.Contour,P(0,5,10),P(10,5,10));
                var hi=Entity("hi",SourceEntityType.Contour,P(3,5,20),P(7,5,20));
                var model=Model(Entity("p0",SourceEntityType.Point,P(0,0,1)),
                    Entity("p1",SourceEntityType.Point,P(10,0,1)),
                    Entity("p2",SourceEntityType.Point,P(10,10,1)),
                    Entity("p3",SourceEntityType.Point,P(0,10,1)),lo,hi);
                var opt=new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true};
                var prepared=new SurfaceInputPreparer().Prepare(model,opt);
                Check(!prepared.HasErrors && prepared.VerticalConflictGroups.Count>=2,
                    "Overlapping contours at different Z still block TIN");
                var tin=Build(prepared,opt);
                Check(tin.VerticalFaces.Count>0 && tin.RequiresSectionReview &&
                    tin.RelaxedBreaklineConstraints>=2,
                    "Overlapping contours did not retain vertical faces and relaxed-edge count");
                Check(hi.Vertices[0].Position.Z==20,"Source contours were modified");
            },
            ["mean_z_preview_all_entities_and_original_unchanged"] = ()=>{
                var contour=Entity("contour",SourceEntityType.Contour,P(0,0,0),P(10,0,0));
                var poly=Entity("p3",SourceEntityType.Polyline3d,P(0,0,94.862),P(10,10,30));
                var l=Entity("line",SourceEntityType.Line,P(0,0,20),P(0,10,20));
                var model=Model(contour,poly,l);
                var result=MeanZPreviewPreparer.Prepare(model);
                Check(result.OriginalVertexCount==6,"Preview should include EVERY enabled input vertex");
                Check(result.Prepared.Sites.Count==4,"Three coincident corner points merge into ONE XY");
                Check(result.DuplicateGroupCount==1&&result.MergedVertexCount==2,
                    "Preview dedup groups not counted");
                Check(result.Conflicts.Count==1,"One conflicting duplicate XY group");
                Check(Math.Abs(result.Conflicts[0].MeanZ-(94.862+20)/3)<1e-9,
                    "Z average must use every observation (not just min/max)");
                Check(Math.Abs(result.Conflicts[0].DeltaZ-94.862)<1e-9,
                    "Largest real mine ΔZ must be preserved in report");
                Check(result.IgnoredBreaklineSegments==3&&result.Prepared.Breaklines.Count==0,
                    "Conflicting breaklines MUST NOT be sent into experimental CDT");
                Check(contour.Vertices[0].Position.Z==0&&poly.Vertices[0].Position.Z==94.862&&
                    l.Vertices[0].Position.Z==20,"Experimental average edited source CAD vertices");
                var tin=Build(result.Prepared);
                Check(tin.Triangles.Count>0,"Experimental averaged points should triangulate");
                Check(!result.ForOfficialVolumes,"Experimental TIN cannot be official");
            },
            ["mean_z_preview_same_bucket_distinct_xy_and_neighbour_join"] = ()=>{
                // Distinct points within the same 1µm hash bucket can still
                // be farther than the Euclidean XY tolerance. The optional
                // secondary bucket must not omit either site.
                var a=Entity("a",SourceEntityType.Point,P(5000.0000001,5000.0000001,1));
                var b=Entity("b",SourceEntityType.Point,P(5000.0000009,5000.0000009,2));
                var c=Entity("c",SourceEntityType.Point,P(5000.00000095,5000.00000095,3));
                var d=Entity("d",SourceEntityType.Point,P(5000.0000015,5000.0000015,4));
                var m=Model(a,b,c,d,Entity("e",SourceEntityType.Point,P(5001,5000,0)),
                    Entity("f",SourceEntityType.Point,P(5000,5001,0)));
                var r=MeanZPreviewPreparer.Prepare(m);
                Check(r.Prepared.Sites.Count==4,"Distinct XY in one bucket accidentally merged");
                Check(r.Conflicts.Count==1 && r.Conflicts[0].VertexCount==3,
                    "Neighbor cell duplicate sources omitted");
                Check(Math.Abs(r.Conflicts[0].MeanZ-3)<1e-9,
                    "Mean is not arithmetic mean of the matched 3 source Zs");
                Check(r.Conflicts[0].Members.Count==3,
                    "Lazy member materialization lost first source provenance");
                Check(r.Prepared.Sites.Any(p=>p.Z==1),
                    "First distinct XY site was dropped in hash collision");
            },
            ["mean_z_preview_disabled_inputs_and_identical_z"] = ()=>{
                var a=Entity("a",SourceEntityType.Point,P(0,0,10));
                var b=Entity("b",SourceEntityType.Point,P(0,0,10));
                var disabled=Entity("disabled",SourceEntityType.Point,P(0,0,900));
                disabled.SetEnabled(false);
                var model=Model(a,b,disabled,Entity("c",SourceEntityType.Point,P(10,0,10)),
                    Entity("d",SourceEntityType.Point,P(0,10,10)));
                var result=MeanZPreviewPreparer.Prepare(model);
                Check(result.OriginalVertexCount==4&&result.Prepared.Sites.Count==3,
                    "Disabled entities must not be used");
                Check(result.DuplicateGroupCount==1&&result.Conflicts.Count==0,
                    "Identical XYZ duplicates are not Z conflicts");
                Check(result.Prepared.Sites.Any(p=>p.X==0&&p.Y==0&&p.Z==10),
                    "Identical points should retain source elevation");
                Check(result.Prepared.Breaklines.Count==0,"Experimental preview does not add breaklines");
                var tin=Build(result.Prepared);
                Check(tin.Triangles.Count==1,"Triangle of three usable XY sites");
            },
            ["mean_z_preview_strict_mode_still_rejects_conflicts"] = ()=>{
                var data=Model(Entity("a",SourceEntityType.Point,P(0,0,0)),
                    Entity("b",SourceEntityType.Point,P(0,0,94.862)),
                    Entity("c",SourceEntityType.Point,P(10,0,0)),
                    Entity("d",SourceEntityType.Point,P(0,10,0)));
                var strict=new SurfaceInputPreparer().Prepare(data,new SurfaceBuildOptions());
                Check(strict.HasErrors&&strict.ConflictReport.HasBlockingConflicts,
                    "Official strict mode must still reject conflicting Z");
                var experimental=MeanZPreviewPreparer.Prepare(data);
                Check(!experimental.Prepared.HasErrors&&experimental.Conflicts.Count==1,
                    "Opt-in experimental mode must be independent from strict");
                Check(Math.Abs(experimental.Prepared.Sites.First(p=>p.X==0&&p.Y==0).Z-47.431)<1e-9,
                    "Z middle between 0 and 94.862");
            },
            ["mean_z_preview_threshold_and_review_provenance"] = ()=>{
                foreach(double delta in new[]{0.001,3.0,3.001,94.862}) {
                    var m=Model(Entity("a",SourceEntityType.Point,P(2500000,400000)),
                        Entity("b",SourceEntityType.Polyline3d,P(2500000,400000,delta),P(2500010,400000)),
                        Entity("c",SourceEntityType.Point,P(2500000,400010)));
                    var r=MeanZPreviewPreparer.Prepare(m);
                    Check(r.HasBlockingConflicts==(delta>3),"Strict >3 threshold violated");
                    Check(Math.Abs(r.Conflicts.Single().MeanZ-delta/2)<1e-10,"Wrong mean");
                    var report=r.ToConflictReport(m);
                    Check(report.Groups.Single().Sources.Count==2&&report.Groups[0].Sources.All(x=>!x.Generated&&x.VertexIndex==0),"Lost original provenance");
                    Check(new SurfaceInputPreparer().Prepare(m,new SurfaceBuildOptions()).HasErrors,"Preview relaxed official tolerance");
                }
            },
            ["mean_z_preview_region_mesh_area"] = ()=>{
                var m=Model(Entity("a",SourceEntityType.Point,P(0,0,2)),Entity("b",SourceEntityType.Point,P(10,0,2)),
                    Entity("c",SourceEntityType.Point,P(10,10,2)),Entity("d",SourceEntityType.Point,P(0,10,2)));
                var ring=new[]{new Vec2(0,0),new Vec2(10,0),new Vec2(10,4),new Vec2(4,4),new Vec2(4,10),new Vec2(0,10)};
                var r=MeanZPreviewPreparer.Prepare(m,tinRegion:ring);
                var mesh=new TiledConformingTinBuilder().BuildPrepared("preview",r.Prepared,
                    new SurfaceBuildOptions{ClipBoundary=ring},r.OriginalVertexCount,0,0);
                try {
                    Check(Math.Abs(mesh.Surface.Triangles.Sum(t=>t.Area2D)-64)<1e-8,"Preview escaped concave boundary");
                    Check(mesh.Surface.Triangles.All(t=>new[]{t.A,t.B,t.C}.All(v=>SurfaceRegionClipper.ContainsInclusive(v.XY,ring)&&Math.Abs(v.Z-2)<1e-10)),"Bad clipped vertices/Z");
                } finally {(mesh.Surface.Triangles as IDisposable)?.Dispose();}
            },
            ["regional_conflict_original_index_and_synthetic_guard"] = ()=>{
                var poly=Entity("poly",SourceEntityType.Polyline3d,P(-10,0),P(0,0),P(10,0));
                var m=Model(poly,Entity("point",SourceEntityType.Point,P(0,0,5)),Entity("clip",SourceEntityType.Point,P(-1,0,8)));
                var ring=new[]{new Vec2(0,-2),new Vec2(8,-2),new Vec2(8,2),new Vec2(0,2)};
                var clipped=SurfaceRegionClipper.Clip(m,ring);
                var report=new ConflictScanner().Scan(clipped,new SurfaceBuildOptions());
                var atZero=report.Groups.Single(g=>Math.Abs(g.Position.X)<1e-9);
                Check(atZero.Sources.Any(x=>x.EntityId=="poly"&&!x.Generated&&x.VertexIndex==1&&x.Type==SourceEntityType.Polyline3d),"Clipping renumbered original vertex");
                Check(clipped.Entities.Where(e=>e.Handle=="poly").Any(e=>e.OriginVertexIndices.Any(i=>i<0)),"Synthetic clipping point was mapped to real vertex");
                m.SetVertexEnabled("poly",1,false);
                Check(poly.Vertices[0].IsEnabled&&poly.Vertices[2].IsEnabled&&poly.IsEnabled,"Disabled whole polyline");
                m.SetVertexEnabled("poly",1,true);
                Check(poly.ActiveVertices().Count()==3,"Restore failed");
                bool rejected=false;try{m.OverrideVertexZ("poly",1,double.NaN);}catch(ArgumentOutOfRangeException){rejected=true;}
                Check(rejected&&poly.Vertices[1].Position.Z==0,"Invalid edit mutated source");
            },
            ["mean_z_review_with_less_than_three_sites"] = ()=>{
                var r=MeanZPreviewPreparer.Prepare(Model(Entity("a",SourceEntityType.Point,P(0,0)),Entity("b",SourceEntityType.Point,P(0,0,20))));
                Check(r.HasBlockingConflicts&&r.ToConflictReport(Model()).Groups.Count==1,"Cannot review sparse invalid input");
            },
            ["model_edits_restore_audit_and_snapshot"] = ()=>{
                var m=Model(Entity("a",SourceEntityType.Polyline3d,P(0,0,8),P(10,0,4)));
                var original=SourceFingerprint.Compute(m.Entities[0]);
                m.SetVertexEnabled("a",0,false);m.OverrideVertexZ("a",1,7);m.RestoreVertex("a",0);
                Check(m.EditHistory.Count==3&&m.EditHistory[1].OldZ==4&&m.EditHistory[1].NewZ==7,"Missing audit values");
                Check(m.Entities[0].Vertices[0].IsEnabled&&m.Entities[0].Vertices[1].Position.Z==7,"Restore affected another vertex");
                Check(SourceFingerprint.Compute(m.Entities[0])==original,"Edits changed original fingerprint");
                Check(SourceFingerprint.Compute(Entity("a",SourceEntityType.Polyline3d,P(0,0,9),P(10,0,4)))!=original,"Changed geometry accepted");
                var modelHash=SourceFingerprint.ComputeModel(m);
                Check(modelHash==SourceFingerprint.ComputeModel(m),"Full-source hash is nondeterministic");
                var another=Model(Entity("a",SourceEntityType.Polyline3d,P(0,0,8),P(10,0,4)));
                Check(modelHash==SourceFingerprint.ComputeModel(another),
                    "Model-side overrides/exclusions were incorrectly included in original CAD hash");
                another.Entities.Add(Entity("b",SourceEntityType.Point,P(11,12,13)));
                Check(modelHash!=SourceFingerprint.ComputeModel(another),
                    "New unedited CAD object did not invalidate the source fingerprint");
                var reversed=Model(Entity("b",SourceEntityType.Point,P(11,12,13)),
                    Entity("a",SourceEntityType.Polyline3d,P(0,0,8),P(10,0,4)));
                Check(SourceFingerprint.ComputeModel(another)==SourceFingerprint.ComputeModel(reversed),
                    "Source fingerprint depends on entity selection/enumeration order");
                reversed.Entities[0].Vertices[0].OverrideZ(98);
                Check(SourceFingerprint.ComputeModel(another)==SourceFingerprint.ComputeModel(reversed),
                    "Model-side Z override incorrectly changes the source integrity fingerprint");
                var changedOriginal=Model(Entity("b",SourceEntityType.Point,P(11,12,14)),
                    Entity("a",SourceEntityType.Polyline3d,P(0,0,8),P(10,0,4)));
                Check(SourceFingerprint.ComputeModel(another)!=SourceFingerprint.ComputeModel(changedOriginal),
                    "Changed original CAD Z was not detected by the source integrity fingerprint");
                bool invalid=false;try{m.RestoreVertices(new[]{new KeyValuePair<string,int>("a",1),new KeyValuePair<string,int>("a",9)});}catch(ArgumentOutOfRangeException){invalid=true;}
                Check(invalid&&m.Entities[0].Vertices[1].Position.Z==7,"Bulk restore partially applied invalid selection");
                m.RestoreVertices(new[]{new KeyValuePair<string,int>("a",0)});
                Check(m.Entities[0].Vertices[1].Position.Z==7,"Bulk restore reset unrelated edit");
                var snapshot=new MiningVolume2023.Services.ProjectSnapshot {SavedUtc=DateTime.UtcNow,
                    HadProfiles=true,HadVolumeResult=true,HadEditableSectionCad=true,
                    Existing=new MiningVolume2023.Services.ModelSnapshot {
                        EditHistory=m.EditHistory,FullSourceFingerprint=modelHash}};
                var decoded=MiningVolume2023.Services.ProjectSnapshotCodec.Decode(MiningVolume2023.Services.ProjectSnapshotCodec.Encode(snapshot));
                Check(decoded.Existing.EditHistory.Count==4&&decoded.Existing.EditHistory[1].NewZ==7&&decoded.Existing.EditHistory[1].VertexIndex==1,"Audit snapshot roundtrip lost data");
                Check(decoded.HadEditableSectionCad && decoded.HadProfiles && decoded.HadVolumeResult,
                    "DWG snapshot silently lost the CAD section preservation gate");
                Check(decoded.Existing.FullSourceFingerprint==modelHash,
                    "DWG snapshot silently lost the full HT/TK model source fingerprint");
                var legacy=new MiningVolume2023.Services.ProjectSnapshot {FormatVersion=2,HadProfiles=true,SavedUtc=new DateTime(2026,10,12,0,0,0,DateTimeKind.Utc)};
                var legacyRoundtrip=MiningVolume2023.Services.ProjectSnapshotCodec.Decode(MiningVolume2023.Services.ProjectSnapshotCodec.Encode(legacy));
                Check(!legacyRoundtrip.HadEditableSectionCad,
                    "Backward-compatible project without CAD edit marker must remain readable");
            },
            ["square_area_no_holes"] = ()=> {var t=Build(Prepare(Corners()));Check(t.Triangles.Count==2&&Math.Abs(t.Triangles.Sum(x=>x.Area2D)-100)<1e-9,"Square area");},
            ["grid_system_rotated_matches_no_rotation"] = ()=>{
                var existing=new TinSurface("tn",new[]{new Triangle3(P(0,0,2),P(20,0,2),P(20,20,2)),
                    new Triangle3(P(0,0,2),P(20,20,2),P(0,20,2))},Array.Empty<ValidationIssue>());
                var design=new TinSurface("tk",new[]{new Triangle3(P(0,0),P(20,0),P(20,20)),
                    new Triangle3(P(0,0),P(20,20),P(0,20))},Array.Empty<ValidationIssue>());
                var boundary=new[]{new Vec2(0,0),new Vec2(20,0),new Vec2(20,20),new Vec2(0,20)};
                var grid=new GridVolumeCalculator();
                var straight=grid.Calculate(existing,design,new GridVolumeOptions{Boundary=boundary,SizeX=20,SizeY=20});
                var rotated=grid.Calculate(existing,design,new GridVolumeOptions{Boundary=boundary,SizeX=20,SizeY=20,RotationDegrees=45});
                Check(straight.Cells.Count==1&&rotated.Cells.Count>1,"Grid rotation did not produce multiple partial cells");
                Check(Math.Abs(straight.TotalCutVolume-800)<1e-7&&Math.Abs(rotated.TotalCutVolume-800)<1e-7,
                    "Rotating grid must not change total whole-area volume");
                Check(straight.IsComplete&&rotated.IsComplete,"Rotated grid lost TIN coverage");
            },
            ["grid_system_large_survey_xy"] = ()=>{
                const double x=2300000,y=400000;
                var existing=new TinSurface("tn",new[]{new Triangle3(P(x,y,2),P(x+20,y,2),P(x+20,y+20,2)),
                    new Triangle3(P(x,y,2),P(x+20,y+20,2),P(x,y+20,2))},Array.Empty<ValidationIssue>());
                var design=new TinSurface("tk",new[]{new Triangle3(P(x,y),P(x+20,y),P(x+20,y+20)),
                    new Triangle3(P(x,y),P(x+20,y+20),P(x,y+20))},Array.Empty<ValidationIssue>());
                var ring=new[]{new Vec2(x,y),new Vec2(x+20,y),new Vec2(x+20,y+20),new Vec2(x,y+20)};
                var v=new GridVolumeCalculator().Calculate(existing,design,new GridVolumeOptions{
                    Boundary=ring,Origin=new Vec2(x,y),SizeX=20,SizeY=20});
                Check(v.IsComplete&&Math.Abs(v.TotalCutVolume-800)<1e-7,
                    "Large survey coordinates changed grid volume");
            },
            ["grid_system_no_data_flag"] = ()=>{
                var existing=new TinSurface("tn",new[]{new Triangle3(P(0,0,2),P(10,0,2),P(10,20,2)),
                    new Triangle3(P(0,0,2),P(10,20,2),P(0,20,2))},Array.Empty<ValidationIssue>());
                var design=new TinSurface("tk",new[]{new Triangle3(P(0,0),P(20,0),P(20,20)),
                    new Triangle3(P(0,0),P(20,20),P(0,20))},Array.Empty<ValidationIssue>());
                var ring=new[]{new Vec2(0,0),new Vec2(20,0),new Vec2(20,20),new Vec2(0,20)};
                var v=new GridVolumeCalculator().Calculate(existing,design,new GridVolumeOptions{Boundary=ring,SizeX=20,SizeY=20});
                Check(v.Cells.Count==1&&!v.IsComplete&&Math.Abs(v.TotalNoDataArea-200)<1e-7,
                    "Grid system hid missing TIN area");
            },
            ["mineral_volume_multiple_seams_and_stripping"] = ()=>{
                var band=new LevelBand(1,0,10);
                var excavation=new VolumeResult(new[]{band},
                    new[]{new SectionIntervalVolume{StartSection="MC-01",EndSection="MC-02",
                        Distance=10,CutVolume=40,FillVolume=0}},
                    Array.Empty<LevelVolumeSummary>(),
                    new[]{new SectionBandAreas("MC-01",0,new[]{new BandArea(band,10,0)}),
                        new SectionBandAreas("MC-02",10,new[]{new BandArea(band,10,0)})},
                    Array.Empty<string>());
                var seams=new[]{new MineralSeamInput{Id="A",Name="Vỉa 8",DensityTonPerCubicMetre=1.4},
                    new MineralSeamInput{Id="B",Name="Vỉa 9",DensityTonPerCubicMetre=2.5}};
                var data=new[]{
                    new MineralSectionInput{BodyId="A",SectionName="MC-01",AreaSquareMetres=2},
                    new MineralSectionInput{BodyId="A",SectionName="MC-02",AreaSquareMetres=2},
                    new MineralSectionInput{BodyId="B",SectionName="MC-01",AreaSquareMetres=1},
                    new MineralSectionInput{BodyId="B",SectionName="MC-02",AbsentConfirmed=true}
                };
                var result=MineralVolumeCalculator.Calculate(excavation,seams,data);
                Check(Math.Abs(result.Bodies[0].VolumeCubicMetres-20)<1e-9,
                    "Seam 8 volume 20m3");
                Check(Math.Abs(result.Bodies[1].VolumeCubicMetres-10.0/3)<1e-9,
                    "Pyramidal vanishing seam 9 volume 3.3333m3");
                Check(Math.Abs(result.Bodies[0].MassTonnes-28)<1e-9,
                    "Seam 8 20m3 times 1.4t/m3");
                Check(Math.Abs(result.WasteCubicMetres-(40-20-10.0/3))<1e-9,
                    "Waste should equal excavation minus mineral");
                Check(result.StrippingRatioCubicMetresPerTonne.HasValue&&result.StrippingRatioCubicMetresPerCubicMetre.HasValue,
                    "Stripping ratios must be reported distinctly in m3/t and m3/m3");
            },
            ["mineral_volume_missing_section_must_not_be_zero"] = ()=>{
                var band=new LevelBand(1,0,10);
                var excavation=new VolumeResult(new[]{band},
                    new[]{new SectionIntervalVolume{StartSection="S1",EndSection="S2",
                        Distance=20,CutVolume=300}},
                    Array.Empty<LevelVolumeSummary>(),
                    new[]{new SectionBandAreas("S1",0,new[]{new BandArea(band,15,0)}),
                        new SectionBandAreas("S2",20,new[]{new BandArea(band,15,0)})},
                    Array.Empty<string>());
                var seam=new[]{new MineralSeamInput{Id="A",Name="Vỉa A",DensityTonPerCubicMetre=1.3}};
                bool blocked=false;
                try{
                    MineralVolumeCalculator.Calculate(excavation,seam,
                        new[]{new MineralSectionInput{BodyId="A",SectionName="S1",AreaSquareMetres=2}});
                }catch(InvalidOperationException){blocked=true;}
                Check(blocked,"Missing section cannot silently count zero");
                var confirmed=new[]{new MineralSectionInput{BodyId="A",SectionName="S1",AreaSquareMetres=2},
                    new MineralSectionInput{BodyId="A",SectionName="S2",AbsentConfirmed=true}};
                var result=MineralVolumeCalculator.Calculate(excavation,seam,confirmed);
                Check(Math.Abs(result.TotalMineralCubicMetres-40.0/3)<1e-9,
                    "Confirmed absent end must use pyramid, expected 13.3333m3");
            },
            ["mineral_volume_rejects_excess_seam_and_bad_density"] = ()=>{
                var band=new LevelBand(1,0,10);
                var excavation=new VolumeResult(new[]{band},
                    new[]{new SectionIntervalVolume{StartSection="S1",EndSection="S2",
                        Distance=10,CutVolume=10}},
                    Array.Empty<LevelVolumeSummary>(),
                    new[]{new SectionBandAreas("S1",0,new[]{new BandArea(band,10,0)}),
                        new SectionBandAreas("S2",10,new[]{new BandArea(band,10,0)})},
                    Array.Empty<string>());
                var seam=new[]{new MineralSeamInput{Id="A",Name="Vỉa A",DensityTonPerCubicMetre=1.3}};
                var areas=new[]{new MineralSectionInput{BodyId="A",SectionName="S1",AreaSquareMetres=2},
                    new MineralSectionInput{BodyId="A",SectionName="S2",AreaSquareMetres=2}};
                bool overcut=false;
                try{MineralVolumeCalculator.Calculate(excavation,seam,areas);}
                catch(InvalidOperationException){overcut=true;}
                Check(overcut,"Mineral volume exceeding total excavation must block");
                seam[0].DensityTonPerCubicMetre=0;
                bool invalid=false;
                try{MineralVolumeCalculator.Calculate(excavation,seam,areas);}
                catch(InvalidOperationException){invalid=true;}
                Check(invalid,"Density zero must not silently convert to 0 tonnes");
            },
            ["mineral_section_polygon_inside_cut"] = ()=>{
                var line=new SectionLine(1,"MC-01",0,new Vec2(1,0),new Vec2(0,1),
                    new[]{new SectionSpan(0,20)});
                var seg=new SectionProfileSegment(0,20,new Vec2(0,0),new Vec2(20,0),
                    10,10,0,0);
                var profile=new SectionProfile(line,new[]{seg},200,0,Array.Empty<string>());
                var region=new[]{new Vec2(5,2),new Vec2(15,2),new Vec2(15,7),new Vec2(5,7)};
                double area=MineralSectionPolygonCalculator.ValidateAndMeasure(profile,region);
                Check(Math.Abs(area-50)<1e-9,"Mineral 10x5 must be 50m2");
            },
            ["mineral_section_polygon_outside_cut_blocks"] = ()=>{
                var line=new SectionLine(1,"MC-01",0,new Vec2(1,0),new Vec2(0,1),
                    new[]{new SectionSpan(0,20)});
                var seg=new SectionProfileSegment(0,20,new Vec2(0,0),new Vec2(20,0),
                    10,10,0,0);
                var profile=new SectionProfile(line,new[]{seg},200,0,Array.Empty<string>());
                var region=new[]{new Vec2(5,2),new Vec2(15,2),new Vec2(15,11),new Vec2(5,11)};
                bool rejected=false;
                try {MineralSectionPolygonCalculator.ValidateAndMeasure(profile,region);}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Mineral region outside excavation must reject");
            },
            ["mineral_section_polygon_mixed_zero_crossing"] = ()=>{
                var line=new SectionLine(1,"MC-01",0,new Vec2(1,0),new Vec2(0,1),
                    new[]{new SectionSpan(0,20)});
                var seg=new SectionProfileSegment(0,20,new Vec2(0,0),new Vec2(20,0),
                    -10,10,0,0);
                var profile=new SectionProfile(line,new[]{seg},50,50,Array.Empty<string>());
                var region=new[]{new Vec2(15,0),new Vec2(20,0),new Vec2(20,10),new Vec2(15,5)};
                double area=MineralSectionPolygonCalculator.ValidateAndMeasure(profile,region);
                Check(Math.Abs(area-37.5)<1e-8,"Cut-sided trapezoid expected 37.5m2");
            },
            ["mineral_section_overlap_detected"] = ()=>{
                var a=new[]{new Vec2(0,0),new Vec2(10,0),new Vec2(10,10),new Vec2(0,10)};
                var b=new[]{new Vec2(5,5),new Vec2(15,5),new Vec2(15,15),new Vec2(5,15)};
                var c=new[]{new Vec2(15,15),new Vec2(25,15),new Vec2(25,25),new Vec2(15,25)};
                Check(Math.Abs(MineralSectionPolygonCalculator.OverlapArea(a,b)-25)<1e-8,
                    "Overlap 5x5 must report 25m2");
                Check(MineralSectionPolygonCalculator.OverlapArea(a,c)<1e-8,
                    "Unrelated regions falsely overlap");
            },
            ["grid_exact_constant_cut"] = ()=>{
                var a=new[]{new Triangle3(P(0,0,2),P(20,0,2),P(20,20,2)),
                    new Triangle3(P(0,0,2),P(20,20,2),P(0,20,2))};
                var b=new[]{new Triangle3(P(0,0),P(20,0),P(20,20)),
                    new Triangle3(P(0,0),P(20,20),P(0,20))};
                var cell=new[]{new Vec2(0,0),new Vec2(20,0),new Vec2(20,20),new Vec2(0,20)};
                var v=ExactGridCellCalculator.Calculate(a,b,cell);
                Check(Math.Abs(v.CutVolume-800)<1e-7&&v.FillVolume<1e-7&&v.IsComplete,"20x20 +2m expected 800m3");
            },
            ["grid_exact_mixed_cut_fill"] = ()=>{
                var a=new[]{new Triangle3(P(0,0,-10),P(20,0,10),P(20,20,10)),
                    new Triangle3(P(0,0,-10),P(20,20,10),P(0,20,-10))};
                var b=new[]{new Triangle3(P(0,0),P(20,0),P(20,20)),
                    new Triangle3(P(0,0),P(20,20),P(0,20))};
                var cell=new[]{new Vec2(0,0),new Vec2(20,0),new Vec2(20,20),new Vec2(0,20)};
                var v=ExactGridCellCalculator.Calculate(a,b,cell);
                Check(Math.Abs(v.CutVolume-1000)<1e-7&&Math.Abs(v.FillVolume-1000)<1e-7,
                    "Mixed cell must separate excavation and fill, 1000m3 each");
            },
            ["grid_exact_partial_boundary"] = ()=>{
                var a=new[]{new Triangle3(P(0,0,2),P(20,0,2),P(20,20,2)),
                    new Triangle3(P(0,0,2),P(20,20,2),P(0,20,2))};
                var b=new[]{new Triangle3(P(0,0),P(20,0),P(20,20)),
                    new Triangle3(P(0,0),P(20,20),P(0,20))};
                var cell=new[]{new Vec2(0,0),new Vec2(20,0),new Vec2(20,20),new Vec2(0,20)};
                var boundary=new[]{new Vec2(0,0),new Vec2(17.5,0),new Vec2(17.5,10),new Vec2(0,10)};
                var v=ExactGridCellCalculator.Calculate(a,b,cell,boundary);
                Check(Math.Abs(v.CutVolume-350)<1e-7&&Math.Abs(v.CalculationArea-175)<1e-7&&v.IsComplete,
                    "Partial 175m2 x 2m must yield 350m3");
            },
            ["grid_exact_concave_region"] = ()=>{
                var a=new[]{new Triangle3(P(0,0,2),P(20,0,2),P(20,20,2)),
                    new Triangle3(P(0,0,2),P(20,20,2),P(0,20,2))};
                var b=new[]{new Triangle3(P(0,0),P(20,0),P(20,20)),
                    new Triangle3(P(0,0),P(20,20),P(0,20))};
                var cell=new[]{new Vec2(0,0),new Vec2(20,0),new Vec2(20,20),new Vec2(0,20)};
                var boundary=new[]{new Vec2(0,0),new Vec2(20,0),new Vec2(20,10),
                    new Vec2(10,10),new Vec2(10,20),new Vec2(0,20)};
                var v=ExactGridCellCalculator.Calculate(a,b,cell,boundary);
                Check(Math.Abs(v.CutVolume-600)<1e-7&&Math.Abs(v.CalculationArea-300)<1e-7,
                    "Concave L region expected 300m2/600m3");
            },
            ["grid_exact_boundary_with_hole"] = ()=>{
                var a=new[]{new Triangle3(P(0,0,2),P(20,0,2),P(20,20,2)),
                    new Triangle3(P(0,0,2),P(20,20,2),P(0,20,2))};
                var b=new[]{new Triangle3(P(0,0),P(20,0),P(20,20)),
                    new Triangle3(P(0,0),P(20,20),P(0,20))};
                var cell=new[]{new Vec2(0,0),new Vec2(20,0),new Vec2(20,20),new Vec2(0,20)};
                var hole=(IReadOnlyList<Vec2>)new[]{new Vec2(5,5),new Vec2(10,5),new Vec2(10,10),new Vec2(5,10)};
                var v=ExactGridCellCalculator.Calculate(a,b,cell,cell,new[]{hole});
                Check(Math.Abs(v.CalculationArea-375)<1e-7&&Math.Abs(v.CutVolume-750)<1e-7,
                    "20x20 less 5x5 hole -> 750m3");
            },
            ["grid_exact_reports_no_data_not_zero"] = ()=>{
                var a=new[]{new Triangle3(P(0,0,2),P(10,0,2),P(10,20,2)),
                    new Triangle3(P(0,0,2),P(10,20,2),P(0,20,2))};
                var b=new[]{new Triangle3(P(0,0),P(20,0),P(20,20)),
                    new Triangle3(P(0,0),P(20,20),P(0,20))};
                var cell=new[]{new Vec2(0,0),new Vec2(20,0),new Vec2(20,20),new Vec2(0,20)};
                var v=ExactGridCellCalculator.Calculate(a,b,cell);
                Check(Math.Abs(v.NoDataArea-200)<1e-7&&Math.Abs(v.CutVolume-400)<1e-7&&!v.IsComplete,
                    "Missing TIN data must not be silently treated as fill=0");
            },

            ["grid_empty_report_is_not_accepted"] = ()=>{
                var empty = new GridVolumeReport { Cells = Array.Empty<GridVolumeCell>() };
                Check(!empty.IsComplete, "Zero computed cells must not certify volume=0");
            },
            ["grid_positive_subthreshold_cell_is_not_accepted"] = ()=>{
                var tiny = new GridCellVolume {
                    CalculationArea = 5e-10, CoveredArea = 0
                };
                Check(tiny.CalculationArea > 0 && !tiny.IsComplete,
                    "A positive clipped sliver must not be reported as a valid zero-volume cell");
            },
            ["grid_positive_tiny_tin_face_is_not_silently_accepted"] = ()=>{
                // A 5e-10 m2 projected face is accepted by the TIN renderer
                // but cannot be integrated by the grid kernel at Eps=1e-9.
                // Its large delta-Z makes silent omission unacceptable.
                var small = new Triangle3(P(4,4,100000), P(4.00001,4,100000),
                    P(4,4.0001,100000));
                Check(small.Area2D > 0 && small.Area2D < 1e-9,
                    "Microface fixture must remain positive and subthreshold");
                var existing = new[]{
                    new Triangle3(P(0,0,100),P(20,0,100),P(20,20,100)),
                    new Triangle3(P(0,0,100),P(20,20,100),P(0,20,100)),
                    small
                };
                var design = new[]{
                    new Triangle3(P(0,0),P(20,0),P(20,20)),
                    new Triangle3(P(0,0),P(20,20),P(0,20))
                };
                var cell = new[]{new Vec2(0,0),new Vec2(20,0),
                    new Vec2(20,20),new Vec2(0,20)};
                var result = ExactGridCellCalculator.Calculate(existing,design,cell);
                Check(result.HasUnresolvedSmallTriangles && !result.IsComplete,
                    "Positive subthreshold TIN face must flag the result as not accepted");
            },

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
            ["strict_xy_sparse_index_keeps_distinct_roots_in_same_cell"] = ()=>{
                // Two diagonally separated XYs may occupy the same hash cell yet
                // differ by more than the Euclidean XY tolerance. A third point
                // close to the second root must not be assigned to the first.
                // Also exercises real mine-survey coordinate magnitudes.
                foreach(double origin in new[]{5000.0,2500000.0}){
                    var a=Entity("root-a",SourceEntityType.Point,P(origin+0.00000010,6000.00000010,1));
                    var b=Entity("root-b",SourceEntityType.Point,P(origin+0.00000090,6000.00000090,6));
                    var c=Entity("near-b",SourceEntityType.Point,P(origin+0.00000092,6000.00000087,8));
                    var d=Entity("near-a",SourceEntityType.Point,P(origin+0.00000012,6000.00000011,1));
                    var model=Model(a,b,c,d);
                    var scan=new ConflictScanner().Scan(model,new SurfaceBuildOptions{
                        XyTolerance=1e-6,ZConflictTolerance=1e-4});
                    Check(scan.Groups.Count==1,"Scanner merged diagonal distinct XY roots");
                    var group=scan.Groups[0];
                    Check(group.Sources.Count==2 && group.Sources.Any(x=>x.EntityId=="root-b") &&
                        group.Sources.Any(x=>x.EntityId=="near-b") &&
                        !group.Sources.Any(x=>x.EntityId=="root-a" || x.EntityId=="near-a"),
                        "Scanner assigned a source to a different XY root");
                    Check(Math.Abs(group.DeltaZ-2)<1e-9 && scan.SameXYZDuplicates==1,
                        "Incorrect strict XY/Z conflict groups after index compaction");
                    Check(model.Revision==0,"Conflict scan modified original model");
                }
            },
            ["edited_cad_section_updates_areas_and_never_changes_source"] = ()=>{
                var line = new SectionLine(1,"MC-01",0,new Vec2(1,0),new Vec2(0,1),
                    new[]{new SectionSpan(0,10)});
                var seg = new SectionProfileSegment(0,10,new Vec2(0,0),new Vec2(10,0),
                    10,10,5,5);
                var source = new SectionProfile(line,new[]{seg},50,0,Array.Empty<string>());
                var ht = new[]{new EditedSectionPath(0,new[]{
                    new EditedSectionVertex(0,10), new EditedSectionVertex(5,12),
                    new EditedSectionVertex(10,10)})};
                var tk = new[]{new EditedSectionPath(0,new[]{
                    new EditedSectionVertex(0,5), new EditedSectionVertex(10,5)})};
                var updated=EditedSectionProfileBuilder.Rebuild(source,ht,tk);
                Check(Math.Abs(updated.CutArea-60)<1e-8 && updated.FillArea==0,
                    "Edited CAD vertices must drive newly calculated section area");
                Check(updated.Segments.Count==2 && source.Segments.Count==1 &&
                    source.CutArea==50 && source.Segments[0].ExistingZ0==10,
                    "CAD editing unexpectedly mutated source profile");
                var mixed=EditedSectionProfileBuilder.Rebuild(source,
                    new[]{new EditedSectionPath(0,new[]{
                        new EditedSectionVertex(0,10),new EditedSectionVertex(10,0)})},tk);
                Check(Math.Abs(mixed.CutArea-12.5)<1e-8 &&
                    Math.Abs(mixed.FillArea-12.5)<1e-8,
                    "Edited section sign crossing must split cut/fill exactly");
                bool rejected=false;
                try {
                    EditedSectionProfileBuilder.Rebuild(source,
                        new[]{new EditedSectionPath(0,new[]{
                            new EditedSectionVertex(0,10),new EditedSectionVertex(8,12),
                            new EditedSectionVertex(7,13),new EditedSectionVertex(10,10)})},tk);
                } catch(InvalidOperationException) { rejected=true; }
                Check(rejected,"Reversed CAD profile must not be used for new volume");
                rejected=false;
                try {
                    EditedSectionProfileBuilder.Rebuild(source,
                        new[]{new EditedSectionPath(0,new[]{
                            new EditedSectionVertex(1,10),new EditedSectionVertex(10,10)})},tk);
                } catch(InvalidOperationException) { rejected=true; }
                Check(rejected,"Edited path must preserve source coverage endpoints");
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
            ["clipped_upright_3d_face_never_crosses_selected_tin_region"] = ()=>{
                var ring=new[]{new Vec2(0,0),new Vec2(8,0),new Vec2(8,10),new Vec2(0,10)};
                var face=new Triangle3(P(-2,5,10),P(-2,5,20),P(10,5,12));
                var parts=VerticalFaceBuilder.ClipToBoundary(new[]{face},ring);
                Check(parts.Count>=2,
                    "Cutting an upright 3D face must retain its nonzero-area interior portions");
                foreach(var t in parts)
                    foreach(var v in new[]{t.A,t.B,t.C})
                        Check(SurfaceRegionClipper.ContainsInclusive(v.XY,ring,1e-7),
                            "Upright 3D TIN extended outside CAD region");
                Check(parts.SelectMany(t=>new[]{t.A,t.B,t.C}).Any(v=>
                    Math.Abs(v.X)<1e-7&&Math.Abs(v.Z-(10+2.0/6.0))<1e-7),
                    "Clipped 3D face lost interpolated lower Z on selected edge");
                Check(parts.SelectMany(t=>new[]{t.A,t.B,t.C}).Any(v=>
                    Math.Abs(v.X)<1e-7&&Math.Abs(v.Z-(20-8.0/6.0))<1e-7),
                    "Clipped 3D face lost interpolated upper Z on selected edge");
                var inside=new Triangle3(P(1,1,0),P(2,1,0),P(1,2,0));
                TinRegionIntegrityVerifier.Verify(
                    new TinSurface("clipped",new[]{inside},Array.Empty<ValidationIssue>(),parts),
                    ring);
                bool rejected=false;
                try { TinRegionIntegrityVerifier.Verify(
                    new TinSurface("outside",new[]{new Triangle3(P(-1,0),P(2,1),P(1,2))},
                        Array.Empty<ValidationIssue>()),ring); }
                catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Outside flat support triangle silently accepted");
                rejected=false;
                try { TinRegionIntegrityVerifier.Verify(
                    new TinSurface("outside vertical",new[]{inside},
                        Array.Empty<ValidationIssue>(),new[]{face}),ring); }
                catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Outside upright 3D face silently accepted");
            },
            ["cropped_xy_duplicate_tin_is_inside_polygon_in_memory_and_filebacked"] = ()=>{
                var ring=new[]{new Vec2(0,0),new Vec2(8,0),new Vec2(8,10),new Vec2(0,10)};
                var options=new SurfaceBuildOptions{
                    ClipBoundary=ring,PreserveDuplicateXYVerticalFaces=true};
                var survey=new[]{P(0,0,0),P(10,0,0),P(10,10,0),
                    P(0,10,0),P(5,5,10),P(5,5,20)};
                var source=Model(Entity("survey",SourceEntityType.Point,survey));
                var prepared=new SurfaceInputPreparer().Prepare(source,options);
                Check(!prepared.HasErrors,"Duplicate XY clip preparation blocked");
                var normal=Build(prepared,options);
                Check(normal.VerticalFaces.Count>0,"Clipped XY duplicate lost all 3D faces");
                TinRegionIntegrityVerifier.Verify(normal,ring);
                var stored=new TiledConformingTinBuilder().Build("clip-3d",source,options);
                try {
                    Check(stored.Surface.VerticalFaces.Count>0,"File-backed clip lost upright 3D faces");
                    TinRegionIntegrityVerifier.Verify(stored.Surface,ring);
                    Check(Math.Abs(normal.Triangles.Sum(x=>x.Area2D)-
                        stored.Surface.Triangles.Sum(x=>x.Area2D))<1e-6,
                        "Disk-backed and in-memory boundary-clipped support differ");
                }
                finally{(stored.Surface.Triangles as IDisposable)?.Dispose();}
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
                    Check(r.PreviewTriangles.Count==0,"Official TIN must not build a stride-sampled CAD mesh");
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
        FinalAuditRegression.Add(tests);
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
    static void Fixture(string path, string boundary, int seconds, DuplicateXYConflictPolicy policy, bool vertical=false)
    {
        var sw=Stopwatch.StartNew();var m=ReadModel(path);var o=new SurfaceBuildOptions {DuplicateXYConflictPolicy=policy,PreserveDuplicateXYVerticalFaces=vertical};
        Console.WriteLine(JsonSerializer.Serialize(new{phase="loaded",entities=m.Entities.Count,vertices=m.Entities.Sum(x=>x.Vertices.Count),ms=sw.ElapsedMilliseconds}));
        using(var c=new CancellationTokenSource(TimeSpan.FromSeconds(seconds)))try {
            if(boundary!="-") {using(var r=new BinaryReader(File.OpenRead(boundary))){int n=r.ReadInt32();var b=new Vec2[n];for(int i=0;i<n;i++)b[i]=new Vec2(r.ReadDouble(),r.ReadDouble());o.ClipBoundary=b;}m=SurfaceRegionClipper.Clip(m,o.ClipBoundary,o.XyTolerance,c.Token);}
            int vertices=m.Entities.Sum(x=>x.Vertices.Count);
            Console.WriteLine(JsonSerializer.Serialize(new{phase="region",entities=m.Entities.Count,vertices,ms=sw.ElapsedMilliseconds}));
            TinSurface tin;
            if(vertices>=200000){var r=new TiledConformingTinBuilder().Build(m.Name,m,o,(d,t,s)=>Console.WriteLine(JsonSerializer.Serialize(new{phase="progress",done=d,total=t,message=s,ms=sw.ElapsedMilliseconds})),c.Token);tin=r.Surface;}
            else {var p=new SurfaceInputPreparer().Prepare(m,o,s=>Console.WriteLine(s),c.Token);Console.WriteLine(JsonSerializer.Serialize(new{phase="prepared",sites=p.Sites.Count,breaklines=p.Breaklines.Count,errors=p.Issues.Count(x=>x.Severity==ValidationSeverity.Error),issues=p.Issues.GroupBy(x=>x.Code).ToDictionary(x=>x.Key,x=>x.Count()),ms=sw.ElapsedMilliseconds}));if(p.HasErrors){Environment.ExitCode=2;return;}tin=new ConformingTinBuilder().Build(m.Name,p,o,s=>Console.WriteLine(s),c.Token);}
            int oldCadRejected=0;double smallestArea=double.PositiveInfinity;double coverage=0;
            foreach(var triangle in tin.Triangles) {
                TinTriangleQuality.ValidateForCad(triangle);
                if(Math.Abs(Vec2.Cross(triangle.A.XY,triangle.B.XY,triangle.C.XY))*0.5<=1e-12)oldCadRejected++;
                smallestArea=Math.Min(smallestArea,triangle.Area2D);coverage+=triangle.Area2D;
            }
            Console.WriteLine(JsonSerializer.Serialize(new{phase="cad_validation",oldCadRejected,smallestArea,coverage}));
            Console.WriteLine(JsonSerializer.Serialize(new{phase="complete",verticalFaces=tin.VerticalFaces.Count,issues=tin.Issues.Select(x=>x.Code).ToArray(),triangles=tin.Triangles.Count,ms=sw.ElapsedMilliseconds,managedBytes=GC.GetTotalMemory(false)}));(tin.Triangles as IDisposable)?.Dispose();
        } catch(Exception ex){Console.WriteLine(JsonSerializer.Serialize(new{phase="error",kind=ex.GetType().Name,message=ex.Message,issues=(ex as DuplicateXYConflictException)?.Issues.GroupBy(x=>x.Code).ToDictionary(x=>x.Key,x=>x.Count()),ms=sw.ElapsedMilliseconds}));Environment.ExitCode=2;}
    }
    static void ReviewFixture(string path,string boundary,int seconds)
    {
        var watch=Stopwatch.StartNew();var model=ReadModel(path);
        using(var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(seconds))) {
            Vec2[] region=null;
            if(boundary!="-")using(var r=new BinaryReader(File.OpenRead(boundary))) {
                region=new Vec2[r.ReadInt32()];for(int i=0;i<region.Length;i++)region[i]=new Vec2(r.ReadDouble(),r.ReadDouble());
            }
            var mean=MeanZPreviewPreparer.Prepare(model,token:cancel.Token,tinRegion:region);
            Console.WriteLine(JsonSerializer.Serialize(new {phase="mean_review",entities=model.Entities.Count,
                vertices=model.Entities.Sum(e=>e.Vertices.Count),participating=mean.OriginalVertexCount,
                sites=mean.Prepared.Sites.Count,groups=mean.Conflicts.Count,above3=mean.ReviewGroups.Count,
                maxDelta=mean.Conflicts.Count==0?0:mean.Conflicts.Max(g=>g.DeltaZ),blocked=mean.HasBlockingConflicts,
                ms=watch.ElapsedMilliseconds,managedBytes=GC.GetTotalMemory(false)}));
            mean=null;GC.Collect();
            var input=region==null?model:SurfaceRegionClipper.Clip(model,region,cancellationToken:cancel.Token);
            var strict=new ConflictScanner().Scan(input,new SurfaceBuildOptions(),token:cancel.Token);
            Console.WriteLine(JsonSerializer.Serialize(new {phase="strict_review",groups=strict.Groups.Count,
                sources=strict.SourceRecordCount,above3=strict.Groups.Count(g=>g.DeltaZ>3),
                originalRows=strict.Groups.Sum(g=>g.Sources.Count(x=>!x.Generated)),
                generatedRows=strict.Groups.Sum(g=>g.Sources.Count(x=>x.Generated)),
                blocked=strict.HasBlockingConflicts,ms=watch.ElapsedMilliseconds,managedBytes=GC.GetTotalMemory(false)}));
        }
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
    static void GridFixture(string path)
    {
        using(var doc=JsonDocument.Parse(File.ReadAllText(path))) {
            var root=doc.RootElement;
            Func<JsonElement,Vec2[]> ring=e=>e.EnumerateArray().Select(p=>new Vec2(p[0].GetDouble(),p[1].GetDouble())).ToArray();
            Func<JsonElement,Triangle3[]> faces=e=>e.EnumerateArray().Select(t=>{
                var p=t.EnumerateArray().Select(v=>new Vec3(v[0].GetDouble(),v[1].GetDouble(),v[2].GetDouble())).ToArray();
                return new Triangle3(p[0],p[1],p[2]);
            }).ToArray();
            var ht=faces(root.GetProperty("existing"));var tk=faces(root.GetProperty("design"));
            foreach(var r in root.GetProperty("regions").EnumerateArray()) {
                var prepared=ExactGridCellCalculator.PrepareBoundary(ring(r.GetProperty("polygon")));
                foreach(var c in root.GetProperty("cells").EnumerateArray()) {
                    var cell=ring(c.GetProperty("polygon"));
                    double minX=cell.Min(p=>p.X),maxX=cell.Max(p=>p.X),minY=cell.Min(p=>p.Y),maxY=cell.Max(p=>p.Y);
                    Func<Triangle3,bool> touches=t=>Math.Max(t.A.X,Math.Max(t.B.X,t.C.X))>=minX&&Math.Min(t.A.X,Math.Min(t.B.X,t.C.X))<=maxX&&
                        Math.Max(t.A.Y,Math.Max(t.B.Y,t.C.Y))>=minY&&Math.Min(t.A.Y,Math.Min(t.B.Y,t.C.Y))<=maxY;
                    string region=r.GetProperty("id").GetString(),id=c.GetProperty("id").GetString();
                    try {
                        var v=ExactGridCellCalculator.CalculatePrepared(ht.Where(touches).ToArray(),tk.Where(touches).ToArray(),cell,prepared);
                        Console.WriteLine(JsonSerializer.Serialize(new{gridFixture=true,region,id,status="CALCULATED",cut=v.CutVolume,fill=v.FillVolume,
                            area=v.CalculationArea,covered=v.CoveredArea,noData=v.NoDataArea,complete=v.IsComplete}));
                    } catch(Exception e) {
                        Console.WriteLine(JsonSerializer.Serialize(new{gridFixture=true,region,id,status="REJECTED",error=e.Message}));
                    }
                }
            }
        }
    }
    static void Main(string[] args){if(args.Length==0)RunTests();else if(args[0]=="--grid-fixture")GridFixture(args[1]);else if(args[0]=="--vertical-fixture")Fixture(args[1],args[2],int.Parse(args[3]),DuplicateXYConflictPolicy.Stop,true);else if(args[0]=="--report-fixtures")FinalAuditRegression.Fixtures(args[1]);else if(args[0]=="--clip-oracle")ClipOracle();else if(args[0]=="--review-fixture")ReviewFixture(args[1],args[2],int.Parse(args[3]));else Fixture(args[0],args.Length>1?args[1]:"-",args.Length>2?int.Parse(args[2]):120,args.Length>3?(DuplicateXYConflictPolicy)Enum.Parse(typeof(DuplicateXYConflictPolicy),args[3]):DuplicateXYConflictPolicy.Stop);}
}
