"""Execute real offline-ledger code; DPAPI/Registry remain Windows-only gates."""
import argparse, json, pathlib, subprocess, tempfile
p = argparse.ArgumentParser()
p.add_argument('--dotnet-root', required=True)
a = p.parse_args()
root = pathlib.Path(a.dotnet_root)
project = pathlib.Path(__file__).resolve().parents[2]
with tempfile.TemporaryDirectory(prefix='namvecta-license-') as temp:
    out = pathlib.Path(temp)
    refs = sorted((root/'packs/Microsoft.NETCore.App.Ref').glob('8.*'))[-1]/'ref/net8.0'
    sdk = sorted((root/'sdk').glob('8.*'))[-1]
    runtime = sorted((root/'shared/Microsoft.NETCore.App').glob('8.*'))[-1].name
    rsp = ['-nologo', '-target:exe', '-out:'+str(out/'LicenseRegression.dll')]
    rsp += ['-r:'+str(f) for f in refs.glob('*.dll')]
    rsp += [str(project/'src/MiningVolume.Plugin2023/Services/LicenseService.cs'), str(pathlib.Path(__file__).with_name('LicenseRegression.cs'))]
    (out/'build.rsp').write_text('\n'.join(rsp))
    subprocess.run([str(root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(out/'build.rsp')],check=True)
    (out/'LicenseRegression.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime}}}))
    raise SystemExit(subprocess.run([str(root/'dotnet'),str(out/'LicenseRegression.dll')]).returncode)
