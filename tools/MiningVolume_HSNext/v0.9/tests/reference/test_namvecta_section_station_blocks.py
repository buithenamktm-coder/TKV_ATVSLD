"""Plan-section stations must be individual native AutoCAD blocks.

Static regression only: the actual BlockReference display/edit/save/reopen
path still requires Windows AutoCAD + real DWG acceptance.
"""
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
renderer=(ROOT/"src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs").read_text(encoding="utf-8")
workflow=(ROOT/"src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs").read_text(encoding="utf-8")
section=(ROOT/"src/MiningVolume.Plugin2023/UI/SectionPage.cs").read_text(encoding="utf-8")


def test_each_plan_station_is_one_block_reference_not_loose_geometry():
    pre=renderer.split("public static void ReplacePreview(",1)[1].split("private static ObjectId UpdateStationDefinition(",1)[0]
    assert 'NAMVECTA_MC_TUYEN_' in pre
    assert 'UpdateStationDefinition(db, tr, blockName' in pre
    assert 'new BlockReference(anchor, blockId) { LayerId = layerId }' in pre
    assert 'ms.AppendEntity(instance);' in pre
    assert 'AddLine(ms, tr,' not in pre
    assert 'ms.AppendEntity(txt)' not in pre


def test_station_block_contains_all_segments_and_label():
    builder=renderer.split("private static ObjectId UpdateStationDefinition(",1)[1].split("public static void ClearPreview(",1)[0]
    assert 'foreach (var span in line.Spans)' in builder
    assert 'definition.AppendEntity(segment);' in builder
    assert 'definition.AppendEntity(label);' in builder
    assert 'TextString = line.Name' in builder
    assert 'new Point3d(first.X - basePoint.X, first.Y - basePoint.Y, 0)' in builder
    assert 'TextStyleId = styleId' in builder
    assert 'tr.AddNewlyCreatedDBObject(instance, true)' not in builder
    assert '(!' in builder or '(entity.LayerId != planLayer' in builder


def test_station_actions_reuse_block_renderer():
    for name in ("Preview", "DeleteLine", "AddLineAtPoint", "MoveLineToPoint"):
        body=workflow.split("public static SectionSystem "+name+"(",1)[1].split("public static ",1)[0]
        assert "DrawPreview(" in body
    assert "SectionCadRenderer.ReplacePreview(doc.Database, system);" in workflow


def test_clear_old_stations_erases_cad_preview_then_state_and_asks_confirmation():
    assert 'ClearAllStationLines()' in workflow
    f=workflow.split('public static void ClearAllStationLines()',1)[1].split('public static void ClearPreview()',1)[0]
    assert 'SectionCadRenderer.ClearPreview(doc.Database);' in f
    assert f.index('SectionCadRenderer.ClearPreview(doc.Database);') < f.index('state.SectionSystem = null;')
    assert 'state.VolumeResult = null;' in f
    assert 'Button("Xóa toàn bộ tuyến cũ", ClearOldStationLines)' in section
    assert 'MessageBoxButtons.YesNo' in section
    clear=renderer.split('public static void ClearPreview(Database db)',1)[1].split('public static void ReplaceProfiles(',1)[0]
    assert 'EraseOnLayers(ms, tr, clear)' in clear
    assert 'lt.Has(PreviewLayer)' in clear
    assert 'lt.Has(PreviewLabelLayer)' in clear
