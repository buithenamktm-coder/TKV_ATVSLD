"""Regression: deletion of old DWG TIN is physical, atomic, source-safe.

These are source-contract tests. Real AutoCAD DWG reopening still requires
Windows host acceptance and must not be inferred from a green CI result.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SVC = ROOT / "src" / "MiningVolume.Plugin2023" / "Services"
UI = ROOT / "src" / "MiningVolume.Plugin2023" / "UI"


def code(path):
    return path.read_text(encoding="utf-8")


def test_delete_uses_persisted_dwg_faces_without_session_model():
    model = code(UI / "ModelPage.cs")
    service = code(SVC / "SurfaceWorkflowService.cs")
    state = code(SVC / "ProjectState.cs")
    assert '"MV_TIN_HIENTRANG"' in state
    assert '"MV_TIN_THIETKE"' in state
    assert "TinCadRenderer.InspectLayers(doc.Database, names)" in model
    assert "cadFaces = inspection[names[0]].Faces + inspection[names[1]].Faces;" in model
    assert "SurfaceWorkflowService.DeleteAllTinsStoredInCurrentDrawing()" in model
    assert "if (cadFaces == 0 && !hasSession)" in model
    assert "TinCadRenderer.ClearPair(doc.Database" in service
    assert "InvalidateTin(ModelRole.Existing, clearCadLayer: false, notify: false);" in service
    assert "InvalidateTin(ModelRole.Design, clearCadLayer: false, notify: false);" in service
    assert service.index("removed = TinCadRenderer.ClearPair(") < service.index(
        "InvalidateTin(ModelRole.Existing, clearCadLayer: false, notify: false);")


def test_cad_delete_is_two_phase_fail_closed_and_transactional():
    renderer = code(SVC / "TinCadRenderer.cs")
    begin = renderer.index("public static int ClearPair(Database db, string existingLayer")
    end = renderer.index("private static void ValidateSurfaceBeforePairMutation", begin)
    operation = renderer[begin:end]
    assert "ValidateExistingPairBeforeMutation(" in operation
    assert "using (var tr = db.TransactionManager.StartTransaction())" in operation
    assert "face.Erase();" in operation
    assert "afterClearInSameTransaction?.Invoke(tr);" in operation
    assert "tr.Commit();" in operation
    assert operation.index("ValidateExistingPairBeforeMutation(") < operation.index("face.Erase();")
    assert operation.index("afterClearInSameTransaction?.Invoke(tr);") < operation.index("tr.Commit();")
    assert "EnsureLayer(" not in operation  # do not synthesize output layers
    assert "layer.IsLocked = lockState.Value;" in operation
    assert "ent.Erase()" not in operation  # never erase arbitrary survey entity
    assert "if (!(entity is Face))" in renderer
    assert 'Không xóa tự động để tránh mất dữ liệu CAD.' in renderer


def test_project_restore_flags_are_invalidated_in_same_cad_transaction():
    service = code(SVC / "SurfaceWorkflowService.cs")
    persistence = code(SVC / "ProjectPersistenceService.cs")
    assert "tr => ProjectPersistenceService.MarkTinModelsDeletedInTransaction(" in service
    begin = persistence.index("internal static void MarkTinModelsDeletedInTransaction(")
    end = persistence.index("public static ProjectLoadResult LoadCurrentProject(", begin)
    routine = persistence[begin:end]
    for marker in (
        "ProjectSnapshotCodec.Decode(FromBuffer(rec.Data))",
        "snap.Existing.HadTin = false;",
        "snap.Design.HadTin = false;",
        "snap.HadProfiles = false;",
        "snap.HadVolumeResult = false;",
        "rec.Data = ToBuffer(ProjectSnapshotCodec.Encode(snap));",
    ):
        assert marker in routine
    assert "DeleteSavedProject(" not in routine
    assert "snap.Existing.Layer = null" not in routine
    assert "snap.Design.Layer = null" not in routine


def test_user_is_told_when_legacy_faces_are_on_nonstandard_layers():
    model = code(UI / "ModelPage.cs")
    assert "Nếu TIN cũ nằm trên layer khác" in model
    assert "MessageBoxButtons.YesNo" in model
    assert "MessageBoxDefaultButton.Button2" in model
    assert "Tin3DViewService.Stop()" in model
