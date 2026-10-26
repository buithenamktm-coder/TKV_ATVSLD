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
    assert "var createdByGrid = new System.Collections.Generic.List<ObjectId>();" in renderer
    assert "createdByGrid.Add(cellId);" in renderer
    assert "createdByGrid.Add(labelId);" in renderer
    assert "RemoveAbortedGridEntities(database, createdByGrid);" in renderer
    assert '"Refusing to erase non-grid entity while recovering a canceled export."' in renderer
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



def test_cancelled_section_area_labels_restore_original_text_and_identity():
    service = load("src/MiningVolume.Plugin2023/Services/EditableSectionCadService.cs")
    assert "var originalText = new Dictionary<ObjectId, string>();" in service
    assert "originalText.Add(label.ObjectId, label.TextString);" in service
    assert "RestoreAbortedAreaLabels(db, originalText);" in service
    assert "label.TextString = pair.Value;" in service
    assert "GetXDataForApplication(SectionCadRenderer.AreaRegApp)" in service
    assert '"Refusing to restore an area label with invalid calculation identity."' in service
    assert "throw new AggregateException(" in service



def test_canceled_final_section_redraw_restores_original_handles():
    src = load("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    qa = load("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    assert "var createdBySection = new List<ObjectId>();" in src
    assert "var erasedBySection = new List<ObjectId>();" in src
    assert "EraseOnLayers(ms, tr, oldLayers, pollCancellation, erasedBySection.Add)" in src
    assert "trackCreated: createdBySection.Add" in src
    assert "RecoverFailedSectionExport(db, createdBySection, erasedBySection)" in src
    assert "trackErased?.Invoke(id);" in src
    assert "if (entity.IsErased) entity.Erase(false);" in src
    assert '"Refusing to erase foreign entity after section redraw failure."' in src
    assert '"Refusing to unerase foreign entity after section redraw failure."' in src
    assert "Final renderer cancellation failed to restore drawing." in qa
