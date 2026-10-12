from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def source(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_liquid_glass_is_a_native_static_palette_surface_without_autocad_blur():
    theme = source("src/MiningVolume.Plugin2023/UI/UiTheme.cs")
    palette = source("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    assert "internal sealed class GlassPanel : Panel" in theme
    assert "ControlStyles.OptimizedDoubleBuffer" in theme
    assert "ControlStyles.ResizeRedraw" in theme
    assert "LinearGradientBrush(" in theme
    assert "GlassHighlight" in theme
    assert "group.BackColor = GlassSurface" in theme
    assert "var header = new GlassPanel" in palette
    assert "var sidebar = new GlassPanel" in palette
    assert "_compactHeader = new GlassPanel" in palette
    assert "UiTheme.ApplyPage(page)" in palette
    assert "SetWindowCompositionAttribute" not in theme
    assert "DwmEnableBlurBehindWindow" not in theme
    assert "System.Windows.Media" not in theme


def test_liquid_glass_preserves_contrasting_native_actions_and_responsive_navigation():
    theme = source("src/MiningVolume.Plugin2023/UI/UiTheme.cs")
    palette = source("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    assert "button.UseVisualStyleBackColor = false" in theme
    assert "button.BackColor = Accent" in theme
    assert "button.ForeColor = Color.White" in theme
    assert "button.BackColor = ButtonSecondary" in theme
    assert "button.ForeColor = ButtonText" in theme
    assert "UiTheme.ApplyRoundedButtonMask" in palette
    assert "ClientSize.Width < 650" in palette
    assert "_compactNavigation" in palette


def test_glass_winforms_visual_probe_and_qa_artifact():
    probe = source("tests/ui/verify_buttons.ps1")
    workflow = (ROOT.parents[2] / ".github/workflows/build-miningvolume-autocad2023.yml").read_text(encoding="utf-8")
    assert "UI_GLASS_CHROME.png" in probe
    assert "glass_gradient_delta" in probe
    assert "if($gradientDelta -lt 9)" in probe
    assert "UI_GLASS_CHROME.png" in workflow
    assert "liquid_glass_chrome_sha256" in workflow
