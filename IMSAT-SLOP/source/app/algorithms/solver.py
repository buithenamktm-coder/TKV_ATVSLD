from __future__ import annotations

from collections import Counter
from dataclasses import dataclass, field, replace
import math
import random
from typing import Iterable, Optional, Sequence

from shapely.geometry import Point as ShapelyPoint, Polygon, box, LineString
from shapely.ops import unary_union

from app.core.geometry import clean_ring, upper_envelope
from app.core.models import GeoStudioModel, Material, Point, SliceData, SlipSurface
from .morgenstern_price import solve_morgenstern_price

EPS = 1e-9


@dataclass
class SurfaceGeometry:
    path: list[Point]
    center_x: float = 0.0
    center_y: float = 0.0
    radius: float = 0.0


@dataclass
class SliceBuildReport:
    slices: list[SliceData] = field(default_factory=list)
    code: str = "OK"
    reason: str = ""
    slice_number: int = 0

    @property
    def valid(self) -> bool:
        return bool(self.slices) and self.code == "OK"


@dataclass
class SolverContext:
    terrain: list[Point]
    records: list[tuple]
    domain: object
    xmin: float
    ymin: float
    xmax: float
    ymax: float
    scale: float


def _x_monotonic(points: Sequence[Point], upper: bool = True) -> list[Point]:
    """Return an X-monotonic envelope without reordering polygon boundaries elsewhere."""
    selected: dict[float, float] = {}
    for x, y in points:
        x, y = float(x), float(y)
        if x not in selected:
            selected[x] = y
        else:
            selected[x] = max(selected[x], y) if upper else min(selected[x], y)
    return sorted(selected.items())


def interp_y(points: Sequence[Point], x: float, *, upper: bool = True) -> Optional[float]:
    pts = _x_monotonic(points, upper)
    if len(pts) < 2 or x < pts[0][0] - EPS or x > pts[-1][0] + EPS:
        return None
    if x <= pts[0][0]:
        return pts[0][1]
    if x >= pts[-1][0]:
        return pts[-1][1]
    lo, hi = 0, len(pts) - 1
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if pts[mid][0] <= x:
            lo = mid
        else:
            hi = mid
    x1, y1 = pts[lo]
    x2, y2 = pts[hi]
    if abs(x2 - x1) < EPS:
        return max(y1, y2) if upper else min(y1, y2)
    return y1 + (y2 - y1) * (x - x1) / (x2 - x1)


def _region_polygons(model: GeoStudioModel):
    output = []
    # Higher priority wins when boundaries overlap by tiny CAD tolerances.
    for region in sorted(model.regions, key=lambda r: (r.priority, r.source_id), reverse=True):
        ring = clean_ring(region.polygon)
        if len(ring) < 3:
            continue
        poly = Polygon(ring)
        if not poly.is_valid:
            poly = poly.buffer(0)
        if poly.is_empty or poly.area <= EPS:
            continue
        material = model.material_by_id(region.material_source_id)
        if material:
            output.append((region, material, poly))
    return output


def _make_context(model: GeoStudioModel) -> Optional[SolverContext]:
    terrain = _x_monotonic(model.terrain or upper_envelope(model.regions), upper=True)
    records = _region_polygons(model)
    if len(terrain) < 2 or not records:
        return None
    domain = unary_union([poly for _, _, poly in records])
    if domain.is_empty:
        return None
    xmin, ymin, xmax, ymax = map(float, domain.bounds)
    scale = max(1.0, xmax - xmin, ymax - ymin)
    return SolverContext(terrain, records, domain, xmin, ymin, xmax, ymax, scale)


def material_at(model: GeoStudioModel, x: float, y: float, context: Optional[SolverContext] = None) -> Optional[Material]:
    context = context or _make_context(model)
    if context is None:
        return None
    point = ShapelyPoint(float(x), float(y))
    for _, material, poly in context.records:
        if poly.covers(point):
            return material
    return None


def _material_above_base(
    model: GeoStudioModel,
    x: float,
    y: float,
    alpha: float,
    context: SolverContext,
) -> Optional[Material]:
    """Find the material immediately above the slip base.

    A fixed ``y + 1e-5`` probe is unreliable for large-coordinate CAD models and
    for steep bases.  The probe distance is now scale-aware and follows the base
    normal into the sliding mass.
    """
    nx, ny = -math.sin(alpha), math.cos(alpha)
    if ny < 0:
        nx, ny = -nx, -ny
    probe = max(1e-6, context.scale * 1e-7)
    candidates = [
        (x + nx * probe * factor, y + ny * probe * factor)
        for factor in (1.0, 5.0, 25.0, 100.0)
    ]
    candidates += [(x, y + probe * factor) for factor in (1.0, 10.0, 100.0)]
    for px, py in candidates:
        material = material_at(model, px, py, context)
        if material is not None:
            return material
    # Last resort: accept a boundary-touching Region within a scale-aware tolerance.
    point = ShapelyPoint(x, y)
    tolerance = max(1e-7, context.scale * 1e-8)
    for _, material, poly in context.records:
        if poly.distance(point) <= tolerance:
            return material
    return None


