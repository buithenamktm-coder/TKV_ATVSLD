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
