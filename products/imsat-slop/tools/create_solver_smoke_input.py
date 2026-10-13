from __future__ import annotations

from pathlib import Path

from app.io.cad_model_import import cad_to_model
from app.io.cad_reader import CadEntity, CadModel

ROOT = Path(__file__).resolve().parents[1]
INPUT = ROOT / "tests" / "_windows_solver_smoke_input.issv6"
OUTPUT = ROOT / "tests" / "_windows_solver_smoke_output.issv6"


def main() -> int:
    # Frozen-worker smoke follows the real failing workflow: one closed CAD
    # section, no manual Region creation, then Morgenstern–Price.
    ring = [
        (0, 445), (90, 415), (190, 400), (280, 392), (400, 338), (530, 362),
        (650, 430), (760, 395), (840, 435), (940, 395), (1040, 375),
        (1130, 350), (1220, 325), (1320, 295), (1420, 255), (1530, 220),
        (1640, 195), (1720, 170), (1720, 0), (0, 0),
    ]
    cad = CadModel(
        source_file="KTOD_smoke.dxf",
        working_dxf="KTOD_smoke.dxf",
        entities=[CadEntity("LWPOLYLINE", "0", ring, closed=True)],
        layers=["0"],
        extents=(0, 0, 1720, 445),
    )
    model = cad_to_model(cad, {"0": "Chưa gán"})
    if len(model.regions) != 1 or model.regions[0].material_source_id != 1:
        raise SystemExit("[FAIL] CAD smoke did not create one assigned Region.")
    model.name = "V6.2 CAD frozen-worker smoke"
    model.analysis.method = "Morgenstern-Price"
    model.analysis.slices = 40
    model.analysis.max_iterations = 160
    model.search.entry_steps = 4
    model.search.exit_steps = 4
    model.search.depth_steps = 8
    model.search.random_trials = 0
    model.search.critical_surfaces_to_store = 5
    model.results = []
    model.critical = None
    model.analysis_diagnostics = {}
    model.save(INPUT)
    OUTPUT.unlink(missing_ok=True)
    print(INPUT)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
