from __future__ import annotations

from dataclasses import asdict, dataclass, field, fields
from pathlib import Path
from typing import Any, Optional, Tuple
import json

Point = Tuple[float, float]
Polyline = list[Point]


def _only(cls, data: dict[str, Any] | None) -> dict[str, Any]:
    allowed = {f.name for f in fields(cls)}
    return {k: v for k, v in (data or {}).items() if k in allowed}


@dataclass
class Material:
    name: str = "Vật liệu 1"
    unit_weight: float = 20.0
    saturated_unit_weight: float = 21.0
    cohesion: float = 10.0
    phi: float = 28.0
    source_id: int = 1
    slope_model: str = "Mohr-Coulomb"
    color: str = "#B4B4B4"


@dataclass
class GeoRegion:
    source_id: int
    polygon: Polyline = field(default_factory=list)
    material_source_id: Optional[int] = None
    name: str = "Region"
    priority: int = 0
    source_type: str = "Manual"
    source_name: str = ""


@dataclass
class SearchRange:
    entry_left: Optional[Point] = None
    entry_right: Optional[Point] = None
    exit_left: Optional[Point] = None
    exit_right: Optional[Point] = None


@dataclass
class WaterDefinition:
    enabled: bool = False
    level: float = 0.0
    phreatic_line: Polyline = field(default_factory=list)
    gamma_water: float = 9.807


@dataclass
class DistributedLoad:
    load_id: str = "LOAD-01"
    enabled: bool = False
    x_start: float = 0.0
    x_end: float = 0.0
    magnitude: float = 0.0
    angle_from_vertical_deg: float = 0.0


@dataclass
class SeismicDefinition:
    enabled: bool = False
    kh: float = 0.0
    kv: float = 0.0


@dataclass
class SearchDefinition:
    option: str = "Entry and Exit"
    entry_x_min: float = 0.0
    entry_x_max: float = 0.0
    exit_x_min: float = 0.0
    exit_x_max: float = 0.0
    entry_steps: int = 12
    exit_steps: int = 12
    min_depth: float = 5.0
    max_depth: float = 80.0
    depth_steps: int = 18
    grid_x_min: float = 0.0
    grid_x_max: float = 0.0
    grid_y_min: float = 0.0
    grid_y_max: float = 0.0
    grid_x_steps: int = 10
    grid_y_steps: int = 10
    radius_min: float = 10.0
    radius_max: float = 300.0
    radius_steps: int = 20
    fully_specified_points: Polyline = field(default_factory=list)
    optimize_critical: bool = True
    refinement_rounds: int = 2
    critical_surfaces_to_store: int = 50
    random_seed: int = 20260728
    random_trials: int = 1200


@dataclass
class AnalysisDefinition:
    method: str = "Morgenstern-Price"
    slip_direction: str = "Auto"
    side_function: str = "Half-Sine"
    slices: int = 40
    tolerance: float = 1e-6
    max_iterations: int = 100


@dataclass
class SliceData:
    number: int
    x_left: float
    x_right: float
    y_bot_left: float
    y_bot_right: float
    y_top_left: float
    y_top_right: float
    weight: float
    pore_pressure: float
    base_length: float
    base_angle: float
    cohesion: float = 0.0
    phi: float = 0.0
    material_id: Optional[int] = None
    centroid_x: float = 0.0
    centroid_y: float = 0.0
    interslice_normal_left: float = 0.0
    interslice_normal_right: float = 0.0
    surcharge_vertical: float = 0.0
    surcharge_horizontal: float = 0.0
    seismic_horizontal: float = 0.0


@dataclass
class LambdaPoint:
    value: float
    fs_force: Optional[float]
    fs_moment: Optional[float]


@dataclass
class SlipSurface:
    number: int
    fs: float
    center_x: float = 0.0
    center_y: float = 0.0
    radius: float = 0.0
    volume: float = 0.0
    weight: float = 0.0
    resisting_force: float = 0.0
    activating_force: float = 0.0
    resisting_moment: float = 0.0
    activating_moment: float = 0.0
    path: Polyline = field(default_factory=list)
    slices_data: list[SliceData] = field(default_factory=list)
    method: str = "Morgenstern-Price"
    converged: bool = True
    iterations: int = 0
    lambda_value: Optional[float] = None
    warnings: list[str] = field(default_factory=list)
    source: str = "IMSAT"


