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
    public static void Add(IDictionary<string,Action> tests) {
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
        tests["rf15_grid_rejects_stale_boundary_and_cell_settings"]=()=>{
            var st=ProjectState.Current;
            st.Reset();
            var existing=Plane(5);
            var design=Plane(0);
            st.Existing.Tin=existing;
            st.Design.Tin=design;
            st.Existing.TinBuiltFromSourceRevision=st.Existing.Source.Revision;
            st.Design.TinBuiltFromSourceRevision=st.Design.Source.Revision;
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
        tests["rf15_grip_modified_same_handle_boundary_blocks_export"]=()=>{
            var st=ProjectState.Current;
            st.Reset();
            var existing=Plane(5);var design=Plane(0);
            st.Existing.Tin=existing;st.Design.Tin=design;
            st.Existing.TinBuiltFromSourceRevision=st.Existing.Source.Revision;
            st.Design.TinBuiltFromSourceRevision=st.Design.Source.Revision;
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
        tests["rf14_three_production_workbooks_metadata_formulas_logo"]=()=>{
            var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
            try {Fixtures(dir);foreach(var file in Directory.GetFiles(dir,"*.xlsx"))VerifyWorkbook(file);}
            finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        };
        tests["rf14_failed_branding_preserves_existing_workbook"]=()=>{
            var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            var file=Path.Combine(dir,"previous.xlsx");var logo=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","imsat_logo_64.png");
            var bytes=File.ReadAllBytes(logo);File.WriteAllText(file,"previous-accepted-workbook");
            try {File.Delete(logo);bool failed=false;try{ExportWorkflowService.WriteXlsx(file,ExportWorkflowService.BuildReport(State(),new ExportOptions()));}catch(FileNotFoundException){failed=true;}
                Check(failed&&File.ReadAllText(file)=="previous-accepted-workbook","Failed export overwrote user's file");
                Check(Directory.GetFiles(dir).Length==1,"Staging file leaked");}
            finally{File.WriteAllBytes(logo,bytes);Directory.Delete(dir,true);}
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
            Check(zip.GetEntry("xl/media/imsat_logo.png")!=null,"IMSAT logo missing: "+file);
            string Read(string name){using(var r=new StreamReader(zip.GetEntry(name).Open()))return r.ReadToEnd();}
            var sheets=zip.Entries.Where(x=>x.FullName.StartsWith("xl/worksheets/sheet")&&x.FullName.EndsWith(".xml")).Select(x=>Read(x.FullName)).ToArray();
            var all=string.Join("\n",sheets);Check(all.Contains("Đơn vị đối chứng IMSAT")&&all.Contains("NGƯỜI TÍNH TOÁN")&&all.Contains("NGƯỜI KIỂM TRA")&&all.Contains("0967280686"),"Missing metadata/signature/contact");
            Check(all.Contains("Kỳ tính khối lượng")&&!all.Contains("#REF!"),"Period/formula references missing");
            Check(sheets.Any(x=>x.Contains("<f>")),"No real Excel formulas");
            var styles=Read("xl/styles.xml");Check(styles.Contains("Arial")&&styles.Contains("val=\"12\""),"Wrong font");
            foreach(var entry in zip.Entries.Where(x=>x.FullName.EndsWith(".xml")))XDocument.Parse(Read(entry.FullName));
        }
    }
}
