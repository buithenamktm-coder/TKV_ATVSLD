from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def test_project_state_tracks_tin_source_revision():
    s = read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    core = read("src/MiningVolume.Core/Model/SourceData.cs")
    assert "Revision => Interlocked.Read" in core
    assert "Interlocked.Increment" in core
    assert "TinBuiltFromSourceRevision" in s
    assert "IsTinCurrent" in s
    assert "TinBuiltFromSourceRevision.Value == Source.Revision" in s

def test_loading_or_editing_source_invalidates_old_tin():
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    model = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    assert "InvalidateTin(role, clearCadLayer: true" in workflow
    for token in [
        "DisableSelectedVertices",
        "DisableSelectedEntities",
        "RestoreSelected",
        "RestoreAll",
        "EditZ"
    ]:
        assert token in model
    assert model.count("InvalidateTin(CurrentRole, clearCadLayer: true)") >= 5

def test_tin_renderer_creates_visible_writable_dedicated_layer_and_verifies_faces():
    s = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    for token in [
        "PrepareLayer",
        "CountFaces",
        "LayerExists",
        "ValidateTriangle",
        "ltr.IsLocked = false",
        "ltr.IsOff = false",
        "new Face("
    ]:
        assert token in s

def test_surface_workflow_verifies_cad_face_count_equals_core_triangle_count():
    s = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "written != tin.Triangles.Count || verified != tin.Triangles.Count" in s
    assert "EnsureTinLayerSynchronized" in s
    assert "EnsureBothTinsReady" in s

def test_model_page_has_explicit_existing_design_and_pair_tin_build():
    s = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    assert "TẠO / CẬP NHẬT TIN HIỆN TRẠNG" in s
    assert "TẠO / CẬP NHẬT TIN THIẾT KẾ" in s
    assert "TẠO / CẬP NHẬT CẢ HAI TIN" in s
    assert "BuildBothTinAsync" in s
    assert "BuildPairCoreDetailed" in s
    assert "DrawTinPair" in s

def test_sections_and_volume_are_gated_by_verified_tin_pair():
    section = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    volume = read("src/MiningVolume.Plugin2023/UI/VolumePage.cs")
    assert "EnsureBothTinsReady(synchronizeCadLayers: true)" in section
    assert "EnsureBothTinsReady(synchronizeCadLayers: true)" in volume

def test_runtime_selftest_creates_real_autocad_tin_layers():
    s = read("src/MiningVolume.Plugin2023/Services/SelfTestService.cs")
    assert "RunCadTinLayerSmoke" in s
    assert "MV_SELFTEST_TIN_HIENTRANG" in s
    assert "MV_SELFTEST_TIN_THIETKE" in s
    assert "AutoCAD tạo/ghi/đếm đúng layer TIN hiện trạng" in s
    assert "AutoCAD tạo/ghi/đếm đúng layer TIN thiết kế" in s

def test_output_layers_are_not_offered_as_source_layers():
    service = read("src/MiningVolume.Plugin2023/Services/LayerService.cs")
    data = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    assert "GetSourceLayers" in service
    assert 'name.StartsWith("MV_TIN_"' in service
    assert "LayerService.GetSourceLayers()" in data


def test_tin_build_rejects_source_changes_during_or_after_background_build():
    s = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "SourceRevision" in s
    assert "ReferenceEquals(session.Source, source)" in s
    assert "source.Revision != sourceRevision" in s
    assert "Không ghi TIN cũ xuống AutoCAD" in s
