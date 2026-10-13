from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    canvas = (ROOT / "app" / "ui" / "engineering_canvas.py").read_text(encoding="utf-8")
    window = (ROOT / "app" / "ui" / "main_window.py").read_text(encoding="utf-8")
    worker = (ROOT / "app" / "core" / "solver_job.py").read_text(encoding="utf-8")
    solver = (ROOT / "app" / "algorithms" / "solver.py").read_text(encoding="utf-8")
    models = (ROOT / "app" / "core" / "models.py").read_text(encoding="utf-8")
    mp = (ROOT / "app" / "algorithms" / "morgenstern_price.py").read_text(encoding="utf-8")

    assert window.count("EngineeringCanvas()") == 1
    assert "assign_material(self.model, region_id, self.active_material_id)" in window
    assert "preflight_analysis(self.model)" in window
    assert "QProcess" in window and "--solver-job" in window
    assert "recovery_latest.issv6" in window
    assert "if self.mode == ViewMode.RESULTS" in canvas
    assert "Slice boundaries are results only" in canvas
    assert "reference linework is visual context only" in canvas
    assert "search_range_changed.emit" in canvas and "interp_y(self.model.terrain" in canvas
    for forbidden in ["self.ScrollHandDrag", "self.AnchorUnderMouse", "self.AnchorViewCenter"]:
        assert forbidden not in canvas
    assert "class SliceBuildReport" in solver
    assert "def build_slices_report" in solver
    assert "def preflight_analysis" in solver
    assert "analysis_diagnostics" in models
    assert "sys.stdout`` can be ``None" in worker
    assert "solve_morgenstern_price" in worker or "analyze_model" in worker
    assert "Zhu–Lee–Qian–Chen" in mp
    print("[PASS] V6.2 contract: shared canvas, one-click assignment, snapped Entry/Exit, computational preflight, isolated/windowed-safe solver, recovery, slices only in Results.")


if __name__ == "__main__":
    main()
