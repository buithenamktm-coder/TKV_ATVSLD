from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def test_ribbon_is_direct_event_driven():
    s = read("src/MiningVolume.Plugin2023/RibbonBuilder.cs")
    assert "SendStringToExecute" not in s
    assert "Action action" in s
    assert "DirectHandler" in s
    assert "EntryPoint.Open(AppPage.Data)" in s
    assert "EntryPoint.Open(AppPage.Section)" in s
    assert "EntryPoint.Open(AppPage.Volume)" in s

def test_canvas_pick_temporarily_hides_palette_and_restores_it():
    s = read("src/MiningVolume.Plugin2023/Services/SelectionService.cs")
    assert "PalettePickScope" in s
    assert "EntryPoint.SetPaletteVisible(false)" in s
    assert "EntryPoint.SetPaletteVisible(true)" in s
    assert "using (new PalettePickScope())" in s

def test_installer_does_not_drive_autocad_by_script_or_command():
    s = read("installer/main.go")
    assert ".scr" not in s
    assert "MVSELFTEST" not in s
    assert '"/b"' not in s
    assert "runAutoCADSelfTest" not in s
    assert "exec.Command(acad" not in s

def test_health_check_runs_inside_loaded_addin_not_from_command_script():
    s = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert "TryInternalHealthCheck" in s
    assert "SelfTestService.Run()" in s
    assert "MessageBox.Show" in s