def water_y(model: GeoStudioModel, x: float) -> Optional[float]:
    if not model.water.enabled:
        return None
    if len(model.water.phreatic_line) >= 2:
        return interp_y(model.water.phreatic_line, x, upper=True)
    return model.water.level


def _slice_mass(model: GeoStudioModel, polygon: Polygon, context: SolverContext) -> tuple[float, float, float, float, bool]:
    """Return area, weight, weighted centroid and material coverage state.

    Intersections are consumed in priority order so a tiny CAD overlap is not
    double-counted in the slice weight.
    """
    area = weight = mx = my = 0.0
    remaining = polygon
    for _, material, region_poly in context.records:
        if remaining.is_empty:
            break
        part = remaining.intersection(region_poly)
        if part.is_empty:
            continue
        a = float(part.area)
        if a <= EPS:
            continue
        centroid = part.centroid
        wy = water_y(model, float(centroid.x))
        gamma = material.saturated_unit_weight if wy is not None and centroid.y < wy else material.unit_weight
        w = a * gamma
        area += a
        weight += w
        mx += w * centroid.x
        my += w * centroid.y
        try:
            remaining = remaining.difference(region_poly)
        except Exception:
            pass
    full = float(polygon.area)
    if weight <= EPS:
        centroid = polygon.centroid
        return area, weight, float(centroid.x), float(centroid.y), False
    uncovered = max(0.0, full - area)
    tolerance = max(EPS, full * 2e-5)
    return area, weight, mx / weight, my / weight, uncovered <= tolerance


def _load_components(model: GeoStudioModel, x1: float, x2: float) -> tuple[float, float]:
    vertical = horizontal = 0.0
    for load in model.loads:
        if not load.enabled:
            continue
        overlap = max(0.0, min(x2, load.x_end) - max(x1, load.x_start))
        if overlap <= 0:
            continue
        force = load.magnitude * overlap
        angle = math.radians(load.angle_from_vertical_deg)
        vertical += force * math.cos(angle)
        horizontal += force * math.sin(angle)
    return vertical, horizontal


def build_slices_report(
    model: GeoStudioModel,
    surface: Sequence[Point],
    count: Optional[int] = None,
    context: Optional[SolverContext] = None,
) -> SliceBuildReport:
    context = context or _make_context(model)
    if context is None:
        return SliceBuildReport(code="MODEL_DOMAIN_MISSING", reason="Không xác định được miền vật liệu hoặc địa hình.")
    terrain = context.terrain
    slip = _x_monotonic(surface, upper=False)
    if len(slip) < 2:
        return SliceBuildReport(code="SURFACE_TOO_FEW_POINTS", reason="Mặt trượt có dưới hai điểm.")
    x0, xn = slip[0][0], slip[-1][0]
    if xn - x0 <= max(EPS, context.scale * 1e-10):
        return SliceBuildReport(code="SURFACE_ZERO_WIDTH", reason="Mặt trượt có chiều rộng bằng 0.")
    if x0 < context.xmin - context.scale * 1e-7 or xn > context.xmax + context.scale * 1e-7:
        return SliceBuildReport(code="SURFACE_OUTSIDE_MODEL", reason="Điểm vào/ra nằm ngoài miền mô hình.")

    n = max(8, min(200, int(count or model.analysis.slices)))
    xs = [x0 + (xn - x0) * i / n for i in range(n + 1)]
    margin = max(10.0, context.ymax - context.ymin, context.scale * 0.1)
    slices: list[SliceData] = []
    min_thickness = max(1e-7, context.scale * 1e-8)

    for index, (x1, x2) in enumerate(zip(xs, xs[1:]), 1):
        yt1 = interp_y(terrain, x1, upper=True)
        yt2 = interp_y(terrain, x2, upper=True)
        yb1 = interp_y(slip, x1, upper=False)
        yb2 = interp_y(slip, x2, upper=False)
        if None in (yt1, yt2, yb1, yb2):
            return SliceBuildReport(code="INTERPOLATION_FAILED", reason="Không nội suy được biên lát.", slice_number=index)
        if yb1 > yt1 + min_thickness or yb2 > yt2 + min_thickness:
            return SliceBuildReport(code="SURFACE_ABOVE_TERRAIN", reason="Mặt trượt đi lên trên địa hình.", slice_number=index)
        mid_x = (x1 + x2) / 2.0
        top_mid = interp_y(terrain, mid_x, upper=True)
        bot_mid = interp_y(slip, mid_x, upper=False)
        if top_mid is None or bot_mid is None or top_mid - bot_mid <= min_thickness:
            return SliceBuildReport(code="SLICE_ZERO_THICKNESS", reason="Có lát không có chiều dày dương.", slice_number=index)

        # Exact material-domain intersection above the linearized base segment.
        above_base = Polygon([
            (x1, yb1), (x2, yb2),
            (x2, context.ymax + margin), (x1, context.ymax + margin),
        ])
        strip = box(x1, context.ymin - margin, x2, context.ymax + margin)
        polygon = context.domain.intersection(strip).intersection(above_base)
        if polygon.is_empty or polygon.area <= EPS:
            return SliceBuildReport(code="SLICE_OUTSIDE_DOMAIN", reason="Lát không giao với miền vật liệu.", slice_number=index)
        area, weight, cx, cy, covered = _slice_mass(model, polygon, context)
        if area <= EPS or weight <= EPS:
            return SliceBuildReport(code="SLICE_ZERO_WEIGHT", reason="Lát có diện tích hoặc trọng lượng bằng 0.", slice_number=index)
        if not covered:
            return SliceBuildReport(code="SLICE_MATERIAL_GAP", reason="Lát có khoảng trống vật liệu chưa được gán.", slice_number=index)

        dx = x2 - x1
        dy = yb2 - yb1
        base_length = math.hypot(dx, dy)
        if base_length <= EPS:
            return SliceBuildReport(code="SLICE_ZERO_BASE", reason="Chiều dài đáy lát bằng 0.", slice_number=index)
        alpha = math.atan2(dy, dx)
        material = _material_above_base(model, mid_x, (yb1 + yb2) / 2.0, alpha, context)
        if material is None:
            return SliceBuildReport(code="BASE_MATERIAL_MISSING", reason="Không xác định được vật liệu ngay trên đáy lát.", slice_number=index)

        mid_y = (yb1 + yb2) / 2.0
        wy = water_y(model, mid_x)
        pore_force = 0.0
        if wy is not None and wy > mid_y:
            pore_force = model.water.gamma_water * (wy - mid_y) * base_length
        surcharge_vertical, surcharge_horizontal = _load_components(model, x1, x2)
        seismic_horizontal = model.seismic.kh * weight if model.seismic.enabled else 0.0
        slices.append(SliceData(
            number=index,
            x_left=x1,
            x_right=x2,
            y_bot_left=yb1,
            y_bot_right=yb2,
            y_top_left=yt1,
            y_top_right=yt2,
            weight=weight,
            pore_pressure=pore_force,
            base_length=base_length,
            base_angle=alpha,
            cohesion=material.cohesion,
            phi=material.phi,
            material_id=material.source_id,
            centroid_x=cx,
            centroid_y=cy,
            surcharge_vertical=surcharge_vertical,
            surcharge_horizontal=surcharge_horizontal,
            seismic_horizontal=seismic_horizontal,
        ))
    return SliceBuildReport(slices=slices)


