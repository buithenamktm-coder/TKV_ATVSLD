from __future__ import annotations

from pathlib import Path
from typing import Mapping
import math

from shapely.geometry import LineString, Polygon
from shapely.ops import polygonize, unary_union

from app.core.geometry import clean_ring, upper_envelope
from app.core.models import GeoRegion, GeoStudioModel, Material
from app.io.cad_reader import CadModel, polyline_length


COMBINED_ROLE = "Mặt cắt kín (Region + địa hình)"


def _effectively_closed(entity, cad: CadModel) -> bool:
    if entity.closed:
        return True
    if len(entity.points) < 3:
        return False
    if cad.extents:
        xmin, ymin, xmax, ymax = cad.extents
        tolerance = max(1e-6, math.hypot(xmax - xmin, ymax - ymin) * 1e-5)
    else:
        tolerance = 1e-5
    a, b = entity.points[0], entity.points[-1]
    return math.hypot(float(a[0]) - float(b[0]), float(a[1]) - float(b[1])) <= tolerance


def _valid_ring(points):
    ring = clean_ring(points)
    if len(ring) < 3:
        return []
    polygon = Polygon(ring)
    if not polygon.is_valid:
        polygon = polygon.buffer(0)
    if polygon.is_empty or polygon.area <= 1e-8 or polygon.geom_type != "Polygon":
        return []
    return [(float(x), float(y)) for x, y in list(polygon.exterior.coords)[:-1]]


def _unique_rings(rings):
    unique = []
    signatures = set()
    for ring in rings:
        polygon = Polygon(ring)
        signature = (
            round(float(polygon.area), 6),
            round(float(polygon.centroid.x), 6),
            round(float(polygon.centroid.y), 6),
        )
        if signature not in signatures:
            signatures.add(signature)
            unique.append(ring)
    return unique


