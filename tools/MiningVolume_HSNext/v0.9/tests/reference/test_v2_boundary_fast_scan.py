"""Boundary selection on 1.6M-face AutoCAD 2023 mines must not open each Face.

Static contracts supplement host-only SectionCadRegression. CI build alone
is not real AutoCAD or final release acceptance.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def source(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_unique_boundary_uses_polyline_class_before_database_open():
    code = source("src/MiningVolume.Plugin2023/Services/BoundaryGeometryService.cs")
    scan = code.split("internal static string FindUniqueBoundaryOnLayer(Database db,", 1)[1].split(
        "public static IReadOnlyList<Vec2> ReadBoundary(", 1
    )[0]
    assert "RXObject.GetClass(typeof(Polyline))" in scan
    assert "id.ObjectClass.IsDerivedFrom(polyClass)" in scan
    assert scan.index("id.ObjectClass.IsDerivedFrom(polyClass)") < scan.index(
        "tr.GetObject(id, OpenMode.ForRead)"
    )
    assert "pl.Closed" in scan
    assert "pl.NumberOfVertices < 3" in scan
    assert "string.Equals(pl.Layer, layer, StringComparison.OrdinalIgnoreCase)" in scan
    assert "Layer {layer} có nhiều đường bao khép kín" in scan
    assert "Không tìm thấy đường bao khép kín" in scan
    assert "StartOpenCloseTransaction()" in scan


def test_boundary_scan_polls_esc_and_preserves_cancel_integrity():
    code = source("src/MiningVolume.Plugin2023/Services/BoundaryGeometryService.cs")
    assert "Action pollCancellation = null" in code
    assert 'new SectionCadProgress(1, "tìm đường bao")' in code
    assert "(scanned++ & 1023) == 0" in code
    assert "pollCancellation?.Invoke();" in code
    assert "progress.Poll();" in code
    assert "doc.Database, layer, pollCancellation" in code
    host = source("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    assert "VerifyBoundarySelectionSkipsTinAndSupportsCancel();" in host
    assert 'TinCadRenderer.CountFaces(db,"QA_TIN") != 2300' in host
    assert "if (++polls >= 2) throw new OperationCanceledException();" in host
    assert "Ambiguous closed boundaries must require explicit selection." in host
