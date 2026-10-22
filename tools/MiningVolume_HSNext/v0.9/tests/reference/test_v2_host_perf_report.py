"""The Work reviewer must never infer speed or acceptance from unrelated CI rows."""
import importlib.util
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "performance" / "report_autocad2023_host.py"
spec = importlib.util.spec_from_file_location("mv_host_perf_report", SCRIPT)
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)


def row(sha, dwg, items, geometry, ms, stage="SECTION_CAD_EXPORT"):
    return {
        "source_sha": sha,
        "drawing_fingerprint": dwg,
        "assembly_mvid": "mvid",
        "utc": "2026-10-22T00:00:00Z",
        "operation": stage,
        "status": "PASS",
        "items": items,
        "completed": items,
        "geometry_count": geometry,
        "elapsed_ms": ms,
    }


def test_host_report_requires_85_real_sections():
    r = report.summarize([row("sha", "dwg", 15, 1500, 500)], [], 85)
    assert r["status"] == "NO_HOST_85_SECTION_EVIDENCE"
    assert not r["release_accepted"]
    assert r["qualifying_section_runs"] == 0


def test_host_report_only_compares_same_dwg_and_geometry():
    current = [row("after", "A", 85, 8500, 1000),
               row("after", "A", 85, 8500, 1100)]
    baseline = [row("before", "B", 85, 8500, 5000),
                row("before", "A", 15, 1500, 4000),
                row("before", "A", 85, 8500, 2100)]
    result = report.summarize(current, baseline, 85)
    assert result["status"] == "HOST_MEASURED_NOT_RELEASE_VERIFIED"
    assert result["median_current_ms"] == 1050
    assert result["baseline_median_ms"] == 2100
    assert result["speedup_ratio"] == 2.0
    assert result["time_reduction_percent"] == 50.0
    assert not result["release_accepted"]

    unrelated = [row("before", "B", 85, 8500, 200)]
    result = report.summarize(current, unrelated, 85)
    assert result["baseline_status"] == "NOT_COMPARABLE_DIFFERENT_DWG_OR_GEOMETRY"
    assert "speedup_ratio" not in result


def test_host_report_reads_tab_separated_accepted_rows(tmp_path):
    f = tmp_path / "host.tsv"
    f.write_text(
        "utc\toperation\tstatus\tdrawing_fingerprint\tsource_sha\tassembly_mvid\titems\tcompleted\tgeometry_count\telapsed_ms\tbatch_ms\n"
        "utc\tSECTION_CAD_EXPORT\tPASS\tA\tsha\tmvid\t85\t85\t8500\t1234.5\t12;13\n"
        "utc\tSECTION_CAD_EXPORT\tCANCELED\tA\tsha\tmvid\t85\t20\t8500\t1000\t5\n"
        "utc\tSECTION_CAD_EXPORT\tPASS\tA\tsha\tmvid\t85\t80\t8500\t100\t1\n",
        encoding="utf-8",
    )
    parsed = report.read_passes(f)
    assert len(parsed) == 1
    assert parsed[0]["elapsed_ms"] == 1234.5