def cad_to_model(cad: CadModel, mapping: Mapping[str, str]) -> GeoStudioModel:
    """Convert mapped CAD linework into a calculable model.

    The common mine-section export is one closed LWPOLYLINE containing both the
    ground surface and the lower model boundary.  Earlier releases treated that
    object only as a display line when its layer was mapped as Terrain or left
    unassigned, which produced ``Geometry (0 Region)`` and made calculation
    impossible.  V6.2 recognizes the closed section as a Region, derives the
    upper terrain envelope, and assigns the initial material explicitly.
    """
    model = GeoStudioModel(source_file=cad.source_file, name=Path(cad.source_file).stem)
    model.materials = [Material(name="Vật liệu 1", source_id=1, color="#F5E66F")]
    terrain_lines = []
    region_rings = []
    fallback_closed_rings = []
    boundary_lines = []
    fallback_lines = []
    water_lines = []
    reference = []
    all_points = []

    for layer in cad.layers:
        role = mapping.get(layer, "Chưa gán")
        entities = [entity for entity in cad.entities if entity.layer == layer and len(entity.points) >= 2]
        all_points.extend(point for entity in entities for point in entity.points)

        # Closed polygons are retained as a safe fallback unless the user has
        # explicitly excluded the layer or declared it as water.
        if role not in {"Không nhập", "Mực nước"}:
            for entity in entities:
                if _effectively_closed(entity, cad):
                    ring = _valid_ring(entity.points)
                    if ring:
                        fallback_closed_rings.append(ring)
            fallback_lines.extend([list(entity.points) for entity in entities])

        if role in {"Chưa gán", "Ranh giới địa chất"}:
            reference.extend([list(entity.points) for entity in entities])
        if role == "Địa hình":
            terrain_lines.extend(cad.stitched_polylines(layer))
            # A closed terrain section is also the material domain.
            for entity in entities:
                if _effectively_closed(entity, cad):
                    ring = _valid_ring(entity.points)
                    if ring:
                        region_rings.append(ring)
        elif role in {"Region kín", COMBINED_ROLE}:
            for entity in entities:
                if _effectively_closed(entity, cad):
                    ring = _valid_ring(entity.points)
                    if ring:
                        region_rings.append(ring)
                else:
                    boundary_lines.append(list(entity.points))
            if role == COMBINED_ROLE:
                terrain_lines.extend(cad.stitched_polylines(layer))
        elif role == "Ranh giới địa chất":
            boundary_lines.extend([list(entity.points) for entity in entities])
        elif role == "Mực nước":
            water_lines.extend(cad.stitched_polylines(layer))

    # Polygonize explicitly mapped Region/geological linework when no closed
    # Region exists.
    if not region_rings and boundary_lines:
        lines = [LineString(points) for points in boundary_lines if len(points) >= 2]
        if lines:
            for polygon in polygonize(unary_union(lines)):
                ring = _valid_ring(list(polygon.exterior.coords))
                if ring:
                    region_rings.append(ring)

    # Last-resort CAD recovery: use valid closed entities from unassigned or
    # terrain layers.  This is the exact path needed for single-polyline mine
    # sections such as KTOD/KTDO.
    if not region_rings:
        region_rings.extend(fallback_closed_rings)

    # If the drawing has no closed entity, try polygonizing all non-excluded
    # structural linework.  Only valid non-zero polygons are accepted.
    if not region_rings and fallback_lines:
        lines = [LineString(points) for points in fallback_lines if len(points) >= 2]
        try:
            merged = unary_union(lines)
            for polygon in polygonize(merged):
                ring = _valid_ring(list(polygon.exterior.coords))
                if ring:
                    region_rings.append(ring)
        except Exception:
            pass

    unique = _unique_rings(region_rings)
    model.regions = [
        GeoRegion(
            source_id=index,
            polygon=ring,
            material_source_id=1,  # Explicit temporary assignment; user may change it.
            name=f"Region {index}",
            source_type="CAD",
            source_name=Path(cad.source_file).name,
        )
        for index, ring in enumerate(unique, 1)
    ]

    # Prefer the upper envelope of actual Regions.  A stitched closed polyline
    # contains both top and bottom paths; sorting that entire path by X can
    # otherwise create a misleading terrain.
    if model.regions:
        model.terrain = upper_envelope(model.regions)
    elif terrain_lines:
        terrain = max(terrain_lines, key=polyline_length)
        if terrain and terrain[0][0] > terrain[-1][0]:
            terrain = list(reversed(terrain))
        upper = {}
        for x, y in terrain:
            upper[float(x)] = max(float(y), upper.get(float(x), float("-inf")))
        model.terrain = sorted(upper.items())

    # Do not redraw a closed ring as dashed reference geometry once it has
    # become a Region.  Keep only open contextual/geological linework.
    used_signatures = {
        (round(Polygon(region.polygon).area, 6), round(Polygon(region.polygon).centroid.x, 6), round(Polygon(region.polygon).centroid.y, 6))
        for region in model.regions
    }
    filtered_reference = []
    for line in reference + boundary_lines:
        ring = _valid_ring(line)
        if ring:
            poly = Polygon(ring)
            sig = (round(poly.area, 6), round(poly.centroid.x, 6), round(poly.centroid.y, 6))
            if sig in used_signatures:
                continue
        filtered_reference.append(line)
    model.reference_geometry = filtered_reference
    model.points = [(float(x), float(y)) for x, y in all_points]

    if water_lines:
        line = max(water_lines, key=polyline_length)
        if line[0][0] > line[-1][0]:
            line = list(reversed(line))
        model.water.enabled = True
        model.water.phreatic_line = [(float(x), float(y)) for x, y in line]

    # View bounds must follow calculable geometry, not remote CAD annotations.
    active_points = [point for region in model.regions for point in region.polygon]
    active_points += list(model.terrain)
    active_points += [point for line in model.reference_geometry for point in line]
    if active_points:
        xs = [p[0] for p in active_points]
        ys = [p[1] for p in active_points]
        model.view_bounds = [float(min(xs)), float(min(ys)), float(max(xs)), float(max(ys))]
    elif cad.extents:
        model.view_bounds = [float(v) for v in cad.extents]

    if model.regions:
        model.validation_notes.append(
            f"CAD: đã tạo {len(model.regions)} Region và gán tạm Vật liệu 1 để mô hình có thể kiểm tra/tính toán."
        )
    else:
        model.validation_notes.append("CAD: chưa nhận diện được Region kín; cần khép kín đường bao hoặc ánh xạ đúng layer.")
    return model
