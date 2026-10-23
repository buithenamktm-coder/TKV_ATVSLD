import pathlib, subprocess, tempfile, json, os
root=pathlib.Path.cwd(); sdk=pathlib.Path('/tmp/imsat-dotnet'); base='029dfc17ce1d147c71bbc87f310abda35cacdba3'
old=subprocess.check_output(['git','show',base+':tools/MiningVolume_HSNext/v0.9/src/MiningVolume.Core/Sections/EditedSectionProfileBuilder.cs'],text=True)
old=old[:old.index('    /// <summary>')]+old[old.index('    public static class EditedSectionProfileBuilder'):]
old=old.replace('class EditedSectionProfileBuilder','class BaselineEditedSectionProfileBuilder')
runner=r'''
using System; using System.Linq; using System.Diagnostics; using MiningVolume.Core.Sections; using MiningVolume.Core.Geometry;
class Bench {
static void Main() {
foreach(int n in new[]{17,1025,10001}) {
var l=new SectionLine(1,"MC",0,new Vec2(1,0),new Vec2(0,1),new[]{new SectionSpan(0,10)});
var p=new SectionProfile(l,new[]{new SectionProfileSegment(0,10,l.PointAt(0),l.PointAt(10),5,5,0,0)},50,0,Array.Empty<string>());
var e=new[]{new EditedSectionPath(0,Enumerable.Range(0,n).Select(i=>new EditedSectionVertex(10.0*i/(n-1),5-10.0*i/(n-1))).ToArray())};
var d=new[]{new EditedSectionPath(0,Enumerable.Range(0,n+12).Select(i=>new EditedSectionVertex(10.0*i/(n+11),0)).ToArray())};
Func<SectionProfile> before=()=>BaselineEditedSectionProfileBuilder.Rebuild(p,e,d);
Func<SectionProfile> after=()=>EditedSectionProfileBuilder.Rebuild(p,e,d);
var a=before();var b=after();
if(a.CutArea!=b.CutArea||a.FillArea!=b.FillArea||!EditedSectionProfileBuilder.SameGeometry(new[]{a},new[]{b},1e-12))throw new Exception("Result mismatch");
for(int i=0;i<10;i++){before();after();}
for(int trial=0;trial<5;trial++) {
foreach(int mode in trial%2==0?new[]{0,1}:new[]{1,0}) {
GC.Collect();var f=mode==0?before:after;long bytes=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();
for(int i=0;i<85;i++) f();sw.Stop();bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
Console.WriteLine("{\"vertices_per_surface\":"+n+",\"sections\":85,\"trial\":"+trial+",\"mode\":\""+(mode==0?"before":"after")+"\",\"ms\":"+sw.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"allocated_bytes\":"+bytes+",\"geometry_equal\":true}");
}
}
}
}
}'''
with tempfile.TemporaryDirectory() as tmp:
 t=pathlib.Path(tmp);(t/'old.cs').write_text(old,encoding='utf-8');(t/'runner.cs').write_text(runner,encoding='utf-8')
 sources=[p for p in (root/'src/MiningVolume.Core').rglob('*.cs') if not any(x in p.parts for x in ('bin','obj'))]+[t/'old.cs',t/'runner.cs']
 refs=list((sdk/'packs/Microsoft.NETCore.App.Ref/8.0.15/ref/net8.0').glob('*.dll'))
 args=['-nologo','-target:exe','-out:'+str(t/'bench.dll')]+['-r:'+str(p) for p in refs]+[str(p) for p in sources]
 (t/'compile.rsp').write_text('\n'.join(args));subprocess.run([str(sdk/'dotnet'),str(sdk/'sdk/8.0.408/Roslyn/bincore/csc.dll'),'@'+str(t/'compile.rsp')],check=True)
 (t/'bench.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.15'}}}))
 env=dict(os.environ,DOTNET_TieredCompilation='0',DOTNET_PROCESSOR_COUNT='4')
 subprocess.run([str(sdk/'dotnet'),str(t/'bench.dll')],env=env,check=True)
