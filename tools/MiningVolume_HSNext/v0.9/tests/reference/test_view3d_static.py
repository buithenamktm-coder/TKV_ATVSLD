from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_3d_uses_existing_autocad_3dfaces_not_separate_viewer():
    src = read("src/MiningVolume.Plugin2023/Services/Tin3DViewService.cs")
    assert "SurfaceWorkflowService.EnsureTinReady" in src
    assert "TinCadRenderer.CountFaces" in src
    assert 'doc.SendStringToExecute("_.3DORBIT "' in src
    assert "doc.Editor.SetCurrentView(view);" in src
    assert "ITiledTriangleSource" in src
    assert 'EntryPoint.Palette.Visible = false;' in src
    assert 'EntryPoint.Palette.Visible = true;' in src


def test_esc_returns_to_palette_and_restores_visibility():
    src = read("src/MiningVolume.Plugin2023/Services/Tin3DViewService.cs")
    assert "doc.CommandCancelled += OnOrbitFinished;" in src
    assert "doc.CommandEnded += OnOrbitFinished;" in src
    assert "doc.CommandFailed += OnOrbitFinished;" in src
    assert "AcApp.Idle += RestoreOnIdle;" in src
    assert "SurfaceWorkflowService.SetTinVisible(ModelRole.Existing, _existingWasVisible);" in src
    assert "SurfaceWorkflowService.SetTinVisible(ModelRole.Design, _designWasVisible);" in src


def test_ui_and_commands_choose_which_tin_to_view():
    model = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    entry = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert 'Button("VIEW TIN 3D"' in model
    assert "Tin3DViewService.Start(CurrentRole)" in model
    assert 'CommandMethod("MV_VIEW3D_HT"' in entry
    assert 'CommandMethod("MV_VIEW3D_TK"' in entry
