from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT/path).read_text(encoding="utf-8")


def test_grid_uses_both_accepted_tins_without_building_again():
    grid=read("src/MiningVolume.Plugin2023/UI/GridPage.cs")
    assert "SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers:false)" in grid
    assert "var existing=st.Existing.Tin;var design=st.Design.Tin;" in grid
    assert "new GridVolumeCalculator().Calculate(existing,design," in grid
    assert "SurfaceWorkflowService.BuildCoreDetailed(" not in grid
    assert "SurfaceWorkflowService.DrawTin(" not in grid
    assert "GridWorkflowService.Commit(result,options,existing,design" in grid
    assert "ĐÃ CÓ TIN HIỆN TRẠNG (" in grid
    assert "KHÔNG cần dựng TIN mới." in grid
    assert "Chỉ tạo/cập nhật mô hình còn thiếu" in grid


def test_reloading_identical_survey_retains_original_tins_and_approved_calculations():
    workflow=read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    block=workflow.split("private static SurfaceLoadResult CommitLoadedSource(",1)[1].split(
        "private const int LargeDatasetVertexThreshold",1)[0]
    assert "session.IsTinCurrent" in block
    assert "session.AllowedTypes.SetEquals(" in block
    assert "SourceFingerprint.ComputeModel(session.Source)" in block
    assert "SourceFingerprint.ComputeModel(model)" in block
    assert "EnsureTinRegionMatchesCad();" in block
    assert "ReusedExistingTin = true" in block
    assert block.index("ReusedExistingTin = true") < block.index(
        "InvalidateTin(role, clearCadLayer: clearPreviousCadTin"
    )
    data=read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    assert "r.ReusedExistingTin" in data
    assert "result.ReusedExistingTin" in data


def test_selecting_same_clip_or_clearing_absent_clip_never_stales_a_tin():
    data=read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    picked=data.split("private void SelectTinRegion()",1)[1].split(
        "private void ClearTinRegion()",1)[0]
    assert "bool sameRegion =" in picked
    assert "if (sameRegion)" in picked
    assert "Vùng tạo TIN không thay đổi" in picked
    assert picked.index("if (sameRegion)") < picked.index("state.MarkTinRegionChanged();")
    clear=data.split("private void ClearTinRegion()",1)[1].split(
        "private void RefreshTinRegionStatus()",1)[0]
    assert "if (!hadRegion && string.IsNullOrWhiteSpace(state.TinRegionHandle))" in clear
    assert "if (hadRegion) state.MarkTinRegionChanged();" in clear
    assert "Chưa giới hạn vùng TIN; hai mô hình hiện có được giữ nguyên." in clear
