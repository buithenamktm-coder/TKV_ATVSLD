from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_palette_button_rounding_resizes_at_dpi_changes():
    ui = read("src/MiningVolume.Plugin2023/UI/UiTheme.cs")
    assert "GraphicsPath" in ui
    assert "path.AddArc" in ui
    assert "button.Region = new Region(path);" in ui
    assert "button.Resize += RefreshButtonMask;" in ui
    assert "button.HandleCreated += RefreshButtonMask;" in ui
    assert "button.FlatAppearance.MouseOverBackColor" in ui
    assert "button.ForeColor = Color.White;" in ui
    assert "ApplyRoundedButtonMask(button);" in ui


def test_main_nav_has_same_rounding_as_page_buttons():
    nav = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    assert "UiTheme.ApplyRoundedButtonMask(b);" in nav
