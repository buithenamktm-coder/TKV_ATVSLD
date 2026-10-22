from __future__ import annotations

import json
from copy import deepcopy
from pathlib import Path

from app.algorithms.solver import analyze_model
from app.core.geometry import assign_material, model_bounds
from app.io.cad_model_import import cad_to_model
from app.io.cad_reader import CadEntity, CadModel, stitch_polylines
from app.io.gsz_reader import read_gsz
from app.io.project_io import load_project, save_project
from app.reports import export_html_report, export_results_csv, export_slices_csv

ROOT = Path(__file__).resolve().parents[1]


def main():
    model = read_gsz(ROOT / "examples" / "MC D1-D2.gsz")
    clone = deepcopy(model)
    first, second = clone.regions
    old_second = second.material_source_id
    assign_material(clone, first.source_id, clone.materials[-1].source_id)
    assert first.material_source_id == clone.materials[-1].source_id
    assert second.material_source_id == old_second

    clone.analysis.method = "Morgenstern-Price"
    clone.analysis.slices = 30
    clone.search.entry_steps = 4; clone.search.exit_steps = 4; clone.search.depth_steps = 8; clone.search.random_trials = 0; clone.search.critical_surfaces_to_store = 5
    assert analyze_model(clone)

    temp = ROOT / "tests" / "_roundtrip.issv6"
    save_project(clone, temp)
    restored = load_project(temp)
    assert restored.regions[0].material_source_id == clone.regions[0].material_source_id
    assert restored.regions[1].material_source_id == clone.regions[1].material_source_id
    assert restored.critical and restored.results and restored.critical.fs == clone.critical.fs
    assert model_bounds(restored) == model_bounds(clone)

    html = export_html_report(restored, ROOT / "tests" / "_report.html")
    surfaces = export_results_csv(restored, ROOT / "tests" / "_surfaces.csv")
    slices = export_slices_csv(restored, ROOT / "tests" / "_slices.csv")
    assert "Fs tới hạn" in html.read_text(encoding="utf-8")
    assert surfaces.stat().st_size > 100 and slices.stat().st_size > 100

    # CAD stitching and closed Region import contract.
    chains = stitch_polylines([[(0, 0), (5, 0)], [(5, 0), (10, 0)]], 1e-6)
    assert len(chains) == 1 and len(chains[0]) == 3
    cad = CadModel("sample.dxf", "sample.dxf", [
        CadEntity("LWPOLYLINE", "REGION", [(0, 0), (10, 0), (10, 5), (0, 5)], closed=True),
        CadEntity("LWPOLYLINE", "TERRAIN", [(0, 5), (10, 5)], closed=False),
    ], ["REGION", "TERRAIN"], (0, 0, 10, 5))
    imported = cad_to_model(cad, {"REGION": "Region kín", "TERRAIN": "Địa hình"})
    assert len(imported.regions) == 1 and imported.terrain == [(0.0, 5.0), (10.0, 5.0)]

    report = {"status": "PASS", "roundtrip_fs": restored.critical.fs, "results": len(restored.results), "reports": [str(html.name), str(surfaces.name), str(slices.name)], "cad_regions": len(imported.regions)}
    (ROOT / "tests" / "V6_PROJECT_REPORT_VALIDATION.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    for path in [temp, html, surfaces, slices]: path.unlink(missing_ok=True)


if __name__ == "__main__":
    main()
