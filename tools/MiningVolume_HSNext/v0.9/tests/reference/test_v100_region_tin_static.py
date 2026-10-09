from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def test_v1_data_page_can_select_or_clear_common_tin_region():
    s = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    assert 'Text = "Phạm vi tạo TIN"' in s
    assert 'Btn("Chọn vùng trên CAD"' in s
    assert 'Btn("Dùng toàn bộ"' in s
    assert "SelectionService.PickClosedBoundary()" in s
    assert "BoundaryGeometryService.ReadBoundary(handle)" in s
    assert "TinRegionPolygon.AddRange(polygon)" in s
    assert "InvalidateTin(" in s


def test_v1_tin_region_is_core_geometry_not_only_ui_filter():
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    clipper = read("src/MiningVolume.Core/Surface/SurfaceRegionClipper.cs")
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    conforming = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")

    assert "SurfaceRegionClipper.Clip" in workflow
    assert "ClipBoundary = clipBoundary" in workflow
    assert "ClipSegment(" in clipper
    assert "ContainsInclusive(" in clipper
    assert "Interpolate(segment" in clipper
    assert "BuildSupportRectangle" in clipper
    assert "0.10" in clipper
    assert "Geometry2D.TriangleIntersectsPolygon" in tiled
    assert "options.ClipBoundary" in conforming


def test_v1_tin_region_persists_in_dwg_project_snapshot():
    snap = read("src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs")
    persistence = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    assert "TinRegionHandle" in snap
    assert "List<Point2Snapshot> TinRegion" in snap
    assert "state.TinRegionPolygon" in persistence
    assert "state.TinRegionHandle = snap.TinRegionHandle" in persistence
    assert "FormatVersion { get; set; } = 2" in snap


def test_v1_region_tin_keeps_boundary_crossing_triangles():
    geom = read("src/MiningVolume.Core/Geometry/Geometry2D.cs")
    conforming = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")
    assert "TriangleIntersectsPolygon" in geom
    assert "PointInTriangle" in geom
    assert "SegmentsTouchOrCross" in geom
    assert "TriangleIntersectsPolygon" in conforming
