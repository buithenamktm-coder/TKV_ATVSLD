from __future__ import annotations

import os
from pathlib import Path

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PySide6.QtWidgets import QApplication

from app.algorithms.solver import preflight_analysis
from app.io.cad_model_import import cad_to_model
from app.io.cad_reader import CadEntity, CadModel
from app.ui.engineering_canvas import ViewMode
from app.ui.main_window import MainWindow


def _cad_model():
    ring = [
        (0, 445), (90, 415), (190, 400), (280, 392), (400, 338),
        (530, 362), (650, 430), (760, 395), (840, 435), (940, 395),
        (1040, 375), (1130, 350), (1220, 325), (1320, 295),
        (1420, 255), (1530, 220), (1640, 195), (1720, 170),
        (1720, 0), (0, 0),
    ]
    cad = CadModel(
        source_file="KTOD_ui_smoke.dxf",
        working_dxf="KTOD_ui_smoke.dxf",
        entities=[CadEntity("LWPOLYLINE", "0", ring, closed=True)],
        layers=["0"],
        extents=(0, 0, 1720, 445),
    )
    return cad_to_model(cad, {"0": "Chưa gán"})


def main() -> None:
    app = QApplication.instance() or QApplication([])
    root = Path(__file__).resolve().parents[1]
    window = MainWindow()

    # Existing GSZ workflow remains intact.
    window.load_model(root / "examples" / "MC D1-D2.gsz")
    assert window.model is not None and len(window.model.regions) == 2
    before = [list(region.polygon) for region in window.model.regions]
    window.set_mode(ViewMode.ASSIGN)
    window.active_material_id = window.model.materials[-1].source_id
    region_id = window.model.regions[0].source_id
    window._region_clicked(region_id)
    assert window.model.regions[0].material_source_id == window.active_material_id

    window.set_mode(ViewMode.DRAW_ENTRY)
    window._range_changed("entry", 26.5, 52.0)
    window.set_mode(ViewMode.DRAW_EXIT)
    window._range_changed("exit", 71.0, 91.0)
    assert window.model.search.entry_x_min == 26.5
    assert window.model.search.exit_x_max == 91.0

    window.model.analysis.method = "Morgenstern-Price"
    window.model.search.entry_steps = 3
    window.model.search.exit_steps = 3
    window.model.search.depth_steps = 6
    window.model.search.random_trials = 0
    preflight = preflight_analysis(window.model, max_candidates=80)
    assert preflight["status"] == "PASS", preflight
    assert preflight["slice_valid"] > 0 or preflight["fallback_slice_valid"] > 0
    window.set_mode(ViewMode.DEFINE)
    window.set_mode(ViewMode.RESULTS)
    window.canvas.fit_all()
    assert before == [list(region.polygon) for region in window.model.regions]

    # Regression for the user-reported KTOD/KTDO import: one closed CAD section
    # must become one visible/calculable Region and use full-size scene bounds.
    cad_model = _cad_model()
    window.set_model(cad_model, fit=True)
    assert len(window.model.regions) == 1
    assert window.model.regions[0].material_source_id == 1
    xmin, ymin, xmax, ymax = window.canvas._bounds
    assert xmax - xmin > 1600 and ymax - ymin > 400, window.canvas._bounds
    assert window.canvas.sceneRect().width() > 1600
    window.model.analysis.method = "Morgenstern-Price"
    window.model.search.entry_steps = 4
    window.model.search.exit_steps = 4
    window.model.search.depth_steps = 8
    window.model.search.random_trials = 0
    cad_preflight = preflight_analysis(window.model, max_candidates=120)
    assert cad_preflight["status"] == "PASS", cad_preflight

    app.processEvents()
    window.close()
    print(
        "[PASS] Windows Qt offscreen: startup, GSZ, CAD closed-section Region, "
        "full-size fit, one-click assign, Entry/Exit, computational preflight, Results."
    )


if __name__ == "__main__":
    main()
