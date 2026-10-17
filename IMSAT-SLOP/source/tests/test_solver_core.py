from __future__ import annotations

import json
import math
from pathlib import Path

from app.algorithms.solver import SurfaceGeometry, analyze_model, solve_surface
from app.io.gsz_reader import read_gsz

ROOT = Path(__file__).resolve().parents[1]


def main():
    report = {"status": "PASS", "methods": []}
    for method in ["Fellenius", "Bishop Simplified", "Janbu Simplified", "Morgenstern-Price"]:
        model = read_gsz(ROOT / "examples" / "MC D1-D2.gsz")
        model.analysis.method = method
        model.analysis.slices = 30
        model.search.entry_steps = 4
        model.search.exit_steps = 4
        model.search.depth_steps = 8
        model.search.random_trials = 0
        model.search.critical_surfaces_to_store = 5
        results = analyze_model(model)
        assert results, method
        critical = results[0]
        assert critical.converged and math.isfinite(critical.fs) and critical.fs > 0
        assert critical.slices_data
        report["methods"].append({"method": method, "results": len(results), "critical_fs": critical.fs, "slices": len(critical.slices_data), "iterations": critical.iterations, "lambda": critical.lambda_value})

    # The critical GeoStudio path can be rebuilt from Geometry for a simple two-Region model.
    model = read_gsz(ROOT / "examples" / "MC D1-D2.gsz")
    model.analysis.method = "Morgenstern-Price"
    model.analysis.slices = len(model.reference_critical.slices_data)
    geometry = SurfaceGeometry(model.reference_critical.path, model.reference_critical.center_x, model.reference_critical.center_y, model.reference_critical.radius)
    result = solve_surface(model, geometry)
    assert result.converged and math.isfinite(result.fs)
    report["reference_path"] = {"reference_fs": model.reference_critical.fs, "rebuilt_fs": result.fs, "difference_percent": abs(result.fs / model.reference_critical.fs - 1) * 100}
    assert report["reference_path"]["difference_percent"] < 0.5

    output = ROOT / "tests" / "V6_SOLVER_VALIDATION.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
