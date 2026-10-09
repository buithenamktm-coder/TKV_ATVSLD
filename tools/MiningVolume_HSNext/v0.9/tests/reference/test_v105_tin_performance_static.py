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


def test_large_tin_delaunay_uses_x_sweep_and_cached_circumcircles():
    s = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")
    assert "private struct WorkTri" in s
    assert "RightX" in s
    assert ".OrderBy(i => vertices[i].X)" in s
    assert "wt.RightX < point.X - tol" in s
    assert "TryWorkTri" in s

def test_breakline_recovery_reuses_adjacency_and_spatial_edge_index():
    s = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")
    build = s[s.index("public TinSurface Build"):s.index("private static List<Tri> BowyerWatson")]
    recover = s[s.index("private static void RecoverConstraint"):s.index("private static bool TryFlip")]
    assert "var adjacency = BuildAdjacency(tris)" in build
    assert "var edgeIndex = new EdgeGridIndex" in build
    assert "BuildAdjacency(tris)" not in recover
    assert "edgeIndex.Query(ca, cb)" in recover
    assert "private sealed class EdgeGridIndex" in s

def test_constraint_flip_updates_adjacency_locally_without_scanning_all_locked_edges():
    s = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")
    flip = s[s.index("private static bool TryFlip"):s.index("private static Dictionary<EdgeKey, List<int>> BuildAdjacency")]
    assert "RemoveTriangleOwners" in flip
    assert "AddTriangleOwners" in flip
    assert "foreach (var le in locked)" not in flip


def test_section_sampler_uses_reusable_triangle_spatial_index():
    s = read("src/MiningVolume.Core/Sections/TinSectionSampler.cs")
    assert "private sealed class TriangleGridIndex" in s
    assert "IndexFor(existing)" in s
    assert "IndexFor(design)" in s
    assert "QuerySegment(a, b)" in s
    assert "_cells.TryGetValue" in s
    assert "foreach (var tri in tin.Triangles)" not in s

def test_section_page_builds_profiles_off_ui_thread_with_progress():
    page = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    assert "private async void BuildProfiles" in page
    assert "await Task.Run" in page
    assert "Đang tính mặt cắt" in page
    assert "BuildProfilesCore" in workflow
    assert "CommitProfiles" in workflow


def test_section_renderer_batches_tin_segments_into_polylines():
    s = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    assert "AddProfilePolylines" in s
    assert "FlushProfilePolyline" in s
    assert "new Polyline(points.Count)" in s
    block = s[s.index("AddProfilePolylines(ms, tr, p"):s.index("currentY -= height + 45.0")]
    assert "AddLine(ms, tr" not in block


def test_section_profiles_are_parallelized_but_keep_original_order():
    s = read("src/MiningVolume.Core/Sections/TinSectionSampler.cs")
    assert "Parallel.For(0, lines.Count" in s
    assert "var result = new SectionProfile[lines.Count]" in s
    assert "result[i] = BuildProfileIndexed" in s
    assert "Interlocked.Increment" in s

def test_section_cad_output_is_batched_to_avoid_one_giant_transaction():
    renderer = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    page = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert "ReplaceProfilesBatched" in renderer
    assert "const int batchSize = 20" in workflow
    assert "start == 0" in workflow
    assert "Đang vẽ mặt cắt {done:n0}/{total:n0}" in page
    assert "drawWatch.Elapsed.TotalSeconds" in page


def test_large_mine_tin_routes_to_tiled_engine_instead_of_one_global_delaunay():
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    preparer = read("src/MiningVolume.Core/Surface/SurfaceInputPreparer.cs")
    assert "LargeDatasetVertexThreshold = 200000" in workflow
    assert "new TiledConformingTinBuilder().Build" in workflow
    assert "TargetCoreVertices = 25000" in tiled
    assert "HaloFactors" in tiled
    assert "CircumcircleInside" in tiled
    assert "TryClip" in tiled
    assert "PrepareRaw" in preparer
    assert "10 triệu đỉnh" in workflow
