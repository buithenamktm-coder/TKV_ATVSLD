"""Compile and execute the real Core/Surface sources with the .NET 8 SDK.
No extra application/project, package restore or AutoCAD installation is needed.
Optional positional arguments: fixture.bin boundary.bin seconds [UseUpper|UseLower|Stop].
"""
import argparse, json, os, pathlib, re, shutil, subprocess, tempfile

def version(path):
    return tuple(int(x) for x in re.findall(r'\d+',path.name))

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--dotnet-root');ap.add_argument('fixture',nargs='*');args=ap.parse_args()
    executable=shutil.which('dotnet')
    root=pathlib.Path(args.dotnet_root or os.environ.get('DOTNET_ROOT') or (str(pathlib.Path(executable).resolve().parent) if executable else ''))
    dotnet=root/('dotnet.exe' if os.name=='nt' else 'dotnet')
    sdks=sorted((root/'sdk').glob('8.*'),key=version)
    refs=sorted((root/'packs/Microsoft.NETCore.App.Ref').glob('8.*'),key=version)
    runtimes=sorted((root/'shared/Microsoft.NETCore.App').glob('8.*'),key=version)
    if not sdks or not refs or not runtimes: ap.error('Install .NET SDK 8 or pass --dotnet-root')
    project=pathlib.Path(__file__).resolve().parents[2]
    with tempfile.TemporaryDirectory(prefix='imsat-core-') as temp:
        tmp=pathlib.Path(temp);assembly=tmp/'CoreRegression.dll'
        sources=[p for name in ['MiningVolume.Core','MiningVolume.Surface'] for p in (project/'src'/name).rglob('*.cs') if not any(x in p.parts for x in ('obj','bin'))]
        lines=['-nologo','-target:exe','-out:"'+str(assembly)+'"']
        lines+=['-r:"'+str(p)+'"' for p in (refs[-1]/'ref/net8.0').glob('*.dll')]
        lines+=['"'+str(p)+'"' for p in sources+[pathlib.Path(__file__).with_name('CoreRegression.cs').resolve()]]
        rsp=tmp/'compile.rsp';rsp.write_text('\n'.join(lines),encoding='utf8')
        subprocess.run([str(dotnet),str(sdks[-1]/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
        (tmp/'CoreRegression.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtimes[-1].name}}}))
        return subprocess.run([str(dotnet),str(assembly)]+args.fixture).returncode
if __name__=='__main__': raise SystemExit(main())
