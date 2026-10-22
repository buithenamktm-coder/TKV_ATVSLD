from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def test_renderer_replaces_once_and_prefilters_tin_clear_before_destructive_phase():
    s = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    pair = s[s.index("public static int[] ReplacePair"):s.index("private static void ClearExistingPairInTransaction")]
    pair_clear = s[s.index("private static void ClearExistingPairInTransaction"):s.index("private static int ReplaceInTransaction")]
    replace = s[s.index("private static int ReplaceInTransaction"):s.index("public static int Clear")]
    clear = s[s.index("public static int Clear"):s.index("public static int CountFaces")]
    # Pair export clears both layers in one traversal, then writes each TIN
    # without entering the single-TIN cleanup loop.
    pair_preflight = s[s.index("private static List<ObjectId> ValidateExistingPairBeforeMutation"):s.index("private static void ClearExistingPairInTransaction")]
    assert pair_preflight.count("foreach (ObjectId id in ms)") == 1
    assert "oldFaces.Add(id)" in pair_preflight
    assert "progress.Poll();" in pair_preflight
    assert pair_clear.count("foreach (ObjectId id in originalFaces)") == 1
    assert "face.Erase();" in pair_clear
    assert "progress.Poll()" not in pair_clear
    assert "ClearExistingPairInTransaction(" in pair
    assert "existingLayer, existing, 1, false" in pair
    assert "designLayer, design, 3, false" in pair
    assert replace.count("foreach (ObjectId id in ms)") == 1
    assert "if (clearExisting)" in replace
    assert clear.count("foreach (ObjectId id in ms)") == 1
    assert "second full scan" in replace
    assert "ent.LayerId != layerId" in clear
    assert "if (!(ent is Face))" in clear
    # Host cancellation proved that mid-erase exception rollback is unsafe.
    # One non-destructive ModelSpace scan gathers only the verified Face IDs;
    # the second phase erases those IDs directly, without another traversal.
    assert "faces.Add(id);" in clear
    assert "face.Erase();" in clear
    assert clear.index("faces.Add(id);") < clear.index("face.Erase();")
    assert clear.index("face.Erase();") < clear.rindex("tr.Commit();")
    assert "progress.Poll();" in clear
    after_erase = clear.split("face.Erase();", 1)[1]
    assert "progress.Poll();" not in after_erase
    assert "pollCancellation?.Invoke();" not in after_erase

def test_pair_build_does_not_rescan_just_written_cad_layers():
    s = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    pair = s[s.index("public static void DrawTinPair"):s.index("public static void DrawTin(ModelRole role, SurfaceBuildResult build)")]
    draw = s[s.index("private static void DrawTinInternal"):s.index("public static void EnsureOutputLayer")]
    assert "EnsureBothTinsReady(synchronizeCadLayers: false)" in pair
    assert "TinCadRenderer.CountFaces(" not in draw
    assert "written != expectedCadFaces" in draw

def test_legacy_breakline_endpoint_lookup_uses_spatial_site_index():
    s = read("tests/core/legacy/LegacyConformingTinBuilder.cs")
    assert "var siteIndex = new SiteIndex" in s
    assert "siteIndex.Find(seg.A)" in s
    assert "siteIndex.Find(seg.B)" in s
    assert "private sealed class SiteIndex" in s
    assert "FindSiteIndex(vertices, realCount" not in s

def test_data_page_reports_each_heavy_phase_and_timings():
    s = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    assert "BuildCoreDetailed(" in s and "ModelRole.Existing" in s
    assert "BuildCoreDetailed(" in s and "ModelRole.Design" in s
    assert "Đang dựng TIN {st.Get(role).Name}" in s
    assert "Đang ghi TIN hiện trạng xuống AutoCAD" in read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "cadWatch.Elapsed.TotalSeconds" in s
    assert "totalWatch.Elapsed.TotalSeconds" in s


def test_legacy_large_tin_delaunay_uses_x_sweep_and_cached_circumcircles():
    s = read("tests/core/legacy/LegacyConformingTinBuilder.cs")
    assert "private struct WorkTri" in s
    assert "RightX" in s
    assert ".OrderBy(i => vertices[i].X)" in s
    assert "wt.RightX < point.X - tol" in s
    assert "TryWorkTri" in s

def test_legacy_breakline_recovery_reuses_adjacency_and_spatial_edge_index():
    s = read("tests/core/legacy/LegacyConformingTinBuilder.cs")
    build = s[s.index("public TinSurface Build"):s.index("private static List<Tri> BowyerWatson")]
    recover = s[s.index("private static void RecoverConstraint"):s.index("private static int ConstraintVertexDegree")]
    assert "var adjacency = BuildAdjacency(tris)" in build
    assert "var edgeIndex = new EdgeGridIndex" in build
    assert "BuildAdjacency(tris)" not in recover
    assert "edgeIndex.Query(ca, cb)" in recover
    assert "private sealed class EdgeGridIndex" in s

