from __future__ import annotations

import json
import math
import sys
from pathlib import Path
from tempfile import TemporaryDirectory

from app.algorithms.solver import analyze_model, preflight_analysis
from app.core.models import GeoRegion, GeoStudioModel, Material
from app.core.solver_job import run_solver_job

ROOT = Path(__file__).resolve().parents[1]


def build_large_model() -> GeoStudioModel:
    # Regression geometry based on the large two-layer open-pit section that
    # previously passed Geometry validation but produced no solver result.
    terrain = [
        (0,445),(40,430),(90,415),(140,405),(190,400),(240,408),(280,392),
        (320,375),(360,355),(400,338),(440,340),(480,355),(530,362),(580,378),
        (620,410),(650,430),(690,425),(720,405),(760,395),(800,392),(840,435),
        (900,410),(940,395),(1000,395),(1040,375),(1090,375),(1130,350),
        (1180,350),(1220,325),(1280,325),(1320,295),(1380,295),(1420,255),
        (1490,255),(1530,220),(1600,220),(1640,195),(1680,175),(1720,170),
    ]
    boundary = []
    for index, (x, y) in enumerate(terrain):
        if index in (0, len(terrain)-1):
            by = y
        else:
            trend = 430.0 - 0.158 * x + 10.0 * math.sin(x / 135.0)
            by = min(y - 4.0, trend)
        boundary.append((x, max(155.0, by)))
    lower = boundary + [(1720, 0), (0, 0)]
    upper = terrain + list(reversed(boundary))
    model = GeoStudioModel(
        name="Large two-layer open pit",
        terrain=terrain,
        materials=[
            Material("Vật liệu 1", 20, 21, 10, 28, 1, color="#F4E77D"),
            Material("Vật liệu 2", 15, 16, 15, 30, 2, color="#8F8F20"),
        ],
        regions=[
            GeoRegion(1, lower, 1, "Region 1"),
            GeoRegion(2, upper, 2, "Region 2"),
        ],
    )
    model.search.entry_x_min = 700
    model.search.entry_x_max = 850
    model.search.exit_x_min = 1650
    model.search.exit_x_max = 1710
    model.search.entry_steps = 4
    model.search.exit_steps = 4
    model.search.min_depth = 5
    model.search.max_depth = 80
    model.search.depth_steps = 8
    model.search.random_trials = 0
    model.search.critical_surfaces_to_store = 10
    model.analysis.slices = 40
    model.analysis.tolerance = 1e-6
    model.analysis.max_iterations = 100
    return model


def main() -> None:
    report = {"status": "PASS", "cases": []}
    for method in ("Bishop Simplified", "Morgenstern-Price"):
        model = build_large_model()
        model.analysis.method = method
        preflight = preflight_analysis(model, max_candidates=120)
        assert preflight["status"] == "PASS", preflight
        results = analyze_model(model)
        assert results, (method, model.analysis_diagnostics)
        assert model.critical and model.critical.converged
        assert math.isfinite(model.critical.fs) and model.critical.fs > 0
        assert model.critical.slices_data
        report["cases"].append({
            "method": method,
            "critical_fs": model.critical.fs,
            "results": len(results),
            "slices": len(model.critical.slices_data),
            "diagnostics": model.analysis_diagnostics,
        })

    # Entry and Exit may be drawn in reverse order; the search must still work.
    reversed_model = build_large_model()
    reversed_model.analysis.method = "Morgenstern-Price"
    reversed_model.search.entry_x_min, reversed_model.search.entry_x_max = 1650, 1710
    reversed_model.search.exit_x_min, reversed_model.search.exit_x_max = 700, 850
    reversed_results = analyze_model(reversed_model)
    assert reversed_results and math.isfinite(reversed_results[0].fs)
    report["reversed_entry_exit_fs"] = reversed_results[0].fs


    # Completely unusable endpoint ranges must not leave the user with a blank
    # result: adaptive rescue expands the search over the terrain and records it.
    fallback_model = build_large_model()
    fallback_model.analysis.method = "Morgenstern-Price"
    fallback_model.search.entry_x_min, fallback_model.search.entry_x_max = -200, -100
    fallback_model.search.exit_x_min, fallback_model.search.exit_x_max = -200, -100
    fallback_results = analyze_model(fallback_model)
    assert fallback_results and fallback_model.analysis_diagnostics.get("fallback_used")
    report["adaptive_fallback_fs"] = fallback_results[0].fs

    # Full isolated solver-job contract: the same user workflow must create an
    # output project, not only pass an in-process unit test.
    with TemporaryDirectory() as temp_dir:
        source = Path(temp_dir) / "input.issv6"
        target = Path(temp_dir) / "output.issv6"
        job_model = build_large_model()
        job_model.analysis.method = "Morgenstern-Price"
        job_model.save(source)
        code = run_solver_job(source, target)
        assert code == 0 and target.exists()
        solved = GeoStudioModel.load(target)
        assert solved.critical and math.isfinite(solved.critical.fs)
        report["isolated_solver_fs"] = solved.critical.fs

    # PyInstaller windowed applications may expose sys.stdout as None.  The
    # worker must still finish and write its result project in that mode.
    with TemporaryDirectory() as temp_dir:
        source = Path(temp_dir) / "windowed_input.issv6"
        target = Path(temp_dir) / "windowed_output.issv6"
        job_model = build_large_model()
        job_model.analysis.method = "Morgenstern-Price"
        job_model.save(source)
        original_stdout = sys.stdout
        try:
            sys.stdout = None
            code = run_solver_job(source, target)
        finally:
            sys.stdout = original_stdout
        assert code == 0 and target.exists()
        solved = GeoStudioModel.load(target)
        assert solved.critical and math.isfinite(solved.critical.fs)
        report["windowed_stdout_none_fs"] = solved.critical.fs

    output = ROOT / "tests" / "V6_2_LARGE_OPEN_PIT_VALIDATION.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
