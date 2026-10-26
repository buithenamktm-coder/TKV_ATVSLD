"""Fail-closed gate against stale installed AutoCAD plugins during V2 host QA.

A prior QA run loaded SHA 6c5fa5cb from NAMVECTAVolume2023.bundle, even
though the verifier had staged a different build under MiningVolume2023.bundle.
These are source-only contracts, not evidence of host acceptance.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = (ROOT / "release/verify_autocad2023_runtime.ps1").read_text(
    encoding="utf-8-sig"
)


def test_v2_uses_canonical_autoload_name_from_real_failure():
    assert "'NAMVECTAVolume2023.bundle'" in SCRIPT
    assert "'MiningVolume2023.bundle'" in SCRIPT
    assert "$ProductVersion -eq '2.0'" in SCRIPT
    assert "$target = Join-Path $appPlugins $(if" in SCRIPT
    assert "$packageText -notmatch 'LoadOnAutoCADStartup=\"True\"'" in SCRIPT
    assert "$ProductVersion -eq '1.0'" in SCRIPT


def test_conflicting_preinstalled_addins_are_reversibly_isolated():
    assert "Autodesk\\ApplicationPlugins" in SCRIPT
    assert "$env:APPDATA" in SCRIPT and "$env:ProgramFiles" in SCRIPT
    assert "$candidate + '.hostqa-disabled'" in SCRIPT
    assert "Move-Item -LiteralPath $candidate -Destination $parked" in SCRIPT
    assert "Move-Item -LiteralPath $entry.Parked -Destination $entry.Original" in SCRIPT
    assert "for ($i = $isolatedBundles.Count - 1;" in SCRIPT
    assert "BLOCKED: competing installed bundle and stranded backup" in SCRIPT


def test_source_sha_and_loaded_path_are_proven_before_accepting_pass():
    assert "if ($ProductVersion -eq '2.0' -and $env:SOURCE_SHA -match" in SCRIPT
    assert "Loaded plugin directory: " in SCRIPT
    assert "SOURCE_SHA=" in SCRIPT
    assert "BLOCKED: MVSELFTEST proof is not from pinned QA DLL" in SCRIPT
    assert "Status=PASS" in SCRIPT and "Status=FAIL" in SCRIPT
    assert "MVSELFTEST log không đúng phiên bản" in SCRIPT


def test_existing_installation_restored_even_on_failure():
    assert "$backup = $target + '.runtimeverify.bak'" in SCRIPT
    assert "target and interrupted QA backup both exist" in SCRIPT
    assert "V2 host QA may only stage a temporary bundle" in SCRIPT
    assert "Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop" in SCRIPT
    assert "Move-Item -LiteralPath $backup -Destination $target -ErrorAction Stop" in SCRIPT
    assert "finally {" in SCRIPT
