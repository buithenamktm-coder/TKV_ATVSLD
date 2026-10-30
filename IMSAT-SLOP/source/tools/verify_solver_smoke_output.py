from __future__ import annotations

import math
from pathlib import Path

from app.core.models import GeoStudioModel

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "tests" / "_windows_solver_smoke_output.issv6"


def main() -> int:
    if not OUTPUT.exists():
        raise SystemExit("[FAIL] Frozen EXE worker did not create the output project.")
    model = GeoStudioModel.load(OUTPUT)
    if model.critical is None or not math.isfinite(model.critical.fs) or model.critical.fs <= 0:
        raise SystemExit("[FAIL] Frozen EXE worker output has no finite critical Fs.")
    if not model.critical.slices_data:
        raise SystemExit("[FAIL] Frozen EXE worker output has no critical slices.")
    
    if not model.regions:
        raise SystemExit("[FAIL] Frozen EXE output lost the CAD Region.")
    print(f"[PASS] Frozen EXE CAD solver: Fs={model.critical.fs:.8f}; results={len(model.results)}; slices={len(model.critical.slices_data)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
