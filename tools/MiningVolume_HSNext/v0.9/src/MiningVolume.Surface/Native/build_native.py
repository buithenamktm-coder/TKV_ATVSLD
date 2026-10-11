"""Build the pinned CDT C ABI as a self-contained x64 native library."""
import argparse, os, pathlib, subprocess

def build(output):
    root=pathlib.Path(__file__).resolve().parent
    output=pathlib.Path(output).resolve(); output.parent.mkdir(parents=True,exist_ok=True)
    source=root/'bridge.cpp'; include=root/'vendor/CDT'
    if os.name=='nt':
        vswhere=pathlib.Path(os.environ.get('ProgramFiles(x86)','C:/Program Files (x86)'))/'Microsoft Visual Studio/Installer/vswhere.exe'
        install=subprocess.check_output([str(vswhere),'-latest','-products','*','-requires','Microsoft.VisualStudio.Component.VC.Tools.x86.x64','-property','installationPath'],text=True).strip()
        if not install: raise RuntimeError('Visual Studio C++ x64 Build Tools are required on the build host')
        vcvars=pathlib.Path(install)/'VC/Auxiliary/Build/vcvars64.bat'
        # All paths come from this checkout, the SDK locator or the requested build output.
        for path in (vcvars,source,include,output):
            if '"' in str(path): raise ValueError('Quote in build path')
        command=f'call "{vcvars}" >nul && cl /nologo /O2 /MT /EHsc /std:c++17 /fp:strict /LD "{source}" /I"{include}" /Fo:"{output.parent / "bridge.obj"}" /Fe:"{output}" /link /INCREMENTAL:NO'
        subprocess.run(command,shell=True,check=True,cwd=output.parent)
    else:
        subprocess.run(['g++','-std=c++17','-O2','-fPIC','-shared','-ffp-contract=off','-I',str(include),str(source),'-o',str(output)],check=True)
    return output
if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--output',required=True);args=ap.parse_args();build(args.output)
