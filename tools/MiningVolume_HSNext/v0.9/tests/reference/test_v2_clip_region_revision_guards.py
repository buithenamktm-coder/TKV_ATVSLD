from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_clip_scope_has_independent_generation_counter():
    state = read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    data = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    restore = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    assert "TinRegionRevision => System.Threading.Interlocked.Read" in state
    assert "MarkTinRegionChanged()" in state
    assert data.count("state.MarkTinRegionChanged();") >= 2
    assert "state.MarkTinRegionChanged();" in workflow
    assert "state.MarkTinRegionChanged();" in restore


def test_background_triangulation_cannot_commit_obsolete_region():
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    body = workflow.split("public static SurfaceBuildResult BuildCoreDetailed(", 1)[1].split(
        "public static TinSurface BuildCore(", 1
    )[0]
    assert "var regionRevision = state.TinRegionRevision;" in body
    assert body.count("state.TinRegionRevision != regionRevision") >= 2
    assert body.count("TinRegionRevision = regionRevision,") == 2
    assert "build.TinRegionRevision != state.TinRegionRevision" in workflow
    assert "build.TinRegionRevision != ProjectState.Current.TinRegionRevision" in workflow
    assert "(tiled.Surface.Triangles as IDisposable)?.Dispose();" in body


def test_edited_cad_clip_boundary_cannot_silently_preserve_old_tin():
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    method = workflow.split("private static void EnsureTinRegionMatchesCad()", 1)[1].split(
        "public static void EnsureBothTinsReady(", 1
    )[0]
    assert "BoundaryGeometryService.ReadBoundary(state.TinRegionHandle)" in method
    assert "actual.Count != expected.Count" in method
    assert "Geometry2D.AlmostSame(actual[i], expected[i], 1e-7)" in method
    assert "Đường bao tạo TIN trên CAD đã bị chỉnh Grip" in method
    assert workflow.count("EnsureTinRegionMatchesCad();") >= 4


def test_excel_only_export_rechecks_live_clipping_boundary():
    export = read("src/MiningVolume.Plugin2023/UI/ExportPage.cs")
    method = export.split("private static void EnsureExportInputsCurrent(", 1)[1].split(
        "private void StateChanged(", 1
    )[0]
    assert "SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers: false);" in method
    assert "EditableSectionCadService.EnsureProfilesMatch(doc.Database, expectedProfiles);" in method
    # CAD drawing of the report is optional; the guard is called for both outputs.
    run = export.split("private async void ExportClicked(", 1)[1].split(
        "private static void EnsureExportInputsCurrent(", 1
    )[0]
    assert "if (exportExcel)" in run
    assert "EnsureExportInputsCurrent(doc, st, currentResult, approvedProfiles," in run


def test_tin_region_revision_participates_in_calculation_freshness():
    state = read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "public long? TinBuiltFromRegionRevision" in state
    assert "TinBuiltFromRegionRevision.Value == ProjectState.Current.TinRegionRevision" in state
    assert "s.TinBuiltFromRegionRevision = null;" in state
    assert "session.TinBuiltFromRegionRevision = ProjectState.Current.TinRegionRevision;" in workflow
    invalidate = workflow.split("public static void InvalidateTin(", 1)[1].split(
        "private static void EnsureTinRegionMatchesCad()", 1
    )[0]
    assert "session.TinBuiltFromRegionRevision = null;" in invalidate


def test_refusing_to_clear_old_cad_layer_preserves_accepted_tin_and_source():
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    invalid = workflow.split("public static void InvalidateTin(", 1)[1].split(
        "private static void EnsureTinRegionMatchesCad()", 1
    )[0]
    assert invalid.index("TinCadRenderer.Clear(doc.Database, session.TinLayer)") < invalid.index(
        "session.Tin = null;"
    )
    assert invalid.index("TinCadRenderer.Clear(doc.Database, session.TinLayer)") < invalid.index(
        "state.SectionProfiles.Clear();"
    )
    source_commit = workflow.split("private static SurfaceLoadResult CommitLoadedSource(", 1)[1].split(
        "private const int LargeDatasetVertexThreshold", 1
    )[0]
    assert source_commit.index("InvalidateTin(role, clearCadLayer: true, notify: false);") < source_commit.index(
        "session.Source = model;"
    )
    assert source_commit.count("InvalidateTin(role, clearCadLayer: true, notify: false);") == 1
