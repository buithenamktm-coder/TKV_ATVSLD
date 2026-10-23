"""Section CAD drawing regression: a million-face mine TIN must not require
opening each 3DFACE merely to erase IMSAT section LINE/POLYLINE/TEXT.
Static verification only; real runtime safety uses SectionCadRegression.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_section_erase_classifies_ids_before_opening_entities():
    renderer = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    erase = renderer.split("private static void EraseOnLayers(", 1)[1].split(
        "private static ObjectId EnsureLayer(", 1
    )[0]
    assert "RXObject.GetClass(typeof(Face))" in erase
    assert "id.ObjectClass.IsDerivedFrom(faceClass)" in erase
    assert erase.index("id.ObjectClass.IsDerivedFrom(faceClass)") < erase.index(
        "tr.GetObject(id, OpenMode.ForRead)"
    )
    assert "set.Contains(e.LayerId)" in erase
    assert "e.UpgradeOpen(); e.Erase();" in erase
    assert "pollCancellation?.Invoke()" in erase


def test_section_redraw_preserves_atomic_rollback_and_tagged_polylines():
    service = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    draw = service.split("public static void DrawProfiles(", 1)[1].split(
        "public static void ClearPreview(", 1
    )[0]
    assert "allSections = doc.Database.TransactionManager.StartTransaction()" in draw
    assert "meter.Poll" in draw
    assert draw.index("ReplaceProfilesBatched(") < draw.index("allSections.Commit()")
    renderer = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    assert "StampSection(a, section, \"HT\"" in renderer
    assert "StampSection(c, section, \"TK\"" in renderer

    host = read("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    assert "VerifySectionEraseSkipsTinFaces();" in host
    assert "TinCadRenderer.CountFaces(db, tinLayer) != 2048" in host
    assert "new OperationCanceledException()" in host
