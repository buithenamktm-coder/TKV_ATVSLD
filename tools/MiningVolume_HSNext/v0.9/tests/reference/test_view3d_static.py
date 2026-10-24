from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_3d_uses_existing_cad_tin_and_never_synchronously_scans_million_faces():
    src = read("src/MiningVolume.Plugin2023/Services/Tin3DViewService.cs")
    assert "SurfaceWorkflowService.EnsureTinReady(role, synchronizeCadLayer: false)" in src
    assert "TinCadRenderer.CountFaces" not in src
    assert "session.TinCadFaceCount != faceCount" in src
    assert "ITiledTriangleSource" in src
    assert "doc.Editor.SetCurrentView(view);" in src
    assert 'AcApp.SetSystemVariable("VSCURRENT", "2D Wireframe")' in src
    assert "SendStringToExecute" not in src
    assert "doc.Editor.Regen()" not in src
    assert "LargeTinFaceThreshold = 200000" in src
    assert "MessageBoxButtons.YesNo" in src


def test_2d_restore_is_explicit_and_preserves_original_visibility():
    src = read("src/MiningVolume.Plugin2023/Services/Tin3DViewService.cs")
    assert "public static void Stop()" in src
    assert "_previousView.Dispose();" in src
    assert "doc.Editor.SetCurrentView(_previousView)" in src
    assert "TinCadRenderer.SetVisible(doc.Database, existing.TinLayer, _existingWasVisible)" in src
    assert "TinCadRenderer.SetVisible(doc.Database, design.TinLayer, _designWasVisible)" in src
    assert "EntryPoint.Palette.Visible = false;" not in src
    assert "EntryPoint.Palette.Visible = true;" in src


def test_ui_and_commands_choose_tin_and_allow_return_to_2d():
    model = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    entry = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert 'Button("Xem TIN", (s, e) => ViewTin3D())' in model
    assert "Tin3DViewService.Start(CurrentRole)" in model
    assert "Tin3DViewService.Stop()" in model
    assert '"Trở về 2D"' in model
    assert 'CommandMethod("MV_VIEW3D_HT"' in entry
    assert 'CommandMethod("MV_VIEW3D_TK"' in entry
