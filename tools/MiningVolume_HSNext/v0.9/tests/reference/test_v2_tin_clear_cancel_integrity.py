"""Safety of native CAD TIN erase for mine drawings with millions of 3DFACEs.

These are source guards; the host-side SectionCadRegression must be run
inside licensed AutoCAD 2023 before final product acceptance.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(relative):
    return (ROOT / relative).read_text(encoding="utf-8")


def test_clear_cancellation_before_any_cad_mutation():
    renderer = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    clear = renderer.split("public static int Clear(", 1)[1].split(
        "public static int CountFaces(", 1
    )[0]
    assert "Action pollCancellation = null" in clear
    assert clear.count("StartOpenCloseTransaction()") == 1
    assert clear.count("StartTransaction()") == 1
    assert "new List<ObjectId>()" in clear
    assert 'new SectionCadProgress(1, "kiểm tra TIN trước khi xóa "' in clear
    assert "(visited++ & 1023) == 0" in clear
    assert "pollCancellation?.Invoke();" in clear
    assert "progress.Poll();" in clear
    assert "faces.Add(id);" in clear
    assert "if (!(ent is Face))" in clear
    assert "face.Erase();" in clear
    assert "tr.Commit();" in clear

    # No writes to the CAD DB in the cancellable preflight.
    validation = clear.split("using (var check =", 1)[1].split(
        "using (var tr = db.TransactionManager.StartTransaction())", 1
    )[0]
    mutation = clear.split("using (var tr = db.TransactionManager.StartTransaction())", 1)[1]
    assert "OpenMode.ForWrite" not in validation
    assert ".Erase()" not in validation
    assert "IsLocked = false" not in validation
    assert "MoveCurrentLayerAwayIfNeeded" not in validation
    assert "pollCancellation?.Invoke();" in validation

    # Once destructive deletion starts, do not poll Esc or invoke user callbacks:
    # AutoCAD 2023 host reported that mid-erase cancellation was not fully
    # restored by a disposed transaction, losing part of the TIN.
    assert "face.Erase();" in mutation
    assert "pollCancellation?.Invoke();" not in mutation
    assert "progress.Poll();" not in mutation
    assert "new SectionCadProgress" not in mutation
    assert mutation.index("face.Erase();") < mutation.index("tr.Commit();")



def test_cancellation_keeps_model_state_and_host_regression():
    model = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    assert 'catch (OperationCanceledException) {' in model
    assert "TIN và kết quả khối lượng trước thao tác được giữ nguyên" in model
    svc = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    invalid = svc.split("public static void InvalidateTin(", 1)[1].split(
        "public static void EnsureBothTinsReady(", 1
    )[0]
    assert invalid.index("TinCadRenderer.Clear(") < invalid.index("session.Tin = null;")
    # No separate, committed PrepareLayer transaction before cancellation:
    # even layer visibility and existence must remain untouched on failure.
    assert "TinCadRenderer.PrepareLayer(" not in invalid

    qa = read("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    assert "VerifyTinClearRollbackOnCancelAndForeignObject();" in qa
    assert "CancelAt=" in qa
    assert 'faces=" + remaining' in qa
    assert "new[] { 1, 2, 3, 4 }" in qa
    assert "TinCadRenderer.CountFaces(db, layer)" in qa
    assert "TinCadRenderer.CountUnexpectedEntities(db, layer) != 1" in qa
    assert "TinCadRenderer.Clear(db, layer) != 2300" in qa
