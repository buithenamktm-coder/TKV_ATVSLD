"""Guard atomic dual TIN output against repeated million-face CAD cleanup.
Only source/contract checks run in CI; runtime proof requires AutoCAD 2023.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_pair_clears_both_layers_before_writing_once():
    src = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    pair = src.split("public static int[] ReplacePair(", 1)[1].split(
        "private static void ClearExistingPairInTransaction(", 1
    )[0]
    assert pair.count("StartTransaction()") == 1
    assert pair.count("ReplaceInTransaction(") == 2
    assert pair.index("ClearExistingPairInTransaction(") < pair.index(
        "ReplaceInTransaction("
    ) < pair.index("tr.Commit()")
    assert "existingLayer, existing, 1, false" in pair
    assert "designLayer, design, 3, false" in pair

    cleanup = src.split("private static void ClearExistingPairInTransaction(", 1)[1].split(
        "private static int ReplaceInTransaction(", 1
    )[0]
    assert cleanup.count("foreach (ObjectId id in ms)") == 1
    assert "ent.LayerId == existingId" in cleanup
    assert "ent.LayerId == designId" in cleanup
    assert "if (!(ent is Face))" in cleanup
    assert "ent.Erase()" in cleanup
    assert "progress.Poll()" in cleanup

    single = src.split("private static int ReplaceInTransaction(", 1)[1].split(
        "public static int Clear(", 1
    )[0]
    assert "if (clearExisting)" in single
    assert "foreach (var t in tin.Triangles)" in single
    assert "foreach (var t in tin.VerticalFaces)" in single


def test_pair_failure_retains_original_faces_through_host_regression():
    regress = read("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    assert "VerifyTinPairRollbackAfterSecondSurfaceFailure();" in regress
    assert 'TinCadRenderer.ReplacePair(db, "QA_PAIR_HT", plane(20), "QA_PAIR_TK", invalid)' in regress
    assert "TinPairHandles(db) != original" in regress
    assert "TinPairHandles(db) == original" in regress
