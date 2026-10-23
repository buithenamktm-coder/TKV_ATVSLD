using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Sections;
using MiningVolume.Core.Surface;
using MiningVolume.Core.Volumes;
using MiningVolume.Core.Grid;
using MiningVolume.Core.Reporting;
using MiningVolume2023.Services;

// Production report builders and writer, not a second implementation of exports.
internal static class FinalAuditRegression
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Near(double x,double y){Check(Math.Abs(x-y)<1e-7,$"Expected {y:R}; actual {x:R}");}
    static SectionProfile Profile(int i,double ht,double tk=0) {
        var l=new SectionLine(i,"MC-"+i,(i-1)*20,new Vec2(1,0),new Vec2(0,1),new[]{new SectionSpan(0,10)});
        return new SectionProfile(l,new[]{new SectionProfileSegment(0,10,l.PointAt(0),l.PointAt(10),ht,ht,tk,tk)},10*Math.Max(0,ht-tk),10*Math.Max(0,tk-ht),Array.Empty<string>());
    }
    static SectionProfile Edit(SectionProfile p,double a,double b)=>EditedSectionProfileBuilder.Rebuild(p,
        new[]{new EditedSectionPath(0,new[]{new EditedSectionVertex(0,a),new EditedSectionVertex(10,b)})},
        new[]{new EditedSectionPath(0,new[]{new EditedSectionVertex(0,0),new EditedSectionVertex(10,0)})});
    static readonly Vec2[] Boundary={new Vec2(0,0),new Vec2(10,0),new Vec2(10,20),new Vec2(0,20)};
    static TinSurface Plane(double z)=>new MiningVolume.Surface.ConformingTinBuilder().Build("plane",
        new MiningVolume.Core.Surface.SurfaceInputPreparer().PrepareRaw(Boundary.Select(v=>new Vec3(v.X,v.Y,z)),null,new SurfaceBuildOptions()),new SurfaceBuildOptions());
    static VolumeResult Calculate(params SectionProfile[] p)=>new SectionVolumeCalculator().Calculate(
        new SectionSystem(new Vec2(1,0),new Vec2(0,1),Boundary,p.Select(x=>x.Line).ToArray(),Array.Empty<string>()),p,Plane(5),Plane(0),
        new VolumeCalculationOptions{FromLevel=0,ToLevel=10,LevelStep=5});
    sealed class ReadProbe<T> : IReadOnlyList<T> {
        readonly IReadOnlyList<T> items;
        public Action<int> OnRead;
        public ReadProbe(IReadOnlyList<T> items) { this.items=items; }
        public int Count => items.Count;
        public T this[int i] { get { OnRead?.Invoke(i);return items[i]; } }
        public IEnumerator<T> GetEnumerator() { for(int i=0;i<Count;i++)yield return this[i]; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    sealed class CancelText {
        public Action OnText;
        public override string ToString() { OnText?.Invoke(); return "cancel-row"; }
    }
    static void ExpectCanceled(Action action) {
        bool canceled=false;
        try { action(); } catch(System.OperationCanceledException) { canceled=true; }
        Check(canceled,"Canceled operation returned a result");
    }
    public static void Add(IDictionary<string,Action> tests) {
        tests["xlsx_revalidate_after_write_preserves_previous_file"] = () => {
            var directory = Path.Combine(Path.GetTempPath(), "imsat-publish-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try {
                var target = Path.Combine(directory, "accepted.xlsx");
                var old = new byte[] { 1, 2, 3, 4 };
                File.WriteAllBytes(target, old);
                var report = new SpreadsheetReport { Title = "publication" };
                var sheet = new ReportSheet { Name = "Data" }; sheet.Headers.Add("Volume");
                sheet.Rows.Add(new[] { ReportCell.N2(12.5) }); report.Sheets.Add(sheet);
                int checks = 0; bool rejected = false;
                try {
                    ExportWorkflowService.WriteValidatedXlsxAsync(target, report, () => {
                        if (++checks == 2) throw new InvalidOperationException("DWG changed");
                    }).GetAwaiter().GetResult();
                } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && checks == 2, "No validation after file preparation");
                Check(File.ReadAllBytes(target).SequenceEqual(old), "Stale workbook replaced accepted file");
                Check(Directory.GetFiles(directory).Length == 1, "Rejected staging file leaked");
                using (var cts = new System.Threading.CancellationTokenSource()) {
                    checks = 0;
                    ExpectCanceled(() => ExportWorkflowService.WriteValidatedXlsxAsync(target, report,
                        () => { if (++checks == 2) cts.Cancel(); }, cts.Token).GetAwaiter().GetResult());
                }
                Check(File.ReadAllBytes(target).SequenceEqual(old), "Final cancellation replaced accepted file");
                Check(Directory.GetFiles(directory).Length == 1, "Canceled staging file leaked");
                ExportWorkflowService.WriteValidatedXlsxAsync(target, report, () => {}).GetAwaiter().GetResult();
                using (var zip = ZipFile.OpenRead(target))
                    Check(zip.GetEntry("xl/worksheets/sheet1.xml") != null, "Published workbook is invalid");
                Check(Directory.GetFiles(directory).Length == 1, "Successful staging file leaked");
            } finally { Directory.Delete(directory, true); }
        };
        tests["edited_profile_large_merge_and_cancellation"] = () => {
            var original = Profile(1, 5);
            var ht = new[] { new EditedSectionPath(0, Enumerable.Range(0, 4097)
                .Select(i => new EditedSectionVertex(10.0 * i / 4096, 5 - 10.0 * i / 4096)).ToArray()) };
            var tk = new[] { new EditedSectionPath(0, Enumerable.Range(0, 6001)
                .Select(i => new EditedSectionVertex(10.0 * i / 6000, 0)).ToArray()) };
            int polls = 0;
            var updated = EditedSectionProfileBuilder.Rebuild(original, ht, tk,
                pollCancellation: () => polls++);
            Near(updated.CutArea, 12.5); Near(updated.FillArea, 12.5);
            Check(updated.Segments.Count > 6000, "Merged stations were lost");
            foreach (var segment in updated.Segments) {
                Near(segment.ExistingZ0, 5 - segment.S0);
                Near(segment.ExistingZ1, 5 - segment.S1);
                Near(segment.DesignZ0, 0); Near(segment.DesignZ1, 0);
            }
            Check(polls > 100, "Large rebuild did not poll within each processing stage");
            // Every checkpoint, including final publication, must propagate cancellation.
            for (int stop = 1; stop <= polls; stop++) {
                int calls = 0;
                ExpectCanceled(() => EditedSectionProfileBuilder.Rebuild(original, ht, tk,
                    pollCancellation: () => { if (++calls == stop) throw new OperationCanceledException(); }));
            }
            Near(original.CutArea, 50); Near(original.FillArea, 0);
            Check(original.Segments.Count == 1, "Canceled rebuild mutated the accepted profile");
        };
        tests["section_axis_ticks_bounded_and_precision_safe"] = () => {
            var ticks = SectionAxisTicks.Create(-10, 10, 0.1, 250);
            Check(ticks.Count == 201, "Fractional axis lost endpoint");
            Near(ticks[0], -10); Near(ticks[200], 10);
            for (int i=1;i<ticks.Count;i++) Check(ticks[i]>ticks[i-1], "Repeated elevation tick");
            Check(SectionAxisTicks.Create(0,249,1,250).Count==250,"Budget boundary rejected");
            foreach (var args in new[] {
                new[]{1e16,1e16,0.1}, new[]{1e6,1e6,1e-12},
                new[]{0.0,250.0,1.0}, new[]{0.0,1.0,double.NaN},
                new[]{0.0,double.PositiveInfinity,1.0}, new[]{1.0,0.0,1.0} }) {
                bool rejected=false;
                try { SectionAxisTicks.Create(args[0],args[1],args[2],250); }
                catch(ArgumentOutOfRangeException) { rejected=true; }
                Check(rejected,"Invalid/unrepresentable axis accepted");
            }
        };
        tests["integrated_excel_cancel_mid_write_preserves_existing_file_and_cleans_staging"] = () => {
            string directory=Path.Combine(Path.GetTempPath(),"imsat-cancel-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string target=Path.Combine(directory,"accepted.xlsx");
            File.WriteAllText(target,"previous accepted output");
            try {
                using(var cts=new System.Threading.CancellationTokenSource()) {
                    var report=new SpreadsheetReport { IncludeImsatLogo=false };
                    var sheet=new ReportSheet { Name="QA" };sheet.Headers.Add("Value");report.Sheets.Add(sheet);
                    sheet.Rows.Add(new[] { ReportCell.Text(new CancelText { OnText=()=>cts.Cancel() }) });
                    sheet.Rows.Add(new[] { ReportCell.Text("Never publish partial workbook") });
                    ExpectCanceled(()=>ExportWorkflowService.WriteXlsx(target,report,cts.Token));
                    Check(File.ReadAllText(target)=="previous accepted output","Cancellation replaced accepted Excel");
                    Check(Directory.GetFiles(directory).Length==1,"Canceled export leaked staging files");
                    sheet.Rows.Clear();sheet.Rows.Add(new[] {ReportCell.N3(12.5)});
                    ExportWorkflowService.WriteXlsx(target,report);
                    using(var zip=ZipFile.OpenRead(target))
                        Check(zip.GetEntry("xl/worksheets/sheet1.xml")!=null,"Export did not recover after cancellation");
                }
            } finally {Directory.Delete(directory,true);}
        };
        tests["section_report_build_cancel_recovers_and_preserves_volume_formulas"] = () => {
            var state = State();
            var options = new ExportOptions();
            string id = state.VolumeResult.CalculationId;
            using (var cts = new System.Threading.CancellationTokenSource()) {
                cts.Cancel();
                ExpectCanceled(() => ExportWorkflowService.BuildReport(state, options, cts.Token));
            }
            // The next attempt is not poisoned by the canceled report build;
            // volume provenance and all worksheet formulas stay unchanged.
            var recovered = ExportWorkflowService.BuildReport(state, options,
                System.Threading.CancellationToken.None);
            var baseline = ExportWorkflowService.BuildReport(state, options);
            Check(state.VolumeResult.CalculationId == id, "Cancelled report changed the volume identity");
            Check(recovered.Sheets.Count == baseline.Sheets.Count,
                "Cancelled report changed the selected sheets on retry");
            for (int i = 0; i < recovered.Sheets.Count; i++) {
                var lhs = recovered.Sheets[i];
                var rhs = baseline.Sheets[i];
                Check(lhs.Name == rhs.Name && lhs.Rows.Count == rhs.Rows.Count,
                    "Canceled report changed worksheet structure on retry");
                for (int row = 0; row < lhs.Rows.Count; row++) {
                    Check(lhs.Rows[row].Count == rhs.Rows[row].Count, "Canceled report changed columns");
                    for (int col = 0; col < lhs.Rows[row].Count; col++) {
                        var x = lhs.Rows[row][col];
                        var y = rhs.Rows[row][col];
                        Check(x.Kind == y.Kind && Equals(x.Value, y.Value),
                            "Canceled report changed an Excel value/formula");
                    }
                }
            }
        };
        tests["quick_section_cancel_during_route_processing_returns_no_partial_profile"] = () => {
            using(var cts=new System.Threading.CancellationTokenSource()) {
                var route=new ReadProbe<Vec2>(new[] { new Vec2(0,1),new Vec2(5,1),new Vec2(10,1) });
                route.OnRead=i=>{if(i==2)cts.Cancel();};
                ExpectCanceled(()=>new QuickSectionProfileBuilder().Build(1,"quick",route,Plane(5),Plane(0),cancellationToken:cts.Token));
            }
        };

        tests["section_index_is_shared_across_workflows_and_replaced_with_tin"] = () => {
            var triangles=new ReadProbe<Triangle3>(new[] {
                new Triangle3(new Vec3(0,0,5),new Vec3(10,0,5),new Vec3(0,10,5)) });
            int reads=0;
            triangles.OnRead=i=>System.Threading.Interlocked.Increment(ref reads);
            var tin=new TinSurface("shared",triangles,Array.Empty<ValidationIssue>());
            // Empty routes isolate index construction from legitimate query reads.
            System.Threading.Tasks.Parallel.For(0,16,i=>new TinSectionSampler().BuildProfiles(
                Array.Empty<SectionLine>(),tin,tin));
            Check(reads==1,"Concurrent samplers rebuilt the same spatial index");
            double z;
            Check(new TinSectionSampler().TryElevation(tin,new Vec2(1,1),1e-7,out z),"Shared index lost coverage");
            Near(z,5);
            var replaced=new TinSurface("shared",new[] {
                new Triangle3(new Vec3(0,0,9),new Vec3(10,0,9),new Vec3(0,10,9)) },Array.Empty<ValidationIssue>());
            Check(new TinSectionSampler().TryElevation(replaced,new Vec2(1,1),1e-7,out z),"New TIN lost coverage");
            Near(z,9);
        };

        tests["grid_async_workbook_captures_project_metadata_before_yield"] = () => {
            var state=State();
            state.MiningUnitGroup=MiningUnitCatalog.OutsideTkv;
            state.MiningUnitName="Original grid unit";
            var options=new GridVolumeOptions { Boundary=Boundary, SizeX=1, SizeY=1 };
            var grid=new GridVolumeCalculator().Calculate(Plane(5),Plane(0),options);
            var expected=GridVolumeExcelReportBuilder.Build(grid,options);
            var pending=GridVolumeExcelReportBuilder.BuildAsync(grid,options);
            state.MiningUnitName="Changed after export started";
            var actual=pending.GetAwaiter().GetResult();
            Check(actual.Sheets.Count==6,"Async grid lost sheets");
            Check(!actual.IncludeImsatLogo,"Async grid lost outside-unit snapshot");
            for(int i=0;i<6;i++) {
                Check(actual.Sheets[i].Title==expected.Sheets[i].Title,"Async grid read live unit");
                Check(actual.Sheets[i].Rows.Count==expected.Sheets[i].Rows.Count,"Async grid lost rows");
            }
            string file=Path.Combine(Path.GetTempPath(),"imsat-grid-async-"+Guid.NewGuid().ToString("N")+".xlsx");
            try {
                ExportWorkflowService.WriteXlsx(file,actual);
                using(var zip=ZipFile.OpenRead(file))
                using(var stream=zip.GetEntry("xl/worksheets/sheet1.xml").Open()) {
                    var xml=XDocument.Load(stream);
                    Check(xml.Descendants().Any(x=>x.Name.LocalName=="t" && x.Value==grid.CalculationId),"Async ID missing");
                    Check(!xml.ToString().Contains("Changed after export started"),"Async metadata changed");
                    using(var summary=zip.GetEntry("xl/worksheets/sheet5.xml").Open())
                        Check(!XDocument.Load(summary).ToString().Contains("Changed after export started"),"Async signature metadata changed");
                }
            } finally { if(File.Exists(file))File.Delete(file); }
        };

        tests["grid_calculation_id_survives_workbook_export_and_changes_on_recalculation"] = () => {
            State();
            var options=new GridVolumeOptions { Boundary=Boundary, SizeX=5, SizeY=5 };
            var ht=Plane(5);var tk=Plane(0);var calculator=new GridVolumeCalculator();
            var grid=calculator.Calculate(ht,tk,options);
            var report=GridVolumeExcelReportBuilder.Build(grid,options);
            var again=calculator.Calculate(ht,tk,options);
            Check(grid.CalculationId!=again.CalculationId,"Grid recalculation reused identity");
            Near(grid.TotalCutVolume,again.TotalCutVolume);
            string file=Path.Combine(Path.GetTempPath(),"imsat-grid-id-"+Guid.NewGuid().ToString("N")+".xlsx");
            try {
                ExportWorkflowService.WriteXlsx(file,report);
                using(var zip=ZipFile.OpenRead(file))
                using(var stream=zip.GetEntry("xl/worksheets/sheet1.xml").Open()) {
                    var xml=XDocument.Load(stream);
                    Check(xml.Descendants().Count(x=>x.Name.LocalName=="t" && x.Value==grid.CalculationId)==1,
                        "Grid workbook lost its calculation identity");
                    Check(!xml.Descendants().Any(x=>x.Name.LocalName=="t" && x.Value==again.CalculationId),
                        "Grid workbook identity changed after recalculation");
                }
            } finally { if(File.Exists(file))File.Delete(file); }
        };
        tests["section_calculation_id_is_stable_across_exports_and_changes_on_recalculation"] = () => {
            var st=State();
            string id=st.VolumeResult.CalculationId;
            Guid parsed;
            Check(Guid.TryParseExact(id,"N",out parsed),"Invalid calculation identity");
            var first=ExportWorkflowService.BuildReport(st,new ExportOptions());
            var second=ExportWorkflowService.BuildReport(st,new ExportOptions());
            Check(st.VolumeResult.CalculationId==id,"Export changed calculation identity");
            st.VolumeResult=Calculate(st.SectionProfiles.ToArray());
            Check(st.VolumeResult.CalculationId!=id,"Recalculation reused prior identity");
            var dir=Path.Combine(Path.GetTempPath(),"imsat-calculation-id-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try {
                int n=0;
                foreach(var report in new[]{first,second}) {
                    string file=Path.Combine(dir,(++n)+".xlsx");
                    ExportWorkflowService.WriteXlsx(file,report);
                    int occurrences=0;
                    using(var zip=ZipFile.OpenRead(file))
                        foreach(var entry in zip.Entries.Where(e=>e.FullName.StartsWith("xl/worksheets/") && e.FullName.EndsWith(".xml")))
                            using(var stream=entry.Open()) {
                                var xml=XDocument.Load(stream);
                                occurrences+=xml.Descendants().Count(x=>x.Name.LocalName=="t" && x.Value==id);
                                Check(!xml.Descendants().Any(x=>x.Name.LocalName=="t" && x.Value==st.VolumeResult.CalculationId),
                                    "Writing workbook consulted newer calculation");
                            }
                    Check(occurrences==2,"Calculation identity missing from information/summary sheets");
                }
            } finally { Directory.Delete(dir,true); }
        };
        tests["section_band_search_matches_individual_clipping_for_order_gaps_overlap_and_tolerance"] = () => {
            var random=new Random(20261022);
            var calc=new SectionBandAreaCalculator();
            for(int trial=0;trial<400;trial++) {
                var line=Profile(1,1).Line;
                double e0=random.NextDouble()*40-20,e1=random.NextDouble()*40-20;
                double d0=random.NextDouble()*40-20,d1=random.NextDouble()*40-20;
                if(trial%5==0) { e0=5+5e-10;e1=5-5e-10;d0=4;d1=4; }
                var p=new SectionProfile(line,new[]{new SectionProfileSegment(
                    0,10,line.PointAt(0),line.PointAt(10),e0,e1,d0,d1)},0,0,Array.Empty<string>());
                var bands=Enumerable.Range(0,24).Select(i=>new LevelBand(i,i*2-24,
                    i*2-24+(trial%3==0?3:trial%3==1?1:2))).ToArray();
                if(trial%4==0)Array.Reverse(bands);
                if(trial%4==1) { var swap=bands[3];bands[3]=bands[18];bands[18]=swap; }
                if(trial%4==2)bands[12]=new LevelBand(12,-50,50); // nested: fallback
                var all=calc.Calculate(p,bands);
                for(int i=0;i<bands.Length;i++) {
                    var individual=calc.Calculate(p,new[]{bands[i]});
                    Check(all.Bands[i].CutArea==individual.CutArea && all.Bands[i].FillArea==individual.FillArea,
                        "Band search changed exact clipping output at trial "+trial+", band "+i);
                }
            }
        };
        tests["section_band_buffers_preserve_sloped_strip_areas_and_parallel_isolation"] = () => {
            // e(x)=2-0.4x, d(x)=0. Each half is a triangle of area 5.
            // Its unit-height strips have areas 3.75 and 1.25 analytically.
            var line=Profile(1,1).Line;
            var segments=Enumerable.Range(0,40).Select(i=>new SectionProfileSegment(
                i*10,(i+1)*10,line.PointAt(i*10),line.PointAt((i+1)*10),
                i%2==0?2:-2,i%2==0?-2:2,0,0)).ToArray();
            var profile=new SectionProfile(line,segments,200,200,Array.Empty<string>());
            var bands=SectionVolumeCalculator.BuildBands(-4,4,1).ToArray();
            // Interleave empty and occupied bands to expose stale scratch data.
            var shuffled=new[]{bands[0],bands[4],bands[7],bands[2],bands[5],bands[1],bands[3],bands[6]};
            var calculator=new SectionBandAreaCalculator();
            System.Threading.Tasks.Parallel.For(0,64,iteration=>{
                var order=iteration%2==0?shuffled:shuffled.Reverse().ToArray();
                var result=calculator.Calculate(profile,order);
                for(int i=0;i<order.Length;i++) {
                    double low=order[i].LowerZ;
                    Near(result.Bands[i].CutArea,low==0?150:low==1?50:0);
                    Near(result.Bands[i].FillArea,low==-1?150:low==-2?50:0);
                }
                Near(result.CutArea,200);Near(result.FillArea,200);
                var zero=calculator.Calculate(Profile(1,0,0),order);
                Near(zero.CutArea,0);Near(zero.FillArea,0);
            });
        };
        tests["sampler_cancel_during_index_build_does_not_publish_partial_cache"] = () => {
            using(var cts=new System.Threading.CancellationTokenSource()) {
                var triangles=new ReadProbe<Triangle3>(Enumerable.Repeat(new Triangle3(
                    new Vec3(0,0,5),new Vec3(10,0,5),new Vec3(0,10,5)),1024).ToArray());
                var tin=new TinSurface("cancel",triangles,Array.Empty<ValidationIssue>());
                triangles.OnRead=i=>{if(i==300)cts.Cancel();};
                var sampler=new TinSectionSampler();
                ExpectCanceled(()=>sampler.BuildProfile(Profile(1,5).Line,tin,tin,cancellationToken:cts.Token));
                triangles.OnRead=null;
                var recovered=new TinSectionSampler().BuildProfile(Profile(1,5).Line,tin,tin);
                Check(recovered.HasData,"Canceled index poisoned sampler cache");
                Near(recovered.CutArea,0);Near(recovered.FillArea,0);
            }
        };
        tests["parallel_sections_cancel_from_progress_never_returns_partial_array"] = () => {
            using(var cts=new System.Threading.CancellationTokenSource()) {
                var sampler=new TinSectionSampler();var ht=Plane(5);var tk=Plane(0);
                var lines=new[]{Profile(1,5).Line,Profile(2,5).Line};
                ExpectCanceled(()=>sampler.BuildProfiles(lines,ht,tk,(done,total,name)=>cts.Cancel(),
                    cancellationToken:cts.Token));
                var fresh=sampler.BuildProfiles(lines,ht,tk);
                Check(fresh.Count==2,"Recovery lost sections");
                Near(fresh[0].CutArea,50);Near(fresh[1].CutArea,50);
            }
        };
        tests["volume_cancel_during_band_area_never_returns_partial_result"] = () => {
            using(var cts=new System.Threading.CancellationTokenSource()) {
                var line=Profile(1,5).Line;
                var segments=new ReadProbe<SectionProfileSegment>(Enumerable.Range(0,600).Select(i=>{
                    double a=i/60d,b=(i+1)/60d;
                    return new SectionProfileSegment(a,b,line.PointAt(a),line.PointAt(b),5,5,0,0);
                }).ToArray());
                var first=new SectionProfile(line,segments,50,0,Array.Empty<string>());
                var second=Profile(2,5);
                var system=new SectionSystem(new Vec2(1,0),new Vec2(0,1),Boundary,
                    new[]{line,second.Line},Array.Empty<string>());
                var ht=Plane(5);var tk=Plane(0);var calc=new SectionVolumeCalculator();
                var options=new VolumeCalculationOptions{FromLevel=0,ToLevel=10,LevelStep=5};
                segments.OnRead=i=>{if(i==300)cts.Cancel();};
                ExpectCanceled(()=>calc.Calculate(system,new[]{first,second},ht,tk,options,cts.Token));
                segments.OnRead=null;
                var result=calc.Calculate(system,new[]{first,second},ht,tk,options);
                Near(result.TotalCutVolume,1000);Near(result.TotalFillVolume,0);
                Check(result.IsComplete,"Canceled operation mutated inputs");
            }
        };

        tests["section_band_extent_filter_preserves_analytic_cut_fill_and_band_order"] = () => {
            var random = new Random(20261021);
            var calculator = new SectionBandAreaCalculator();
            for (int trial = 0; trial < 1000; trial++) {
                double ht = random.NextDouble()*100-50;
                double tk = random.NextDouble()*100-50;
                var profile = Profile(1, ht, tk);
                var bands = SectionVolumeCalculator.BuildBands(-100,100,5).ToArray();
                if (trial % 2 == 0) Array.Reverse(bands);
                var result = calculator.Calculate(profile,bands);
                for (int i=0;i<bands.Length;i++) {
                    double height = Math.Max(0,Math.Min(Math.Max(ht,tk),bands[i].UpperZ)-
                        Math.Max(Math.Min(ht,tk),bands[i].LowerZ));
                    Near(result.Bands[i].CutArea,ht>tk?height*10:0);
                    Near(result.Bands[i].FillArea,ht<tk?height*10:0);
                }
                Near(result.CutArea,10*Math.Max(0,ht-tk));
                Near(result.FillArea,10*Math.Max(0,tk-ht));
            }
            var line=Profile(1,1).Line;
            var crossing=new SectionProfile(line,new[]{new SectionProfileSegment(
                0,10,line.PointAt(0),line.PointAt(10),2,-2,0,0)},5,5,Array.Empty<string>());
            var split=calculator.Calculate(crossing,SectionVolumeCalculator.BuildBands(10,-10,1));
            Near(split.CutArea,5);Near(split.FillArea,5);
        };
        tests["tin_region_corner_tolerance_uses_distance_not_segment_length"] = () => {
            const double x=470000,y=2400000,tol=1e-5;
            var ring=new[]{new Vec2(x,y),new Vec2(x+1000,y),new Vec2(x+1000,y+1000),new Vec2(x,y+1000)};
            // A clipped mine-coordinate vertex may round a few ULPs outside
            // both incident edges. It is still much closer than the XY tolerance.
            var near=new Vec2(x-1e-9,y-1e-9);
            Check(SurfaceRegionClipper.ContainsInclusive(near,ring,tol),"Sub-tolerance corner rejected");
            Check(!SurfaceRegionClipper.ContainsInclusive(new Vec2(x-2*tol,y-2*tol),ring,tol),"Outside corner accepted");
            Check(SurfaceRegionClipper.ContainsInclusive(near,ring.Reverse().ToArray(),tol),"Ring direction changes tolerance");
            Check(!SurfaceRegionClipper.ContainsInclusive(new Vec2(x-.9*tol,y-.9*tol),ring,tol),
                "Endpoint tolerance became a square instead of a distance");
            Check(!SurfaceRegionClipper.ContainsInclusive(new Vec2(x+500,y-2*tol),ring,tol),
                "Perpendicular distance tolerance was widened");
        };
        tests["tin_segment_index_preserves_padded_candidates_at_mine_coordinates"] = () => {
            const double tol=1e-6;
            var random=new Random(613);
            var lines=new List<Segment3>();
            for(int i=0;i<400;i++) {
                double x=500000+random.NextDouble()*20,y=2300000+random.NextDouble()*20;
                lines.Add(new Segment3(new Vec3(x,y,0),new Vec3(x+random.NextDouble()*4,y+random.NextDouble()*4,0),i.ToString()));
            }
            // Crossings, overlaps, near-tolerance parallels, zero-length and long diagonals.
            foreach(double offset in new[]{0d,tol*.5,tol*1.5,tol*3})
                lines.Add(new Segment3(new Vec3(500000,2300000+offset,0),new Vec3(500020,2300000+offset,1),"edge"));
            lines.Add(new Segment3(new Vec3(500010,2300000,0),new Vec3(500010,2300020,1),"cross"));
            lines.Add(new Segment3(new Vec3(500010,2300010,0),new Vec3(500010,2300010,1),"point"));
            var index=new SurfaceInputPreparer.SegmentGridIndex(lines,tol,default(System.Threading.CancellationToken));
            for(int i=0;i<lines.Count;i++) {
                var hits=index.Query(i).ToArray();
                Check(hits.Length==hits.Distinct().Count(),"Repeated candidate");
                var actual=new HashSet<int>(hits);var a=lines[i];
                for(int j=0;j<lines.Count;j++) {
                    var b=lines[j];
                    bool expected=Math.Min(a.A.X,a.B.X)-tol<=Math.Max(b.A.X,b.B.X)+tol &&
                        Math.Max(a.A.X,a.B.X)+tol>=Math.Min(b.A.X,b.B.X)-tol &&
                        Math.Min(a.A.Y,a.B.Y)-tol<=Math.Max(b.A.Y,b.B.Y)+tol &&
                        Math.Max(a.A.Y,a.B.Y)+tol>=Math.Min(b.A.Y,b.B.Y)-tol;
                    Check(actual.Contains(j)==expected,"Padded candidate lost or disjoint candidate retained");
                }
            }
        };
        tests["multicad_grid_stress_10000_50000_100000_cells_analytic_volume"] = () => {
            foreach(int count in new[]{10000,50000,100000}) {
                const int columns=100;
                int rows=count/columns;
                var ring=new[]{new Vec2(0,0),new Vec2(columns,0),new Vec2(columns,rows),new Vec2(0,rows)};
                Func<double,TinSurface> surface=z=>new TinSurface("stress",new[]{
                    new Triangle3(new Vec3(0,0,z),new Vec3(columns,0,z),new Vec3(columns,rows,z)),
                    new Triangle3(new Vec3(0,0,z),new Vec3(columns,rows,z),new Vec3(0,rows,z))},Array.Empty<ValidationIssue>());
                int notified=0;
                var watch=System.Diagnostics.Stopwatch.StartNew();
                var report=new GridVolumeCalculator().Calculate(surface(5),surface(2),
                    new GridVolumeOptions{Boundary=ring,SizeX=1,SizeY=1,MaxCells=count},
                    (done,total)=>{Check(done>=notified&&total==count,"Invalid progress");notified=done;});
                Check(report.Cells.Count==count&&notified==count&&report.IsComplete,"Large grid incomplete");
                Check(Math.Abs(report.TotalCutVolume-3d*count)<1e-6,"Analytic stress cut differs");
                Near(report.TotalFillVolume,0);
                Console.WriteLine("{\"benchmark\":\"grid_cells\",\"cells\":"+count+",\"ms\":"+watch.ElapsedMilliseconds+"}");
            }
        };
        tests["multicad_grid_cancel_before_index_and_between_cells"] = () => {
            var ht=Plane(5);var tk=Plane(0);
            foreach(bool before in new[]{true,false}) {
                using(var cts=new System.Threading.CancellationTokenSource()) {
                    if(before)cts.Cancel();
                    bool cancelled=false;
                    try {new GridVolumeCalculator().Calculate(ht,tk,
                        new GridVolumeOptions{Boundary=Boundary,SizeX=.1,SizeY=.1},
                        (done,total)=>cts.Cancel(),cts.Token);}
                    catch(OperationCanceledException){cancelled=true;}
                    Check(cancelled,"Grid returned a completed report after cancellation");
                }
            }
            using(var cts=new System.Threading.CancellationTokenSource()) {
                cts.Cancel(); bool cancelled=false;
                try {ExactGridCellCalculator.CalculatePrepared(ht.Triangles,tk.Triangles,Boundary,
                    ExactGridCellCalculator.PrepareBoundary(Boundary),cts.Token);}
                catch(OperationCanceledException){cancelled=true;}
                Check(cancelled,"Dense-cell overlay ignores cancellation");
            }
        };
        tests["multicad_grid_overlap_outside_selected_boundary_does_not_block"] = () => {
            var cell = new[]{new Vec2(0,0),new Vec2(10,0),new Vec2(10,10),new Vec2(0,10)};
            var region = new[]{new Vec2(0,0),new Vec2(2,0),new Vec2(2,2),new Vec2(0,2)};
            var baseFaces = new[]{
                new Triangle3(new Vec3(0,0,5),new Vec3(10,0,5),new Vec3(10,10,5)),
                new Triangle3(new Vec3(0,0,5),new Vec3(10,10,5),new Vec3(0,10,5))};
            var extra = new Triangle3(new Vec3(7,7,20),new Vec3(9,7,20),new Vec3(9,9,20));
            var design = baseFaces.Select(t=>new Triangle3(new Vec3(t.A.X,t.A.Y,0),
                new Vec3(t.B.X,t.B.Y,0),new Vec3(t.C.X,t.C.Y,0))).ToArray();
            var result=ExactGridCellCalculator.Calculate(baseFaces.Concat(new[]{extra}).ToArray(),design,cell,region);
            Near(result.CalculationArea,4); Near(result.CoveredArea,4); Near(result.CutVolume,20);
            var hole=new[]{new Vec2(7,7),new Vec2(9,7),new Vec2(9,9),new Vec2(7,9)};
            var holed=ExactGridCellCalculator.Calculate(baseFaces.Concat(new[]{extra}).ToArray(),design,cell,
                cell,new IReadOnlyList<Vec2>[]{hole});
            Near(holed.CalculationArea,96); Near(holed.CoveredArea,96); Near(holed.CutVolume,480);
        };
        tests["multicad_grid_subnanometre_sign_change_keeps_cut_and_fill"] = () => {
            var cell=new[]{new Vec2(0,0),new Vec2(100,0),new Vec2(100,100),new Vec2(0,100)};
            var ht=new[]{
                new Triangle3(new Vec3(0,0,-1e-10),new Vec3(100,0,1e-10),new Vec3(100,100,1e-10)),
                new Triangle3(new Vec3(0,0,-1e-10),new Vec3(100,100,1e-10),new Vec3(0,100,-1e-10))};
            var tk=ht.Select(t=>new Triangle3(new Vec3(t.A.X,t.A.Y,0),new Vec3(t.B.X,t.B.Y,0),new Vec3(t.C.X,t.C.Y,0))).ToArray();
            var result=ExactGridCellCalculator.Calculate(ht,tk,cell);
            // Each half: 5,000 m2 * mean depth 0.5e-10 m, analytically.
            Check(Math.Abs(result.CutVolume-2.5e-7)<1e-16,"Small cut was lost");
            Check(Math.Abs(result.FillVolume-2.5e-7)<1e-16,"Small fill was lost");
        };
        tests["normalizer_cell_keys_preserve_neighbors_at_positive_and_negative_mine_coordinates"] = () => {
            foreach(double origin in new[]{0d,2500000d,-2500000d}) {
                var raw=new[]{new Vec3(origin,450000,5),new Vec3(origin+0.0000005,450000,5),
                    new Vec3(origin+10,450000,5),new Vec3(origin+10,450010,5),new Vec3(origin,450010,5)};
                var options=new SurfaceBuildOptions();
                var input=new SurfaceInputPreparer().PrepareRaw(raw,null,options);
                Check(input.Sites.Count==4 && !input.HasErrors,"Neighbor duplicate changed after cell-key optimization");
                var mesh=new MiningVolume.Surface.ConformingTinBuilder().Build("translated-cell-test",input,options);
                Near(mesh.Triangles.Sum(t=>t.Area2D),100);
                Check(raw.Length==5 && raw[1].Z==5,"Source was changed");
            }
        };
        tests["p0_unresolved_breaklines_are_relaxed_only_at_conflict"] = () => {
            var prepared=new PreparedSurfaceInput();
            prepared.Sites.AddRange(new[]{new Vec3(0,0,1),new Vec3(0,10,1),
                new Vec3(2,5,10),new Vec3(8,5,10),new Vec3(5,2,20),new Vec3(5,8,20)});
            prepared.Breaklines.Add(new Segment3(new Vec3(0,0,1),new Vec3(0,10,1),"SAFE-CONTOUR"));
            prepared.Breaklines.Add(new Segment3(new Vec3(2,5,10),new Vec3(8,5,10),"CONFLICT-A"));
            prepared.Breaklines.Add(new Segment3(new Vec3(5,2,20),new Vec3(5,8,20),"CONFLICT-B"));
            var crossing=new ConflictGroup{Position=new Vec2(5,5)};
            crossing.Sources.Add(new ConflictSource{Z=10,Generated=true});
            crossing.Sources.Add(new ConflictSource{Z=20,Generated=true});
            prepared.VerticalConflictGroups.Add(crossing);
            prepared.Issues.Add(new ValidationIssue(ValidationSeverity.Error,
                "BREAKLINE_NORMALIZE_CROSSING_FAILED","Conflicting crossing after snap."));
            var method=typeof(SurfaceInputPreparer).GetMethod("RecoverConflictingContourTopology",
                System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            Check(method!=null,"Local conflict resolver not found");
            method.Invoke(null,new object[]{prepared,
                new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true},
                System.Threading.CancellationToken.None});
            Check(!prepared.HasErrors,"Locally resolved crossing still blocks TIN");
            Check(prepared.Breaklines.Count==1 &&
                prepared.Breaklines[0].SourceId=="SAFE-CONTOUR",
                "Unrelated safe contour was removed by fallback");
            Check(prepared.RelaxedBreaklineConstraints==2,
                "Only the two conflicting segments may be locally relaxed");
            Check(prepared.Issues.Any(x=>x.Code=="LOCALLY_RELAXED_UNRESOLVED_TOPOLOGY"),
                "Missing audit warning for local constraint relaxation");
            Check(prepared.Sites.Count==6,"Conflict recovery changed original survey sites");
        };
        tests["p0_unrecoverable_validation_error_must_not_clear_constraints"] = () => {
            var prepared=new PreparedSurfaceInput();
            prepared.Breaklines.Add(new Segment3(new Vec3(0,0,0),new Vec3(10,0,0),"SAFE"));
            var group=new ConflictGroup{Position=new Vec2(5,5)};
            group.Sources.Add(new ConflictSource{Z=0});
            group.Sources.Add(new ConflictSource{Z=10});
            prepared.VerticalConflictGroups.Add(group);
            prepared.Issues.Add(new ValidationIssue(ValidationSeverity.Error,
                "TOO_FEW_POINTS","Invalid source must remain invalid."));
            var method=typeof(SurfaceInputPreparer).GetMethod("RecoverConflictingContourTopology",
                System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            method.Invoke(null,new object[]{prepared,
                new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true},
                System.Threading.CancellationToken.None});
            Check(prepared.HasErrors && prepared.Breaklines.Count==1,
                "Unsupported error must fail closed without losing CAD constraints");
        };
        tests["p0_unattributed_topology_error_must_remain_blocking"] = () => {
            var prepared=new PreparedSurfaceInput();
            prepared.Sites.AddRange(new[]{
                new Vec3(0,0,0),new Vec3(10,0,0),new Vec3(10,10,0),new Vec3(0,10,0)
            });
            prepared.Breaklines.Add(new Segment3(new Vec3(0,0,0),new Vec3(10,0,0),"SAFE"));
            var group=new ConflictGroup{Position=new Vec2(5,5)};
            group.Sources.Add(new ConflictSource{Z=0});
            group.Sources.Add(new ConflictSource{Z=10});
            prepared.VerticalConflictGroups.Add(group);
            prepared.Issues.Add(new ValidationIssue(ValidationSeverity.Error,
                "BREAKLINE_NORMALIZE_CROSSING_FAILED",
                "Unattributed topology failure must not be downgraded"));
            var method=typeof(SurfaceInputPreparer).GetMethod("RecoverConflictingContourTopology",
                System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            method.Invoke(null,new object[]{prepared,
                new SurfaceBuildOptions{PreserveDuplicateXYVerticalFaces=true},
                System.Threading.CancellationToken.None});
            Check(prepared.HasErrors && prepared.Breaklines.Count==1 &&
                prepared.RelaxedBreaklineConstraints==0,
                "Topology error silently accepted without an identifiable unsafe edge");
        };
        tests["rf11_catalog_retains_current_legal_names"] = () => {
            Check(!MiningUnitCatalog.CoalUnits.Any(x=>x.Contains("Tây Nam Đá Mài")), "Merged company still listed independently");
            Check(MiningUnitCatalog.MineralUnits.Contains("Công ty TNHH MTV Nhôm Lâm Đồng - TKV"), "Incorrect legal form");
        };
        tests["v2_selected_tkv_unit_appears_below_parent_in_all_excel_sheets"]=()=>{
            var state=State();
            string groupBefore=state.MiningUnitGroup, nameBefore=state.MiningUnitName;
            try {
                state.MiningUnitGroup=MiningUnitCatalog.Coal;
                state.MiningUnitName="Công ty Cổ phần Than Cao Sơn - TKV";
                Check(MiningUnitCatalog.CoalUnits.Contains(state.MiningUnitName),
                    "Requested Cao Sơn legal name missing");
                Check(MiningUnitCatalog.CoalUnits.Contains(
                    "Công ty Cổ phần Than Đèo Nai - Cọc Sáu - TKV"),
                    "Requested Đèo Nai - Cọc Sáu name missing");
                Check(MiningUnitCatalog.CoalUnits.Contains(
                    "Công ty Cổ phần Than Hà Tu - Vinacomin"),
                    "Requested Hà Tu name missing");
                var report=ExportWorkflowService.BuildReport(state,new ExportOptions());
                var summary=report.Sheets.Single(x=>x.Name=="Tổng khối");
                string originalFormula=(string)summary.Rows[0][2].Value;
                ExportWorkflowService.ApplyOrganizationHeader(report,state);
                string parent=ExportWorkflowService.TkvParentHeading;
                string expected=parent+"\n"+state.MiningUnitName+"\n";
                Check(report.Sheets.All(x=>x.Title.StartsWith(expected,
                    StringComparison.Ordinal)), "TKV parent and selected unit are not stacked in sheet header");
                Check((string)summary.Rows[0][2].Value==originalFormula,
                    "Company heading changed formula links/row indexes");
                ExportWorkflowService.ApplyOrganizationHeader(report,state);
                Check(report.Sheets.All(x=>x.Title.StartsWith(expected,
                    StringComparison.Ordinal) && x.Title.IndexOf(parent,expected.Length,
                    StringComparison.Ordinal)<0), "Duplicated group header");
                state.MiningUnitName="Công ty Cổ phần Than Hà Tu - Vinacomin";
                ExportWorkflowService.ApplyOrganizationHeader(report,state);
                Check(report.Sheets.All(x=>x.Title.StartsWith(parent+"\n"+
                    state.MiningUnitName+"\n",StringComparison.Ordinal) &&
                    !x.Title.Contains("Công ty Cổ phần Than Cao Sơn - TKV")),
                    "Changing selected organization stacked the old company in Excel");

                state.MiningUnitGroup=MiningUnitCatalog.OutsideTkv;
                state.MiningUnitName="Công ty TNHH Mỏ ngoài TKV";
                var outsideReport=ExportWorkflowService.BuildReport(state,new ExportOptions());
                ExportWorkflowService.ApplyOrganizationHeader(outsideReport,state);
                Check(outsideReport.Sheets.All(x=>
                    x.Title.StartsWith(state.MiningUnitName+"\n",StringComparison.Ordinal)
                    && !x.Title.Contains(parent)), "Non-TKV unit falsely shown under TKV");
                Check(outsideReport.Sheets.Single(x=>x.Name=="Thông tin").Rows
                    .Any(row=>row.Any(cell=>Equals(cell.Value,state.MiningUnitName))),
                    "Non-TKV unit missing in information worksheet");
                state.MiningUnitName=" ";
                bool rejected=false;
                try{ExportWorkflowService.BuildReport(state,new ExportOptions());}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Missing external-unit name must block misleading Excel export");
            } finally {
                state.MiningUnitGroup=groupBefore;
                state.MiningUnitName=nameBefore;
            }
        };
        tests["v2_async_excel_export_keeps_original_project_organization"]=()=>{
            var st=State();
            string previousGroup=st.MiningUnitGroup,previousName=st.MiningUnitName;
            try {
                st.MiningUnitGroup=MiningUnitCatalog.Coal;
                st.MiningUnitName="Công ty Cổ phần Than Cao Sơn - TKV";
                var report=ExportWorkflowService.BuildReport(st,new ExportOptions());
                var summary=report.Sheets.Single(x=>x.Name=="Tổng khối");
                var info=report.Sheets.Single(x=>x.Name=="Thông tin");
                // Emulate switching DWG / project after workbook generation and
                // before the background Excel writer starts.
                st.MiningUnitGroup=MiningUnitCatalog.OutsideTkv;
                st.MiningUnitName="Công ty ngoài TKV hoàn toàn khác";
                string expected=ExportWorkflowService.TkvParentHeading+"\n"+
                    "Công ty Cổ phần Than Cao Sơn - TKV\n";
                Check(report.Sheets.All(x=>x.Title.StartsWith(expected,StringComparison.Ordinal)),
                    "Report header changed after switching Project while exporting");
                Check(info.Rows.Any(r=>r.Any(c=>Equals(c.Value,"Công ty Cổ phần Than Cao Sơn - TKV"))),
                    "Original unit missing from report metadata snapshot");
                Check(!summary.Rows.SelectMany(r=>r).Any(c=>Equals(c.Value,st.MiningUnitName)),
                    "The new project's unit contaminated the old report");

                // This must remain true even if the report goes through the
                // real writer once ProjectState.Current has changed.
                string dir=Path.Combine(Path.GetTempPath(),"imsat-unit-snapshot-"+Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                try {
                    string file=Path.Combine(dir,"unit-async.xlsx");
                    ExportWorkflowService.WriteXlsx(file,report);
                    using(var archive=ZipFile.OpenRead(file))
                    using(var stream=archive.GetEntry("xl/worksheets/sheet1.xml").Open()){
                        var xml=XDocument.Load(stream);
                        string cell=xml.Descendants().Where(x=>x.Name.LocalName=="c"
                            && (string)x.Attribute("r")=="A1")
                            .Descendants().First(x=>x.Name.LocalName=="t").Value;
                        Check(cell.StartsWith(expected,StringComparison.Ordinal),
                            "Excel writer relabeled frozen volume report with a later project");
                        Check(!cell.Contains("ngoài TKV hoàn toàn khác"),
                            "Background XLSX I/O read mutable ProjectState.Current");
                    }
                } finally {Directory.Delete(dir,true);}
            } finally {
                st.MiningUnitGroup=previousGroup;
                st.MiningUnitName=previousName;
            }
        };
        tests["v2_report_discloses_actual_gap_after_manual_station_edits"]=()=>{
            var st=State();
            var before=st.SectionSystem;
            double priorSpacing=st.SectionSpacing;
            try {
                var boundary=new[]{new Vec2(0,0),new Vec2(10,0),
                    new Vec2(10,60),new Vec2(0,60)};
                var lines=new[]{Profile(1,10).Line,Profile(3,10).Line};
                st.SectionSystem=new SectionSystem(new Vec2(1,0),
                    new Vec2(0,1),boundary,lines,Array.Empty<string>());
                st.SectionSpacing=20.0;
                var info=ExportWorkflowService.BuildReport(st,new ExportOptions())
                    .Sheets.Single(x=>x.Name=="Thông tin");
                var actual=info.Rows.Single(row=>
                    row.Count>=2&&Equals(row[0].Value,
                        "Khoảng cách tuyến lớn nhất thực tế, m"));
                Check(Equals(actual[1].Value,"40"),
                    "Actual manual section gap not disclosed: expected 40 m");
                var configured=info.Rows.Single(row=>
                    row.Count>=2&&Equals(row[0].Value,"Bước tuyến thiết lập, m"));
                Check(Equals(configured[1].Value,"20"),
                    "Original requested station spacing should remain separately visible");
            } finally {
                st.SectionSystem=before;
                st.SectionSpacing=priorSpacing;
            }
        };
        tests["rf03_full_grip_plus_one_area_and_volume"]=()=>{
            var a=Profile(1,5);var b=Profile(2,5);var old=Calculate(a,b);
            var ea=Edit(a,6,6);var eb=Edit(b,6,6);var now=Calculate(ea,eb);
            Near(ea.CutArea-a.CutArea,10);Near(now.TotalCutVolume-old.TotalCutVolume,200);
            Near(now.TotalCutVolume,1200);Check(!now.Intervals[0].HasMidArea,"Edited result contains stale TIN midpoint");
            Near(a.CutArea,50);Near(a.Segments[0].ExistingZ0,5);
        };
        tests["rf03_one_grip_plus_one_and_geometry_race"]=()=>{
            var p=Profile(1,5);var q=Edit(p,5,6);Near(q.CutArea-p.CutArea,5);
            Check(!EditedSectionProfileBuilder.SameGeometry(new[]{p},new[]{q}),"Concurrent edit not detected");
            Check(EditedSectionProfileBuilder.SameGeometry(new[]{q},new[]{Edit(p,5,6)}),"Equivalent geometry rejected");
        };
        tests["rf03_nonlinear_multilevel_authoritative_contributions"]=()=>{
            // Band 0..5: areas 50,50 -> 1000. Band 5..10: 50,10 -> frustum.
            // Whole-area formula would give 1600, not the sum of band formulas.
            var r=Calculate(Edit(Profile(1,10),10,10),Edit(Profile(2,6),6,6));
            double expected=1000+20*(50+10+Math.Sqrt(500))/3;
            Near(r.TotalCutVolume,expected);Near(r.Levels.Sum(x=>x.CutVolume),expected);
            Near(r.Intervals.Single().BandVolumes.Sum(x=>x.CutVolume),expected);
        };
        tests["rf03_level_parameters_never_silently_truncated"]=()=>{
            foreach(var a in new[]{new[]{0d,1d,double.NaN},new[]{0d,double.PositiveInfinity,5d},new[]{0d,100001d,1d}}){
                bool rejected=false;try{SectionVolumeCalculator.BuildBands(a[0],a[1],a[2]);}catch(ArgumentException){rejected=true;}
                Check(rejected,"Invalid/oversized band input accepted");}
            var bands=SectionVolumeCalculator.BuildBands(11,0,5);Check(bands.Count==3,"Descending bands wrong");Near(bands.Last().EndZ,0);
        };
        tests["hsm0_vertical_faces_require_section_review_but_integrate_valid_planar_TIN"]=()=>{
            var center = new ConflictGroup { Position = new Vec2(5,5) };
            center.Sources.Add(new ConflictSource {Z=5,VertexIndex=0});
            center.Sources.Add(new ConflictSource {Z=9,VertexIndex=0});
            var support=Plane(5);
            var vertical=new TinSurface("vertical",support.Triangles,
                Array.Empty<ValidationIssue>(),
                new[]{new Triangle3(new Vec3(5,5,5),new Vec3(5,5,9),new Vec3(10,5,5))},
                new[]{center});
            var design=Plane(0);
            var profiles=new[]{Profile(1,5),Profile(2,5)};
            var sectionSystem=new SectionSystem(new Vec2(1,0),new Vec2(0,1),
                Boundary,profiles.Select(p=>p.Line).ToArray(),Array.Empty<string>());
            var volumeOptions=new VolumeCalculationOptions{FromLevel=0,ToLevel=10,LevelStep=5};
            var calculated=new SectionVolumeCalculator().Calculate(
                sectionSystem,profiles,vertical,design,volumeOptions);
            Check(calculated.IsComplete && calculated.TotalCutVolume>0 &&
                calculated.ExcludedIntervals==0,
                "Valid profile crossing zero-XY-area vertical wall was incorrectly rejected");
            var localGrid=new GridVolumeCalculator().Calculate(vertical,design,
                new GridVolumeOptions{Boundary=Boundary,SizeX=5,SizeY=5});
            Check(localGrid.AmbiguousCells==0 && localGrid.IsComplete,
                "Planar grid wrongly excluded cells touching zero-area vertical faces");
            Check(localGrid.Cells.All(c=>c.Volume.IsComplete) &&
                localGrid.TotalCutVolume>0,
                "Valid 2.5D support triangles were not integrated");
            var reviewed=profiles.Select(p=>Edit(p,5,5)).ToArray();
            var volumes=new SectionVolumeCalculator().Calculate(sectionSystem,reviewed,vertical,design,volumeOptions);
            Check(volumes.TotalCutVolume>0,"Accepted section volume did not calculate");
        };
        tests["rf18_remote_double_z_must_not_block_safe_grid_or_sections"]=()=>{
            var far=new ConflictGroup{Position=new Vec2(100,100)};
            far.Sources.Add(new ConflictSource{Z=10,VertexIndex=0});
            far.Sources.Add(new ConflictSource{Z=20,VertexIndex=1});
            var support=Plane(5);
            var remote=new TinSurface("remote",support.Triangles,
                Array.Empty<ValidationIssue>(),
                new[]{new Triangle3(new Vec3(100,100,10),
                    new Vec3(100,100,20),new Vec3(110,100,10))},
                new[]{far});
            var plain=Plane(0);
            var grid=new GridVolumeCalculator().Calculate(remote,plain,
                new GridVolumeOptions{Boundary=Boundary,SizeX=5,SizeY=5});
            Check(grid.IsComplete&&grid.AmbiguousCells==0,
                "Unrelated double-Z point blocked safe grid cells");
            Near(grid.TotalCutVolume,1000);
            var a=Profile(1,5);var d=Profile(2,5);
            var section=new SectionSystem(new Vec2(1,0),new Vec2(0,1),
                Boundary,new[]{a.Line,d.Line},Array.Empty<string>());
            var volumes=new SectionVolumeCalculator().Calculate(section,
                new[]{a,d},remote,plain,
                new VolumeCalculationOptions{FromLevel=0,ToLevel=10,LevelStep=5});
            Check(volumes.IsComplete && volumes.ExcludedIntervals==0 &&
                volumes.TotalCutVolume>0,"Remote double-Z blocked valid section volume");
        };
        tests["rf19_prepared_boundary_matches_exact_cell_and_supports_over_5000"]=()=>{
            var tn=Plane(5);var tk=Plane(0);
            var cell=new Vec2[]{new Vec2(2,3),new Vec2(7,3),
                new Vec2(7,8),new Vec2(2,8)};
            var direct=ExactGridCellCalculator.Calculate(
                tn.Triangles,tk.Triangles,cell,Boundary);
            var clip=ExactGridCellCalculator.PrepareBoundary(Boundary);
            var reused=ExactGridCellCalculator.CalculatePrepared(
                tn.Triangles,tk.Triangles,cell,clip);
            Near(direct.CalculationArea,reused.CalculationArea);
            Near(direct.CoveredArea,reused.CoveredArea);
            Near(direct.CutVolume,reused.CutVolume);
            Near(direct.FillVolume,reused.FillVolume);
            var grid=new GridVolumeCalculator().Calculate(tn,tk,
                new GridVolumeOptions{Boundary=Boundary,SizeX=0.2,SizeY=0.19});
            Check(grid.Cells.Count>5000&&grid.IsComplete,
                "More-than-5,000-cell calculation failed with one pretriangulated boundary");
            Near(grid.TotalCutVolume,1000);
        };
        tests["rf20_zero_plan_area_vertical_curtain_does_not_void_support_triangles"]=()=>{
            var conflict=new ConflictGroup{Position=new Vec2(0,0)};
            conflict.Sources.Add(new ConflictSource{Z=5,VertexIndex=0});
            conflict.Sources.Add(new ConflictSource{Z=9,VertexIndex=1});
            var baseTin=Plane(5);
            var vertical=new TinSurface("multiZ",baseTin.Triangles,
                Array.Empty<ValidationIssue>(),
                new[]{new Triangle3(new Vec3(0,0,5),new Vec3(0,0,9),
                    new Vec3(.2,0,5))},new[]{conflict});
            var roots=new VerticalConflictFootprint.ConflictRootIndex(vertical,Plane(0));
            Check(roots.Contains(new Vec2(0,0))&&roots.AnyIncident(baseTin.Triangles),
                "Collapsed multi-Z root not indexed in support triangles");
            Check(!roots.Contains(new Vec2(5,5)),
                "Conflict-root index falsely matches a remote point");
            var grid=new GridVolumeCalculator().Calculate(vertical,Plane(0),
                new GridVolumeOptions{Boundary=Boundary,SizeX=2,SizeY=2});
            Check(grid.AmbiguousCells==0 && grid.IsComplete,
                "Zero-area vertical curtain must not quarantine covered planar TIN cells");
            Near(grid.TotalCutVolume,1000);
            Check(grid.Cells.All(c=>c.Volume.IsComplete),
                "Planar triangles unexpectedly lost coverage near a vertical curtain");
            // Neither the section routes nor the upright face at y=0 crosses
            // this strip, but the incident support triangles cover it.
            var nearA = new SectionLine(41,"CUT-41",6,new Vec2(1,0),
                new Vec2(0,1),new[]{new SectionSpan(0,10)});
            var nearB = new SectionLine(42,"CUT-42",8,new Vec2(1,0),
                new Vec2(0,1),new[]{new SectionSpan(0,10)});
            SectionProfile Flat(SectionLine line) =>
                new SectionProfile(line,
                    new[]{new SectionProfileSegment(0,10,line.PointAt(0),
                        line.PointAt(10),5,5,0,0)},50,0,Array.Empty<string>());
            var validSections=new SectionVolumeCalculator().Calculate(
                new SectionSystem(new Vec2(1,0),new Vec2(0,1),
                    Boundary,new[]{nearA,nearB},Array.Empty<string>()),
                new[]{Flat(nearA),Flat(nearB)},vertical,Plane(0),
                new VolumeCalculationOptions{FromLevel=0,ToLevel=10,LevelStep=5});
            Check(validSections.IsComplete && validSections.TotalCutVolume>0,
                "Zero-area vertical face wrongly excluded a valid section strip");
        };
        tests["rf27_offrange_elevation_bands_never_fake_zero_volume"]=()=>{
            var a=Profile(1,5);var b=Profile(2,5);
            var system=new SectionSystem(new Vec2(1,0),new Vec2(0,1),
                Boundary,new[]{a.Line,b.Line},Array.Empty<string>());
            bool rejected=false;
            try{
                new SectionVolumeCalculator().Calculate(system,new[]{a,b},
                    Plane(5),Plane(0),new VolumeCalculationOptions{
                        FromLevel=300,ToLevel=400,LevelStep=5});
            }catch(InvalidOperationException ex){
                rejected=ex.Message.Contains("không chứa cao độ TIN") &&
                    ex.Message.Contains("Đề nghị tính từ");
            }
            Check(rejected,
                "Off-elevation band produced zero volume instead of a clear measured-Z error");
            var result=new SectionVolumeCalculator().Calculate(
                system,new[]{a,b},Plane(5),Plane(0),
                new VolumeCalculationOptions{FromLevel=0,ToLevel=10,LevelStep=5});
            Check(result.TotalCutVolume>0,"Valid corrected level bands lost quantities");
        };
        tests["rf21_missing_elevation_in_one_section_must_not_certify_partial_volume"]=()=>{
            var a=Profile(1,5);var b=Profile(2,5);
            var missing=new SectionProfile(a.Line,a.Segments,a.CutArea,a.FillArea,
                new[]{"MC-1: có đoạn thiếu giao TIN hiện trạng/thiết kế; không đủ dữ liệu."});
            var system=new SectionSystem(new Vec2(1,0),new Vec2(0,1),
                Boundary,new[]{a.Line,b.Line},Array.Empty<string>());
            var options=new VolumeCalculationOptions{
                FromLevel=0,ToLevel=10,LevelStep=5};
            bool failed=false;
            try {
                new SectionVolumeCalculator().Calculate(system,
                    new[]{missing,b},Plane(5),Plane(0),options);
            } catch(InvalidOperationException) { failed=true; }
            Check(failed,"A partly sampled section was erroneously certified as a full-volume result");
        };
        tests["rf22_grid_missing_tin_diagnostics_are_specific_and_do_not_claim_zero"]=()=>{
            var ht=Plane(5);
            var tk=Plane(0);
            var shifted= new TinSurface("design-far",
                tk.Triangles.Select(triangle=>new Triangle3(
                    new Vec3(triangle.A.X+100,triangle.A.Y+100,triangle.A.Z),
                    new Vec3(triangle.B.X+100,triangle.B.Y+100,triangle.B.Z),
                    new Vec3(triangle.C.X+100,triangle.C.Y+100,triangle.C.Z))).ToArray(),
                Array.Empty<ValidationIssue>());
            var opts=new GridVolumeOptions{Boundary=Boundary,SizeX=5,SizeY=5};
            var report=new GridVolumeCalculator().Calculate(ht,shifted,opts);
            Check(report.Cells.Count>0 && !report.IsComplete &&
                report.MissingDesignCells==report.Cells.Count &&
                report.MissingExistingCells==0,
                "Missing design-TIN cells were not identified separately from existing TIN");
            Check(report.Cells.All(c=>c.Volume.CoveredArea==0 &&
                c.ExistingCoveredArea>=c.Volume.CalculationArea-1e-7 &&
                c.DesignCoveredArea==0 &&
                c.CoverageStatus=="Thiếu phủ TIN thiết kế"),
                "Missing design-TIN volume falsely reported as an authoritative zero");
            var reversed=new GridVolumeCalculator().Calculate(shifted,ht,opts);
            Check(reversed.MissingExistingCells==reversed.Cells.Count &&
                reversed.MissingDesignCells==0 &&
                reversed.Cells.All(c=>c.CoverageStatus=="Thiếu phủ TIN hiện trạng"),
                "Missing existing-TIN cells were misclassified");
            var reportXlsx=GridVolumeExcelReportBuilder.Build(report,opts);
            var detail=reportXlsx.Sheets.First(x=>x.Name=="CHI TIẾT Ô LƯỚI");
            Check(detail.Rows.Count>0 &&
                detail.Rows.All(row=>row[7].Value.ToString()=="" &&
                    row[8].Value.ToString()=="" &&
                    row[9].Value.ToString()==""),
                "Unmeasured cell got fake 0.00 m³ in Excel");
        };
        tests["rf24_vertical_step_3D_wall_zero_plan_area_grid_has_both_cut_and_fill"]=()=>{
            // One actual step face: the same XY at x=1 has two elevations
            // 8 m and 12 m. The vertical face has zero projected area.
            // Two planar support pieces still cover a 2x2 m grid completely.
            var ht=new TinSurface("ht",new[]{
                new Triangle3(new Vec3(0,0,10),new Vec3(2,0,10),new Vec3(2,2,10)),
                new Triangle3(new Vec3(0,0,10),new Vec3(2,2,10),new Vec3(0,2,10))
            },Array.Empty<ValidationIssue>());
            var tk=new TinSurface("tk",new[]{
                new Triangle3(new Vec3(0,0,8),new Vec3(1,0,8),new Vec3(1,2,8)),
                new Triangle3(new Vec3(0,0,8),new Vec3(1,2,8),new Vec3(0,2,8)),
                new Triangle3(new Vec3(1,0,12),new Vec3(2,0,12),new Vec3(2,2,12)),
                new Triangle3(new Vec3(1,0,12),new Vec3(2,2,12),new Vec3(1,2,12))
            },Array.Empty<ValidationIssue>(),
            new[]{
                new Triangle3(new Vec3(1,0,8),new Vec3(1,0,12),new Vec3(1,2,8)),
                new Triangle3(new Vec3(1,2,8),new Vec3(1,0,12),new Vec3(1,2,12))
            },
            new[]{
                new ConflictGroup{Position=new Vec2(1,0)},
                new ConflictGroup{Position=new Vec2(1,2)}
            });
            var r=new GridVolumeCalculator().Calculate(ht,tk,
                new GridVolumeOptions{Boundary=new[]{
                    new Vec2(0,0),new Vec2(2,0),
                    new Vec2(2,2),new Vec2(0,2)},SizeX=1,SizeY=1});
            Check(r.IsComplete && r.AmbiguousCells==0 && r.Cells.Count==4,
                "Wall with zero XY area removed covered grid cells");
            Near(r.TotalCutVolume,4.0);
            Near(r.TotalFillVolume,4.0);
            Near(r.TotalNoDataArea,0.0);
            Check(r.Cells.All(c=>c.Volume.IsComplete),
                "Vertical step wrongly marked as missing TIN");
        };
        tests["rf25_overlapping_planar_multisheet_must_fail_not_fabricate_volume"]=()=>{
            var footprint=new[]{
                new Vec2(0,0),new Vec2(2,0),
                new Vec2(2,2),new Vec2(0,2)};
            var ht=Plane(10);
            var lower=new Triangle3(new Vec3(0,0,5),new Vec3(2,0,5),new Vec3(2,2,5));
            var upper=new Triangle3(new Vec3(0,0,12),new Vec3(2,0,12),new Vec3(2,2,12));
            var tk=new TinSurface("overlapping-sheets",
                new[]{lower,upper},Array.Empty<ValidationIssue>());
            bool refused=false;
            try{
                new GridVolumeCalculator().Calculate(ht,tk,
                    new GridVolumeOptions{Boundary=footprint,SizeX=1,SizeY=1});
            }catch(InvalidOperationException){refused=true;}
            Check(refused,"Overlapping planar TIN sheets were silently accepted as a single elevation");
        };
        tests["rf26_actual_KLmb_DXF_3DFACE_vertical_wall_grid_reference"]=()=>{
            // Extracted, XY-translated (to avoid large coordinates) from the
            // user's real KL mb.dxf: 8 existing + 17 design planar 3DFACEs.
            // Region origin in original drawing: 472468.6 / 2399099.1.
            // Independent Shapely geometry reference: area=0.81 square m,
            // cut=0.00062917912154 m3 and fill=0.15513678565021 m3.
            var ht=new TinSurface("KLmb-HT",new[]{
                new Triangle3(new Vec3(1.09057541261,1.01273436192,357.05669195741),new Vec3(-0.139397529885,1.71029254608,357.1114133271),new Vec3(0.231725389545,0.530345237814,357)),
                new Triangle3(new Vec3(-0.906560207368,0.512142819818,357),new Vec3(-2.97682430857,-5.42806296702,356.5),new Vec3(0.231725389545,0.530345237814,357)),
                new Triangle3(new Vec3(-1.95220935857,-6.51513914717,356.5),new Vec3(0.231725389545,0.530345237814,357),new Vec3(-2.97682430857,-5.42806296702,356.5)),
                new Triangle3(new Vec3(-1.0782808548,2.24276421685,357.16284945242),new Vec3(-0.906560207368,0.512142819818,357),new Vec3(0.231725389545,0.530345237814,357)),
                new Triangle3(new Vec3(-1.0782808548,2.24276421685,357.16284945242),new Vec3(0.231725389545,0.530345237814,357),new Vec3(-0.139397529885,1.71029254608,357.1114133271)),
                new Triangle3(new Vec3(0.231725389545,0.530345237814,357),new Vec3(-1.95220935857,-6.51513914717,356.5),new Vec3(1.20201016305,0.491533867083,357)),
                new Triangle3(new Vec3(1.4679673083,0.79870298272,357.03486669317),new Vec3(1.09057541261,1.01273436192,357.05669195741),new Vec3(0.231725389545,0.530345237814,357)),
                new Triangle3(new Vec3(1.4679673083,0.79870298272,357.03486669317),new Vec3(0.231725389545,0.530345237814,357),new Vec3(1.20201016305,0.491533867083,357))
            },Array.Empty<ValidationIssue>());
            var root=new ConflictGroup{Position=new Vec2(0.463389133045,0.521078692749)};
            root.Sources.Add(new ConflictSource{Z=357,VertexIndex=0});
            root.Sources.Add(new ConflictSource{Z=357.14541593259,VertexIndex=1});
            var tk=new TinSurface("KLmb-TK",new[]{
                new Triangle3(new Vec3(0.231725389545,0.530345237814,357),new Vec3(-2.97682430857,-5.42806296702,356.5),new Vec3(0.463389133045,0.521078692749,357)),
                new Triangle3(new Vec3(0.570006424095,1.30796618899,357.5),new Vec3(2.31732670922,-9.13237032807,357.5),new Vec3(0.719438603439,1.2232182757,357.5)),
                new Triangle3(new Vec3(-0.139397529885,1.71029254608,357.1114133271),new Vec3(0.231725389545,0.530345237814,357),new Vec3(0.142959203338,1.55015873909,357.08743363075)),
                new Triangle3(new Vec3(0.231725389545,0.530345237814,357),new Vec3(-0.139397529885,1.71029254608,357.1114133271),new Vec3(-1.0782808548,2.24276421685,357.16284945242)),
                new Triangle3(new Vec3(0.231725389545,0.530345237814,357),new Vec3(-1.0782808548,2.24276421685,357.16284945242),new Vec3(-0.906560207368,0.512142819818,357)),
                new Triangle3(new Vec3(2.10697622824,-9.08063039416,357.5),new Vec3(0.463389133045,0.521078692749,357),new Vec3(1.6250180697,-8.65712742088,356.58826192562)),
                new Triangle3(new Vec3(-1.95220935857,-6.51513914717,356.5),new Vec3(0.463389133045,0.521078692749,357),new Vec3(-2.97682430857,-5.42806296702,356.5)),
                new Triangle3(new Vec3(-1.11859351455,-7.40724215424,356.5),new Vec3(0.463389133045,0.521078692749,357),new Vec3(-1.95220935857,-6.51513914717,356.5)),
                new Triangle3(new Vec3(2.10697622824,-9.08063039416,357.5),new Vec3(0.497664282098,1.34899380105,357.5),new Vec3(0.347851698811,1.43395745382,357.07814142764)),
                new Triangle3(new Vec3(2.10697622824,-9.08063039416,357.5),new Vec3(0.347851698811,1.43395745382,357.07814142764),new Vec3(0.463389133045,0.521078692749,357)),
                new Triangle3(new Vec3(1.6250180697,-8.65712742088,356.58826192562),new Vec3(0.463389133045,0.521078692749,357),new Vec3(-1.11859351455,-7.40724215424,356.5)),
                new Triangle3(new Vec3(0.719438603439,1.2232182757,357.5),new Vec3(2.31732670922,-9.13237032807,357.5),new Vec3(0.889676785446,1.12667059386,357.5)),
                new Triangle3(new Vec3(0.231725389545,0.530345237814,357),new Vec3(-0.906560207368,0.512142819818,357),new Vec3(-2.97682430857,-5.42806296702,356.5)),
                new Triangle3(new Vec3(0.142959203338,1.55015873909,357.08743363075),new Vec3(0.231725389545,0.530345237814,357),new Vec3(0.463389133045,0.521078692749,357)),
                new Triangle3(new Vec3(0.142959203338,1.55015873909,357.08743363075),new Vec3(0.463389133045,0.521078692749,357),new Vec3(0.347851698811,1.43395745382,357.07814142764)),
                new Triangle3(new Vec3(2.31732670922,-9.13237032807,357.5),new Vec3(0.570006424095,1.30796618899,357.5),new Vec3(0.497664282098,1.34899380105,357.5)),
                new Triangle3(new Vec3(2.31732670922,-9.13237032807,357.5),new Vec3(0.497664282098,1.34899380105,357.5),new Vec3(2.10697622824,-9.08063039416,357.5))
            },Array.Empty<ValidationIssue>(),new[]{
                new Triangle3(new Vec3(0.463389133045,0.521078692749,357),
                    new Vec3(0.463389133045,0.521078692749,357.14541593259),
                    new Vec3(0.231725389545,0.530345237814,357))
            },new[]{root});
            var square=new[]{
                new Vec2(0,0),new Vec2(.9,0),
                new Vec2(.9,.9),new Vec2(0,.9)};
            var r=new GridVolumeCalculator().Calculate(ht,tk,
                new GridVolumeOptions{Boundary=square,SizeX=.9,SizeY=.9});
            Check(r.Cells.Count==1 && r.IsComplete && r.AmbiguousCells==0,
                "Actual KL mb.dxf single-valued planar TIN patch was wrongly quarantined");
            Check(Math.Abs(r.Cells[0].Volume.CalculationArea-0.81)<1e-7,
                "Actual KL mb.dxf cell area changed");
            Check(Math.Abs(r.TotalNoDataArea)<1e-7,
                "Actual KL mb.dxf two accepted TIN surfaces lost overlapping XY support");
            Check(Math.Abs(r.TotalCutVolume-0.00062917912154)<1e-5 &&
                  Math.Abs(r.TotalFillVolume-0.15513678565021)<1e-5,
                "Exact signed integration disagrees with independent KL mb.dxf reference");
        };
        tests["rf23_large_survey_coordinates_and_rotated_grid_keep_full_TIN_overlap"]=()=>{
            const double shiftX=472500.0,shiftY=2399000.0;
            TinSurface Move(TinSurface source)=>new TinSurface("translated",
                source.Triangles.Select(t=>new Triangle3(
                    new Vec3(t.A.X+shiftX,t.A.Y+shiftY,t.A.Z),
                    new Vec3(t.B.X+shiftX,t.B.Y+shiftY,t.B.Z),
                    new Vec3(t.C.X+shiftX,t.C.Y+shiftY,t.C.Z))).ToArray(),
                Array.Empty<ValidationIssue>());
            var bounds=Boundary.Select(v=>new Vec2(v.X+shiftX,v.Y+shiftY)).ToArray();
            foreach(double angle in new[]{0d,17d,90d})
            {
                var report=new GridVolumeCalculator().Calculate(Move(Plane(5)),
                    Move(Plane(0)),new GridVolumeOptions{
                        Origin=bounds[0],Boundary=bounds,SizeX=5,SizeY=5,
                        RotationDegrees=angle});
                Check(report.Cells.Count>0 && report.IsComplete &&
                    report.MissingExistingCells==0 &&
                    report.MissingDesignCells==0 &&
                    report.IntersectionMismatchCells==0,
                    "Correctly overlaid TINs at mine XY reported missing: rotation="+angle);
                Near(report.TotalCutVolume,1000.0);
                Near(report.TotalNoDataArea,0.0);
            }
        };
        tests["rf15_grid_rejects_stale_boundary_and_cell_settings"]=()=>{
            var st=ProjectState.Current;
            st.Reset();
            var existing=Plane(5);
            var design=Plane(0);
            st.Existing.Tin=existing;
            st.Design.Tin=design;
            st.Existing.TinBuiltFromSourceRevision=st.Existing.Source.Revision;
            st.Design.TinBuiltFromSourceRevision=st.Design.Source.Revision;
            st.Existing.TinBuiltFromRegionRevision=st.TinRegionRevision;
            st.Design.TinBuiltFromRegionRevision=st.TinRegionRevision;
            st.GridBoundaryHandle="BOUNDARY-1";
            st.GridSizeX=5;st.GridSizeY=5;st.GridRotationDegrees=15;
            var options=new GridVolumeOptions{Boundary=Boundary,SizeX=5,SizeY=5,RotationDegrees=15};
            var report=new GridVolumeCalculator().Calculate(existing,design,options);
            long er=st.Existing.Source.Revision,dr=st.Design.Source.Revision;
            GridWorkflowService.Commit(report,options,existing,design,er,dr,"BOUNDARY-1");
            Check(GridWorkflowService.IsCurrent,"Grid report invalid immediately after a verified commit");
            st.GridSizeX=10;
            Check(!GridWorkflowService.IsCurrent,"Changing cell size silently retained stale grid");
            bool rejected=false;
            try{GridWorkflowService.Commit(report,options,existing,design,er,dr,"BOUNDARY-1");}
            catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Stale grid committed after cell size changed");
            st.GridSizeX=5;st.GridBoundaryHandle="BOUNDARY-2";
            Check(!GridWorkflowService.IsCurrent,"Changing boundary silently retained stale grid");
            rejected=false;
            try{GridWorkflowService.Commit(report,options,existing,design,er,dr,"BOUNDARY-1");}
            catch(InvalidOperationException){rejected=true;}
            Check(rejected,"Stale grid committed after boundary handle changed");
            GridWorkflowService.Invalidate();
        };
        tests["p0_tin_region_change_must_invalidate_grid_and_both_tins"]=()=>{
            var st=ProjectState.Current;
            st.Reset();
            var existing=Plane(5);var design=Plane(0);
            st.Existing.Tin=existing;st.Design.Tin=design;
            st.Existing.TinBuiltFromSourceRevision=st.Existing.Source.Revision;
            st.Design.TinBuiltFromSourceRevision=st.Design.Source.Revision;
            st.Existing.TinBuiltFromRegionRevision=st.TinRegionRevision;
            st.Design.TinBuiltFromRegionRevision=st.TinRegionRevision;
            st.GridBoundaryHandle="R";
            st.GridSizeX=5;st.GridSizeY=5;st.GridRotationDegrees=0;
            var options=new GridVolumeOptions{Boundary=Boundary,SizeX=5,SizeY=5,RotationDegrees=0};
            var result=new GridVolumeCalculator().Calculate(existing,design,options);
            GridWorkflowService.Commit(result,options,existing,design,
                st.Existing.Source.Revision,st.Design.Source.Revision,"R");
            Check(GridWorkflowService.IsCurrent,"Fresh region unexpectedly rejected");
            long old=st.TinRegionRevision;
            st.MarkTinRegionChanged();
            Check(st.TinRegionRevision>old,"TIN region revision did not advance");
            Check(!st.Existing.IsTinCurrent&&!st.Design.IsTinCurrent,
                "Clipping region changed but old TIN remains current");
            Check(!GridWorkflowService.IsCurrent && GridWorkflowService.Report==null,
                "Old grid volumes were still exportable after clipping region changed");
            GridWorkflowService.Invalidate();
        };
        tests["rf16_section_geometry_tolerance_detects_real_grip_edit"]=()=>{
            var initial=Profile(1,5);
            var close=Edit(initial,5+1e-9,5-1e-9);
            Check(EditedSectionProfileBuilder.SameGeometry(new[]{initial},new[]{close}),
                "CAD X/Y roundtrip noise was incorrectly considered a real edit");
            var changed=Edit(initial,5,5.001);
            Check(!EditedSectionProfileBuilder.SameGeometry(new[]{initial},new[]{changed}),
                "One-millimetre vertical grip edit was silently treated as roundtrip noise");
        };
        tests["rf15_grip_modified_same_handle_boundary_blocks_export"]=()=>{
            var st=ProjectState.Current;
            st.Reset();
            var existing=Plane(5);var design=Plane(0);
            st.Existing.Tin=existing;st.Design.Tin=design;
            st.Existing.TinBuiltFromSourceRevision=st.Existing.Source.Revision;
            st.Design.TinBuiltFromSourceRevision=st.Design.Source.Revision;
            st.Existing.TinBuiltFromRegionRevision=st.TinRegionRevision;
            st.Design.TinBuiltFromRegionRevision=st.TinRegionRevision;
            st.GridBoundaryHandle="SAME-CAD-HANDLE";
            st.GridSizeX=5;st.GridSizeY=5;st.GridRotationDegrees=0;
            var accepted=Boundary.ToArray();
            var opt=new GridVolumeOptions{Boundary=accepted,SizeX=5,SizeY=5,RotationDegrees=0};
            var report=new GridVolumeCalculator().Calculate(existing,design,opt);
            GridWorkflowService.Commit(report,opt,existing,design,
                st.Existing.Source.Revision,st.Design.Source.Revision,"SAME-CAD-HANDLE");
            GridWorkflowService.EnsureBoundaryCurrent(Boundary.ToArray());
            Check(GridWorkflowService.IsCurrent,"Unmodified CAD boundary spuriously invalidated grid");
            var grip=Boundary.ToArray();grip[1]=new Vec2(11,0);
            bool stopped=false;
            try{GridWorkflowService.EnsureBoundaryCurrent(grip);}
            catch(InvalidOperationException){stopped=true;}
            Check(stopped && !GridWorkflowService.IsCurrent && GridWorkflowService.Report==null,
                "CAD grip changed boundary without new handle, but stale grid remains exportable");
            GridWorkflowService.Invalidate();
        };
        tests["rf13_invalidate_volume_clears_success_timing"]=()=>{
            var s=ProjectState.Current;s.VolumeResult=Calculate(Profile(1,5),Profile(2,5));s.VolumeCalculatedUtc=DateTime.UtcNow;s.VolumeCalculationSeconds=1.23;
            s.VolumeResult=null;Check(!s.VolumeCalculatedUtc.HasValue&&!s.VolumeCalculationSeconds.HasValue,"Stale timing survives invalidation");
        };
        tests["rf17_excel_uneven_vietnamese_headers_have_full_text_and_keep_formula_row"]=()=>{
            var report=new SpreadsheetReport{Title="IMSAT VOLUME"};
            var sheet=new ReportSheet{Name="Header QA",Title="BẢNG KHỐI LƯỢNG"};
            sheet.Notes.Add("Kiểm tra tên cột không bị cắt");
            sheet.Headers.AddRange(new[]{
                "Thứ tự","Khối lượng đào (m³)","Khối lượng đắp (m³)","Chênh lệch đào đắp (m³)"
            });
            sheet.ColumnWidths.AddRange(new[]{12d,12d,12d,12d});
            sheet.Rows.Add(new ReportCell[]{
                ReportCell.Int(1),ReportCell.N3(10),ReportCell.N3(3),
                ReportCell.Formula3("B4-C4")
            });
            report.Sheets.Add(sheet);
            var dir=Path.Combine(Path.GetTempPath(),"imsat-hdr-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try{
                var target=Path.Combine(dir,"headers.xlsx");
                SimpleXlsxWriter.Write(target,report);
                using(var zip=ZipFile.OpenRead(target))
                using(var stream=zip.GetEntry("xl/worksheets/sheet1.xml").Open()){
                    var xml=XDocument.Load(stream);
                    var header=xml.Descendants().FirstOrDefault(x=>x.Name.LocalName=="row"
                        && (string)x.Attribute("r")=="3");
                    Check(header!=null && double.Parse((string)header.Attribute("ht"),
                        System.Globalization.CultureInfo.InvariantCulture)>=60,
                        "Long wrapped Vietnamese header is clipped to old fixed 30-point row");
                    foreach(var column in new[]{"B3","C3","D3"}){
                        var cell=xml.Descendants().FirstOrDefault(x=>x.Name.LocalName=="c"
                            && (string)x.Attribute("r")==column);
                        Check(cell!=null && cell.Descendants().Any(x=>x.Name.LocalName=="t"
                            && x.Value.Contains("m³")),"Lost full column heading: "+column);
                    }
                    var formula=xml.Descendants().FirstOrDefault(x=>x.Name.LocalName=="c"
                        && (string)x.Attribute("r")=="D4");
                    Check(formula!=null && formula.Descendants().Any(x=>
                        x.Name.LocalName=="f" && x.Value=="B4-C4"),
                        "Excel heading height changed formula/data coordinates");
                }
            }finally{Directory.Delete(dir,true);}
        };
        tests["rf14_three_production_workbooks_metadata_formulas_logo"]=()=>{
            var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
            try {Fixtures(dir);foreach(var file in Directory.GetFiles(dir,"*.xlsx"))VerifyWorkbook(file);}
            finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        };
        tests["rf14_embedded_logo_missing_file_and_outside_tkv_both_export"]=()=>{
            var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            var file=Path.Combine(dir,"previous.xlsx");
            var logo=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","imsat_logo_64.png");
            byte[] bytes=File.Exists(logo)?File.ReadAllBytes(logo):null;
            File.WriteAllText(file,"previous-accepted-workbook");
            try {
                if(bytes!=null)File.Delete(logo);
                var tkv=ExportWorkflowService.BuildReport(State(),new ExportOptions());
                Check(tkv.IncludeImsatLogo,"TKV report should request optional branding");
                ExportWorkflowService.WriteXlsx(file,tkv);
                using(var zip=ZipFile.OpenRead(file))
                    Check(zip.GetEntry("xl/media/imsat_logo.png")!=null,
                        "Embedded IMSAT logo was not used when external Assets file was absent");
                Check(Directory.GetFiles(dir).Length==1,"A staging file leaked");
                var outside=State();
                outside.MiningUnitGroup=MiningUnitCatalog.OutsideTkv;
                outside.MiningUnitName="Công ty ngoài TKV QA";
                var report=ExportWorkflowService.BuildReport(outside,new ExportOptions());
                Check(!report.IncludeImsatLogo,"Outside TKV must not show IMSAT logo");
                ExportWorkflowService.WriteXlsx(Path.Combine(dir,"outside.xlsx"),report);
                using(var zip=ZipFile.OpenRead(Path.Combine(dir,"outside.xlsx"))){
                    Check(zip.GetEntry("xl/media/imsat_logo.png")==null,
                        "Outside TKV incorrectly branded with IMSAT logo");
                    var first=zip.GetEntry("xl/worksheets/sheet1.xml");
                    using(var rd=new StreamReader(first.Open())){
                        var xml=rd.ReadToEnd();
                        Check(xml.Contains("Công ty ngoài TKV QA") &&
                            !xml.Contains(ExportWorkflowService.TkvParentHeading),
                            "Outside TKV report carries wrong organizational affiliation");
                    }
                }
                Check(Directory.GetFiles(dir).Length==2,"Staging file leaked");
            } finally {
                if(bytes!=null)File.WriteAllBytes(logo,bytes);
                Directory.Delete(dir,true);
            }
        };
    }
    static ProjectState State(){
        var s=ProjectState.Current;s.MiningUnitName="Đơn vị đối chứng IMSAT";s.MiningUnitGroup=MiningUnitCatalog.Coal;
        s.SectionProfiles.Clear();s.SectionProfiles.AddRange(new[]{Edit(Profile(1,10),10,10),Edit(Profile(2,6),6,6)});
        s.VolumeResult=Calculate(s.SectionProfiles.ToArray());s.VolumeCalculatedUtc=new DateTime(2026,10,12,1,2,3,DateTimeKind.Utc);s.VolumeCalculationSeconds=1.234;
        s.GridCalculatedUtc=s.VolumeCalculatedUtc;s.GridCalculationSeconds=2.345;return s;
    }
    public static void Fixtures(string directory){
        Directory.CreateDirectory(directory);var s=State();
        ExportWorkflowService.WriteXlsx(Path.Combine(directory,"sections.xlsx"),ExportWorkflowService.BuildReport(s,new ExportOptions()));
        var opt=new GridVolumeOptions{Boundary=Boundary,SizeX=5,SizeY=5,RotationDegrees=15};
        var grid=new GridVolumeCalculator().Calculate(Plane(5),Plane(0),opt);Near(grid.TotalCutVolume,1000);
        ExportWorkflowService.WriteXlsx(Path.Combine(directory,"grid.xlsx"),GridVolumeExcelReportBuilder.Build(grid,opt));
        var seams=Enumerable.Range(1,3).Select(i=>new MineralSeamInput{Id="V"+i,Name="Vỉa "+i,DensityTonPerCubicMetre=1+i*0.1}).ToArray();
        var areas=seams.SelectMany(b=>s.SectionProfiles.Select(p=>new MineralSectionInput{BodyId=b.Id,SectionName=p.Line.Name,AreaSquareMetres=2})).ToArray();
        var mineral=MineralVolumeCalculator.Calculate(s.VolumeResult,seams,areas);Near(mineral.TotalMineralCubicMetres,120);Near(mineral.TotalMineralTonnes,144);
        mineral.CalculatedUtc=s.VolumeCalculatedUtc;mineral.CalculationSeconds=3.456;
        var report=MineralVolumeExcelReportBuilder.Build(mineral,Array.Empty<MineralBody>());
        Check(report.Sheets.Count==4,"Mineral sheet count changed");
        Check((string)report.Sheets[3].Rows[2][1].Value=="B4-B5","Waste references shifted after metadata");
        ExportWorkflowService.WriteXlsx(Path.Combine(directory,"minerals.xlsx"),report);
    }
    static void VerifyWorkbook(string file){
        using(var zip=ZipFile.OpenRead(file)){
            foreach(var drawing in zip.Entries.Where(x=>x.FullName.StartsWith("xl/drawings/drawingMV")&&x.FullName.EndsWith(".xml")))
                using(var stream=drawing.Open()) {
                    var xml=XDocument.Load(stream);
                    Check(xml.Descendants().Where(x=>x.Name.LocalName=="colOff").All(x=>int.Parse(x.Value)>0),"Logo overlaps centered report title");
                }
            Check(zip.GetEntry("xl/media/imsat_logo.png")!=null,"IMSAT logo missing: "+file);
            string Read(string name){using(var r=new StreamReader(zip.GetEntry(name).Open()))return r.ReadToEnd();}
            var sheets=zip.Entries.Where(x=>x.FullName.StartsWith("xl/worksheets/sheet")&&x.FullName.EndsWith(".xml")).Select(x=>Read(x.FullName)).ToArray();
            var all=string.Join("\n",sheets);Check(all.Contains("Đơn vị đối chứng IMSAT")&&all.Contains("NGƯỜI TÍNH TOÁN")&&all.Contains("NGƯỜI KIỂM TRA")&&all.Contains("0967280686"),"Missing metadata/signature/contact");
            foreach(var worksheetXml in sheets) {
                var doc=XDocument.Parse(worksheetXml);
                var titleCell=doc.Descendants().FirstOrDefault(x=>x.Name.LocalName=="c"
                    && (string)x.Attribute("r")=="A1");
                var actualTitle=titleCell?.Descendants().FirstOrDefault(x=>x.Name.LocalName=="t")?.Value??"";
                string expected=ExportWorkflowService.TkvParentHeading+"\n"+"Đơn vị đối chứng IMSAT"+"\n";
                Check(actualTitle.StartsWith(expected,StringComparison.Ordinal),
                    "Workbook organization does not show TKV immediately above selected mining unit");
                var row1=doc.Descendants().FirstOrDefault(x=>x.Name.LocalName=="row"
                    && (string)x.Attribute("r")=="1");
                Check(row1!=null&&double.Parse((string)row1.Attribute("ht"),
                    System.Globalization.CultureInfo.InvariantCulture)>=60,
                    "Merged company/title Excel row height clips text");
            }
            Check(all.Contains("Kỳ tính khối lượng")&&!all.Contains("#REF!"),"Period/formula references missing");
            Check(sheets.Any(x=>x.Contains("<f>")),"No real Excel formulas");
            var styles=Read("xl/styles.xml");Check(styles.Contains("Arial")&&styles.Contains("val=\"12\""),"Wrong font");
            foreach(var entry in zip.Entries.Where(x=>x.FullName.EndsWith(".xml")))XDocument.Parse(Read(entry.FullName));
        }
    }
}
