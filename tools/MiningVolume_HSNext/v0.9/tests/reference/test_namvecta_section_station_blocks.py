"""Plan-section stations must be one native AutoCAD block for the entire system.

Static regression only: the actual BlockReference display/edit/save/reopen
path still requires Windows AutoCAD + real DWG acceptance.
"""
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
renderer=(ROOT/"src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs").read_text(encoding="utf-8")
workflow=(ROOT/"src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs").read_text(encoding="utf-8")
section=(ROOT/"src/MiningVolume.Plugin2023/UI/SectionPage.cs").read_text(encoding="utf-8")


def test_all_plan_stations_share_one_block_reference():
    pre=renderer.split("public static void ReplacePreview(",1)[1].split(
        "private static ObjectId UpdateStationSystemDefinition(",1)[0]
    assert 'NAMVECTA_MC_HE_TUYEN' in pre
    assert 'UpdateStationSystemDefinition(db, tr,' in pre
    assert 'new BlockReference(anchor, blockId) { LayerId = layerId }' in pre
    assert 'ms.AppendEntity(instance);' in pre
    assert 'foreach (var line in system.Lines)' not in pre
    assert 'AddLine(ms, tr,' not in pre


def test_one_station_system_block_contains_all_segments_and_labels():
    builder=renderer.split("private static ObjectId UpdateStationSystemDefinition(",1)[1].split(
        "public static void ClearPreview(",1)[0]
    assert 'foreach (var line in lines)' in builder
    assert 'foreach (var span in line.Spans)' in builder
    assert 'definition.AppendEntity(segment);' in builder
    assert 'definition.AppendEntity(label);' in builder
    assert 'TextString = line.Name' in builder
    assert 'new Point3d(first.X - basePoint.X, first.Y - basePoint.Y, 0)' in builder
    assert 'TextStyleId = styleId' in builder
    assert 'tr.AddNewlyCreatedDBObject(instance, true)' not in builder
    assert 'entity.LayerId != planLayer && entity.LayerId != labelLayer' in builder


def test_station_actions_reuse_block_renderer():
    for name in ("Preview", "DeleteLine", "AddLineAtPoint", "MoveLineToPoint"):
        body=workflow.split("public static SectionSystem "+name+"(",1)[1].split("public static ",1)[0]
        assert "DrawPreview(" in body
    assert "SectionCadRenderer.ReplacePreview(doc.Database, system);" in workflow


def test_clear_old_stations_erases_cad_preview_then_state_and_asks_confirmation():
    assert 'ClearAllStationLines()' in workflow
    f=workflow.split('public static void ClearAllStationLines()',1)[1].split('public static void ClearPreview()',1)[0]
    assert 'SectionCadRenderer.ClearPreview(doc.Database,' in f
    assert 'ProjectPersistenceService.MarkStationLinesDeletedInTransaction(' in f
    assert 'SectionCadRenderer.ClearPreview(doc.Database);' in workflow  # plain "Xóa preview" preserves saved station state
    assert f.index('SectionCadRenderer.ClearPreview(doc.Database,') < f.index('state.SectionSystem = null;')
    assert 'state.VolumeResult = null;' in f
    assert 'Button("Xóa toàn bộ tuyến cũ", ClearOldStationLines)' in section
    assert 'MessageBoxButtons.YesNo' in section
    clear=renderer.split('public static void ClearPreview(Database db,',1)[1].split('public static void ReplaceProfiles(',1)[0]
    assert 'EraseOnLayers(ms, tr, clear)' in clear
    assert 'lt.Has(PreviewLayer)' in clear
    assert 'lt.Has(PreviewLabelLayer)' in clear