def build_slices(model: GeoStudioModel, surface: Sequence[Point], count: Optional[int] = None) -> list[SliceData]:
    return build_slices_report(model, surface, count=count).slices


def _driving_for_sign(slices: Sequence[SliceData], sign: float) -> float:
    return sum(
        (s.weight + s.surcharge_vertical) * math.sin(sign * s.base_angle)
        + s.seismic_horizontal * math.cos(sign * s.base_angle)
        + s.surcharge_horizontal * math.cos(sign * s.base_angle)
        for s in slices
    )


def _direction_sign(model: GeoStudioModel, slices: Sequence[SliceData]) -> tuple[float, list[str]]:
    selection = model.analysis.slip_direction.lower().replace(" ", "")
    requested = None
    if selection in {"lefttoright", "trái→phải", "traitphai", "trai-phai", "left-right"}:
        requested = 1.0
    elif selection in {"righttoleft", "phải→trái", "phaitrai", "phai-trai", "right-left"}:
        requested = -1.0
    d_plus = _driving_for_sign(slices, 1.0)
    d_minus = _driving_for_sign(slices, -1.0)
    if requested is not None:
        requested_driving = d_plus if requested > 0 else d_minus
        opposite_driving = d_minus if requested > 0 else d_plus
        if requested_driving > EPS:
            return requested, []
        if opposite_driving > EPS:
            return -requested, ["Hướng trượt khai báo cho lực gây trượt không dương; phần mềm đã tự đảo hướng."]
        return requested, ["Không xác định được thành phần gây trượt dương ở cả hai hướng."]
    if max(d_plus, d_minus) <= EPS:
        return 1.0, ["Không xác định được thành phần gây trượt dương ở cả hai hướng."]
    return (1.0 if d_plus >= d_minus else -1.0), []


def solve_fellenius(model: GeoStudioModel, slices: Sequence[SliceData]):
    sign, warnings = _direction_sign(model, slices)
    resisting = driving = 0.0
    for s in slices:
        a = sign * s.base_angle
        vertical = s.weight + s.surcharge_vertical
        normal = vertical * math.cos(a) + s.surcharge_horizontal * math.sin(a) - s.pore_pressure
        resisting += s.cohesion * s.base_length + max(0.0, normal) * math.tan(math.radians(s.phi))
        driving += vertical * math.sin(a) + s.seismic_horizontal * math.cos(a) + s.surcharge_horizontal * math.cos(a)
    fs = resisting / driving if driving > EPS else math.nan
    return fs, math.isfinite(fs) and fs > 0, 1, None, warnings + ([] if driving > EPS else ["Thành phần gây trượt không dương."])


