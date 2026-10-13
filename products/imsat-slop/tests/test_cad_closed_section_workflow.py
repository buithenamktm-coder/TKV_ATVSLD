from __future__ import annotations

import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from app.algorithms.solver import analyze_model, preflight_analysis
from app.core.geometry import model_bounds, validate_model
from app.core.models import GeoStudioModel
from app.io.cad_model_import import COMBINED_ROLE, cad_to_model
from app.io.cad_reader import CadEntity, CadModel

def closed_open_pit_cad() -> CadModel:
    # One closed section polyline, matching the common KTOD/KTDO CAD workflow.
    ring = [
        (0, 445), (40, 430), (90, 415), (140, 405), (190, 400), (240, 408),
        (280, 392), (320, 375), (360, 355), (400, 338), (440, 340), (480, 355),
        (530, 362), (580, 378), (620, 410), (650, 430), (690, 425), (720, 405),
        (760, 395), (800, 392), (840, 435), (900, 410), (940, 395), (1000, 395),
        (1040, 375), (1090, 375), (1130, 350), (1180, 350), (1220, 325),
        (1280, 325), (1320, 295), (1380, 295), (1420, 255), (1490, 255),
        (1530, 220), (1600, 220), (1640, 195), (1680, 175), (1720, 170),
        (1720, 0), (0, 0),
    ]
    return CadModel(
        source_file="KTOD.dxf",
        working_dxf="KTOD.dxf",
        entities=[CadEntity("LWPOLYLINE", "0", ring, closed=True)],
        layers=["0"],
        extents=(0, 0, 1720, 445),
    )


def configure(model: GeoStudioModel, method: str) -> None:
    model.analysis.method = method
    model.analysis.slices = 40
    model.search.entry_steps = 4
    model.search.exit_steps = 4
    model.search.depth_steps = 8
    model.search.random_trials = 0
    model.search.critical_surfaces_to_store = 10


def main() -> None:
    report = {"status": "PASS", "cases": []}
    for role in ("Chưa gán", "Địa hình", COMBINED_ROLE, "Region kín"):
        model = cad_to_model(closed_open_pit_cad(), {"0": role})
        assert len(model.regions) == 1, (role, len(model.regions))
        assert model.regions[0].material_source_id == 1
        assert len(model.terrain) >= 2
        assert not [issue for issue in validate_model(model) if issue.severity == "error"]
        bounds = model_bounds(model)
        assert bounds[2] - bounds[0] > 1600
        assert bounds[3] - bounds[1] > 400
        for method in ("Bishop Simplified", "Morgenstern-Price"):
            configure(model, method)
            preflight = preflight_analysis(model, max_candidates=120)
            assert preflight["status"] == "PASS", (role, method, preflight)
            results = analyze_model(model)
            assert results, (role, method, model.analysis_diagnostics)
            assert model.critical and model.critical.converged
            assert math.isfinite(model.critical.fs) and model.critical.fs > 0
            report["cases"].append({
                "mapping_role": role,
                "method": method,
                "regions": len(model.regions),
                "terrain_points": len(model.terrain),
                "critical_fs": model.critical.fs,
                "results": len(results),
                "diagnostics": model.analysis_diagnostics,
            })

    # Display-only CAD linework must use its real coordinates rather than 0..100.
    display_only = GeoStudioModel(reference_geometry=[[(300, 200), (1700, 450)]])
    bounds = model_bounds(display_only)
    assert bounds[0] < 300 and bounds[2] > 1700
    report["reference_bounds"] = list(bounds)

    output = ROOT / "tests" / "V6_2_CAD_CLOSED_SECTION_VALIDATION.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
