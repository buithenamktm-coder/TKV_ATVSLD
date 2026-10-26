"""Host-verified cancellation must explicitly abort CAD mutations.

These static tests are NOT real AutoCAD-host evidence. Host MVSELFTEST must
independently prove rollback of block definitions, DBText and quick sections.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(name):
    return (ROOT / f"src/MiningVolume.Plugin2023/Services/{name}.cs").read_text(
        encoding="utf-8-sig"
    )


def test_quantity_export_explicit_abort_on_every_failure():
    src = read("CadVolumeTableService")
    method = src.split("public static void Insert(", 1)[1].split(
        "private static ObjectId EnsureArialTextStyle", 1
    )[0]
    assert "using (var tr = database.TransactionManager.StartTransaction())" in method
    assert "tr.Commit();" in method
    assert "catch\n                {\n                    tr.Abort();\n                    throw;\n                }" in method
    assert "BlockPrefix + result.CalculationId" in method


def test_grid_output_explicit_abort_before_disposal():
    src = read("GridCadRenderer")
    method = src.split("public static void Draw(", 1)[1].split(
        "private static ObjectId EnsureLayer", 1
    )[0]
    assert "tr.Commit();" in method
    assert "catch\n                {\n                    tr.Abort();\n                    throw;\n                }" in method
    assert "model.AppendEntity(label);" in method
    assert "poly.XData = identity;" in method


def test_quick_section_explicit_abort_without_weakening_commit():
    src = read("SectionCadRenderer")
    quick = src.split("public static void DrawQuickProfiles(", 1)[1].split(
        "// A user can set the CAD elevation-axis step", 1
    )[0]
    assert "tr.Commit();" in quick
    assert "catch\n                {\n                    tr.Abort();\n                    throw;\n                }" in quick
    assert "AddProfilePolylines(ms,tr,profile" in quick