def solve_bishop(model: GeoStudioModel, slices: Sequence[SliceData]):
    sign, warnings = _direction_sign(model, slices)
    tolerance = model.analysis.tolerance
    max_iterations = model.analysis.max_iterations
    driving = _driving_for_sign(slices, sign)
    if driving <= EPS:
        return math.nan, False, 0, None, warnings + ["Thành phần gây trượt không dương."]
    fs = 1.5
    for iteration in range(1, max_iterations + 1):
        numerator = 0.0
        for s in slices:
            a = sign * s.base_angle
            tan_phi = math.tan(math.radians(s.phi))
            width = s.x_right - s.x_left
            m_alpha = math.cos(a) + math.sin(a) * tan_phi / fs
            if abs(m_alpha) < EPS:
                return math.nan, False, iteration, None, warnings + ["mα bằng 0."]
            ub = (s.pore_pressure / s.base_length) * width if s.base_length > EPS else 0.0
            effective = s.weight + s.surcharge_vertical - ub
            numerator += (s.cohesion * width + max(0.0, effective) * tan_phi) / m_alpha
        new_fs = numerator / driving
        if not math.isfinite(new_fs) or new_fs <= 0:
            return math.nan, False, iteration, None, warnings + ["Fs không hữu hạn."]
        if abs(new_fs - fs) <= tolerance:
            return new_fs, True, iteration, None, warnings
        # Mild damping avoids oscillation for high-phi/steep-base cases.
        fs = 0.65 * new_fs + 0.35 * fs
    return fs, False, max_iterations, None, warnings + ["Chưa hội tụ trong số vòng lặp cho phép."]


def solve_janbu(model: GeoStudioModel, slices: Sequence[SliceData]):
    sign, warnings = _direction_sign(model, slices)
    tolerance = model.analysis.tolerance
    max_iterations = model.analysis.max_iterations
    fs = 1.5
    for iteration in range(1, max_iterations + 1):
        numerator = denominator = 0.0
        for s in slices:
            a = sign * s.base_angle
            tan_phi = math.tan(math.radians(s.phi))
            width = s.x_right - s.x_left
            m_alpha = math.cos(a) + math.sin(a) * tan_phi / fs
            if abs(m_alpha) < EPS:
                return math.nan, False, iteration, None, warnings + ["mα bằng 0."]
            ub = (s.pore_pressure / s.base_length) * width if s.base_length > EPS else 0.0
            numerator += (s.cohesion * width + max(0.0, s.weight + s.surcharge_vertical - ub) * tan_phi) / m_alpha
            denominator += (s.weight + s.surcharge_vertical) * math.tan(a) + s.seismic_horizontal + s.surcharge_horizontal
        if denominator <= EPS:
            return math.nan, False, iteration, None, warnings + ["Mẫu số Janbu không dương."]
        new_fs = numerator / denominator
        if not math.isfinite(new_fs) or new_fs <= 0:
            return math.nan, False, iteration, None, warnings + ["Fs không hữu hạn."]
        if abs(new_fs - fs) <= tolerance:
            return new_fs, True, iteration, None, warnings
        fs = 0.65 * new_fs + 0.35 * fs
    return fs, False, max_iterations, None, warnings + ["Chưa hội tụ trong số vòng lặp cho phép."]


def _reverse_slices(slices: Sequence[SliceData]) -> list[SliceData]:
    output = []
    for number, s in enumerate(reversed(slices), 1):
        output.append(replace(s, number=number, base_angle=-s.base_angle))
    return output


def _solve_mp_robust(model: GeoStudioModel, slices: Sequence[SliceData]):
    bishop_fs, bishop_ok, _, _, bishop_warnings = solve_bishop(model, slices)
    initial_fs = bishop_fs if bishop_ok and math.isfinite(bishop_fs) else 1.2
    attempts = [
        (slices, model.analysis.max_iterations, initial_fs, 0.0, ""),
        (slices, max(250, model.analysis.max_iterations), initial_fs, 0.15, "Lặp tăng cường"),
        (slices, max(250, model.analysis.max_iterations), initial_fs, -0.15, "Lặp tăng cường"),
        (_reverse_slices(slices), max(250, model.analysis.max_iterations), initial_fs, 0.0, "Tự động đảo hướng trượt"),
    ]
    best = None
    attempt_notes: list[str] = []
    for attempt_slices, max_iterations, fs0, lambda0, note in attempts:
        result = solve_morgenstern_price(
            attempt_slices,
            function=model.analysis.side_function,
            tolerance=model.analysis.tolerance,
            max_iterations=max_iterations,
            initial_fs=fs0,
            initial_lambda=lambda0,
        )
        if best is None or (math.isfinite(result.fs) and not math.isfinite(best.fs)):
            best = result
        if result.converged and math.isfinite(result.fs) and result.fs > 0:
            warnings = list(result.warnings)
            if note:
                warnings.append(note + ".")
            warnings.extend(bishop_warnings)
            return result.fs, True, result.iterations, result.lambda_value, warnings
        attempt_notes.extend(result.warnings)
    assert best is not None
    warnings = list(dict.fromkeys(attempt_notes + bishop_warnings))
    return best.fs, False, best.iterations, best.lambda_value, warnings or ["Morgenstern–Price không hội tụ."]


