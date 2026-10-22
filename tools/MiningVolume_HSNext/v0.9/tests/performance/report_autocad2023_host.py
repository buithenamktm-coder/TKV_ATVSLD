"""Summarize native AutoCAD 2023 TIN/section timing logs.

This is NOT a CI/core benchmark, nor proof of functional release acceptance.
Only compare runs on the SAME DWG fingerprint, section count and geometry count.
Usage:
  python report_autocad2023_host.py --current autocad2023-perf.tsv
  python report_autocad2023_host.py --current new.tsv --baseline old.tsv
"""
import argparse
import csv
import json
import statistics
from pathlib import Path

OPERATIONS = ("TIN_CAD_EXISTING", "TIN_CAD_DESIGN", "TIN_CAD_PAIR", "SECTION_CAD_EXPORT")


def read_passes(file):
    path = Path(file)
    if not path.is_file():
        raise FileNotFoundError("Không có dữ liệu AutoCAD host: " + str(path))
    with path.open(encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream, delimiter="\t"))
    data = []
    for row in rows:
        if row.get("operation") not in OPERATIONS or row.get("status") != "PASS":
            continue
        try:
            items = int(row["items"])
            done = int(row["completed"])
            geometry = int(row["geometry_count"])
            elapsed = float(row["elapsed_ms"])
            sha = row["source_sha"]
            drawing = row["drawing_fingerprint"]
            mvid = row["assembly_mvid"]
            if items < 1 or items != done or geometry < 0 or elapsed < 0 or not drawing or not mvid:
                continue
        except (ValueError, TypeError, KeyError):
            continue
        data.append({
            "operation": row["operation"],
            "items": items,
            "geometry_count": geometry,
            "elapsed_ms": elapsed,
            "source_sha": sha,
            "drawing_fingerprint": drawing,
            "assembly_mvid": mvid,
            "utc": row["utc"],
        })
    return data


def median(entries):
    return round(statistics.median(row["elapsed_ms"] for row in entries), 3)


def summarize(current, baseline, min_sections):
    sections = [r for r in current if r["operation"] == "SECTION_CAD_EXPORT"
                and r["items"] >= min_sections]
    summary = {
        "source": "AutoCAD 2023 host telemetry; not Linux/core-only",
        "host_measurements_total": len(current),
        "qualifying_section_runs": len(sections),
        "minimum_sections": min_sections,
        "release_accepted": False,
        "status": "NO_HOST_85_SECTION_EVIDENCE",
    }
    if not sections:
        return summary
    # The most recent valid section run is the candidate; compare it only
    # against identical source DWG and geometry counts, never arbitrary tests.
    candidate = sections[-1]
    comparable = [r for r in sections
                  if r["drawing_fingerprint"] == candidate["drawing_fingerprint"]
                  and r["items"] == candidate["items"]
                  and r["geometry_count"] == candidate["geometry_count"]
                  and r["source_sha"] == candidate["source_sha"]]
    summary.update({
        "status": "HOST_MEASURED_NOT_RELEASE_VERIFIED",
        "candidate": candidate,
        "same_revision_runs": len(comparable),
        "median_current_ms": median(comparable),
    })
    if baseline:
        matched = [r for r in baseline if r["operation"] == "SECTION_CAD_EXPORT"
                   and r["drawing_fingerprint"] == candidate["drawing_fingerprint"]
                   and r["items"] == candidate["items"]
                   and r["geometry_count"] == candidate["geometry_count"]]
        if matched:
            old = median(matched)
            now = summary["median_current_ms"]
            summary["baseline_median_ms"] = old
            summary["speedup_ratio"] = round(old / now, 3) if now > 0 else None
            summary["time_reduction_percent"] = round(100 * (old - now) / old, 2) if old > 0 else None
            summary["comparable_baseline_runs"] = len(matched)
        else:
            summary["baseline_status"] = "NOT_COMPARABLE_DIFFERENT_DWG_OR_GEOMETRY"
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--current", required=True, type=Path)
    parser.add_argument("--baseline", type=Path)
    parser.add_argument("--min-sections", type=int, default=85)
    args = parser.parse_args()
    if args.min_sections <= 0:
        parser.error("--min-sections phải là số dương")
    result = summarize(read_passes(args.current),
                       read_passes(args.baseline) if args.baseline else [], args.min_sections)
    print(json.dumps(result, indent=2, ensure_ascii=False))
    return 0 if result["qualifying_section_runs"] > 0 else 2


if __name__ == "__main__":
    raise SystemExit(main())
