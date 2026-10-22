from __future__ import annotations
import math
from dataclasses import dataclass
from typing import Iterable, Optional

from shapely.geometry import Polygon
from shapely.ops import unary_union

from .models import GeoRegion, GeoStudioModel, Point

EPS = 1e-8

@dataclass
class GeometryIssue:
    severity: str
    code: str
    message: str
    region_id: Optional[int] = None


def clean_ring(points: Iterable[Point]) -> list[Point]:
    clean: list[Point] = []
    for x, y in points:
        p = (float(x), float(y))
        if clean and math.hypot(p[0]-clean[-1][0], p[1]-clean[-1][1]) <= EPS:
            continue
        clean.append(p)
    if len(clean) > 1 and math.hypot(clean[0][0]-clean[-1][0], clean[0][1]-clean[-1][1]) <= EPS:
        clean.pop()
    return clean


def polygon_area(points: Iterable[Point]) -> float:
    pts = clean_ring(points)
    if len(pts) < 3:
        return 0.0
    return abs(sum(pts[i][0]*pts[(i+1)%len(pts)][1] - pts[(i+1)%len(pts)][0]*pts[i][1]
                   for i in range(len(pts))) / 2.0)


def model_bounds(model: GeoStudioModel, padding: float = 0.04) -> tuple[float, float, float, float]:
    """Bounds of geometry that is actually visible/calculable.

    V6.1 used only Region points and then fell back to a fixed 0..100 box.  A
    freshly imported CAD section with zero Regions was therefore drawn outside
    that box, producing a tiny axis in the middle and linework at the top of the
    window.  The bounds now follow Regions, terrain and reference linework in
    that priority order, with ``view_bounds`` only as a final fallback.
    """
    pts = [p for region in model.regions for p in region.polygon]
    if not pts:
        pts = list(model.terrain)
    if not pts:
        pts = [p for line in model.reference_geometry for p in line]
    if not pts:
        pts = list(model.points)
    if not pts and len(model.view_bounds) >= 4:
        xmin, ymin, xmax, ymax = map(float, model.view_bounds[:4])
    elif pts:
        xs = [float(p[0]) for p in pts]
        ys = [float(p[1]) for p in pts]
        xmin, xmax = min(xs), max(xs)
        ymin, ymax = min(ys), max(ys)
    else:
        return (0.0, 0.0, 100.0, 100.0)
    dx = max(1.0, xmax - xmin)
    dy = max(1.0, ymax - ymin)
    return xmin - padding * dx, ymin - padding * dy, xmax + padding * dx, ymax + padding * dy


def upper_envelope(regions: list[GeoRegion]) -> list[Point]:
    """Top external boundary derived from actual Region edges.

    This function never creates vertical slice lines. It only samples the real
    polygon edges at their existing vertex X values and keeps the highest edge.
    """
    xs = sorted({float(x) for region in regions for x, _ in clean_ring(region.polygon)})
    if not xs:
        return []
    out: list[Point] = []
    for x in xs:
        ys: list[float] = []
        for region in regions:
            ring = clean_ring(region.polygon)
            for a, b in zip(ring, ring[1:] + ring[:1]):
                x1, y1 = a; x2, y2 = b
                if x < min(x1, x2)-EPS or x > max(x1, x2)+EPS:
                    continue
                if abs(x2-x1) <= EPS:
                    if abs(x-x1) <= EPS:
                        ys.extend([y1, y2])
                else:
                    t = (x-x1)/(x2-x1)
                    if -EPS <= t <= 1+EPS:
                        ys.append(y1+t*(y2-y1))
        if ys:
            out.append((x, max(ys)))
    return clean_ring(out)


def validate_model(model: GeoStudioModel) -> list[GeometryIssue]:
    issues: list[GeometryIssue] = []
    material_ids = {m.source_id for m in model.materials}
    polygons = []
    if not model.regions:
        issues.append(GeometryIssue('error','REGION_MISSING','Mô hình chưa có Region kín nên không thể chia lát hoặc tính Fs.'))
    for region in model.regions:
        ring = clean_ring(region.polygon)
        if len(ring) < 3:
            issues.append(GeometryIssue('error','REGION_TOO_FEW_POINTS',f'Region {region.source_id} có dưới 3 điểm.',region.source_id))
            continue
        poly = Polygon(ring)
        if poly.area <= EPS:
            issues.append(GeometryIssue('error','REGION_ZERO_AREA',f'Region {region.source_id} có diện tích bằng 0.',region.source_id))
        if not poly.is_valid:
            issues.append(GeometryIssue('error','REGION_INVALID',f'Region {region.source_id} tự cắt hoặc không hợp lệ.',region.source_id))
        if region.material_source_id not in material_ids:
            issues.append(GeometryIssue('warning','MATERIAL_UNASSIGNED',f'Region {region.source_id} chưa có vật liệu hợp lệ.',region.source_id))
        polygons.append((region.source_id, poly))
    # Overlap test excludes shared boundaries.
    for i, (rid_a, a) in enumerate(polygons):
        if not a.is_valid:
            continue
        for rid_b, b in polygons[i+1:]:
            if not b.is_valid:
                continue
            overlap = a.intersection(b).area
            tol = max(EPS, min(a.area, b.area)*1e-7)
            if overlap > tol:
                issues.append(GeometryIssue('error','REGION_OVERLAP',f'Region {rid_a} và {rid_b} chồng lấn {overlap:.3f} m².'))
    terrain = model.terrain or upper_envelope(model.regions)
    if len(terrain) < 2:
        issues.append(GeometryIssue('error','TERRAIN_MISSING','Không xác định được biên địa hình phía trên.'))
    return issues


def assign_material(model: GeoStudioModel, region_id: int, material_id: int) -> None:
    region = model.region_by_id(region_id)
    if region is None:
        raise KeyError(f'Không tìm thấy Region {region_id}.')
    if model.material_by_id(material_id) is None:
        raise KeyError(f'Không tìm thấy vật liệu {material_id}.')
    region.material_source_id = material_id
