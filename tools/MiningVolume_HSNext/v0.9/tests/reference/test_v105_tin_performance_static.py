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
    assert "written != expectedCadFaces" in draw

def test_breakline_endpoint_lookup_uses_spatial_site_index():
    s = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")
    assert "var siteIndex = new SiteIndex" in s
    assert "siteIndex.Find(seg.A)" in s
    assert "siteIndex.Find(seg.B)" in s
    assert "private sealed class SiteIndex" in s
    assert "FindSiteIndex(vertices, realCount" not in s

def test_data_page_reports_each_heavy_phase_and_timings():
    s = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    assert "BuildCoreDetailed(" in s and "ModelRole.Existing" in s
    assert "BuildCoreDetailed(" in s and "ModelRole.Design" in s
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
    assert "QuerySegmentTriangles(a, b)" in s
    assert "ITiledTriangleSource" in s
    assert "_tiled.QueryTiles" in s
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
    assert "DefaultTargetCoreVertices = 18000" in tiled
    assert "MillionScaleTargetCoreVertices = 12000" in tiled
    assert "MultiMillionTargetCoreVertices = 8000" in tiled
    assert "HaloFactors" in tiled
    assert "CircumcircleInside" in tiled
    assert "TryClip" in tiled
    assert "PrepareRaw" in preparer
    assert "10 triệu đỉnh" in workflow


def test_large_tin_is_file_backed_and_cad_render_is_bounded_preview():
    store = read("src/MiningVolume.Surface/FileBackedTiledTriangleList.cs")
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    state = read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    assert "ITiledTriangleSource" in store
    assert "FileOptions.SequentialScan" in store
    assert "cacheTiles: 6" in tiled
    assert "PreviewTriangles" in tiled
    assert "previewTriangles.Count < 200000" in tiled
    assert "TinCadIsPreview" in state
    assert "cadPreview" in workflow
    assert "BuildCadPreview" in workflow


def test_large_tin_ui_has_progress_and_user_cancel():
    data = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    assert "CancellationTokenSource" in data
    assert 'Btn("Hủy"' in data
    assert "_buildCts.Cancel()" in data
    assert "OperationCanceledException" in data
    assert "SetBuildStatus" in data


def test_large_tin_duplicate_xy_uses_conservative_source_priority_resolution():
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    assert "ResolveDuplicateInputPoints" in tiled
    assert "AUTO_RESOLVE_DUPLICATE_XY_MINOR" in tiled
    assert "AUTO_RESOLVE_DUPLICATE_XY_BY_PRIORITY" in tiled
    assert "SourcePriority" in tiled
    assert "case SourceEntityType.Point: return 600" in tiled
    assert "case SourceEntityType.Polyline3d: return 500" in tiled
    assert "case SourceEntityType.Contour: return 400" in tiled
    assert "cùng mức ưu tiên" in tiled
    assert "Handles:" in tiled
    assert "dữ liệu CAD gốc không bị sửa" in tiled
    assert "SnapSegmentEndpoints" in tiled


def test_duplicate_xy_conflicts_can_continue_with_upper_or_lower_vertex():
    core = read("src/MiningVolume.Core/Surface/SurfaceContracts.cs")
    prep = read("src/MiningVolume.Core/Surface/SurfaceInputPreparer.cs")
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    ui = read("src/MiningVolume.Plugin2023/UI/DuplicateXYConflictUi.cs")
    data = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    model = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")

    assert "DuplicateXYConflictPolicy" in core
    assert "UseUpper" in core and "UseLower" in core
    assert "DuplicateXYConflictException" in core
    assert "USER_RESOLVE_DUPLICATE_XY_UPPER" in prep
    assert "USER_RESOLVE_DUPLICATE_XY_LOWER" in prep
    assert "USER_RESOLVE_DUPLICATE_XY_UPPER" in tiled
    assert "USER_RESOLVE_DUPLICATE_XY_LOWER" in tiled
    assert "duplicateXYPolicy" in workflow
    assert "Bạn có muốn TIẾP TỤC tạo TIN" in ui
    assert "DÙNG ĐỈNH TRÊN (Z LỚN HƠN)" in ui
    assert "DÙNG ĐỈNH DƯỚI (Z NHỎ HƠN)" in ui
    assert "BuildRoleWithConflictChoiceAsync" in data
    assert "DuplicateXYConflictUi.Ask" in data
    assert "DuplicateXYConflictUi.Ask" in model