def test_deleting_station_blocks_clears_only_saved_section_snapshot():
    persistence=(ROOT/"src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs").read_text(encoding="utf-8")
    helper=persistence.split("internal static void MarkStationLinesDeletedInTransaction(",1)[1].split(
        "public static ProjectLoadResult LoadCurrentProject(",1)[0]
    assert "snap.SectionLines?.Clear();" in helper
    assert "snap.Boundary?.Clear();" in helper
    assert "snap.HadProfiles = false;" in helper
    assert "snap.HadVolumeResult = false;" in helper
    assert "rec.Data = ToBuffer(ProjectSnapshotCodec.Encode(snap));" in helper
    for forbidden in ("snap.Existing.HadTin = false;", "snap.Design.HadTin = false;", "snap.TinRegion.Clear()", "snap.BoundaryHandle = null;"):
        assert forbidden not in helper
    clear=renderer.split("public static void ClearPreview(Database db,",1)[1].split(
        "public static void ReplaceProfiles(",1)[0]
    assert "onClearedInSameTransaction?.Invoke(tr);" in clear
    assert clear.index("EraseOnLayers(ms, tr, clear);") < clear.rindex("onClearedInSameTransaction?.Invoke(tr);") < clear.rindex("tr.Commit();")


def test_section_clear_preflight_preserves_foreign_cad_objects():
    # Validate both initial preview replacement and explicit removal.
    pre=renderer.split("public static void ReplacePreview(",1)[1].split(
        "private static ObjectId UpdateStationSystemDefinition(",1)[0]
    clear=renderer.split("public static void ClearPreview(Database db,",1)[1].split(
        "public static void ReplaceProfiles(",1)[0]
    assert pre.index("ValidatePreviewOwnership(") < pre.index("EraseOnLayers(")
    assert clear.index("ValidatePreviewOwnership(") < clear.index("EraseOnLayers(")
    method=renderer.split("private static void ValidatePreviewOwnership(",1)[1].split(
        "private static bool IsStationBlock(",1)[0]
    assert "if (entity is Line) continue;" in method
    assert "entity.LayerId == nameLayer && !(entity is DBText)" in method
    assert "block != null && IsStationBlock(" in method
    assert "throw new InvalidOperationException(" in method
    block=renderer.split("private static bool IsStationBlock(",1)[1].split(
        "private static BlockTableRecord ModelSpace(",1)[0]
    assert 'definition.Name == "NAMVECTA_MC_HE_TUYEN"' in block
    assert 'StartsWith("NAMVECTA_MC_TUYEN_", StringComparison.Ordinal)' in block
    assert "entity is Line && entity.LayerId == planLayer" in block
    assert "entity is DBText && entity.LayerId == nameLayer" in block


def test_quick_section_page_has_no_official_station_edit_or_volume_actions():
    # Fast CAD section drawings never act on the official SectionSystem or
    # calculated earthwork. Toolbar actions are gated by !_quickOnly.
    page = section.split('var tools = new FlowLayoutPanel', 1)[1].split(
        'root.Controls.Add(tools, 0, 3);', 1)[0]
    assert 'if (!_quickOnly)' in page
    assert page.index('if (!_quickOnly)') < page.index(
        'tools.Controls.Add(Button("Thêm tuyến", AddLine));')
    assert 'Button("Tính toán khối lượng trên mặt cắt", UpdateEditedProfiles)' in page
    assert 'tools.Controls.Add(updateEdited);' in page
    assert 'if (_navigate != null)' in page
    assert 'Text = _quickOnly ? "VẼ NHANH MẶT CẮT" : "HỆ MẶT CẮT"' in section
    assert '_rootLayout.RowStyles[3].Height = 0F;' in section

def test_selected_point_order_controls_station_numbering():
    builder=(ROOT/"src/MiningVolume.Core/Sections/SectionSystemBuilder.cs").read_text(encoding="utf-8")
    assert "return new Vec2(selected.Y / length, -selected.X / length);" in builder
    assert "var n = new Vec2(-d.Y, d.X);" in builder
    assert "double o = minN;" in builder
    assert "o += options.Spacing;" in builder
    # For either picked direction, the builder normal is exactly the selected vector.
    for vx, vy in ((1, 0), (0, 1), (-1, 0), (0, -1), (3, -4)):
        import math
        length=math.hypot(vx, vy)
        d=(vy/length, -vx/length)
        normal=(-d[1], d[0])
        assert math.isclose(normal[0],vx/length)
        assert math.isclose(normal[1],vy/length)
