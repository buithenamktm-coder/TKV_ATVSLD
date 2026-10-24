from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def source(p):
    return (ROOT / p).read_text(encoding="utf-8")


def body(text, start, end):
    return text.split(start, 1)[1].split(end, 1)[0]


def test_manual_model_edits_are_not_applied_before_autocad_invalidation():
    ui = source("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    sections = (
        ("private void DisableSelectedVertices(", "private void DisableSelectedEntities(", "SetVerticesEnabled("),
        ("private void DisableSelectedEntities(", "private void RestoreSelected(", "SetEntitiesEnabled("),
        ("private void RestoreSelected(", "private void RestoreAll(", "RestoreSelectedVertices("),
        ("private void RestoreAll(", "private void EditZ(", "RestoreVertices("),
        ("private void EditZ(", "private void ZoomSelected(", "OverrideVertexZ("),
    )
    for start, end, mutator in sections:
        part = body(ui, start, end)
        assert "SurfaceWorkflowService.InvalidateTin(" in part, start
        assert part.index("SurfaceWorkflowService.InvalidateTin(") < part.index(mutator), start
        assert "ProjectState.Current.NotifyChanged();" in part, start


def test_invalidate_tin_clears_cad_before_disposal_and_clears_grid_report():
    workflow = source("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    part = body(workflow, "public static void InvalidateTin(", "private static void EnsureTinRegionMatchesCad()")
    assert part.index("TinCadRenderer.Clear(doc.Database, session.TinLayer)") < part.index(
        "session.Tin = null;"
    )
    assert part.index("TinCadRenderer.Clear(doc.Database, session.TinLayer)") < part.index(
        "GridWorkflowService.Invalidate();"
    )
    assert "GridWorkflowService.Invalidate();" in part
    assert "session.TinBuiltFromRegionRevision = null;" in part


def test_dwg_project_restore_uses_original_verified_faces_without_retriangulation():
    project = source("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    workflow = source("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    renderer = source("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    restore = project.split("private static void RebuildSavedTins(", 1)[1].split(
        "private static double PositiveOr(", 1)[0]
    assert "SurfaceWorkflowService.TryRestoreTinFromCad(role)" in restore
    assert "BuildPairCoreDetailed()" not in restore
    assert "BuildCoreDetailed(role)" not in restore
    assert "DrawTinPair(" not in restore
    assert "DrawTin(role" not in restore
    assert "session.CadRecoveryBlocked" in restore
    assert "TinCadRenderer.ReadStoredTin(" in workflow
    reader = renderer.split("public static TinSurface ReadStoredTin(", 1)[1].split(
        "private static bool Finite(Point3d", 1)[0]
    assert "StartOpenCloseTransaction()" in reader
    assert ".Erase();" not in reader
    assert "tr.Commit()" not in reader


def test_restore_does_not_touch_saved_tin_or_edit_survey_without_verification():
    workflow = source("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    project = source("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    assert "bool clearPreviousCadTin = true" in workflow
    assert "InvalidateTin(role, clearCadLayer: clearPreviousCadTin, notify: false);" in workflow
    restore = project.split("private static void RestoreModel(", 1)[1].split(
        "private static void RebuildSavedTins(", 1)[0]
    assert "SurfaceWorkflowService.LoadLayer(role, snap.Layer, types, clearPreviousCadTin: false)" in restore
    assert "SurfaceWorkflowService.LoadSelectedHandles(role, snap.SelectedHandles, types, clearPreviousCadTin: false)" in restore
    assert "SourceFingerprint.ComputeModel(session.Source)" in restore
    assert "session.CadRecoveryBlocked = true;" in restore
    recovered = project.split("private static void RebuildSavedTins(", 1)[1].split(
        "private static double PositiveOr(", 1)[0]
    assert "SurfaceWorkflowService.TryRestoreTinFromCad(role)" in recovered
    assert "SurfaceWorkflowService.DrawTinPair(" not in recovered
    assert "SurfaceWorkflowService.DrawTin(" not in recovered
