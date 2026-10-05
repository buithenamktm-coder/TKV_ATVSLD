from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def test_numeric_updown_range_is_set_before_value():
    s = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    helper = s[s.index("private static NumericUpDown Num"):s.index("private static Button Button")]
    assert "Minimum = min" in helper
    assert "Maximum = max" in helper
    assert "n.Value =" in helper
    assert helper.index("Minimum = min") < helper.index("n.Value =")
    assert helper.index("Maximum = max") < helper.index("n.Value =")
    assert "new NumericUpDown { Value = v" not in helper

def test_scale_defaults_that_triggered_runtime_bug_are_covered():
    s = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert "_hScale = Num(1000, 1, 100000);" in s
    assert "_vScale = Num(500, 1, 100000);" in s

def test_runtime_selftest_constructs_real_palette_ui():
    s = read("src/MiningVolume.Plugin2023/Services/SelfTestService.cs")
    assert "new MainPaletteControl()" in s
    assert "Khởi tạo đầy đủ giao diện MiningVolume" in s
    assert "EntryPoint.StartupUiError" in s
    assert "Giao diện startup không phát sinh exception" in s

def test_installer_selftest_does_not_block_on_startup_dialog():
    s = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert 'Environment.GetEnvironmentVariable("MININGVOLUME_SELFTEST_FILE")' in s
    assert "StartupUiError = ex.ToString();" in s


def test_no_value_first_numeric_updown_initializer_anywhere():
    plugin = ROOT / "src" / "MiningVolume.Plugin2023"
    bad = []
    pattern = re.compile(r"new\s+NumericUpDown\s*\{\s*Value\s*=")
    for p in plugin.rglob("*.cs"):
        text = p.read_text(encoding="utf-8")
        if pattern.search(text):
            bad.append(str(p.relative_to(ROOT)))
    assert not bad, bad
