"""Guard preflight-first HT/TK rendering on real AutoCAD host.

The licensed AutoCAD 2023 runtime revealed that rollback after partial TIN
deletion did not reliably restore the original faces. Source checks complement
(not substitute for) the native side-DB MVSELFTEST.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_pair_validates_both_meshes_and_both_layers_before_erasing_once():
    src = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    pair = src.split("public static int[] ReplacePair(", 1)[1].split(
        "private static void ValidateSurfaceBeforePairMutation(", 1
    )[0]
    assert pair.count("StartTransaction()") == 1
    assert pair.count("ReplaceInTransaction(") == 2
    assert pair.index("ValidateSurfaceBeforePairMutation(existing, existingLayer)") < pair.index(
        "ValidateSurfaceBeforePairMutation(design, designLayer)") < pair.index(
        "ValidateExistingPairBeforeMutation(db, existingLayer, designLayer)") < pair.index(
        "StartTransaction()"
    )
    assert pair.index("ClearExistingPairInTransaction(") < pair.index(
        "ReplaceInTransaction("
    ) < pair.index("tr.Commit()")
    assert "existingLayer, existing, 1, false, false" in pair
    assert "designLayer, design, 3, false, false" in pair

    validation = src.split("private static void ValidateSurfaceBeforePairMutation(", 1)[1].split(
        "private static List<ObjectId> ValidateExistingPairBeforeMutation(", 1
    )[0]
    assert "ValidateTriangle(triangle)" in validation
    assert "ValidateVerticalFace(triangle)" in validation
    assert "tin.Triangles == null || tin.Triangles.Count == 0" in validation
    assert "progress.Poll();" in validation
    assert "StartTransaction()" not in validation

    inspection = src.split("private static List<ObjectId> ValidateExistingPairBeforeMutation(", 1)[1].split(
        "private static void ClearExistingPairInTransaction(", 1
    )[0]
    assert inspection.count("foreach (ObjectId id in ms)") == 1
    assert "entity.LayerId == existingId" in inspection
    assert "entity.LayerId == designId" in inspection
    assert "if (!(entity is Face))" in inspection
    assert "oldFaces.Add(id)" in inspection
    assert "progress.Poll();" in inspection
    assert "OpenMode.ForWrite" not in inspection
    assert ".Erase();" not in inspection

    cleanup = src.split("private static void ClearExistingPairInTransaction(", 1)[1].split(
        "private static int ReplaceInTransaction(", 1
    )[0]
    assert "foreach (ObjectId id in originalFaces)" in cleanup
    assert "face.Erase();" in cleanup
    assert "progress.Poll()" not in cleanup
    assert "OperationCanceledException" not in cleanup

    writing = src.split("private static int ReplaceInTransaction(", 1)[1].split(
        "public static int Clear(", 1
    )[0]
    assert "if (clearExisting)" in writing
    assert "foreach (var t in tin.Triangles)" in writing
    assert "foreach (var t in tin.VerticalFaces)" in writing
    assert "allowCancellation &&" in writing
    assert "if (allowCancellation) progress.Poll(written, true)" in writing


def test_host_regression_checks_failed_pair_does_not_lose_original_handles():
    regress = read("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    assert "VerifyTinPairRollbackAfterSecondSurfaceFailure();" in regress
    assert 'TinCadRenderer.ReplacePair(db, "QA_PAIR_HT", plane(20), "QA_PAIR_TK", invalid)' in regress
    assert "TinPairHandles(db) != original" in regress
    assert "TinPairHandles(db) == original" in regress


def test_host_qa_foreign_entity_cleanup_respects_tin_layer_lock():
    """Host found eOnLockedLayer while removing QA-only DBText from TK.

    Any unlock belongs solely to test fixture cleanup, after production
    ReplacePair has already refused the foreign object without mutation.
    """
    src = read("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    qa = src.split("private static void VerifyTinPairRollbackAfterSecondSurfaceFailure()", 1)[1].split(
        "private static string TinPairHandles(", 1
    )[0]
    assert "CountUnexpectedEntities(db, \"QA_PAIR_TK\") != 1" in qa
    assert "TinPairHandles(db) != original" in qa
    assert "bool wasLocked = tkLayer.IsLocked;" in qa
    assert "tkLayer.IsLocked = false;" in qa
    assert "((Entity)tr.GetObject(foreignId, OpenMode.ForWrite)).Erase();" in qa
    assert "tkLayer.IsLocked = wasLocked;" in qa
    assert qa.index("tkLayer.IsLocked = false;") < qa.index(
        "tr.GetObject(foreignId, OpenMode.ForWrite)") < qa.index(
        "tkLayer.IsLocked = wasLocked;"
    ) < qa.index("tr.Commit();", qa.index("tkLayer.IsLocked = wasLocked;"))
    assert "!TinCadRenderer.IsLayerLocked(db, \"QA_PAIR_TK\")" in qa
    assert "TinPairHandles(db) != original" in qa
    assert qa.index("if (TinCadRenderer.CountUnexpectedEntities(db, \"QA_PAIR_TK\") != 0") < qa.rindex(
        'TinCadRenderer.ReplacePair(db, "QA_PAIR_HT", plane(12), "QA_PAIR_TK", plane(2))'
    )

    # Production preflight stays READ-ONLY and must not temporarily unlock
    # either layer to inspect a foreign entity.
    renderer = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    preflight = renderer.split("private static List<ObjectId> ValidateExistingPairBeforeMutation(", 1)[1].split(
        "private static void ClearExistingPairInTransaction(", 1
    )[0]
    assert "OpenMode.ForWrite" not in preflight
    assert "IsLocked = false" not in preflight