def test_legacy_constraint_flip_updates_adjacency_locally_without_scanning_all_locked_edges():
    s = read("tests/core/legacy/LegacyConformingTinBuilder.cs")
    flip = s[s.index("private static bool TryFlip"):s.index("private static Dictionary<EdgeKey, List<int>> BuildAdjacency")]
    assert "RemoveTriangleOwners" in flip
    assert "AddTriangleOwners" in flip
    assert "foreach (var le in locked)" not in flip


def test_section_sampler_uses_reusable_triangle_spatial_index():
    s = read("src/MiningVolume.Core/Sections/TinSectionSampler.cs")
    assert "private sealed class TriangleGridIndex" in s
    assert "IndexFor(existing, cancellationToken)" in s
    assert "IndexFor(design, cancellationToken)" in s
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
    block = s[s.index("AddProfilePolylines(ms, tr, p"):s.index("currentY -= height + layoutGap;")]
    assert "AddLine(ms, tr" not in block


def test_section_profiles_are_parallelized_but_keep_original_order():
    s = read("src/MiningVolume.Core/Sections/TinSectionSampler.cs")
    assert "Parallel.For(0, lines.Count" in s
    assert "var result = new SectionProfile[lines.Count]" in s
    # Each parallel iteration still writes to result[i] in input order;
    # the wrapper adds conflict diagnostics without serializing sampling.
    assert "result[i] = AnnotateVerticalConflicts(" in s
    assert "BuildProfileIndexed(lines[i], existingIndex, designIndex, tol, cancellationToken)" in s
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


def test_large_mine_uses_one_verified_mesh_then_adaptive_storage():
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    preparer = read("src/MiningVolume.Core/Surface/SurfaceInputPreparer.cs")
    assert "LargeDatasetVertexThreshold = 200000" in workflow
    assert "new TiledConformingTinBuilder().Build" in workflow
    assert tiled.count("new SurfaceInputPreparer().Prepare") == 1
    assert tiled.count("BuildFileBacked") == 1
    assert "HaloFactors" not in tiled
    assert "ParallelTileRetryException" not in tiled
    assert "MaxTrianglesPerLeaf = 8192" in read("src/MiningVolume.Surface/AdaptiveTinStore.cs")
    assert "10 triệu đỉnh" in workflow


