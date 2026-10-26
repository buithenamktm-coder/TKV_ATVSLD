"""Regression contract for AutoCAD 2023 V2 host self-test dispatch.

These assertions are *static* and do not replace a genuine licensed AutoCAD
MVSELFTEST run. They ensure the Idle fallback remains opt-in, fail-closed,
one-shot, and never affects the ordinary V1/normal-user startup path.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ENTRY = ROOT / "src/MiningVolume.Plugin2023/EntryPoint.cs"
RUNNER = ROOT / "release/verify_autocad2023_runtime.ps1"


def test_idle_selftest_requires_all_host_only_guards():
    src = ENTRY.read_text(encoding="utf-8-sig")
    entry = src.split("private static void OnIdle(", 1)[1].split(
        "private static void HookIdle(", 1
    )[0]
    assert "if (_startupUiOpened && !_hostQaSelfTestTriggered &&" in entry
    assert "AcApp.DocumentManager.MdiActiveDocument != null" in entry
    assert 'Environment.GetEnvironmentVariable("MININGVOLUME_QA_HOST_AUTORUN")' in entry
    assert 'Environment.GetEnvironmentVariable("MININGVOLUME_RUNTIME_PRODUCT_VERSION")' in entry
    assert '"2.0", StringComparison.Ordinal' in entry
    assert 'Environment.GetEnvironmentVariable("MININGVOLUME_SELFTEST_FILE")' in entry
    assert entry.index("_hostQaSelfTestTriggered = true;") < entry.index(
        "SelfTestService.Run();"
    )
    assert "AcApp.Idle -= OnIdle;" in entry
    assert 'StartupLog("V2 host QA: Idle self-test starting")' in entry
    assert '"Status=FAIL"' in entry
    assert '"NAMVECTA VOLUME 2.0 runtime self-test"' in entry


def test_manual_qa_command_never_overwrites_proof_after_idle():
    src = ENTRY.read_text(encoding="utf-8-sig")
    command = src.split('[CommandMethod("NAMVECTA_QA_SELFTEST_V2"', 1)[1]
    command = command.split('[CommandMethod("MVABOUT")', 1)[0]
    assert "if (hostMode && _hostQaSelfTestTriggered)" in command
    assert "if (hostMode) _hostQaSelfTestTriggered = true;" in command
    assert "SelfTest();" in command


def test_verifier_enables_autorun_for_v2_only_and_restores_environment():
    src = RUNNER.read_text(encoding="utf-8-sig")
    assert "$oldQaAutorun = $env:MININGVOLUME_QA_HOST_AUTORUN" in src
    assert "if ($ProductVersion -eq '2.0')" in src
    assert "$env:MININGVOLUME_QA_HOST_AUTORUN = '1'" in src
    assert "Remove-Item Env:MININGVOLUME_QA_HOST_AUTORUN" in src
    assert "$env:MININGVOLUME_QA_HOST_AUTORUN = $oldQaAutorun" in src
    assert src.index("$env:MININGVOLUME_QA_HOST_AUTORUN = '1'") < src.index(
        "Start-Process -FilePath $acad"
    )
    assert "Status=PASS" in src and "Status=FAIL" in src
    assert "MVSELFTEST log không đúng phiên bản" in src
