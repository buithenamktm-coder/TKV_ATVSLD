from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
def code(path):
    return (ROOT / path).read_text(encoding="utf-8")

def test_tin_z_mean_test_is_explicitly_opt_in_and_cannot_become_official_tin():
    ui=code("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    svc=code("src/MiningVolume.Plugin2023/Services/MeanZPreviewWorkflowService.cs")
    assert 'TIN Z TRUNG BÌNH – THỬ' in ui
    assert 'KHÔNG DÙNG LƯỚI NÀY ĐỂ TÍNH KHỐI LƯỢNG' in ui
    assert 'LicenseUiGuard.Check()' in ui
    assert 'MV_TIN_ZMEAN_HT_TEST_ONLY' in svc
    assert 'MV_TIN_ZMEAN_TK_TEST_ONLY' in svc
    assert 'TinCadRenderer.Replace(doc.Database,result.Layer' in svc
    assert 'session.Tin =' not in svc
    assert 'VolumeResult =' not in svc
    assert 'SectionProfiles.Clear()' not in svc
    assert 'ClipBoundary = boundary' in svc
    assert 'cancellationToken, boundary)' in svc
    assert 'mean_z_preview_region_mesh_area' in code('tests/core/CoreRegression.cs')
    assert 'SourceRevision=revision' in svc

def test_z_mean_preparer_keeps_raw_z_and_discards_invalid_constraints():
    svc=code("src/MiningVolume.Core/Surface/MeanZPreviewPreparer.cs")
    assert 'AverageZ += (z-AverageZ)/N' in svc
    assert 'new Vec3(g.XY.X,g.XY.Y,g.AverageZ)' in svc
    assert 'prepared.Breaklines.Add(' not in svc
    assert 'SourceModel.Touch()' not in svc
    assert 'public bool ForOfficialVolumes => false' in svc

def test_test_tin_can_be_cancelled_and_csv_audited():
    ui=code("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    svc=code("src/MiningVolume.Plugin2023/Services/MeanZPreviewWorkflowService.cs")
    assert '_meanZCts?.Cancel()' in ui
    assert 'token.ThrowIfCancellationRequested()' in ui
    assert 'MeanZPreviewWorkflowService.ExportCsv(result' in ui
    assert 'new UTF8Encoding(true)' in svc
    assert 'Conflicts.OrderByDescending(c=>c.DeltaZ)' in svc

def test_native_core_regressions_are_present():
    src=code("tests/core/CoreRegression.cs")
    for name in (
        'mean_z_preview_all_entities_and_original_unchanged',
        'mean_z_preview_disabled_inputs_and_identical_z',
        'mean_z_preview_strict_mode_still_rejects_conflicts',
    ):
        assert name in src
