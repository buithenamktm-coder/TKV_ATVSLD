from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Tuple, Optional
import json
import math
import os
import shutil
import subprocess
import tempfile

Point = Tuple[float, float]
Polyline = List[Point]


@dataclass
class CadEntity:
    kind: str
    layer: str
    points: Polyline = field(default_factory=list)
    text: str = ""
    closed: bool = False
    handle: str = ""


@dataclass
class CadModel:
    source_file: str
    working_dxf: str
    entities: List[CadEntity]
    layers: List[str]
    extents: Optional[Tuple[float, float, float, float]]
    converter_used: str = ""

    @property
    def polylines_by_layer(self) -> Dict[str, List[Polyline]]:
        result: Dict[str, List[Polyline]] = {}
        for entity in self.entities:
            if entity.kind in {
                "LWPOLYLINE", "POLYLINE", "LINE", "SPLINE", "ARC",
                "CIRCLE", "ELLIPSE", "HATCH_BOUNDARY",
            } and len(entity.points) >= 2:
                result.setdefault(entity.layer, []).append(entity.points)
        return result

    def longest_polyline(self, layer: str) -> Polyline:
        items = self.polylines_by_layer.get(layer, [])
        if not items:
            return []
        return max(items, key=polyline_length)

    def stitched_polylines(self, layer: str, tolerance: float | None = None) -> List[Polyline]:
        """Join touching CAD segments so a model split into many entities is not lost."""
        items = [list(points) for points in self.polylines_by_layer.get(layer, []) if len(points) >= 2]
        if not items:
            return []
        if tolerance is None:
            if self.extents:
                xmin, ymin, xmax, ymax = self.extents
                tolerance = max(math.hypot(xmax - xmin, ymax - ymin) * 1e-5, 1e-6)
            else:
                tolerance = 1e-5
        return stitch_polylines(items, tolerance)

    def all_closed_regions(self, layers: List[str] | None = None) -> List[CadEntity]:
        allowed = set(layers or self.layers)
        return [
            entity for entity in self.entities
            if entity.layer in allowed and entity.closed and len(entity.points) >= 3
        ]


def polyline_length(points: Polyline) -> float:
    return sum(
        math.hypot(points[index + 1][0] - points[index][0], points[index + 1][1] - points[index][1])
        for index in range(len(points) - 1)
    )


def _distance(a: Point, b: Point) -> float:
    return math.hypot(a[0] - b[0], a[1] - b[1])


def _dedupe_consecutive(points: Polyline, tolerance: float) -> Polyline:
    output: Polyline = []
    for point in points:
        point = (float(point[0]), float(point[1]))
        if not output or _distance(output[-1], point) > tolerance:
            output.append(point)
    return output


def stitch_polylines(polylines: List[Polyline], tolerance: float) -> List[Polyline]:
    """Greedily connect linework by nearest compatible endpoints.

    AutoCAD sections are frequently exported as tens or hundreds of LINE/LWPOLYLINE
    entities.  The previous reader kept only the longest object, which explains why
    only a fragment of the model appeared.  This routine preserves all chains.
    """
    chains = [_dedupe_consecutive(line, tolerance) for line in polylines]
    chains = [line for line in chains if len(line) >= 2]
    changed = True
    while changed:
        changed = False
        for i in range(len(chains)):
            if changed:
                break
            for j in range(i + 1, len(chains)):
                a, b = chains[i], chains[j]
                candidates = [
                    (_distance(a[-1], b[0]), a + b[1:]),
                    (_distance(a[-1], b[-1]), a + list(reversed(b[:-1]))),
                    (_distance(a[0], b[-1]), b + a[1:]),
                    (_distance(a[0], b[0]), list(reversed(b)) + a[1:]),
                ]
                distance, merged = min(candidates, key=lambda item: item[0])
                if distance <= tolerance:
                    chains[i] = _dedupe_consecutive(merged, tolerance)
                    del chains[j]
                    changed = True
                    break
    return sorted(chains, key=polyline_length, reverse=True)


