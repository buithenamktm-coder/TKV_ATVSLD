from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def test_renderer_replaces_and_clears_tin_in_one_modelspace_pass_each():
    s = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    replace = s[s.index("public static int Replace"):s.index("public static int Clear")]
    clear = s[s.index("public static int Clear"):s.index("public static int CountFaces")]
    assert replace.count("foreach (ObjectId id in ms)") == 1
    assert clear.count("foreach (ObjectId id in ms)") == 1
    assert "second full scan" in replace
    assert "Validate and erase in one pass" in clear

def test_pair_build_does_not_rescan_just_written_cad_layers():
    s = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    pair = s[s.index("public static void DrawTinPair"):s.index("public static void DrawTin(ModelRole role, SurfaceBuildResult build)")]
    draw = s[s.index("private static void DrawTinInternal"):s.index("public static void EnsureOutputLayer")]
    assert "EnsureBothTinsReady(synchronizeCadLayers: false)" in pair
    assert "TinCadRenderer.CountFaces(" not in draw
    assert "written != tin.Triangles.Count" in draw

def test_breakline_endpoint_lookup_uses_spatial_site_index():
    s = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")
    assert "var siteIndex = new SiteIndex" in s
    assert "siteIndex.Find(seg.A)" in s
    assert "siteIndex.Find(seg.B)" in s
    assert "private sealed class SiteIndex" in s
    assert "FindSiteIndex(vertices, realCount" not in s

def test_data_page_reports_each_heavy_phase_and_timings():
    s = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    assert "BuildCoreDetailed(ModelRole.Existing)" in s
    assert "BuildCoreDetailed(ModelRole.Design)" in s
    assert "Đang dựng TIN hiện trạng" in s
    assert "Đang ghi TIN hiện trạng xuống AutoCAD" in read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "cadWatch.Elapsed.TotalSeconds" in s
    assert "totalWatch.Elapsed.TotalSeconds" in s
