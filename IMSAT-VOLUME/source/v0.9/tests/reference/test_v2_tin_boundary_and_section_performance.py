from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(p):
    return (ROOT / p).read_text(encoding="utf-8")


def test_tin_selected_region_clips_all_3d_triangles_before_cad():
    source = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    builder = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")
    vertical = read("src/MiningVolume.Core/Surface/VerticalFaceBuilder.cs")
    verifier = read("src/MiningVolume.Core/Surface/TinRegionIntegrityVerifier.cs")
    assert "ClipBoundary = clipBoundary" in source
    assert "SurfaceRegionClipper.Clip(" in source
    assert "VerticalFaceBuilder.ClipToBoundary(" in builder
    assert builder.count("TinRegionIntegrityVerifier.Verify(") == 2
    assert "store.Dispose(); // never retain an invalid file-backed mesh" in builder
    assert "SurfaceRegionClipper.ClipSegment(" in vertical
    assert "new Triangle3(lo0, hi0, lo1)" in vertical
    assert "new Triangle3(hi0, hi1, lo1)" in vertical
    assert "foreach (var triangle in surface.Triangles)" in verifier
    assert "foreach (var face in surface.VerticalFaces)" in verifier
    assert "Đã chặn ghi mô hình sai phạm vi xuống CAD" in verifier


def test_ui_makes_tin_boundary_distinct_from_section_boundary():
    data = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    model = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    section = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert 'Text = "Phạm vi tạo TIN"' in data
    assert 'Btn("Chọn vùng trên CAD", (s, e) => SelectTinRegion())' in data
    assert "PHẠM VI TIN: ĐÃ GIỚI HẠN" in model
    assert "PHẠM VI TIN: TOÀN BỘ NGUỒN" in model
    assert "Đường bao MẶT CẮT không cắt TIN" in model
    assert "SectionWorkflowService.Preview(" in section


def test_sampling_keeps_complete_geometry_and_avoids_reloading_each_tile_for_each_z():
    sampler = read("src/MiningVolume.Core/Sections/TinSectionSampler.cs")
    assert "private const int MaxHotTiles = 24;" in sampler
    assert "private sealed class TilePointIndex" in sampler
    assert "TryElevationOnTriangle(_triangles[i], p, tol, out z)" in sampler
    assert "MaxDegreeOfParallelism =" in sampler
    assert "Math.Min(4, Environment.ProcessorCount - 1)" in sampler
    assert "GetTile(tileIndex).TryElevation(p, tol, out z)" in sampler
    assert "var tris = GetTile(tileIndex).Triangles;" in sampler
    assert "QuerySegmentTriangles(a, b)" not in sampler.split("private sealed class TilePointIndex", 1)[1]