def solve_surface(
    model: GeoStudioModel,
    geometry: SurfaceGeometry,
    number: int = 1,
    *,
    context: Optional[SolverContext] = None,
    prebuilt_slices: Optional[Sequence[SliceData]] = None,
    build_report: Optional[SliceBuildReport] = None,
) -> SlipSurface:
    context = context or _make_context(model)
    if prebuilt_slices is None:
        build_report = build_report or build_slices_report(model, geometry.path, context=context)
        slices = build_report.slices
    else:
        slices = list(prebuilt_slices)
    if not slices:
        reason = build_report.reason if build_report else "Mặt trượt không tạo được lát hợp lệ."
        code = build_report.code if build_report else "SLICE_BUILD_FAILED"
        return SlipSurface(
            number=number,
            fs=math.nan,
            path=list(geometry.path),
            center_x=geometry.center_x,
            center_y=geometry.center_y,
            radius=geometry.radius,
            converged=False,
            method=model.analysis.method,
            warnings=[f"{code}: {reason}"],
        )
    method = model.analysis.method.lower()
    if "morgenstern" in method or "price" in method:
        fs, converged, iterations, lambda_value, warnings = _solve_mp_robust(model, slices)
    elif "bishop" in method:
        fs, converged, iterations, lambda_value, warnings = solve_bishop(model, slices)
    elif "janbu" in method:
        fs, converged, iterations, lambda_value, warnings = solve_janbu(model, slices)
    else:
        fs, converged, iterations, lambda_value, warnings = solve_fellenius(model, slices)
    volume = sum(
        (s.x_right - s.x_left) * ((s.y_top_left + s.y_top_right - s.y_bot_left - s.y_bot_right) / 2)
        for s in slices
    )
    weight = sum(s.weight for s in slices)
    return SlipSurface(
        number=number,
        fs=float(fs),
        center_x=geometry.center_x,
        center_y=geometry.center_y,
        radius=geometry.radius,
        volume=volume,
        weight=weight,
        path=list(geometry.path),
        slices_data=slices,
        method=model.analysis.method,
        converged=bool(converged),
        iterations=iterations,
        lambda_value=lambda_value,
        warnings=list(warnings),
        source="IMSAT",
    )


def circle_from_chord_sag(p1: Point, p2: Point, sag: float) -> Optional[tuple[float, float, float]]:
    x1, y1 = p1
    x2, y2 = p2
    dx = x2 - x1
    dy = y2 - y1
    chord = math.hypot(dx, dy)
    if chord <= EPS or sag <= EPS:
        return None
    radius = chord * chord / (8 * sag) + sag / 2
    if radius <= chord / 2:
        return None
    mx, my = (x1 + x2) / 2, (y1 + y2) / 2
    nx, ny = -dy / chord, dx / chord
    if ny < 0:
        nx, ny = -nx, -ny
    offset = radius - sag
    return mx + nx * offset, my + ny * offset, radius


def circle_surface(terrain: Sequence[Point], entry_x: float, exit_x: float, sag: float, points: int = 161) -> Optional[SurfaceGeometry]:
    left_x, right_x = sorted((float(entry_x), float(exit_x)))
    y1 = interp_y(terrain, left_x, upper=True)
    y2 = interp_y(terrain, right_x, upper=True)
    if y1 is None or y2 is None or right_x - left_x <= EPS:
        return None
    circle = circle_from_chord_sag((left_x, y1), (right_x, y2), sag)
    if circle is None:
        return None
    cx, cy, radius = circle
    path = []
    for i in range(points):
        x = left_x + (right_x - left_x) * i / (points - 1)
        radicand = radius * radius - (x - cx) ** 2
        if radicand < -EPS:
            return None
        y = cy - math.sqrt(max(0.0, radicand))
        top = interp_y(terrain, x, upper=True)
        if top is None or y > top + 1e-6:
            return None
        path.append((x, y))
    return SurfaceGeometry(path, cx, cy, radius)


def circle_surface_from_center(terrain: Sequence[Point], cx: float, cy: float, radius: float, points: int = 181) -> Optional[SurfaceGeometry]:
    if radius <= EPS or len(terrain) < 2:
        return None
    terrain_line = LineString(_x_monotonic(terrain, upper=True))
    boundary = ShapelyPoint(cx, cy).buffer(radius, resolution=256).boundary
    intersection = terrain_line.intersection(boundary)
    hits = []
    if intersection.is_empty:
        return None
    if intersection.geom_type == "Point":
        hits = [(intersection.x, intersection.y)]
    elif intersection.geom_type == "MultiPoint":
        hits = [(g.x, g.y) for g in intersection.geoms]
    elif hasattr(intersection, "geoms"):
        for geometry in intersection.geoms:
            if geometry.geom_type == "Point":
                hits.append((geometry.x, geometry.y))
            elif geometry.geom_type == "LineString":
                hits.extend([tuple(geometry.coords[0]), tuple(geometry.coords[-1])])
    unique = []
    for hit in sorted(hits):
        if not unique or math.hypot(hit[0] - unique[-1][0], hit[1] - unique[-1][1]) > 1e-5:
            unique.append(hit)
    if len(unique) < 2:
        return None
    left, right = unique[0], unique[-1]
    if right[0] - left[0] <= EPS:
        return None
    path = []
    for index in range(points):
        x = left[0] + (right[0] - left[0]) * index / (points - 1)
        value = radius * radius - (x - cx) ** 2
        if value < -EPS:
            return None
        y = cy - math.sqrt(max(0.0, value))
        top = interp_y(terrain, x, upper=True)
        if top is None or y > top + 1e-5:
            return None
        path.append((x, y))
    return SurfaceGeometry(path, cx, cy, radius)


