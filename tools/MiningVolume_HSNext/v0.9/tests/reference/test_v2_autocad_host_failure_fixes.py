"""Regression contracts for observed AutoCAD 2023 MVSELFTEST failures.

These static assertions are NOT substitutes for real AutoCAD host results.
They keep the fixes covered while the self-hosted QA verifies each behavior.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def src(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def test_arial_style_uses_correct_database_and_restores_ambient_context():
    for path in (
        "src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs",
        "src/MiningVolume.Plugin2023/Services/CadVolumeTableService.cs",
    ):
        s = src(path)
        assert "var previous = HostApplicationServices.WorkingDatabase;" in s
        assert "HostApplicationServices.WorkingDatabase = db;" in s
        assert "HostApplicationServices.WorkingDatabase = previous;" in s
        assert "finally" in s


def test_grid_identity_only_inspects_grid_output_not_foreign_modelspace_objects():
    s = src("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    assert 'entity.Layer.StartsWith("MV_LUOI_", StringComparison.OrdinalIgnoreCase)' in s
    assert "Grid output layer contains unexpected entity type." in s
    assert 'string.Equals(entity.Layer, "MV_LUOI_NHAN"' in s
    assert "labelLayer ? !(entity is DBText) : !(entity is Polyline)" in s
    assert "oldLabels != first.Cells.Count || newLabels != second.Cells.Count" in s
    assert "Missing grid calculation XData." in s
    assert "Later grid export overwrote earlier identity." in s


def test_primary_section_action_fits_small_autocad_palette():
    s = src("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert "new RowStyle(SizeType.Percent, 75F)" in s
    assert "new RowStyle(SizeType.Percent, 25F)" in s
    assert "new RowStyle(SizeType.Absolute, 440F)" not in s
    assert 'draw.Name = "btnDrawSections";' in s
    assert "AutoScroll = true" in s


def test_v2_qa_command_does_not_reuse_old_mvselftest_registration():
    entry = src("src/MiningVolume.Plugin2023/EntryPoint.cs")
    runtime = src("release/verify_autocad2023_runtime.ps1")
    result = src("src/MiningVolume.Plugin2023/Services/SelfTestService.cs")
    assert '[CommandMethod("NAMVECTA_QA_SELFTEST_V2", CommandFlags.Session)]' in entry
    # The unique QA command still invokes the genuine self-test, but now
    # avoids running it a second time after the opt-in AutoCAD Idle hook.
    assert "public static void QaSelfTestV2()" in entry
    qa = entry.split("public static void QaSelfTestV2()", 1)[1].split(
        '[CommandMethod("MVABOUT")', 1
    )[0]
    assert "if (hostMode && _hostQaSelfTestTriggered)" in qa
    assert "if (hostMode) _hostQaSelfTestTriggered = true;" in qa
    assert "SelfTest();" in qa
    assert '[CommandMethod("MVSELFTEST", CommandFlags.Session)]' in entry
    assert "$selfTestCommand" in runtime
    assert "'NAMVECTA_QA_SELFTEST_V2'" in runtime
    assert "'MVSELFTEST'" in runtime
    assert "NAMVECTA VOLUME 2.0 runtime self-test" in runtime
    assert "NAMVECTA VOLUME 2.0 runtime self-test" in result
