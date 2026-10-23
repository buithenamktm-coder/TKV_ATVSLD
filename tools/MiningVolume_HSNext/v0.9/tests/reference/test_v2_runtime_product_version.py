"""Host admission must match the installer's AutoCAD product version policy."""
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]


def test_all_autocad2023_host_gates_use_product_version():
    paths = [ROOT / "release/verify_autocad2023_runtime.ps1",
             ROOT / "release/setup_autocad2023_runner.ps1",
             ROOT.parents[2] / ".github/workflows/verify-imsat-volume-v2-autocad2023.yml",
             ROOT.parents[2] / ".github/workflows/verify-miningvolume-autocad2023-runtime.yml"]
    for path in paths:
        source = path.read_text(encoding="utf-8-sig")
        admissions = [line for line in source.splitlines()
                      if "VersionInfo." in line and "$acadFileVersion" not in line]
        assert admissions and all("VersionInfo.ProductVersion" in line for line in admissions), path
    runtime = paths[0].read_text(encoding="utf-8-sig")
    assert "('AutoCADFileVersion=' + $acadFileVersion)" in runtime
    assert "('AutoCADProductVersion=' + $acadVersion)" in runtime
