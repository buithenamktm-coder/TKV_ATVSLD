from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def test_project_restore_rebuilds_saved_tin_pair_atomically():
    s = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    assert "RebuildSavedTins" in s
    assert "BuildPairCoreDetailed" in s
    assert "DrawTinPair" in s
    assert "Không dựng lại được cặp TIN hiện trạng/thiết kế" in s

def test_tin_sync_rejects_foreign_entities_even_when_face_count_matches():
    renderer = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "CountUnexpectedEntities" in renderer
    assert "unexpected > 0" in workflow
    assert "không phải 3DFACE" in workflow

def test_runtime_selftest_verifies_tin_color_lock_and_visibility():
    s = read("src/MiningVolume.Plugin2023/Services/SelfTestService.cs")
    for token in [
        "GetLayerColorIndex",
        "IsLayerLocked",
        "IsLayerVisible",
        "SetVisible",
        "Layer TIN đúng màu quy ước",
        "Có thể ẩn cả hai TIN",
        "Có thể hiện lại cả hai TIN",
    ]:
        assert token in s

def test_release_pipeline_emits_build_verification_and_vets_installer():
    s = read("../../../.github/workflows/build-miningvolume-autocad2023.yml")
    assert "go vet main.go" in s
    assert "BUILD_VERIFICATION.txt" in s
    assert "Source commit: $env:SOURCE_SHA" in s
    assert "Compile errors: 0" in s
    assert "AutoCAD 2023 host runtime self-test: NOT EXECUTED ON GITHUB-HOSTED RUNNER" in s
    assert "Runtime gate: Setup runs MVSELFTEST" in s


def test_installer_requires_current_runtime_selftest_and_persists_proof():
    s = read("installer/main.go")
    assert "MiningVolume HS-Next v0.10.4 runtime self-test" in s
    assert "writeRuntimeVerification(cad, detail)" in s
    assert "runtime_verification.txt" in s


def test_runtime_gate_has_real_autocad_self_hosted_workflow_and_proof():
    ps = read("release/verify_autocad2023_runtime.ps1")
    wf = read("../../../.github/workflows/verify-miningvolume-autocad2023-runtime.yml")
    assert "Status=PASS" in ps
    assert "AutoCADFileVersion" in ps
    assert "RUNTIME_VERIFICATION.txt" in ps
    assert "MVSELFTEST_RUNTIME.txt" in ps
    assert "Layer TIN đúng màu quy ước" in ps
    assert "runs-on: [self-hosted, Windows, X64, autocad2023]" in wf
    assert "Run MVSELFTEST in AutoCAD 2023" in wf
    assert "MiningVolume-AutoCAD2023-Runtime-v0.10.4" in wf

def test_hosted_ci_parses_runtime_verifier_script():
    s = read("../../../.github/workflows/build-miningvolume-autocad2023.yml")
    assert "Parse AutoCAD runtime verifier" in s
    assert "verify_autocad2023_runtime.ps1" in s
    assert "Parser]::ParseFile" in s


def test_installer_and_runtime_verifier_recover_interrupted_backups_safely():
    installer = read("installer/main.go")
    runtime = read("release/verify_autocad2023_runtime.ps1")
    assert "Recover conservatively from an interrupted previous install" in installer
    assert "if exists(backup)" in installer
    assert "func restoreBackup(backup,target string) error" in installer
    assert "Recover from an interrupted earlier runtime verification" in runtime
    assert "Move-Item $backup $target -Force" in runtime

def test_runtime_pass_requires_critical_tin_checks_and_interactive_admin_session():
    installer = read("installer/main.go")
    runtime = read("release/verify_autocad2023_runtime.ps1")
    for token in [
        "PASS | Khởi tạo đầy đủ giao diện MiningVolume",
        "PASS | Layer TIN đúng màu quy ước: hiện trạng ACI 1, thiết kế ACI 3",
        "PASS | Có thể hiện lại cả hai TIN và layer vẫn khóa",
    ]:
        assert token in installer
        assert token in runtime
    assert "WindowsBuiltInRole]::Administrator" in runtime
    assert "[Environment]::UserInteractive" in runtime


def test_palette_auto_open_is_user_controlled_and_defaults_off():
    settings = read("src/MiningVolume.Plugin2023/Services/UserSettingsService.cs")
    entry = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    project = read("src/MiningVolume.Plugin2023/UI/ProjectPage.cs")
    assert 'AutoOpenPalette' in settings
    assert 'return false;' in settings
    assert 'Registry.CurrentUser' in settings
    assert 'if (autoOpen)' in entry
    assert 'Startup palette auto-open is OFF' in entry
    assert 'Tự động mở bảng MiningVolume khi khởi động AutoCAD' in project
    assert 'UserSettingsService.AutoOpenPalette = _autoOpen.Checked' in project
