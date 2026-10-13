from __future__ import annotations
import json
import math
from pathlib import Path

from app.algorithms.morgenstern_price import solve_geostudio_column_rows, solve_morgenstern_price
from app.core.models import SliceData
from app.io.gsz_reader import read_critical_slice_rows

ROOT = Path(__file__).resolve().parents[1]


def rows_to_slices(rows):
    output = []
    for row in rows:
        output.append(SliceData(
            number=int(float(row["SliceNum"])), x_left=float(row["XLeft"]), x_right=float(row["XRight"]),
            y_bot_left=float(row["YBotLeft"]), y_bot_right=float(row["YBotRight"]),
            y_top_left=float(row["YTopLeft"]), y_top_right=float(row["YTopRight"]),
            weight=float(row["SliceWeight"]), pore_pressure=float(row["PoreWaterPressure"]),
            base_length=float(row["BaseLength"]), base_angle=float(row["RelativeBaseAngle"]),
            cohesion=float(row["CohesiveStrength"]), phi=math.degrees(float(row["BasePhi"]))))
    return output


def main():
    report = {"status": "PASS", "acceptance_difference_percent": 0.02, "models": []}
    for path in sorted((ROOT / "examples").glob("MC *.gsz")):
        reference, rows, column, number = read_critical_slice_rows(path)
        result = solve_geostudio_column_rows(rows) if column else solve_morgenstern_price(rows_to_slices(rows))
        difference = abs(result.fs / reference - 1) * 100
        assert result.converged, (path.name, result)
        assert difference <= report["acceptance_difference_percent"], (path.name, reference, result.fs, difference)
        report["models"].append({"file": path.name, "critical_surface": number, "table_type": "column" if column else "slice", "reference_fs": reference, "imsat_fs": result.fs, "difference_percent": difference, "iterations": result.iterations, "lambda": result.lambda_value})
    output = ROOT / "tests" / "V6_MP_BENCHMARK.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
