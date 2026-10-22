"""Performance regression contracts for file-backed TIN section sampling.

These source checks complement the compiled C# exact-geometry comparisons.
They do not constitute an AutoCAD 2023 runtime benchmark.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def test_tiled_sections_use_cached_tile_segment_candidates():
    code = read("src/MiningVolume.Core/Sections/TinSectionSampler.cs")
    tiled = code.split("public IEnumerable<Triangle3> QuerySegmentTriangles(Vec2 a, Vec2 b)", 1)[1].split(
        "// Exact geometry: candidate bins only filter triangle bounding", 1
    )[0]
    assert "GetTile(tileIndex).QuerySegmentTriangles(a, b)" in tiled
    assert "foreach (var tri in tris)" not in tiled

    local = code.split("public IReadOnlyList<Triangle3> Triangles => _triangles;", 1)[1].split(
        "public bool TryElevation(Vec2 p, double tol, out double z)", 1
    )[0]
    assert "public IEnumerable<Triangle3> QuerySegmentTriangles(Vec2 a, Vec2 b)" in local
    assert "private static bool Clip(" in local
    assert "var candidates = new HashSet<int>();" in local
    assert "foreach (int id in _broad) candidates.Add(id);" in local
    assert "yield return _triangles[id];" in local
    assert "_maxX[id] < minX" in local and "_minY[id] > maxY" in local


def test_core_checks_tiled_vs_full_mesh_and_long_diagonal():
    code = read("tests/core/CoreRegression.cs")
    assert "tiled_section_spatial_query_matches_exact_reference" in code
    assert "CheckTiledSectionParity()" in code
    assert 'line.Name == "diagonal" ? n*Math.Sqrt(2.0) : n' in code
    assert "Broad triangles omitted from tiled section spatial query" in code
