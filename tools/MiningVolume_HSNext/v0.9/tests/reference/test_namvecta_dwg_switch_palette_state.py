"""Prevent carrying old drawing's survey-layer UI state into a new DWG.

This static contract does not replace opening two actual drawings in AutoCAD.
"""
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "src" / "MiningVolume.Plugin2023"

def source(file):
    return (SRC / file).read_text(encoding="utf-8")

def test_dwg_change_resets_global_session_then_refreshes_palette():
    ep = source("EntryPoint.cs")
    assert "ProjectState.Current.Reset();" in ep
    assert 'MainControl.RefreshForNewDrawing();' in ep
    routine = ep.split("private static void EnsureCurrentDrawingProjectLoaded()", 1)[1].split(
        "private static string CurrentFingerprint()", 1)[0]
    assert routine.index("ProjectState.Current.Reset();") < routine.index(
        "MainControl.RefreshForNewDrawing();")
    assert routine.index("_loadedDocument = doc;") < routine.index(
        "MainControl.RefreshForNewDrawing();")

def test_model_workflow_forwards_drawing_change_to_data_page():
    workflow = source("UI/ModelWorkflowPage.cs")
    palette = source("UI/MainPaletteControl.cs")
    assert "workflow.RefreshForNewDrawing();" in palette
    assert "ProjectState.Current.NotifyChanged();" in palette
    assert "data?.RefreshForNewDrawing();" in workflow

def test_data_page_clears_prior_combo_selection_but_preserves_loaded_source_layers():
    page = source("UI/DataPage.cs")
    region = page.split("internal void RefreshForNewDrawing()", 1)[1].split(
        "private void RefreshLayers(", 1)[0]
    assert "RefreshLayers(preservePriorSelection: false);" in region
    assert "RefreshTinRegionStatus();" in region
    assert "RefreshBuildButtons();" in region
    assert "Fill(_existingLayer, layers, existing, preservePriorSelection);" in page
    assert "Fill(_designLayer, layers, design, preservePriorSelection);" in page
    assert "preservePriorSelection ? cb.Text : string.Empty" in page


def test_manual_project_reload_invalidates_old_grid_before_disposing_tin():
    project = source("Services/ProjectState.cs")
    reset = project.split("public void Reset()", 1)[1].split(
        "private static void ResetModel(", 1)[0]
    assert "GridWorkflowService.Invalidate();" in reset
    assert reset.index("GridWorkflowService.Invalidate();") < reset.index(
        "ResetModel(Existing);")
    cache = source("Services/GridWorkflowService.cs")
    assert "_report = null; _options = null; _existing = null; _design = null;" in cache


def test_explicit_saved_project_load_updates_existing_palette_state():
    ep = source("EntryPoint.cs")
    method = ep.split("public static void LoadProject()", 1)[1].split(
        "private static void EnsureCurrentDrawingProjectLoaded()", 1)[0]
    assert 'Services.ProjectPersistenceService.LoadCurrentProject(true)' in method
    assert 'MainControl.RefreshForNewDrawing();' in method
    assert "if (!r.Found)" in method
    assert method.index("if (!r.Found)") < method.index("ProjectState.Current.Reset();") < method.index(
        "MainControl.RefreshForNewDrawing();")
    assert method.index("MainControl.RefreshForNewDrawing();") < method.index(
        "Open(AppPage.Project);")