def _search_ranges(model: GeoStudioModel, *, fallback: bool = False):
    terrain = _x_monotonic(model.terrain or upper_envelope(model.regions), upper=True)
    xmin, xmax = terrain[0][0], terrain[-1][0]
    span = max(EPS, xmax - xmin)
    if fallback:
        return xmin + 0.02 * span, xmin + 0.48 * span, xmin + 0.52 * span, xmax - 0.02 * span
    search = model.search
    entry_min, entry_max = sorted((search.entry_x_min, search.entry_x_max))
    exit_min, exit_max = sorted((search.exit_x_min, search.exit_x_max))
    if entry_max <= entry_min:
        if model.search_range.entry_left and model.search_range.entry_right:
            entry_min, entry_max = sorted((model.search_range.entry_left[0], model.search_range.entry_right[0]))
        else:
            entry_min, entry_max = xmin + 0.03 * span, xmin + 0.38 * span
    if exit_max <= exit_min:
        if model.search_range.exit_left and model.search_range.exit_right:
            exit_min, exit_max = sorted((model.search_range.exit_left[0], model.search_range.exit_right[0]))
        else:
            exit_min, exit_max = xmin + 0.58 * span, xmin + 0.97 * span
    entry_min = min(max(entry_min, xmin), xmax)
    entry_max = min(max(entry_max, xmin), xmax)
    exit_min = min(max(exit_min, xmin), xmax)
    exit_max = min(max(exit_max, xmin), xmax)
    return entry_min, entry_max, exit_min, exit_max


def _linspace(start: float, end: float, count: int) -> list[float]:
    count = max(2, int(count))
    if abs(end - start) <= EPS:
        return [start]
    return [start + (end - start) * i / (count - 1) for i in range(count)]


def _adaptive_depths(model: GeoStudioModel, p1: Point, p2: Point, context: Optional[SolverContext], *, expand: bool = False) -> list[float]:
    search = model.search
    requested = _linspace(search.min_depth, search.max_depth, max(3, min(30, search.depth_steps)))
    if not expand:
        # Primary search honours exactly the user's declared depth interval.
        return sorted({round(float(v), 8) for v in requested if v > 1e-5})
    chord = math.hypot(p2[0] - p1[0], p2[1] - p1[1])
    relief = abs(p2[1] - p1[1])
    adaptive = [chord * ratio for ratio in (0.01, 0.02, 0.035, 0.05, 0.075, 0.10, 0.14, 0.19, 0.25, 0.33, 0.42)]
    adaptive += [max(1.0, relief * ratio) for ratio in (0.15, 0.3, 0.5, 0.75, 1.0)]
    if context is not None:
        max_top = max(p1[1], p2[1])
        available = max(1.0, max_top - context.ymin)
        adaptive += [available * ratio for ratio in (0.15, 0.3, 0.5, 0.7, 0.9)]
        cap = max(chord * 0.6, available * 1.1, search.max_depth)
    else:
        cap = max(chord * 0.6, search.max_depth)
    values = sorted({round(float(v), 8) for v in requested + adaptive if v > 1e-5 and v <= cap})
    return values[:24]