@dataclass
class GeoStudioModel:
    source_file: str = ""
    name: str = "Dự án mới"
    analysis_name: str = "SLOPE/W Analysis"
    points: Polyline = field(default_factory=list)
    terrain: Polyline = field(default_factory=list)
    materials: list[Material] = field(default_factory=lambda: [Material()])
    regions: list[GeoRegion] = field(default_factory=list)
    search_range: SearchRange = field(default_factory=SearchRange)
    water: WaterDefinition = field(default_factory=WaterDefinition)
    loads: list[DistributedLoad] = field(default_factory=list)
    seismic: SeismicDefinition = field(default_factory=SeismicDefinition)
    search: SearchDefinition = field(default_factory=SearchDefinition)
    analysis: AnalysisDefinition = field(default_factory=AnalysisDefinition)
    reference_surfaces: list[SlipSurface] = field(default_factory=list)
    results: list[SlipSurface] = field(default_factory=list)
    critical: Optional[SlipSurface] = None
    reference_critical: Optional[SlipSurface] = None
    lambda_curve: list[LambdaPoint] = field(default_factory=list)
    reference_geometry: list[Polyline] = field(default_factory=list)
    view_bounds: list[float] = field(default_factory=list)
    validation_notes: list[str] = field(default_factory=list)
    analysis_diagnostics: dict[str, Any] = field(default_factory=dict)

    @property
    def method(self) -> str:
        return self.analysis.method

    @method.setter
    def method(self, value: str) -> None:
        self.analysis.method = value

    @property
    def slip_direction(self) -> str:
        return self.analysis.slip_direction

    @slip_direction.setter
    def slip_direction(self, value: str) -> None:
        self.analysis.slip_direction = value

    @property
    def surfaces(self) -> list[SlipSurface]:
        return self.reference_surfaces

    @surfaces.setter
    def surfaces(self, value: list[SlipSurface]) -> None:
        self.reference_surfaces = value

    @property
    def slices(self) -> list[SliceData]:
        return self.reference_critical.slices_data if self.reference_critical else []

    @property
    def critical_path(self) -> Polyline:
        return self.reference_critical.path if self.reference_critical else []

    def material_by_id(self, source_id: Optional[int]) -> Optional[Material]:
        return next((m for m in self.materials if m.source_id == source_id), None)

    def region_by_id(self, source_id: int) -> Optional[GeoRegion]:
        return next((r for r in self.regions if r.source_id == source_id), None)

    def save(self, path: str | Path) -> None:
        Path(path).write_text(json.dumps(asdict(self), ensure_ascii=False, indent=2), encoding="utf-8")

    @classmethod
    def load(cls, path: str | Path) -> "GeoStudioModel":
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        model = cls(
            source_file=data.get("source_file", ""),
            name=data.get("name", "Dự án mới"),
            analysis_name=data.get("analysis_name", "SLOPE/W Analysis"),
            points=[tuple(p) for p in data.get("points", [])],
            terrain=[tuple(p) for p in data.get("terrain", [])],
            materials=[Material(**_only(Material, m)) for m in data.get("materials", [])] or [Material()],
            regions=[GeoRegion(
                source_id=int(r.get("source_id", 0)),
                polygon=[tuple(p) for p in r.get("polygon", [])],
                material_source_id=r.get("material_source_id"),
                name=r.get("name", "Region"),
                priority=int(r.get("priority", 0)),
                source_type=r.get("source_type", "Manual"),
                source_name=r.get("source_name", ""),
            ) for r in data.get("regions", [])],
            search_range=SearchRange(**{
                k: (tuple(v) if isinstance(v, list) else v)
                for k, v in _only(SearchRange, data.get("search_range")).items()
            }),
            water=WaterDefinition(
                enabled=data.get("water", {}).get("enabled", False),
                level=float(data.get("water", {}).get("level", 0.0)),
                phreatic_line=[tuple(p) for p in data.get("water", {}).get("phreatic_line", [])],
                gamma_water=float(data.get("water", {}).get("gamma_water", 9.807)),
            ),
            loads=[DistributedLoad(**_only(DistributedLoad, item)) for item in data.get("loads", [])],
            seismic=SeismicDefinition(**_only(SeismicDefinition, data.get("seismic"))),
            search=SearchDefinition(**_only(SearchDefinition, data.get("search"))),
            analysis=AnalysisDefinition(**_only(AnalysisDefinition, data.get("analysis"))),
            reference_geometry=[[tuple(p) for p in line] for line in data.get("reference_geometry", [])],
            view_bounds=[float(v) for v in data.get("view_bounds", [])[:4]],
            validation_notes=list(data.get("validation_notes", [])),
            analysis_diagnostics=dict(data.get("analysis_diagnostics", {})),
        )
        model.search.fully_specified_points = [tuple(p) for p in model.search.fully_specified_points]
        def surface(item: dict[str, Any]) -> SlipSurface:
            return SlipSurface(
                **{
                    **_only(SlipSurface, item),
                    "path": [tuple(p) for p in item.get("path", [])],
                    "slices_data": [SliceData(**_only(SliceData, s)) for s in item.get("slices_data", [])],
                }
            )
        model.reference_surfaces = [surface(s) for s in data.get("reference_surfaces", data.get("surfaces", []))]
        model.results = [surface(s) for s in data.get("results", [])]
        rc = data.get("reference_critical")
        model.reference_critical = surface(rc) if rc else None
        c = data.get("critical")
        model.critical = surface(c) if c else (model.results[0] if model.results else model.reference_critical)
        model.lambda_curve = [LambdaPoint(**_only(LambdaPoint, x)) for x in data.get("lambda_curve", [])]
        return model
