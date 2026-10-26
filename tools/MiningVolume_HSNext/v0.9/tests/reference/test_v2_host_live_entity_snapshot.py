"""Host regression must compare LIVE AutoCAD state, not rolled-back tombstones.

AutoCAD side databases can retain erased ObjectIds in block/model iterators
after abort; skipping only IsErased ids preserves live-handle and geometry
comparisons. This is a source contract, NOT an AutoCAD runtime PASS claim.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = (ROOT / "src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs").read_text(
    encoding="utf-8-sig"
)


def test_quantity_rollback_counts_only_live_references_and_definitions():
    body = SRC.split("private static string TableSnapshot(", 1)[1].split(
        "private static void DrawBatches(", 1
    )[0]
    assert "if (id.IsErased) continue;" in body
    assert "if (!definition.IsErased &&" in body
    assert "if (reference.IsErased) continue;" in body
    assert "references != expectedExports || definitions != expectedExports" in body
    assert "if (count != expectedExports * 3)" in body
    assert "Quantity output must be a block reference." in body


def test_cancel_snapshot_preserves_all_live_handles_and_geometry():
    body = SRC.split("private static string Snapshot(", 1)[1].split(
        "private static void Point(", 1
    )[0]
    assert "if (id.IsErased) continue;" in body
    assert "if (entity.IsErased) continue;" in body
    assert "if (handles) output.Append(entity.Handle.ToString());" in body
    assert "Point(output, poly.GetPoint3dAt(i))" in body
    assert "Point(output, line.StartPoint)" in body


def test_grid_labels_are_text_with_calculation_identity():
    body = SRC.split("private static void VerifyGridIdentity()", 1)[1].split(
        "private static void VerifyTableRollback()", 1
    )[0]
    assert 'string.Equals(entity.Layer, "MV_LUOI_NHAN"' in body
    assert "labelLayer ? !(entity is DBText) : !(entity is Polyline)" in body
    assert "GetXDataForApplication(GridCadRenderer.CalculationIdApp)" in body
    assert "oldLabels != first.Cells.Count || newLabels != second.Cells.Count" in body
