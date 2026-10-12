"""Guard against publishing stale volumes after changing AutoCAD inputs.

These static tests cover the AutoCAD-only orchestration. Native C# regression
checks the GridWorkflowService commit gate with real grid volumes.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "src" / "MiningVolume.Plugin2023"
SERVICES = ROOT / "Services"
UI = ROOT / "UI"


def read(path):
    return path.read_text(encoding="utf-8")


def test_new_section_boundary_or_direction_clears_prior_volume():
    source = read(UI / "SectionPage.cs")
    boundary = source.split("private void PickBoundary(", 1)[1].split("private void PickDirection(", 1)[0]
    direction = source.split("private void PickDirection(", 1)[1].split("private void Preview(", 1)[0]
    for action in (boundary, direction):
        assert "SectionProfiles.Clear();" in action
        assert "VolumeResult = null;" in action
        assert action.index("VolumeResult = null;") < action.index("NotifyChanged();")


def test_volume_task_checks_dwg_tin_revisions_and_profile_set_before_commit():
    source = read(UI / "VolumePage.cs")
    method = source.split("private async void CalculateClicked(", 1)[1].split(
        "private void OnStateChanged(", 1)[0]
    required = (
        "MdiActiveDocument",
        "revisionExisting != st.Existing.Source.Revision",
        "revisionDesign != st.Design.Source.Revision",
        "profiles.Length != st.SectionProfiles.Count",
        "!ReferenceEquals(system, st.SectionSystem)",
        "from != st.FromLevel || to != st.ToLevel || step != st.LevelStep",
    )
    for check in required:
        assert check in method
    assert method.index("var result = await Task.Run(") < method.index(
        "VolumeWorkflowService.Commit(result, from, to, step);")


def test_grid_rejects_boundary_geometry_and_parameters_changed_mid_task():
    page = read(UI / "GridPage.cs")
    service = read(SERVICES / "GridWorkflowService.cs")
    method = page.split("private async Task CalculateAsync()", 1)[1].split(
        "private void DrawCad()", 1)[0]
    for check in (
        "MdiActiveDocument",
        "cts.Token.ThrowIfCancellationRequested();",
        "latestBoundary=BoundaryGeometryService.ReadBoundary(boundaryHandle)",
        "options.SizeX != st.GridSizeX || options.SizeY != st.GridSizeY",
        "GridWorkflowService.Commit(result,options,existing,design,rx,ry,boundaryHandle)",
    ):
        assert check in method
    for check in (
        "string.Equals(_boundaryHandle, state.GridBoundaryHandle",
        "_options.SizeX == state.GridSizeX",
        "_options.SizeY == state.GridSizeY",
        "_options.RotationDegrees == state.GridRotationDegrees",
        "string.Equals(boundaryHandle, state.GridBoundaryHandle",
    ):
        assert check in service

def test_pair_tin_never_writes_after_switching_active_dwg():
    page = read(UI / "DataPage.cs")
    method = page.split("private async Task BuildPairTinAsync()", 1)[1].split(
        "private async Task<SurfaceBuildResult> BuildRoleWithConflictChoiceAsync(", 1)[0]
    assert "var activeDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;" in method
    assert "ReferenceEquals(activeDoc," in method
    assert method.index("ReferenceEquals(activeDoc,") < method.index(
        "SurfaceWorkflowService.DrawTinPair(builds")

def test_palette_cannot_remain_live_on_wrong_dwg_or_bypass_load():
    entry = read(ROOT / "EntryPoint.cs")
    assert "DocumentActivated += OnDocumentActivated" in entry
    assert "DocumentActivated -= OnDocumentActivated" in entry
    event = entry.split("private static void OnDocumentActivated(", 1)[1].split(
        "private static void OnIdle(", 1)[0]
    assert "_loadedDrawingFingerprint = null;" in event
    assert "GridWorkflowService.Invalidate();" in event
    assert "Palette.Visible = false;" in event
    toggle = entry.split("internal static void TogglePalette()", 1)[1].split(
        '[CommandMethod("TKL"', 1)[0]
    assert "Open(AppPage.Project);" in toggle
    save = entry.split("public static void SaveProject()", 1)[1].split(
        '[CommandMethod("MV_PROJECT_LOAD"', 1)[0]
    assert "EnsureCurrentDrawingProjectLoaded();" in save
    for command in ("View3DExisting()", "View3DDesign()"):
        start = entry.split("public static void " + command, 1)[1].split("}", 1)[0]
        assert "EnsureCurrentDrawingProjectLoaded();" in start
