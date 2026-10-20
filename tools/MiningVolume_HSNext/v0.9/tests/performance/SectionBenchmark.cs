using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Sections;
using MiningVolume.Core.Surface;
using MiningVolume.Core.Volumes;
class SectionBenchmark
{
    static void Near(double a,double b) { if(Math.Abs(a-b)>1e-6*Math.Max(1,Math.Abs(b))) throw new Exception($"{a:R} != {b:R}"); }
    static void Measure(string name, Action action) {
        action(); // warm JIT and verify independently before timing
        var times=new List<double>();var allocations=new List<long>();
        for(int n=0;n<5;n++) {
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            long before=GC.GetTotalAllocatedBytes(true);
            var sw=Stopwatch.StartNew();action();sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);allocations.Add(GC.GetTotalAllocatedBytes(true)-before);
        }
        Console.WriteLine(JsonSerializer.Serialize(new {stage=name,ms=times,allocated_bytes=allocations,scope="Linux .NET 8 core only; HOST_RUNTIME_BLOCKED"}));
    }
    static SectionProfile Profile(int n) {
        var line=new SectionLine(1,"test",0,new Vec2(1,0),new Vec2(0,1),new[]{new SectionSpan(0,n)});
        var segs=Enumerable.Range(0,n).Select(i=>new SectionProfileSegment(i,i+1,new Vec2(i,0),new Vec2(i+1,0),103,103,101,101)).ToArray();
        return new SectionProfile(line,segs,2*n,0,Array.Empty<string>());
    }
    static TinSurface Plane(int side,double z) {
        var tris=new List<Triangle3>();
        for(int x=0;x<side;x++)for(int y=0;y<side;y++) {
            var a=new Vec3(x,y,z);var b=new Vec3(x+1,y,z);var c=new Vec3(x+1,y+1,z);var d=new Vec3(x,y+1,z);
            tris.Add(new Triangle3(a,b,c));tris.Add(new Triangle3(a,c,d));
        }
        return new TinSurface("plane",tris,Array.Empty<ValidationIssue>());
    }
    static void Main(string[] args) {
        if(args.Length>0) { VolumeBench(); return; }
        var calc=new SectionBandAreaCalculator();
        foreach(int count in new[]{15,85,300}) {
            var profile=Profile(1000);var bands=SectionVolumeCalculator.BuildBands(-500,500,5);
            Measure("band_area_"+count+"_profiles_1000_segments_200_bands",()=>{
                for(int i=0;i<count;i++) {var r=calc.Calculate(profile,bands);Near(r.CutArea,2000);Near(r.FillArea,0);}
            });
        }
        // Explicit band order and cut/fill crossing: each triangle has area 0.5.
        var l=new SectionLine(1,"cross",0,new Vec2(1,0),new Vec2(0,1),new[]{new SectionSpan(0,2)});
        var cross=new SectionProfile(l,new[]{new SectionProfileSegment(0,2,new Vec2(0,0),new Vec2(2,0),1,-1,0,0)},.5,.5,Array.Empty<string>());
        foreach(var bands in new[]{SectionVolumeCalculator.BuildBands(-10,10,1),SectionVolumeCalculator.BuildBands(10,-10,1)}) {
            var r=calc.Calculate(cross,bands);Near(r.CutArea,.5);Near(r.FillArea,.5);
        }
        var ht=Plane(100,3);var tk=Plane(100,1);
        foreach(int count in new[]{15,85,300}) {
            var lines=Enumerable.Range(0,count).Select(i=>new SectionLine(i,"MC-"+i,.1+99.8*i/Math.Max(1,count-1),new Vec2(1,0),new Vec2(0,1),new[]{new SectionSpan(0,100)})).ToArray();
            Measure("sample_"+count+"_profiles_20000_triangles_per_tin",()=>{
                var profiles=new TinSectionSampler().BuildProfiles(lines,ht,tk);
                foreach(var p in profiles) {Near(p.CutArea,200);Near(p.FillArea,0);if(p.Warnings.Count!=0)throw new Exception("Missing coverage");}
            });
        }
    }
    static void VolumeBench() {
        var ht=Plane(100,3);var tk=Plane(100,1);
        var boundary=new[]{new Vec2(0,0),new Vec2(100,0),new Vec2(100,100),new Vec2(0,100)};
        foreach(int count in new[]{15,85,300}) {
            var lines=Enumerable.Range(0,count).Select(i=>new SectionLine(i,"MC-"+i,.1+99.8*i/Math.Max(1,count-1),new Vec2(1,0),new Vec2(0,1),new[]{new SectionSpan(0,100)})).ToArray();
            var profiles=new TinSectionSampler().BuildProfiles(lines,ht,tk);
            var system=new SectionSystem(new Vec2(1,0),new Vec2(0,1),boundary,lines,Array.Empty<string>());
            Measure("volume_"+count+"_profiles_200_bands_including_mid_sections",()=>{
                var r=new SectionVolumeCalculator().Calculate(system,profiles,ht,tk,
                    new VolumeCalculationOptions{FromLevel=-500,ToLevel=500,LevelStep=5});
                Near(r.Intervals.Sum(x=>x.CutVolume),19960);Near(r.Intervals.Sum(x=>x.FillVolume),0);
                if(!r.IsComplete)throw new Exception("Incomplete volume");
            });
        }
    }
}