def test_resolvable_tin_elevation_conflicts_use_same_upper_lower_dialog():
    prep = read("src/MiningVolume.Core/Surface/SurfaceInputPreparer.cs")
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    ui = read("src/MiningVolume.Plugin2023/UI/DuplicateXYConflictUi.cs")

    for code in [
        "POINT_ON_BREAKLINE_Z_CONFLICT",
        "BREAKLINE_CROSSING_Z_CONFLICT",
        "BREAKLINE_OVERLAP_Z_CONFLICT",
    ]:
        assert code in prep
        assert code in tiled
        assert code in workflow

    assert "USER_RESOLVE_POINT_ON_BREAKLINE_UPPER" in prep
    assert "USER_RESOLVE_POINT_ON_BREAKLINE_LOWER" in prep
    assert "USER_RESOLVE_BREAKLINE_CROSSING_UPPER" in prep
    assert "USER_RESOLVE_BREAKLINE_CROSSING_LOWER" in prep
    assert "USER_RESOLVE_BREAKLINE_OVERLAP_UPPER" in prep
    assert "USER_RESOLVE_BREAKLINE_OVERLAP_LOWER" in prep
    assert "xung đột cao độ Z" in ui
    assert "Bạn có muốn TIẾP TỤC tạo TIN" in ui


def test_breakline_recovery_is_ordered_and_does_not_use_arbitrary_flip_loop():
    s = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")
    assert "CrossingEdgesOrdered" in s
    assert "IntersectionParameterAlongFirst" in s
    assert "var pending = new Queue<EdgeKey>()" in s
    assert "failuresSinceProgress" in s
    assert "maxSuccessfulFlips" in s
    assert "out EdgeKey replacement" in s
    assert "Vượt số vòng lặp khi khôi phục breakline" not in s


def test_breakline_recovery_falls_back_to_collinear_interior_site_chain():
    s = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")
    assert "TryRecoverConstraintThroughInteriorSites" in s
    assert "Geometry2D.PointOnSegment" in s
    assert "onLineTol = Math.Max(tol * 10.0, 1e-8)" in s
    assert "tris, vertices, sub, locked, adjacency, edgeIndex, tol, cancellationToken" in s
    assert "if (adjacency.ContainsKey(sub))" in s
    assert "if (adjacency.ContainsKey(constraint))" in s
    assert "Không có cạnh cắt và cũng không tìm thấy chuỗi site trung gian" in s


def test_proper_intersection_uses_dimensionally_correct_tolerance_and_recovery_has_index_fallback():
    geom = read("src/MiningVolume.Core/Geometry/Geometry2D.cs")
    tin = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")

    assert "double abEps = Math.Max(1e-24, tol * abLen)" in geom
    assert "double cdEps = Math.Max(1e-24, tol * cdLen)" in geom
    assert "CollectCrossingEdges" in tin
    assert "adjacency.Keys" in tin
    assert "spatial edge index is an accelerator, not a source" in tin


def test_large_tin_deduplicates_bucketed_breaklines_and_reports_live_tile_progress():
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    builder = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")

    assert "private readonly struct SegmentRef" in tiled
    assert "var seenSegmentIds = new HashSet<int>()" in tiled
    assert "if (!seenSegmentIds.Add(segmentRef.Id)) continue" in tiled
    assert "mục tiêu ~{targetCoreVertices:n0} đỉnh/ô" in tiled
    assert "đang chuẩn hóa" in tiled
    assert "đang tam giác hóa" in tiled
    assert "message => progress?.Invoke" in tiled

    assert "CancellationToken cancellationToken" in builder
    assert '"Delaunay {orderIndex:n0}/{realCount:n0} điểm..."' in builder
    assert "cancellationToken.ThrowIfCancellationRequested()" in builder
    assert "khôi phục breakline" in builder


def test_large_tin_processes_independent_tiles_in_parallel_with_bounded_workers():
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    assert "Parallel.For(0, total, parallelOptions" in tiled
    assert "MaxDegreeOfParallelism = workerCount" in tiled
    assert "Math.Min(4, Math.Max(1, Environment.ProcessorCount - 1))" in tiled
    assert "lock (writeGate)" in tiled
    assert "Interlocked.Add(ref prepareMs" in tiled
    assert "Interlocked.Add(ref triangulateMs" in tiled
    assert "AggregateException" in tiled
    assert "TIN dữ liệu lớn: {doneNow:n0}/{total:n0} ô xong" in tiled


def test_large_tin_parallel_failure_retries_stable_single_worker():
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    assert "ParallelTileRetryException" in tiled
    assert "forcedWorkerCount: 1" in tiled
    assert "đang tự chuyển sang chế độ ổn định 1 luồng" in tiled
