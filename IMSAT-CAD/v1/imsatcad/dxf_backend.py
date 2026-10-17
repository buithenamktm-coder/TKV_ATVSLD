"""DXF document operations for the IMSAT CAD foundation.

This is deliberately an independent, testable interoperability layer.
It does NOT implement the QCAD engine, DWG I/O, TIN triangulation,
3D visualization, or an AutoCAD-compatible UI.
"""

from __future__ import annotations

from collections import Counter
from pathlib import Path
import os
import tempfile
from typing import Iterable, Sequence

import ezdxf


class DxfValidationError(ValueError):
    """Raised when a DXF operation is invalid or fails an integrity check."""


def _xyz(value: Sequence[float]) -> tuple[float, float, float]:
    if len(value) != 3:
        raise DxfValidationError("Each 3D point must have exactly X, Y, Z.")
    values = tuple(float(v) for v in value)
    from math import isfinite
    if not all(isfinite(v) for v in values):
        raise DxfValidationError("Coordinates must be finite numbers.")
    return values  # type: ignore[return-value]


class DxfWorkspace:
    """DXF modelspace with non-destructive save-as and validation."""

    def __init__(self, doc: ezdxf.document.Drawing, source: Path | None = None):
        self.doc = doc
        self.source = source

    @classmethod
    def open(cls, source: str | Path) -> "DxfWorkspace":
        source = Path(source).expanduser().resolve()
        if not source.is_file():
            raise FileNotFoundError(source)
        try:
            doc = ezdxf.readfile(source)
        except (ezdxf.DXFError, OSError) as exc:
            raise DxfValidationError(f"Unable to read DXF: {source}") from exc
        return cls(doc, source)

    @classmethod
    def new(cls, dxfversion: str = "R2013") -> "DxfWorkspace":
        return cls(ezdxf.new(dxfversion=dxfversion))

    def summary(self) -> dict:
        """Return modelspace and document metadata without modifying drawing."""
        types = Counter(ent.dxftype() for ent in self.doc.modelspace())
        auditor = self.doc.audit()
        return {
            "dxfversion": self.doc.dxfversion,
            "insunits": int(self.doc.header.get("$INSUNITS", 0)),
            "modelspace_entities": sum(types.values()),
            "entity_types": dict(sorted(types.items())),
            "layer_names": sorted(layer.dxf.name for layer in self.doc.layers),
            "audit_errors": len(auditor.errors),
            "audit_fixes": len(auditor.fixes),
        }

    def ensure_layer(self, name: str) -> None:
        if not isinstance(name, str) or not name.strip() or name in {"*Model_Space", "*Paper_Space"}:
            raise DxfValidationError("Invalid layer name.")
        if name not in self.doc.layers:
            self.doc.layers.add(name)

    def add_3d_polyline(self, vertices: Iterable[Sequence[float]], layer: str = "IMSAT_GEOMETRY") -> None:
        pts = [_xyz(p) for p in vertices]
        if len(pts) < 2:
            raise DxfValidationError("A polyline needs at least two vertices.")
        self.ensure_layer(layer)
        self.doc.modelspace().add_polyline3d(pts, dxfattribs={"layer": layer})

    def add_tin_faces(self, triangles: Iterable[Sequence[Sequence[float]]], layer: str = "IMSAT_TIN") -> int:
        """Store provided triangles as 3DFACE. This does not triangulate points."""
        faces = []
        for triangle in triangles:
            if len(triangle) != 3:
                raise DxfValidationError("Every TIN face must contain three vertices.")
            pts = [_xyz(p) for p in triangle]
            x0, y0, _ = pts[0]
            x1, y1, _ = pts[1]
            x2, y2, _ = pts[2]
            area2 = (x1 - x0) * (y2 - y0) - (y1 - y0) * (x2 - x0)
            if abs(area2) <= 1e-12:
                raise DxfValidationError("TIN face is degenerate in XY projection.")
            faces.append(pts)
        self.ensure_layer(layer)
        for face in faces:
            self.doc.modelspace().add_3dface(face, dxfattribs={"layer": layer})
        return len(faces)

    def save_as(self, destination: str | Path, *, overwrite: bool = False) -> Path:
        """Validate and save to a separate path, never overwrite input DXF."""
        target = Path(destination).expanduser().resolve()
        if self.source and target == self.source:
            raise DxfValidationError("In-place save is disabled; use a different output path.")
        if target.exists() and not overwrite:
            raise FileExistsError(target)
        if not target.parent.is_dir():
            raise FileNotFoundError(target.parent)
        audit = self.doc.audit()
        if audit.errors:
            raise DxfValidationError(f"DXF audit reports {len(audit.errors)} error(s).")
        temp_name = None
        try:
            with tempfile.NamedTemporaryFile(
                mode="wb", prefix=".imsat_dxf_", suffix=".dxf", dir=target.parent, delete=False
            ) as f:
                temp_name = f.name
            self.doc.saveas(temp_name)
            verify = ezdxf.readfile(temp_name)
            verified_audit = verify.audit()
            if verified_audit.errors:
                raise DxfValidationError("Output DXF failed its post-save audit.")
            if target.exists() and not overwrite:
                raise FileExistsError(target)
            os.replace(temp_name, target)
            return target
        finally:
            if temp_name and os.path.exists(temp_name):
                os.unlink(temp_name)
