from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def read(p):
    return (ROOT/p).read_text(encoding="utf-8")


def test_section_frame_mapped_in_dwgs_not_in_an_external_viewer():
    s=read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    assert "IMSAT_MV2_MC_FRAME" in s
    assert "sectionTitle.XData = new ResultBuffer" in s
    assert "DxfCode.ExtendedDataReal" in s
    assert "baseZ" in s
    assert "vExag" in s


def test_edited_mineral_regions_stay_on_separate_cad_layer_and_are_revalidated():
    s=read("src/MiningVolume.Plugin2023/Services/MineralSectionRegionService.cs")
    assert 'RegionLayer = "IMSAT_KHOANG_SAN_MC"' in s
    assert "GetXDataForApplication" in s
    assert "new TypedValue((int)DxfCode.ExtendedDataAsciiString,body.Id)" in s
    assert "MineralSectionPolygonCalculator.ValidateAndMeasure(profile, region)" in s
    assert "MineralSectionPolygonCalculator.ValidateAndMeasure(profile,physical)" in s
    assert "MineralSectionPolygonCalculator.OverlapArea" in s
    assert "Không tính trùng" in s
    assert "state.NotifyChanged()" in s


def test_ui_allows_section_binding_and_explicit_update():
    ui=read("src/MiningVolume.Plugin2023/UI/MineralPage.cs")
    assert "GÁN VÙNG TRÊN MẶT CẮT" in ui
    assert "CẬP NHẬT MẶT CẮT" in ui
    assert "SelectionService.PickClosedBoundary()" in ui
    assert "MineralSectionRegionService.AssignRegion" in ui
    assert "MineralSectionRegionService.UpdateAll()" in ui
    assert "Chưa phải thể tích m³/tấn" in ui


def test_polygon_kernel_is_exact_and_has_independent_core_regressions():
    core=read("src/MiningVolume.Core/Sections/MineralSectionPolygonCalculator.cs")
    tests=read("tests/core/CoreRegression.cs")
    assert "ClipConvex" in core
    assert "MakeCutPolygons" in core
    assert "inside < requested - tolerance" in core
    for name in ("mineral_section_polygon_inside_cut","mineral_section_polygon_outside_cut_blocks",
                 "mineral_section_polygon_mixed_zero_crossing","mineral_section_overlap_detected"):
        assert name in tests
