"""Regression guards for CAD section persistence and destructive redraw.

CoreRegression separately exercises the serialized ProjectSnapshot flag;
these tests ensure the AutoCAD-only orchestration keeps it wired into save,
restore and the UI confirmation flow. Actual AutoCAD host test remains required.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SERVICES = ROOT / "src" / "MiningVolume.Plugin2023" / "Services"
UI = ROOT / "src" / "MiningVolume.Plugin2023" / "UI"


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def test_project_snapshot_carries_cad_section_preservation_marker():
    snap = _read(SERVICES / "ProjectSnapshot.cs")
    project = _read(SERVICES / "ProjectPersistenceService.cs")
    assert "HadEditableSectionCad" in snap
    assert "snapshot.HadEditableSectionCad =" in project
    assert "EditableSectionCadService.HasEditableProfiles(doc.Database)" in project
    assert "snap.HadEditableSectionCad && !cadSectionsAvailable" in project
    assert "throw new InvalidOperationException(" in project
    assert project.index("snap.HadEditableSectionCad && !cadSectionsAvailable") < project.index(
        "EditableSectionCadService.ReadEditedProfiles(")


def test_user_must_confirm_before_replacing_existing_cad_sections():
    ui = _read(UI / "SectionPage.cs")
    begin = ui.index("private async void BuildProfiles(")
    end = ui.index("private async void UpdateEditedProfiles(", begin)
    method = ui[begin:end]
    assert "EditableSectionCadService.HasEditableProfiles(activeDoc.Database)" in method
    assert "MessageBoxButtons.YesNo" in method
    assert "MessageBoxDefaultButton.Button2" in method
    assert method.index("EditableSectionCadService.HasEditableProfiles(") < method.index(
        "SaveParameters();")
    assert method.index("SectionWorkflowService.DrawProfiles(") < method.index(
        "SectionWorkflowService.CommitProfiles(profiles);")
    assert "if (!ins.HasValue) return;" in method
    # A background TIN calculation must never draw into whichever DWG became
    # active while the worker was running.
    assert method.count("!ReferenceEquals(activeDoc, Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument)") >= 2
    assert "!ReferenceEquals(existing, state.Existing.Tin)" in method
    assert "!ReferenceEquals(design, state.Design.Tin)" in method
    service = _read(SERVICES / "SectionWorkflowService.cs")
    assert "IReadOnlyList<SectionProfile> profilesToDraw = null" in service
    assert "drawingProfiles" in service
