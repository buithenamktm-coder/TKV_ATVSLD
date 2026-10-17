from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def source(p):
    return (ROOT / p).read_text(encoding="utf-8")


def body(text, start, end):
    return text.split(start, 1)[1].split(end, 1)[0]


def test_manual_model_edits_are_not_applied_before_autocad_invalidation():
    ui = source("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    sections = (
        ("private void DisableSelectedVertices(", "private void DisableSelectedEntities(", "SetVertexEnabled("),
        ("private void DisableSelectedEntities(", "private void RestoreSelected(", "RemoveEntityFromModel("),
        ("private void RestoreSelected(", "private void RestoreAll(", "RestoreEntity("),
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


def test_dwg_project_restore_releases_unadopted_file_backed_tin_stores():
    project = source("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    restore = project.split("private static void RebuildSavedTins(", 1)[1].split(
        "private static void TryRebuildTin(", 1
    )[0]
    assert "SurfaceBuildResult[] builds = null;" in restore
    assert "finally" in restore
    assert "foreach (var build in builds)" in restore
    assert "!ReferenceEquals(ProjectState.Current.Get(build.Role).Tin, build.Tin)" in restore
    assert "(build.Tin.Triangles as IDisposable)?.Dispose();" in restore
    single = project.split("private static void TryRebuildTin(", 1)[1]
    assert "SurfaceBuildResult build = null;" in single
    assert "finally" in single
    assert "!ReferenceEquals(session.Tin, build.Tin)" in single
    assert "(build.Tin.Triangles as IDisposable)?.Dispose();" in single


def test_dwg_restore_does_not_erase_saved_tin_before_replacement_is_ready():
    workflow = source("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    project = source("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    assert "bool clearPreviousCadTin = true" in workflow
    assert "InvalidateTin(role, clearCadLayer: clearPreviousCadTin, notify: false);" in workflow
    restore = project.split("private static void RestoreModel(", 1)[1].split(
        "private static void RebuildSavedTins(", 1
    )[0]
    assert "SurfaceWorkflowService.LoadLayer(role, snap.Layer, types, clearPreviousCadTin: false)" in restore
    assert "SurfaceWorkflowService.LoadSelectedHandles(role, snap.SelectedHandles, types, clearPreviousCadTin: false)" in restore
    rebuild = project.split("private static void RebuildSavedTins(", 1)[1].split(
        "private static void TryRebuildTin(", 1
    )[0]
    assert "SurfaceWorkflowService.DrawTinPair(builds);" in rebuild
    assert "SurfaceWorkflowService.InvalidateTin(ModelRole.Existing, clearCadLayer: false" in rebuild
    assert "SurfaceWorkflowService.InvalidateTin(ModelRole.Design, clearCadLayer: false" in rebuild