def generate_candidate_surfaces(model: GeoStudioModel, *, fallback: bool = False) -> Iterable[SurfaceGeometry]:
    terrain = _x_monotonic(model.terrain or upper_envelope(model.regions), upper=True)
    if len(terrain) < 2:
        return []
    option = model.search.option.lower()
    if option.startswith("fully") and len(model.search.fully_specified_points) >= 2:
        return [SurfaceGeometry(_x_monotonic(model.search.fully_specified_points, upper=False))]
    search = model.search
    if "grid" in option and search.grid_x_max > search.grid_x_min and search.grid_y_max > search.grid_y_min and search.radius_max > search.radius_min:
        nx = max(2, min(40, search.grid_x_steps))
        ny = max(2, min(40, search.grid_y_steps))
        nr = max(2, min(60, search.radius_steps))
        centers_x = _linspace(search.grid_x_min, search.grid_x_max, nx)
        centers_y = _linspace(search.grid_y_min, search.grid_y_max, ny)
        radii = _linspace(search.radius_min, search.radius_max, nr)
        output = []
        for cx in centers_x:
            for cy in centers_y:
                for radius in radii:
                    surface = circle_surface_from_center(terrain, cx, cy, radius)
                    if surface is not None:
                        output.append(surface)
        return output

    entry_min, entry_max, exit_min, exit_max = _search_ranges(model, fallback=fallback)
    ne = max(2, min(18, search.entry_steps))
    nx = max(2, min(18, search.exit_steps))
    entries = _linspace(entry_min, entry_max, ne)
    exits = _linspace(exit_min, exit_max, nx)
    # Include boundaries and midpoints even when the requested step count is small.
    entries = sorted(set(entries + [entry_min, (entry_min + entry_max) / 2, entry_max]))
    exits = sorted(set(exits + [exit_min, (exit_min + exit_max) / 2, exit_max]))
    context = _make_context(model)
    candidates = []
    keys = set()
    max_candidates = 14000
    for entry_x in entries:
        for exit_x in exits:
            left_x, right_x = sorted((entry_x, exit_x))
            if right_x - left_x <= max(EPS, (terrain[-1][0] - terrain[0][0]) * 1e-5):
                continue
            y_left = interp_y(terrain, left_x, upper=True)
            y_right = interp_y(terrain, right_x, upper=True)
            if y_left is None or y_right is None:
                continue
            for sag in _adaptive_depths(model, (left_x, y_left), (right_x, y_right), context, expand=fallback):
                geometry = circle_surface(terrain, left_x, right_x, sag)
                if geometry is None:
                    continue
                key = (round(left_x, 5), round(right_x, 5), round(geometry.radius, 5), round(geometry.center_y, 5))
                if key in keys:
                    continue
                keys.add(key)
                candidates.append(geometry)
                if len(candidates) >= max_candidates:
                    return candidates
    if "auto" in option or "cuckoo" in option:
        rng = random.Random(search.random_seed)
        for _ in range(max(0, min(search.random_trials, 4000))):
            entry_x = rng.uniform(entry_min, entry_max)
            exit_x = rng.uniform(exit_min, exit_max)
            left_x, right_x = sorted((entry_x, exit_x))
            if right_x - left_x <= EPS:
                continue
            y_left = interp_y(terrain, left_x, upper=True)
            y_right = interp_y(terrain, right_x, upper=True)
            if y_left is None or y_right is None:
                continue
            depths = _adaptive_depths(model, (left_x, y_left), (right_x, y_right), context, expand=fallback)
            if not depths:
                continue
            sag = rng.choice(depths)
            geometry = circle_surface(terrain, left_x, right_x, sag)
            if geometry is not None:
                candidates.append(geometry)
                if len(candidates) >= max_candidates:
                    break
    return candidates


def _sample_candidates(candidates: Sequence[SurfaceGeometry], limit: int) -> list[SurfaceGeometry]:
    if len(candidates) <= limit:
        return list(candidates)
    step = len(candidates) / limit
    return [candidates[min(len(candidates) - 1, int(i * step))] for i in range(limit)]


def preflight_analysis(model: GeoStudioModel, max_candidates: int = 240) -> dict:
    context = _make_context(model)
    if context is None:
        return {"status": "ERROR", "generated": 0, "slice_valid": 0, "reasons": {"MODEL_DOMAIN_MISSING": 1}}
    candidates = list(generate_candidate_surfaces(model))
    sampled = _sample_candidates(candidates, max_candidates)
    reasons: Counter[str] = Counter()
    valid = 0
    for geometry in sampled:
        report = build_slices_report(model, geometry.path, context=context)
        if report.valid:
            valid += 1
        else:
            reasons[report.code] += 1
    fallback_valid = 0
    if valid == 0:
        fallback_candidates = _sample_candidates(list(generate_candidate_surfaces(model, fallback=True)), max_candidates)
        for geometry in fallback_candidates:
            report = build_slices_report(model, geometry.path, context=context)
            if report.valid:
                fallback_valid += 1
            else:
                reasons[f"FALLBACK_{report.code}"] += 1
    return {
        "status": "PASS" if valid > 0 or fallback_valid > 0 else "ERROR",
        "generated": len(candidates),
        "sampled": len(sampled),
        "slice_valid": valid,
        "fallback_slice_valid": fallback_valid,
        "reasons": dict(reasons),
    }


