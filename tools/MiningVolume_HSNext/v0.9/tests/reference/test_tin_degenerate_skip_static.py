from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def source(relative):
    return (ROOT / relative).read_text(encoding="utf-8")


def test_large_file_backed_tin_skips_only_nonpositive_or_nonfinite_faces():
    store = source("src/MiningVolume.Surface/AdaptiveTinStore.cs")
    assert "out int small, out int degenerate" in store
    assert "if (!(t.Area2D > 0) || double.IsInfinity(t.Area2D))" in store
    assert "degenerate++;" in store
    assert "continue;" in store[store.index("degenerate++;"):store.index("degenerate++;") + 100]
    assert 'TIN không tạo được tam giác hợp lệ' in store
    assert "if (t.Area2D <= options.MinimumTriangleArea) small++;" in store
    assert "FileBackedTiledTriangleList.WriteTriangle(writer,t)" in store


def test_both_mesh_modes_disclose_skipped_faces_and_preserve_input():
    builder = source("src/MiningVolume.Surface/ConformingTinBuilder.cs")
    assert "out retainedSmall, out skippedDegenerate" in builder
    assert "SKIPPED_DEGENERATE_TRIANGLES" in builder
    assert "skippedDegenerateInMemory++;" in builder
    assert "giữ nguyên dữ liệu CAD gốc" in builder
    assert "if (output.Count == 0)" in builder
    assert "RETAIN_SMALL_TRIANGLES" in builder