def _possible_oda_paths() -> List[Path]:
    env = os.environ.get("ODA_FILE_CONVERTER", "").strip()
    candidates: List[Path] = []
    if env:
        candidates.append(Path(env))
    for name in ("ODAFileConverter.exe", "ODAFileConverter"):
        found = shutil.which(name)
        if found:
            candidates.append(Path(found))
    candidates += [
        Path(r"C:\Program Files\ODA\ODAFileConverter\ODAFileConverter.exe"),
        Path(r"C:\Program Files\ODA\ODAFile Converter\ODAFileConverter.exe"),
        Path(r"C:\Program Files (x86)\ODA\ODAFileConverter\ODAFileConverter.exe"),
        Path(r"C:\Program Files (x86)\ODA\ODAFile Converter\ODAFileConverter.exe"),
    ]
    seen = set()
    output = []
    for path in candidates:
        key = str(path).lower()
        if key not in seen:
            seen.add(key)
            output.append(path)
    return output


def find_oda_converter() -> Optional[Path]:
    return next((path for path in _possible_oda_paths() if path.exists()), None)


def _convert_with_oda(dwg_path: Path, output_dir: Path, converter: Path) -> Path:
    output_dir.mkdir(parents=True, exist_ok=True)
    command = [
        str(converter), str(dwg_path.parent), str(output_dir),
        "ACAD2018", "DXF", "0", "1",
    ]
    completed = subprocess.run(
        command,
        capture_output=True,
        text=True,
        timeout=180,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    candidates = list(output_dir.glob(dwg_path.stem + ".dxf")) + list(output_dir.glob(dwg_path.stem + ".DXF"))
    if not candidates:
        raise RuntimeError(
            "ODA File Converter không tạo được DXF.\n"
            f"Mã thoát: {completed.returncode}\n"
            f"Thông báo: {(completed.stderr or completed.stdout)[-1200:]}"
        )
    return candidates[0]


def ensure_dxf(path: str | Path, cache_dir: str | Path | None = None) -> tuple[Path, str]:
    source = Path(path).resolve()
    if not source.exists():
        raise FileNotFoundError(source)
    suffix = source.suffix.lower()
    if suffix == ".dxf":
        return source, "Đọc DXF trực tiếp"
    if suffix != ".dwg":
        raise ValueError("Chỉ hỗ trợ tệp DWG hoặc DXF.")
    converter = find_oda_converter()
    if converter is None:
        raise RuntimeError(
            "Chưa tìm thấy ODA File Converter.\n\n"
            "Hãy cài ODA File Converter hoặc đặt biến môi trường "
            "ODA_FILE_CONVERTER trỏ tới ODAFileConverter.exe."
        )
    target_root = Path(cache_dir) if cache_dir else Path(tempfile.gettempdir()) / "IMSAT_Slope_DWG_Cache"
    dxf = _convert_with_oda(source, target_root / source.stem, converter)
    return dxf, f"ODA File Converter: {converter}"


def _arc_points(center, radius: float, start_angle: float, end_angle: float, segments: int = 96) -> Polyline:
    start = math.radians(float(start_angle))
    end = math.radians(float(end_angle))
    if end <= start:
        end += 2 * math.pi
    return [
        (
            float(center.x) + radius * math.cos(start + (end - start) * index / segments),
            float(center.y) + radius * math.sin(start + (end - start) * index / segments),
        )
        for index in range(segments + 1)
    ]


def _entity_handle(entity) -> str:
    try:
        return str(entity.dxf.handle or "")
    except Exception:
        return ""


def _append_entity(entities: List[CadEntity], entity, layer_override: str | None = None) -> None:
    kind = entity.dxftype()
    layer = layer_override or str(getattr(entity.dxf, "layer", "0"))
    handle = _entity_handle(entity)
    if kind == "LINE":
        start, end = entity.dxf.start, entity.dxf.end
        entities.append(CadEntity(kind, layer, [(float(start.x), float(start.y)), (float(end.x), float(end.y))], handle=handle))
    elif kind == "LWPOLYLINE":
        points = [(float(point[0]), float(point[1])) for point in entity.get_points()]
        entities.append(CadEntity(kind, layer, points, closed=bool(entity.closed), handle=handle))
    elif kind == "POLYLINE":
        points = [(float(vertex.dxf.location.x), float(vertex.dxf.location.y)) for vertex in entity.vertices]
        entities.append(CadEntity(kind, layer, points, closed=bool(entity.is_closed), handle=handle))
    elif kind == "SPLINE":
        points = [(float(point.x), float(point.y)) for point in entity.approximate(segments=180)]
        entities.append(CadEntity(kind, layer, points, handle=handle))
    elif kind == "ARC":
        entities.append(CadEntity(
            kind, layer,
            _arc_points(entity.dxf.center, float(entity.dxf.radius), entity.dxf.start_angle, entity.dxf.end_angle),
            handle=handle,
        ))
    elif kind == "CIRCLE":
        center = entity.dxf.center
        radius = float(entity.dxf.radius)
        points = [
            (float(center.x) + radius * math.cos(2 * math.pi * index / 128),
             float(center.y) + radius * math.sin(2 * math.pi * index / 128))
            for index in range(129)
        ]
        entities.append(CadEntity(kind, layer, points, closed=True, handle=handle))
    elif kind == "ELLIPSE":
        points = [(float(point.x), float(point.y)) for point in entity.flattening(distance=0.1, segments=16)]
        entities.append(CadEntity(kind, layer, points, closed=bool(entity.closed), handle=handle))
    elif kind in {"TEXT", "MTEXT"}:
        text = entity.plain_text() if kind == "MTEXT" else str(entity.dxf.text)
        point = entity.dxf.insert
        entities.append(CadEntity(kind, layer, [(float(point.x), float(point.y))], text=text, handle=handle))
    elif kind == "HATCH":
        # Extract actual hatch boundary paths instead of retaining empty metadata.
        for path_index, boundary in enumerate(entity.paths):
            points: Polyline = []
            try:
                if hasattr(boundary, "vertices"):
                    points = [(float(vertex[0]), float(vertex[1])) for vertex in boundary.vertices]
                elif hasattr(boundary, "edges"):
                    for edge in boundary.edges:
                        edge_type = edge.__class__.__name__.lower()
                        if "line" in edge_type:
                            segment = [(float(edge.start[0]), float(edge.start[1])), (float(edge.end[0]), float(edge.end[1]))]
                        elif "arc" in edge_type:
                            class Center:
                                x, y = edge.center[0], edge.center[1]
                            segment = _arc_points(Center, float(edge.radius), edge.start_angle, edge.end_angle, 48)
                        else:
                            continue
                        if points and _distance(points[-1], segment[0]) < 1e-7:
                            points.extend(segment[1:])
                        else:
                            points.extend(segment)
                if len(points) >= 3:
                    if _distance(points[0], points[-1]) > 1e-7:
                        points.append(points[0])
                    entities.append(CadEntity(
                        "HATCH_BOUNDARY", layer, points, closed=True,
                        text=f"HATCH {handle} path {path_index + 1}", handle=handle,
                    ))
            except Exception:
                continue
    elif kind == "INSERT":
        # Expand block references so geometry drawn inside blocks is imported.
        try:
            for virtual in entity.virtual_entities():
                _append_entity(entities, virtual, layer_override=layer if str(getattr(virtual.dxf, "layer", "0")) == "0" else None)
        except Exception:
            point = entity.dxf.insert
            entities.append(CadEntity(kind, layer, [(float(point.x), float(point.y))], text=str(entity.dxf.name), handle=handle))


def read_cad(path: str | Path, cache_dir: str | Path | None = None) -> CadModel:
    try:
        import ezdxf
    except ImportError as exc:
        raise RuntimeError("Thiếu thư viện ezdxf. Hãy chạy BUILD_EXE_WINDOWS.bat để cài đủ thư viện.") from exc

    source = Path(path).resolve()
    dxf_path, converter = ensure_dxf(source, cache_dir)
    try:
        document = ezdxf.readfile(str(dxf_path))
    except Exception as exc:
        raise RuntimeError(f"Không đọc được DXF trung gian: {exc}") from exc

    entities: List[CadEntity] = []
    layers = {str(layer.dxf.name) for layer in document.layers}
    for entity in document.modelspace():
        try:
            _append_entity(entities, entity)
        except Exception:
            # A malformed object must not abort the rest of the model.
            continue

    layers.update(entity.layer for entity in entities)
    all_points = [point for entity in entities for point in entity.points]
    extents = None
    if all_points:
        xs = [point[0] for point in all_points]
        ys = [point[1] for point in all_points]
        extents = (min(xs), min(ys), max(xs), max(ys))

    return CadModel(
        source_file=str(source),
        working_dxf=str(dxf_path),
        entities=entities,
        layers=sorted(layers, key=str.lower),
        extents=extents,
        converter_used=converter,
    )


class LayerMappingStore:
    def __init__(self, path: str | Path):
        self.path = Path(path)

    def load(self) -> Dict[str, str]:
        if not self.path.exists():
            return {}
        try:
            return json.loads(self.path.read_text(encoding="utf-8"))
        except Exception:
            return {}

    def save(self, mapping: Dict[str, str]) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.write_text(json.dumps(mapping, ensure_ascii=False, indent=2), encoding="utf-8")
