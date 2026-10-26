"""P0 fixes witnessed by real AutoCAD 2023 host MVSELFTEST.

Static CI checks document protections; only rerunning actual host tests can
establish whether AutoCAD transactions and cancellation truly roll back.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def load(p):
    return (ROOT / p).read_text(encoding="utf-8-sig")


def test_grid_labels_retain_identity_and_cell_layer_is_strict():
    renderer = load("src/MiningVolume.Plugin2023/Services/GridCadRenderer.cs")
    regression = load("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    assert "label.XData = identity;" in renderer
    assert "poly.XData = identity;" in renderer
    assert 'string.Equals(entity.Layer, "MV_LUOI_NHAN"' in regression
    assert "labelLayer ? !(entity is DBText) : !(entity is Polyline)" in regression
    assert "oldLabels != first.Cells.Count || newLabels != second.Cells.Count" in regression
    assert "Missing grid calculation XData." in regression


def test_cancelled_quantity_block_removes_only_its_unique_reference_definition():
    service = load("src/MiningVolume.Plugin2023/Services/CadVolumeTableService.cs")
    assert "string exportBlockName = BlockPrefix + result.CalculationId" in service
    assert "ObjectId exportReferenceId = ObjectId.Null;" in service
    assert "RemoveAbortedExport(database, exportBlockName, exportReferenceId)" in service
    assert "tr.GetObject(referenceId, OpenMode.ForWrite, true)" in service
    assert "obj.Erase();" in service
    assert "record.Erase();" in service
    assert '"Refusing to erase a reference not owned by the aborted quantity export."' in service
    assert "throw new AggregateException(" in service
    assert "tr.Abort();" in service


def test_quick_section_cancel_tracks_only_new_entities():
    service = load("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    regression = load("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    start = service.index("public static void DrawQuickProfiles(")
    end = service.index("private static void RemoveAbortedQuickEntities(", start)
    quick = service[start:end]
    assert "var createdByQuick = new List<ObjectId>();" in quick
    assert "createdByQuick.Add" in quick
    assert "RemoveAbortedQuickEntities(db, createdByQuick);" in quick
    assert "tr.Abort();" in quick
    assert "if (id.IsNull || !id.IsValid || id.IsErased) continue;" in service
    assert 'entity.Layer.StartsWith("MV_MC_NHANH_"' in service
    assert "entity.Erase();" in service
    assert "CancelAt=" in regression and "afterLength=" in regression
