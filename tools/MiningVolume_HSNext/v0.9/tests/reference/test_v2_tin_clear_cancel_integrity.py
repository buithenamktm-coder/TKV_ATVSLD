"""Safety of native CAD TIN erase for mine drawings with millions of 3DFACEs.

These are source guards; the host-side SectionCadRegression must be run
inside licensed AutoCAD 2023 before final product acceptance.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(relative):
    return (ROOT / relative).read_text(encoding="utf-8")


def test_clear_cancellable_with_single_atomic_transaction():
    renderer = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    clear = renderer.split("public static int Clear(", 1)[1].split(
        "public static int CountFaces(", 1
    )[0]
    assert "Action pollCancellation = null" in clear
    assert clear.count("StartTransaction()") == 1
    assert "new TinCadProgress(0, \"xóa \" + layerName)" in clear
    assert "(scanned++ & 1023) == 0" in clear
    assert "pollCancellation?.Invoke();" in clear
    assert "progress.Poll(0, false);" in clear
    assert "ent.UpgradeOpen();" in clear
    assert "ent.Erase();" in clear
    assert "tr.Commit();" in clear
    assert clear.index("progress.Poll(0, false)") < clear.index("ent.Erase();")
    assert clear.index("pollCancellation?.Invoke();", clear.index("ent.Erase();")) < (
        clear.index("tr.Commit();", clear.index("ent.Erase();"))
    )
    assert 'if (!(ent is Face))' in clear


def test_cancellation_keeps_model_state_and_host_regression():
    model = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    assert 'catch (OperationCanceledException) {' in model
    assert "TIN và kết quả khối lượng trước thao tác được giữ nguyên" in model
    svc = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    invalid = svc.split("public static void InvalidateTin(", 1)[1].split(
        "public static void EnsureBothTinsReady(", 1
    )[0]
    assert invalid.index("TinCadRenderer.Clear(") < invalid.index("session.Tin = null;")

    qa = read("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    assert "VerifyTinClearRollbackOnCancelAndForeignObject();" in qa
    assert "TinCadRenderer.CountFaces(db, layer) != 2300" in qa
    assert "TinCadRenderer.CountUnexpectedEntities(db, layer) != 1" in qa
    assert "TinCadRenderer.Clear(db, layer) != 2300" in qa
