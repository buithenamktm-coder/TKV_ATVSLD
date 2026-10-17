from __future__ import annotations

import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TESTS = [
    "tests/test_geometry_core.py",
    "tests/test_morgenstern_price_benchmark.py",
    "tests/test_solver_core.py",
    "tests/test_project_reports.py",
    "tests/test_v6_contract.py",
    "tests/test_large_open_pit_workflow.py",
    "tests/test_cad_closed_section_workflow.py",
]


def main():
    report = {"product": "IMSAT Slope Stability Professional V6.2", "timestamp_utc": datetime.now(timezone.utc).isoformat(), "status": "PASS", "tests": []}
    env = dict(os.environ); env["PYTHONPATH"] = str(ROOT)
    for test in TESTS:
        completed = subprocess.run([sys.executable, str(ROOT / test)], cwd=ROOT, env=env, capture_output=True, text=True)
        item = {"test": test, "returncode": completed.returncode, "status": "PASS" if completed.returncode == 0 else "FAIL", "stdout_tail": completed.stdout[-2000:], "stderr_tail": completed.stderr[-2000:]}
        report["tests"].append(item)
        if completed.returncode != 0:
            report["status"] = "FAIL"
            break
    output = ROOT / "tests" / "V6_2_RELEASE_ACCEPTANCE.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
