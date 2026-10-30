from __future__ import annotations

import json
import math
import os
import sys
import traceback
from pathlib import Path

from app.algorithms.solver import analyze_model
from app.core.geometry import validate_model
from app.core.models import GeoStudioModel


def emit(payload: dict) -> None:
    """Emit one JSON line without making a windowed/frozen worker fail.

    In a PyInstaller GUI executable, ``sys.stdout`` can be ``None``.  The
    earlier implementation raised before analysis began, so the GUI reported
    "no converged result" even though the numerical core was valid.  Output
    is best-effort; writing the solved project remains the worker contract.
    """
    line = json.dumps(payload, ensure_ascii=False) + "\n"
    try:
        stream = getattr(sys, "stdout", None)
        if stream is not None:
            stream.write(line)
            stream.flush()
            return
    except Exception:
        pass
    try:
        os.write(1, line.encode("utf-8", errors="replace"))
    except Exception:
        pass


def _write_terminal_status(output_path: str | Path, payload: dict) -> None:
    """Persist terminal worker status for windowed EXE builds."""
    try:
        path = Path(str(output_path) + ".diagnostics.json")
        path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    except Exception:
        pass


def run_solver_job(input_path: str | Path, output_path: str | Path) -> int:
    source = Path(input_path)
    target = Path(output_path)
    try:
        model = GeoStudioModel.load(source)
        issues = validate_model(model)
        errors = [issue.message for issue in issues if issue.severity == "error"]
        warnings = [issue.message for issue in issues if issue.severity != "error"]
        if errors:
            payload = {"type": "error", "message": "Mô hình hình học chưa hợp lệ.", "details": errors}
            _write_terminal_status(target, payload); emit(payload)
            return 2
        if any(region.material_source_id is None for region in model.regions):
            payload = {"type": "error", "message": "Còn Region chưa được gán vật liệu."}
            _write_terminal_status(target, payload); emit(payload)
            return 2
        emit({"type": "start", "message": "Bắt đầu tìm kiếm mặt trượt", "warnings": warnings})

        def progress(done, total, result):
            if done == 1 or done == total or done % max(1, total // 100) == 0:
                emit({
                    "type": "progress",
                    "done": done,
                    "total": total,
                    "fs": result.fs if math.isfinite(result.fs) else None,
                    "converged": result.converged,
                })

        results = analyze_model(model, progress=progress)
        diagnostics = dict(getattr(model, "analysis_diagnostics", {}) or {})
        if not results:
            details = [
                f"Mặt trượt sinh ban đầu: {diagnostics.get('primary_candidates', 0)}.",
                f"Mặt trượt tạo được lát: {diagnostics.get('slice_valid', 0)}.",
                f"Mặt trượt hội tụ theo {model.analysis.method}: {diagnostics.get('method_converged', 0)}.",
            ]
            if diagnostics.get("fallback_used"):
                details.append(f"Đã tự mở rộng tìm kiếm: {diagnostics.get('fallback_candidates', 0)} mặt thử.")
            for reason, count in list(diagnostics.get("rejected_reasons", {}).items())[:8]:
                details.append(f"{reason}: {count} mặt.")
            payload = {"type": "error", "message": "Không tìm được mặt trượt có Fs hữu hạn và hội tụ.", "details": details, "diagnostics": diagnostics}
            _write_terminal_status(target, payload); emit(payload)
            return 3
        model.save(target)
        payload = {
            "type": "complete",
            "results": len(results),
            "critical_fs": results[0].fs,
            "method": model.analysis.method,
            "output": str(target),
            "diagnostics": diagnostics,
        }
        _write_terminal_status(target, payload); emit(payload)
        return 0
    except Exception as exc:
        payload = {"type": "exception", "message": str(exc), "traceback": traceback.format_exc()}
        _write_terminal_status(target, payload); emit(payload)
        return 10


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python -m app.core.solver_job input.issv6 output.issv6", file=sys.stderr)
        raise SystemExit(64)
    raise SystemExit(run_solver_job(sys.argv[1], sys.argv[2]))
