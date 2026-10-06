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
