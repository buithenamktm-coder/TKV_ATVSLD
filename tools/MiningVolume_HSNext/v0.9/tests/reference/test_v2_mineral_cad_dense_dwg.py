"""CAD mineral region editing must not open every 3DFACE in large mine DWGs.

All previously approved area formulas, overlap checks, XData provenance
and transaction rollback remain unchanged. These static guards are NOT a
substitute for host runtime regression on licensed AutoCAD 2023.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def code():
    return (ROOT / "src/MiningVolume.Plugin2023/Services/"
            "MineralSectionRegionService.cs").read_text(encoding="utf-8")


def between(source, start, end):
    return source.split(start, 1)[1].split(end, 1)[0]


def assert_prefilter(block, kind, op):
    marker = f"id.ObjectClass.IsDerivedFrom({kind})"
    assert marker in block
    assert block.index(marker) < block.index(op)
    assert "GetObject(id,OpenMode.ForRead)" in block


def test_all_four_native_scan_paths_skip_unrelated_tin_faces():
    src = code()
    update = between(src, "public static IReadOnlyList<MineralSectionArea> UpdateAll()",
                     "private static void ValidateNoOverlap(")
    overlap = between(src, "private static void ValidateNoOverlap(",
                      "private static List<Vec2> ReadPhysicalPolygon(")
    frames = between(src, "private static SectionFrame FindFrame(",
                     "private static bool TryReadFrame(")

    region = update.split("int scanned=0;", 1)[1].split(
        "// Cross-body overlap", 1)[0]
    label = update.split("int labelsScanned=0;", 1)[1].split(
        "if(output.Count>0)", 1)[0]
    assert_prefilter(region, "polyClass", "tr.GetObject(id,OpenMode.ForRead)")
    assert_prefilter(label, "textClass", "tr.GetObject(id,OpenMode.ForRead)")
    assert_prefilter(overlap, "polyClass", "tr.GetObject(id,OpenMode.ForRead)")
    assert_prefilter(frames, "textClass", "tr.GetObject(id,OpenMode.ForRead)")

    # The same XData and geometrical area/overlap validators must remain.
    assert "TryReadTag(poly,AreaRegApp" in region
    assert "MineralSectionPolygonCalculator.ValidateAndMeasure(profile,physical)" in region
    assert "MineralSectionPolygonCalculator.OverlapArea(" in overlap
    assert "MineralSectionPolygonCalculator.OverlapArea(" in update
    assert "TryReadFrame(text,name,out var frame)" in frames
    assert "TryReadTag(text,LabelRegApp" in label


def test_esc_rolls_back_transactions_instead_of_partial_mineral_updates():
    src = code()
    assign = between(src, "public static double AssignRegion(",
                     "public static IReadOnlyList<MineralSectionArea> UpdateAll()")
    update = between(src, "public static IReadOnlyList<MineralSectionArea> UpdateAll()",
                     "private static void ValidateNoOverlap(")
    overlap = between(src, "private static void ValidateNoOverlap(",
                      "private static List<Vec2> ReadPhysicalPolygon(")
    frames = between(src, "private static SectionFrame FindFrame(",
                     "private static bool TryReadFrame(")

    assert 'new SectionCadProgress(1, "gán vùng khoáng sản")' in assign
    assert 'new SectionCadProgress(1, "cập nhật vùng khoáng sản")' in update
    assert "FindFrame(doc.Database, tr, profile.Line.Name, meter.Poll)" in assign
    assert "ValidateNoOverlap(doc.Database, tr, poly.ObjectId, profile.Line.Name, frame, region, meter.Poll)" in assign
    assert "FindFrame(doc.Database,tr,sectionName,meter.Poll)" in update

    assert "(scanned++ & 1023)==0)meter.Poll();" in update
    assert "(labelsScanned++ & 1023)==0)meter.Poll();" in update
    assert "(inspected++ & 1023)==0)pollCancellation?.Invoke();" in overlap
    assert "(scanned++ & 1023)==0)pollCancellation?.Invoke();" in frames
    assert assign.index("meter.Poll();\n                tr.Commit();") < assign.index("ProjectState.Current.NotifyChanged()")
    assert update.index("meter.Poll();\n                tr.Commit();") < update.index("state.NotifyChanged();")
    assert "poly.LayerId=layer;" in assign
    assert "text.UpgradeOpen();text.Erase();" in update


def test_new_scan_does_not_disable_multi_frame_validation():
    src = code()
    frames = between(src, "private static SectionFrame FindFrame(",
                     "private static bool TryReadFrame(")
    assert 'if(result!=null)throw new InvalidOperationException("Tên mặt cắt "' in frames
    assert "return result ?? throw new InvalidOperationException(" in frames
