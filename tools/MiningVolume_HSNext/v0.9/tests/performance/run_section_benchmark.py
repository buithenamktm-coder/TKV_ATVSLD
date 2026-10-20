"""Compile production Core; optionally compare a git revision with identical fixtures."""
import argparse, json, os, pathlib, subprocess, tempfile
p=argparse.ArgumentParser()
p.add_argument('--dotnet-root', required=True)
p.add_argument('--source-ref')
p.add_argument('--volume-only', action='store_true')
a=p.parse_args()
root=pathlib.Path(a.dotnet_root)
project=pathlib.Path(__file__).resolve().parents[2]
repo=pathlib.Path(subprocess.check_output(['git','rev-parse','--show-toplevel'],cwd=project,text=True).strip())
with tempfile.TemporaryDirectory(prefix='imsat-perf-') as tmp:
    tmp=pathlib.Path(tmp)
    sources=[]
    for src in sorted((project/'src/MiningVolume.Core').rglob('*.cs')):
        if any(x in src.parts for x in ('obj','bin')): continue
        if a.source_ref:
            dst=tmp/src.relative_to(project)
            dst.parent.mkdir(parents=True,exist_ok=True)
            dst.write_bytes(subprocess.check_output(['git','show',a.source_ref+':'+str(src.relative_to(repo))],cwd=repo))
            sources.append(dst)
        else: sources.append(src)
    sdk=sorted((root/'sdk').glob('8.*'))[-1]
    refs=sorted((root/'packs/Microsoft.NETCore.App.Ref').glob('8.*'))[-1]
    runtime=sorted((root/'shared/Microsoft.NETCore.App').glob('8.*'))[-1]
    dll=tmp/'bench.dll'
    lines=['-nologo','-optimize+','-target:exe',f'-out:"{dll}"']
    lines += [f'-r:"{r}"' for r in (refs/'ref/net8.0').glob('*.dll')]
    lines += [f'"{s}"' for s in sources+[pathlib.Path(__file__).with_name('SectionBenchmark.cs')]]
    rsp=tmp/'compile.rsp';rsp.write_text('\n'.join(lines))
    subprocess.run([str(root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
    dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
    env=dict(os.environ, DOTNET_TieredCompilation='0', DOTNET_PROCESSOR_COUNT='4')
    subprocess.run([str(root/'dotnet'),str(dll)]+(['volume'] if a.volume_only else []),check=True,env=env)
