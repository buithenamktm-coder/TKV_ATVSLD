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
