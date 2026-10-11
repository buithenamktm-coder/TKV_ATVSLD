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


def test_review_has_one_guarded_model_only_edit_flow():
    ui=code("src/MiningVolume.Plugin2023/UI/DuplicateXYConflictUi.cs")
    wrapper=code("src/MiningVolume.Plugin2023/UI/MeanZConflictReviewForm.cs")
    confirm=code("src/MiningVolume.Plugin2023/UI/ModelEditConfirmation.cs")
    service=code("src/MiningVolume.Plugin2023/Services/MeanZPreviewWorkflowService.cs")
    assert "MeanZConflictReviewForm : ConflictReviewForm" in wrapper
    assert "private async Task ExcludeVertexAsync(Row row)" in ui
    assert "private async Task EditVertexZAsync(Row row)" in ui
    assert "Bạn có chắc chắn muốn loại điểm này khỏi mô hình không?" in ui
    assert 'Text="Không"' in confirm and 'Text="Đồng ý"' in confirm
    assert "dialog.AcceptButton=no;dialog.CancelButton=no;" in confirm
    assert "_report=null;_rows.Clear();_grid.RowCount=0;_grid.Enabled=false;" in ui
    assert "foreach(var source in g.Sources.OrderBy" in ui
    assert "RestoreVertices(" in ui
    assert "Erase(" not in ui and "OpenMode.ForWrite" not in ui
    assert service.index("if (prepared.HasBlockingConflicts)") < service.index(".BuildPrepared(")


def test_persisted_edits_require_matching_original_geometry():
    persistence=code("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    snapshot=code("src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs")
    assert "edit.SourceFingerprint!=SourceFingerprint.Compute(entity)" in persistence
    assert "rebuildDerived=false" in persistence
    assert "s.EditHistory.AddRange(session.Source.EditHistory)" in persistence
    assert "128L*1024*1024" in snapshot

def test_z_mean_review_opens_grouped_editor_instead_of_generic_error():
    ui=code("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    editor=code("src/MiningVolume.Plugin2023/UI/DuplicateXYConflictUi.cs")
    review=code("src/MiningVolume.Plugin2023/UI/MeanZConflictReviewForm.cs")
    first=ui.index("catch(MeanZReviewRequiredException ex)")
    fallback=ui.index("catch(Exception ex)",first)
    assert first<fallback
    assert "new MeanZConflictReviewForm(role, ex.Analysis)" in ui[first:fallback]
    assert "_document" in editor and "await ScanAsync();" in editor
    assert "ConflictReviewForm" in review
    assert 'if(_highDeltaOnly.Checked && !(g.DeltaZ>HighDeltaZMetres))continue;' in editor
    assert '"Bạn có chắc chắn muốn loại điểm này khỏi mô hình không?"' in editor
    assert "MeanZPreviewPreparer.Prepare(source,1e-6,1e-4,token,polygon)" in editor

def test_preview_review_memory_is_allocated_only_for_conflicting_xy():
    core=code("src/MiningVolume.Core/Surface/MeanZPreviewPreparer.cs")
    assert "internal List<Member> Members;" in core
    assert "if(Members==null)" in core
    assert "FirstEntity=entity,FirstVertexIndex=vertex.Index" in core
    assert "next.Members.Add(" not in core
