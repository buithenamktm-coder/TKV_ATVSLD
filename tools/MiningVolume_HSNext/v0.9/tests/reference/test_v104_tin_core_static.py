from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def test_official_tin_layers_are_fixed_and_separate():
    s = read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    assert '"MV_TIN_HIENTRANG"' in s
    assert '"MV_TIN_THIETKE"' in s
    assert "IsTinCurrent" in s
    assert "TinBuiltFromSourceUtc" in s

def test_loading_source_prepares_output_layer_and_invalidates_old_tin():
    s = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "InvalidateTin(role, clearCadLayer: true, notify: false)" in s
    assert "EnsureOutputLayer(role)" in s
    assert "OutputTinLayer = session.TinLayer" in s
    assert "Không được dùng layer TIN đầu ra làm dữ liệu nguồn" in s

def test_model_page_has_explicit_existing_design_and_pair_tin_actions():
    s = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    assert "TẠO / CẬP NHẬT TIN HIỆN TRẠNG" in s
    assert "TẠO / CẬP NHẬT TIN THIẾT KẾ" in s
    assert "TẠO / CẬP NHẬT CẢ HAI TIN" in s
    assert "BuildPairCoreDetailed" in s
    assert "DrawTinPair" in s

def test_data_page_can_build_validated_tin_pair_in_one_click():
    s = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    assert "TẠO CẶP TIN HIỆN TRẠNG + THIẾT KẾ" in s
    assert "BuildPairCoreDetailed" in s
    assert "DrawTinPair" in s
    assert "Cặp TIN hợp lệ" in s

def test_renderer_replaces_verifies_and_locks_tin_faces():
    s = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    assert "int Replace(" in s
    assert "CountFaces" in s
    assert "new Face(" in s
    assert "outLayer.IsLocked = true" in s
    assert "ColorIndex = 256" in s
    assert 'db.Clayer = lt["0"]' in s

def test_surface_workflow_keeps_face_count_by_atomic_replace_and_runtime_verification():
    s = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "written != tin.Triangles.Count" in s
    assert "verified != tin.Triangles.Count" not in s
    assert "EnsureBothTinsReady(synchronizeCadLayers: false)" in s
    assert "EnsureTinLayerSynchronized" in s
    assert "EnsureBothTinsReady" in s

def test_source_edits_invalidate_old_tin():
    s = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    assert s.count("SurfaceWorkflowService.InvalidateTin(CurrentRole, clearCadLayer: true)") >= 5

def test_sections_and_volume_require_verified_tin_pair():
    sec = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    vol = read("src/MiningVolume.Plugin2023/UI/VolumePage.cs")
    assert "EnsureBothTinsReady(synchronizeCadLayers: true)" in sec
    assert "EnsureBothTinsReady(synchronizeCadLayers: true)" in vol

def test_startup_creates_both_tin_output_layers():
    s = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert "EnsureOutputLayer(ModelRole.Existing)" in s
    assert "EnsureOutputLayer(ModelRole.Design)" in s
    assert "TIN output layers ready" in s

def test_runtime_selftest_exercises_production_builder_and_real_autocad_faces():
    s = read("src/MiningVolume.Plugin2023/Services/SelfTestService.cs")
    assert "new SurfaceInputPreparer().Prepare" in s
    assert "new ConformingTinBuilder().Build" in s
    assert "RunCadTinLayerSmoke" in s
    assert "TinCadRenderer.Replace" in s
    assert "TinCadRenderer.CountFaces" in s
    assert "Layer TIN hiện trạng chính thức tồn tại trong DWG" in s
    assert "Layer TIN thiết kế chính thức tồn tại trong DWG" in s


def test_reserved_tin_layers_never_blindly_delete_non_faces():
    s = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    assert "chứa đối tượng không phải 3DFACE" in s
    assert "không xóa tự động để tránh mất dữ liệu CAD" in s
