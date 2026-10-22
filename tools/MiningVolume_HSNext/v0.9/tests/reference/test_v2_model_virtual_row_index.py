"""Model management must remain responsive for very large source point sets.

The UI grid is virtual. Its row count must be indexed without creating one
per-vertex RowRef at every unrelated project state notification.
This is a source contract, not an AutoCAD runtime benchmark.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def test_model_grid_does_not_materialize_all_vertex_rows():
    ui = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    idx = read("src/MiningVolume.Core/Model/ModelVertexRowIndex.cs")
    assert "VirtualMode = true" in ui
    assert "new ModelVertexRowIndex(source)" in ui
    assert "private RowRef RowAt(int row)" in ui
    assert "index.TryGet(row, out entity, out vertex)" in ui
    assert "_grid.RowCount = count" in ui
    rebuild = ui.split("private void RebuildRows()", 1)[1].split(
        "private RowRef RowAt(", 1
    )[0]
    assert "foreach (var vertex in entity.Vertices)" not in rebuild
    assert "new RowRef { Entity = entity, Vertex = vertex }" not in rebuild
    assert "_rowIndex.Revision != source.Revision" in rebuild
    assert "if (switched) _grid.ClearSelection();" in rebuild
    assert "s.ActiveVertexCount" not in ui
    assert "s.VertexCount" not in ui

    assert "Array.BinarySearch(_endOffsets, row + 1)" in idx
    assert "vertex = entity.Vertices[row - offset];" in idx
    assert "if (count == 0) continue;" in idx
    assert "if (rows > int.MaxValue - count)" in idx
    assert "foreach (var vertex in entity.Vertices)" in idx


def test_source_actions_still_resolve_current_entities():
    ui = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    assert "private IEnumerable<RowRef> SelectedRows()" in ui
    assert ".Select(x => RowAt(x.Index))" in ui
    assert "source.SetVertexEnabled(r.Entity.Id, r.Vertex.Index, false)" in ui
    assert "source.OverrideVertexZ(row.Entity.Id, row.Vertex.Index, form.Value)" in ui
    assert "CadViewService.ZoomToHandle(row.Entity.Handle)" in ui
    assert "index.Revision != source.Revision" in ui

    core = read("tests/core/CoreRegression.cs")
    assert "model_grid_virtual_index_maps_vertices_and_revisions" in core
    assert "index.RowCount==30003" in core
    assert "renewed.ActiveVertexCount==30002" in core
