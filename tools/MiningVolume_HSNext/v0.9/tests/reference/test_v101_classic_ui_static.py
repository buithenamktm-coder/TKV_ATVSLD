from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def test_palette_auto_opens_even_without_ribbon():
    s = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert "AcApp.Idle += OnIdle" in s
    assert "Open(AppPage.Project);" in s
    assert "_startupUiOpened" in s
    assert 'Ribbon == null ? "OFF/Unavailable"' in s

def test_startup_failure_is_visible_and_logged():
    s = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert 'StartupLog("Startup palette FAILED: " + ex)' in s
    assert 'AcApp.ShowAlertDialog(' in s
    assert '"startup.log"' in s

def test_ribbon_failure_is_not_silently_swallowed():
    s = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert 'StartupLog("Ribbon unavailable/failed: " + ex.Message)' in s
    assert "try { RibbonBuilder.EnsureRibbon(); } catch { }" not in s

def test_package_autoloads_without_user_commands():
    xml = read("bundle/MiningVolume2023.bundle/PackageContents.xml")
    assert 'AppVersion="0.10.3"' in xml
    assert 'LoadOnAutoCADStartup="True"' in xml
    assert "<Commands" not in xml

def test_installer_message_matches_classic_ui_behavior():
    s = read("installer/main.go")
    assert 'version = "0.10.3"' in s
    assert "bảng MiningVolume sẽ tự hiện" in s
