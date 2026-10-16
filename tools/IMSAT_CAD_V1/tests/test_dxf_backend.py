from pathlib import Path
import pytest
import ezdxf

from imsatcad import DxfWorkspace, DxfValidationError


def test_dxf_save_as_preserves_existing_entities_layers_and_units(tmp_path: Path):
    original = tmp_path / "original.dxf"
    source = ezdxf.new("R2013")
    source.header["$INSUNITS"] = 6  # metres
    source.layers.add("SURVEY")
    source.modelspace().add_line((10, 20), (30, 40), dxfattribs={"layer": "SURVEY"})
    source.modelspace().add_text("original", dxfattribs={"layer": "SURVEY"})
    source.saveas(original)

    before = original.read_bytes()
    work = DxfWorkspace.open(original)
    work.add_3d_polyline([(1, 2, 100), (2, 3, 105)])
    n = work.add_tin_faces([[(0, 0, 10), (1, 0, 10), (0, 1, 12)]])
    output = work.save_as(tmp_path / "result.dxf")

    assert n == 1
    assert original.read_bytes() == before  # original DXF remains byte-for-byte unchanged
    entities = list(ezdxf.readfile(output).modelspace())
    types = [e.dxftype() for e in entities]
    assert types.count("LINE") == 1
    assert types.count("TEXT") == 1
    assert types.count("POLYLINE") == 1
    assert types.count("3DFACE") == 1
    assert ezdxf.readfile(output).header["$INSUNITS"] == 6
    assert DxfWorkspace.open(output).summary()["audit_errors"] == 0
    assert DxfWorkspace.open(original).summary()["modelspace_entities"] == 2


def test_reject_inplace_writes_and_unapproved_overwrite(tmp_path: Path):
    source = tmp_path / "source.dxf"
    DxfWorkspace.new().save_as(source)
    ws = DxfWorkspace.open(source)
    with pytest.raises(DxfValidationError):
        ws.save_as(source)
    exists = tmp_path / "exists.dxf"
    ws.save_as(exists)
    with pytest.raises(FileExistsError):
        ws.save_as(exists)


def test_geometry_validation_and_no_partial_tin(tmp_path: Path):
    ws = DxfWorkspace.new()
    with pytest.raises(DxfValidationError):
        ws.add_3d_polyline([(0, 0, 0)])
    with pytest.raises(DxfValidationError):
        ws.add_tin_faces([
            [(0, 0, 0), (1, 0, 0), (0, 1, 1)],
            [(0, 0, 1), (1, 1, 1), (2, 2, 1)],
        ])
    assert ws.summary()["modelspace_entities"] == 0
    assert ws.add_tin_faces([[(0, 0, 0), (1, 0, 2), (0, 1, 3)]]) == 1
    target = ws.save_as(tmp_path / "tin.dxf")
    e = next(e for e in ezdxf.readfile(target).modelspace() if e.dxftype() == "3DFACE")
    assert e.dxf.vtx1.z == 2
    assert e.dxf.vtx2.z == 3


def test_input_validation(tmp_path: Path):
    ws = DxfWorkspace.new()
    with pytest.raises(DxfValidationError):
        ws.add_3d_polyline([(0, 0, 0), (float("nan"), 0, 0)])
    with pytest.raises(DxfValidationError):
        ws.add_3d_polyline([(0, 0), (1, 1, 1)])
