"""CAD section/preview first-export optimization contracts.

These are source assertions, not proof of AutoCAD host speed. The expensive
ModelSpace erase must be skipped ONLY when none of the target output layers
existed prior to creation. On a redraw, previously existing layers are still
fully searched and erased with the existing cancellation/transaction contract.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = (ROOT / "src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")


def code():
    return SOURCE.read_text(encoding="utf-8")


def test_preview_first_draw_skips_erase_only_when_no_output_layer_preexisted():
    s = code()
    preview = s.split("public static void ReplacePreview(", 1)[1].split(
        "public static void ClearPreview(", 1
    )[0]
    assert 'ExistingLayerIds(db, tr, PreviewLayer, PreviewLabelLayer)' in preview
    assert preview.index("var oldLayers = ExistingLayerIds(") < preview.index(
        "EnsureLayer(db, tr, PreviewLayer, 2)"
    )
    assert "if (oldLayers.Count > 0)\n                    EraseOnLayers(ms, tr, oldLayers);" in preview
    assert preview.index("EraseOnLayers(ms, tr, oldLayers)") < preview.index(
        "foreach (var line in system.Lines)"
    )
    assert "tr.Commit();" in preview


def test_section_first_draw_checks_existing_layers_before_creating_any():
    s = code()
    draw = s.split("public static double ReplaceProfilesBatched(", 1)[1].split(
        "public static void DrawQuickProfiles(", 1
    )[0]
    assert "var oldLayers = clearFirst" in draw
    assert "ExistingLayerIds(db, tr, ExistingLayer, DesignLayer, AxisLayer, TextLayer)" in draw
    assert draw.index("var oldLayers = clearFirst") < draw.index(
        "EnsureLayer(db, tr, ExistingLayer, 1)"
    )
    assert "if (clearFirst && oldLayers.Count > 0)\n                    EraseOnLayers(ms, tr, oldLayers, pollCancellation, recordErased);" in draw
    assert draw.index("EraseOnLayers(ms, tr, oldLayers") < draw.index(
        "for (int pi = startIndex;"
    )
    assert "pollCancellation?.Invoke();" in draw
    assert "tr.Commit();" in draw


def test_previously_existing_layers_are_preserved_as_erase_targets():
    s = code()
    capture = s.split("private static List<ObjectId> ExistingLayerIds(", 1)[1].split(
        "private static void EraseOnLayers(", 1
    )[0]
    assert "tr.GetObject(db.LayerTableId, OpenMode.ForRead)" in capture
    assert "table.Has(name)" in capture
    assert "result.Add(table[name])" in capture
    assert "EnsureLayer(" not in capture
    erase = s.split("private static void EraseOnLayers(", 1)[1].split(
        "private static ObjectId EnsureLayer(", 1
    )[0]
    assert "id.ObjectClass.IsDerivedFrom(faceClass)" in erase
    assert "set.Contains(e.LayerId)" in erase
    assert "e.UpgradeOpen();" in erase
    assert "trackErased?.Invoke(id);" in erase
    assert "e.Erase();" in erase
    assert "pollCancellation?.Invoke();" in erase