def analyze_model(model: GeoStudioModel, progress=None, cancel=None) -> list[SlipSurface]:
    context = _make_context(model)
    diagnostics = {
        "method": model.analysis.method,
        "primary_candidates": 0,
        "fallback_candidates": 0,
        "slice_valid": 0,
        "method_evaluated": 0,
        "method_converged": 0,
        "fallback_used": False,
        "rejected_reasons": {},
    }
    if context is None:
        diagnostics["rejected_reasons"] = {"MODEL_DOMAIN_MISSING": 1}
        model.analysis_diagnostics = diagnostics
        model.results = []
        model.critical = None
        return []

    is_mp = "morgenstern" in model.analysis.method.lower() or "price" in model.analysis.method.lower()
    results: list[SlipSurface] = []
    reasons: Counter[str] = Counter()
    seen = set()
    processed = 0

    def process_group(candidates: list[SurfaceGeometry], *, fallback_group: bool = False) -> None:
        nonlocal processed
        valid_records: list[tuple[float, SurfaceGeometry, SliceBuildReport, int]] = []
        total = max(1, len(candidates))
        for group_index, geometry in enumerate(candidates, 1):
            if cancel and cancel():
                return
            key = (
                round(geometry.path[0][0], 5) if geometry.path else 0,
                round(geometry.path[-1][0], 5) if geometry.path else 0,
                round(geometry.radius, 5),
                round(geometry.center_y, 5),
            )
            if key in seen:
                continue
            seen.add(key)
            processed += 1
            report = build_slices_report(model, geometry.path, context=context)
            if not report.valid:
                reasons[report.code] += 1
                if progress and (group_index == 1 or group_index == total or group_index % max(1, total // 100) == 0):
                    progress(group_index, total, SlipSurface(number=processed, fs=math.nan, converged=False, warnings=[report.code]))
                continue
            diagnostics["slice_valid"] += 1
            if is_mp:
                bishop_fs, bishop_ok, _, _, bishop_warnings = solve_bishop(model, report.slices)
                if bishop_ok and math.isfinite(bishop_fs) and bishop_fs > 0:
                    valid_records.append((bishop_fs, geometry, report, processed))
                else:
                    # Keep the surface with a neutral ranking; MP may still solve it
                    # for geometries where simplified Bishop is direction-sensitive.
                    valid_records.append((math.inf, geometry, report, processed))
                    for warning in bishop_warnings:
                        reasons[f"BISHOP_PRESCREEN: {warning}"] += 1
            else:
                result = solve_surface(model, geometry, processed, context=context, prebuilt_slices=report.slices, build_report=report)
                diagnostics["method_evaluated"] += 1
                if result.converged and math.isfinite(result.fs) and result.fs > 0:
                    results.append(result)
                    diagnostics["method_converged"] += 1
                else:
                    reason = result.warnings[0] if result.warnings else "METHOD_NOT_CONVERGED"
                    reasons[reason] += 1
                if progress and (group_index == 1 or group_index == total or group_index % max(1, total // 100) == 0):
                    progress(group_index, total, result)

        if not is_mp or not valid_records or (cancel and cancel()):
            return

        valid_records.sort(key=lambda item: item[0])
        finite_scores = [item[0] for item in valid_records if math.isfinite(item[0])]
        minimum = finite_scores[0] if finite_scores else math.inf
        if len(valid_records) <= 600:
            selected = valid_records
        else:
            # Bishop is used only as a geometric/ranking pre-screen.  All
            # candidates within 35% of its minimum plus the best 500 are kept.
            threshold = minimum * 1.35 if math.isfinite(minimum) else math.inf
            selected = [item for item in valid_records if item[0] <= threshold]
            target = max(180, min(500, model.search.critical_surfaces_to_store * 20))
            selected = (selected + valid_records[:target])
            unique = []
            selected_keys = set()
            for item in selected:
                geometry = item[1]
                key = (round(geometry.center_x, 5), round(geometry.center_y, 5), round(geometry.radius, 5))
                if key not in selected_keys:
                    selected_keys.add(key)
                    unique.append(item)
                if len(unique) >= 600:
                    break
            selected = unique

        mp_total = max(1, len(selected))
        for mp_index, (_, geometry, report, number) in enumerate(selected, 1):
            if cancel and cancel():
                return
            result = solve_surface(model, geometry, number, context=context, prebuilt_slices=report.slices, build_report=report)
            diagnostics["method_evaluated"] += 1
            if result.converged and math.isfinite(result.fs) and result.fs > 0:
                results.append(result)
                diagnostics["method_converged"] += 1
            else:
                reason = result.warnings[0] if result.warnings else "METHOD_NOT_CONVERGED"
                reasons[reason] += 1
            if progress and (mp_index == 1 or mp_index == mp_total or mp_index % max(1, mp_total // 100) == 0):
                progress(mp_index, mp_total, result)

    primary = list(generate_candidate_surfaces(model))
    diagnostics["primary_candidates"] = len(primary)
    process_group(primary)

    # Adaptive rescue: expand endpoint zones and depths when the user ranges
    # produce no converged result.  The project values are not overwritten.
    if not results and not (cancel and cancel()):
        fallback = list(generate_candidate_surfaces(model, fallback=True))
        diagnostics["fallback_candidates"] = len(fallback)
        diagnostics["fallback_used"] = True
        process_group(fallback, fallback_group=True)

    results.sort(key=lambda result: result.fs)
    limit = max(1, model.search.critical_surfaces_to_store)
    model.results = results[:limit]
    model.critical = model.results[0] if model.results else None
    diagnostics["rejected_reasons"] = dict(reasons.most_common(20))
    diagnostics["stored_results"] = len(model.results)
    diagnostics["critical_fs"] = model.critical.fs if model.critical else None
    model.analysis_diagnostics = diagnostics
    return model.results
