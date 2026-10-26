"""Roslyn response-file SDK references must be quoted on Windows.

On a real Windows runner SDK references normally live beneath
C:\\Program Files\\dotnet. Without quotes INSIDE the .rsp, csc.dll treats
the remainder of the path as source files (CS2001) before ledger tests start.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def test_offline_ledger_response_file_quotes_all_paths():
    s = (ROOT / "tests/license/run_license.py").read_text(encoding="utf-8")
    assert "def rsp_path(path):" in s
    assert "'-r:'+rsp_path(f)" in s
    assert "'-out:'+rsp_path(out/'LicenseRegression.dll')" in s
    assert "rsp_path(project/'src/MiningVolume.Plugin2023/Services/LicenseService.cs')" in s
    assert "rsp_path(pathlib.Path(__file__).with_name('LicenseRegression.cs'))" in s
    assert "'@'+str(out/'build.rsp')" in s
    assert "for f in refs.glob('*.dll')" in s