def test_large_tin_is_file_backed_and_cad_render_uses_complete_mesh():
    store = read("src/MiningVolume.Surface/FileBackedTiledTriangleList.cs")
    tiled = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    renderer = read("src/MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    assert "ITiledTriangleSource" in store
    assert "FileOptions.SequentialScan" in store
    assert "cacheTiles:6" in read("src/MiningVolume.Surface/AdaptiveTinStore.cs")
    assert "PreviewTriangles=MiningVolume.Core.Compatibility.EmptyArray<Triangle3>.Value" in tiled
    assert "Math.Min(200000,tiles.Count)" not in tiled
    assert "var ht = existing.Tin;" in workflow
    assert "var tk = design.Tin;" in workflow
    assert "var cadTin = tin;" in workflow
    assert "bool cadPreview = false;" in workflow
    assert "BuildCadPreview" not in workflow
    assert "foreach (var t in tin.Triangles)" in renderer
    assert "foreach (var t in tin.VerticalFaces)" in renderer
    assert "if(allowCancellation && (written&1023)==0)progress.Poll(written,true);" in renderer
    assert "bool allowCancellation = true" in renderer


def test_large_tin_ui_has_progress_and_user_cancel():
    data = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    assert "CancellationTokenSource" in data
    assert 'Btn("Hủy"' in data
    assert "_buildCts.Cancel()" in data
    assert "OperationCanceledException" in data
    assert "SetBuildStatus" in data


def test_manual_review_cannot_choose_upper_or_lower():
    prep = read("src/MiningVolume.Core/Surface/SurfaceInputPreparer.cs")
    core = read("src/MiningVolume.Core/Surface/SurfaceContracts.cs")
    ui = read("src/MiningVolume.Plugin2023/UI/DuplicateXYConflictUi.cs")
    assert "LegacyElevationPolicyIgnored" in core
    assert "get => DuplicateXYConflictPolicy.Stop" in core
    assert "ConflictScanner" in prep
    assert "UseUpper" not in prep and "UseLower" not in prep
    assert "UseUpper" not in ui and "UseLower" not in ui
    assert "ConflictReviewForm" in ui
    assert "ScanAsync" in ui and "ExcludeAsync" in ui
    assert "VirtualMode=true" in ui


def test_manual_conflict_report_tracks_sources_and_derived_intersections():
    report = read("src/MiningVolume.Core/Surface/ConflictReport.cs")
    for field in ["EntityId", "Handle", "Layer", "VertexIndex", "SegmentIndex", "SegmentParameter", "SourceRevision"]:
        assert field in report
    for operation in ["PointOnSegment", "ProperIntersection", "CollinearOverlap", "ThrowIfCancellationRequested"]:
        assert operation in report
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "ConflictReport.ToException()" in workflow


def test_legacy_breakline_recovery_is_ordered_and_does_not_use_arbitrary_flip_loop():
    s = read("tests/core/legacy/LegacyConformingTinBuilder.cs")
    assert "CrossingEdgesOrdered" in s
    assert "IntersectionParameterAlongFirst" in s
    assert "var pending = new Queue<EdgeKey>()" in s
    assert "failuresSinceProgress" in s
    assert "maxSuccessfulFlips" in s
    assert "out EdgeKey replacement" in s
    assert "Vượt số vòng lặp khi khôi phục breakline" not in s


def test_legacy_breakline_recovery_falls_back_to_collinear_interior_site_chain():
    s = read("tests/core/legacy/LegacyConformingTinBuilder.cs")
    assert "TryRecoverConstraintThroughInteriorSites" in s
    assert "Geometry2D.PointOnSegment" in s
    assert "onLineTol = Math.Max(tol * 100.0, 1e-4)" in s
    assert "tris, vertices, sub, locked, adjacency, edgeIndex, tol, cancellationToken" in s
    assert "if (adjacency.ContainsKey(sub))" in s
    assert "if (adjacency.ContainsKey(constraint))" in s
    assert "không có chuỗi cạnh gần thẳng hàng" in s


def test_legacy_proper_intersection_uses_dimensionally_correct_tolerance_and_recovery_has_index_fallback():
    geom = read("src/MiningVolume.Core/Geometry/Geometry2D.cs")
    tin = read("tests/core/legacy/LegacyConformingTinBuilder.cs")

    assert "double abEps = Math.Max(1e-24, tol * abLen)" in geom
    assert "double cdEps = Math.Max(1e-24, tol * cdLen)" in geom
    assert "CollectCrossingEdges" in tin
    assert "adjacency.Keys" in tin
    assert "spatial edge index is an accelerator, not a source" in tin


def test_legacy_large_tin_deduplicates_bucketed_breaklines_and_reports_live_tile_progress():
    tiled = read("tests/core/legacy/LegacyHaloTinBuilder.cs")
    builder = read("src/MiningVolume.Surface/ConformingTinBuilder.cs")

    assert "private readonly struct SegmentRef" in tiled
    assert "var seenSegmentIds = new HashSet<int>()" in tiled
    assert "if (!seenSegmentIds.Add(segmentRef.Id)) continue" in tiled
    assert "mục tiêu ~{targetCoreVertices:n0} đỉnh/ô" in tiled
    assert "đang chuẩn hóa" in tiled
    assert "đang tam giác hóa" in tiled
    assert "message => progress?.Invoke" in tiled

    assert "CancellationToken cancellationToken" in builder
    assert "Tam giác hóa điểm " in builder
    assert "cancellationToken.ThrowIfCancellationRequested()" in builder
    assert "Khôi phục breakline " in builder


def test_legacy_large_tin_processes_independent_tiles_in_parallel_with_bounded_workers():
    tiled = read("tests/core/legacy/LegacyHaloTinBuilder.cs")
    assert "Parallel.For(0, total, parallelOptions" in tiled
    assert "MaxDegreeOfParallelism = workerCount" in tiled
    assert "Math.Min(4, Math.Max(1, Environment.ProcessorCount - 1))" in tiled
    assert "lock (writeGate)" in tiled
    assert "Interlocked.Add(ref prepareMs" in tiled
    assert "Interlocked.Add(ref triangulateMs" in tiled
    assert "AggregateException" in tiled
    assert "TIN dữ liệu lớn: {doneNow:n0}/{total:n0} ô xong" in tiled


def test_legacy_large_tin_parallel_failure_retries_stable_single_worker():
    tiled = read("tests/core/legacy/LegacyHaloTinBuilder.cs")
    assert "ParallelTileRetryException" in tiled
    assert "forcedWorkerCount: 1" in tiled
    assert "đang tự chuyển sang chế độ ổn định 1 luồng" in tiled


def test_legacy_tiled_tin_avoids_redundant_large_normalization_work():
    prep = read("src/MiningVolume.Core/Surface/SurfaceInputPreparer.cs")
    tiled = read("tests/core/legacy/LegacyHaloTinBuilder.cs")
    assert "PrepareRawForTiledTin" in tiled
    assert "Dictionary<long, List<int>> _cells" in prep
    assert "int[] _visited" in prep
    assert "HashSet cho từng breakline query" in prep
    assert "HaloFactors = { 0.12, 0.25, 0.45, 0.65 }" in tiled


def test_legacy_breakline_recovery_repairs_omitted_sites_and_near_collinear_paths():
    tin = read("tests/core/legacy/LegacyConformingTinBuilder.cs")
    assert "EnsureAllSitesReferenced" in tin
    assert "đã phục hồi {repairedSites:n0} site suy biến" in tin
    assert "TryLockNearCollinearEdgePath" in tin
    assert "ConstraintVertexDegree" in tin
    assert "corridor = Math.Max(tol * 100.0, 1e-4)" in tin
    assert "Không tìm thấy dãy cạnh tam giác cắt breakline" not in tin
