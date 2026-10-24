"""Second field QA: preserve TIN generated earlier in a saved DWG.

Static cross-component regression; not a substitute for live AutoCAD 2020–24.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SVC = ROOT / "src/MiningVolume.Plugin2023/Services"
UI = ROOT / "src/MiningVolume.Plugin2023/UI"


def code(path):
    return path.read_text(encoding="utf-8")


def test_read_native_faces_and_keep_cad_unchanged():
    src = code(SVC / "TinCadRenderer.cs")
    start = src.index("public static TinSurface ReadStoredTin(")
    end = src.index("private static bool Finite(Point3d", start)
    reader = src[start:end]
    assert "face.GetVertexAt(0)" in reader
    assert "face.GetVertexAt(1)" in reader
    assert "face.GetVertexAt(2)" in reader
    assert "face.GetVertexAt(3)" in reader
    assert "if (!Close(p2, p3))" in reader
    assert "triangle.Area2D <= 1e-12" in reader
    assert "support.Add(triangle)" in reader
    assert "vertical.Add(triangle)" in reader
    assert "new TinSurface(modelName, support" in reader
    for forbidden in ("face.Erase()", "tr.Commit()", "ReplaceInTransaction(", "new TiledConformingTinBuilder"):
        assert forbidden not in reader


def test_model_state_tracks_validated_cad_restoration_and_blocks_changed_source():
    state = code(SVC / "ProjectState.cs")
    engine = code(SVC / "SurfaceWorkflowService.cs")
    assert "public bool TinRestoredFromCad { get; set; }" in state
    assert "public bool CadRecoveryBlocked { get; set; }" in state
    assert "if (session.Tin != null || session.CadRecoveryBlocked) return false;" in engine
    assert "TinCadRenderer.ReadStoredTin(" in engine
    assert "session.TinBuiltFromSourceRevision = session.Source.Revision;" in engine
    assert "session.TinBuiltFromRegionRevision = state.TinRegionRevision;" in engine
    assert "session.TinRestoredFromCad = true;" in engine
    assert "if (session.Tin == null)" in engine
    assert "TryRestoreTinFromCad(role);" in engine
    assert "if ((session.Source == null || session.Source.Entities.Count == 0) &&" in engine
    assert "!session.TinRestoredFromCad)" in engine


def test_grid_and_sections_reuse_same_loaded_tin_without_rebuilding():
    grid = code(UI / "GridPage.cs")
    engine = code(SVC / "SurfaceWorkflowService.cs")
    sections = code(SVC / "SectionWorkflowService.cs")
    assert "EnsureBothTinsReady(synchronizeCadLayers:false)" in grid
    assert "SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers: true);" in sections
    assert "EnsureTinReady(ModelRole.Existing, false);" in engine
    assert "EnsureTinReady(ModelRole.Design, false);" in engine
    assert "bấm Tính khối lượng để tự đọc lại TIN" in grid


def test_saved_project_reads_faces_instead_of_retriangulating():
    persistence = code(SVC / "ProjectPersistenceService.cs")
    begin = persistence.index("private static void RebuildSavedTins(")
    end = persistence.index("private static double PositiveOr(", begin)
    method = persistence[begin:end]
    assert "SurfaceWorkflowService.TryRestoreTinFromCad(role)" in method
    assert "SurfaceWorkflowService.BuildPairCoreDetailed()" not in method
    assert "SurfaceWorkflowService.BuildCoreDetailed(role)" not in method
    assert "SurfaceWorkflowService.DrawTinPair(builds)" not in method
    assert "SurfaceWorkflowService.DrawTin(role, build)" not in method
    assert "session.CadRecoveryBlocked = true;" in persistence


def test_reopened_dwg_tin_is_viewable_without_source_reload():
    model = code(UI / "ModelPage.cs")
    assert "SurfaceWorkflowService.TryRestoreTinFromCad(CurrentRole)" in model
    assert "_view3D.Enabled = !busy;" in model
