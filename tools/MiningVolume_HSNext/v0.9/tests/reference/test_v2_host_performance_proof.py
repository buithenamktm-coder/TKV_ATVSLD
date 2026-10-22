"""Host timing evidence must come from real AutoCAD 2023 operations.

This source regression does not claim that a host benchmark was run in CI.
HostPerformanceLog is intentionally nonfatal; release still requires Windows
AutoCAD 2023 runtime and real-data acceptance.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_sections_measure_real_host_cad_work_and_never_claim_canceled_pass():
    code = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    draw = code.split("public static void DrawProfiles(", 1)[1].split(
        "public static void ClearPreview(", 1
    )[0]
    assert "var clock = Stopwatch.StartNew();" in draw
    assert "const int batchSize = 20;" in draw
    assert "long beginTicks = clock.ElapsedTicks;" in draw
    assert "Stopwatch.Frequency" in draw
    assert "batchMs.Add(" in draw
    assert "allSections.Commit();" in draw
    assert draw.index('outcome = "PASS";') > draw.index("allSections.Commit();")
    assert 'outcome = "CANCELED";' in draw
    assert "catch (OperationCanceledException)" in draw
    assert 'HostPerformanceLog.Record("SECTION_CAD_EXPORT"' in draw
    assert "completed = start + count;" in draw
    assert "clock.Elapsed.TotalMilliseconds" in draw


def test_tin_timing_requires_successful_cad_write_and_records_failed_attempts():
    code = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    pair = code.split("public static void DrawTinPair(", 1)[1].split(
        "public static void DrawTin(ModelRole role, SurfaceBuildResult build)", 1
    )[0]
    assert "Stopwatch.StartNew();" in pair
    assert "TinCadRenderer.ReplacePair(" in pair
    assert 'HostPerformanceLog.Record("TIN_CAD_PAIR"' in pair
    assert 'outcome = "CANCELED";' in pair

    single = code.split("private static void DrawTinInternal(", 1)[1].split(
        "private static void AdoptTin(", 1
    )[0]
    assert "TinCadRenderer.Replace(" in single
    assert "if (written != checked(cadTin.Triangles.Count + cadTin.VerticalFaces.Count))" in single
    assert single.index('outcome = "PASS";') > single.index(
        "if (written != checked(cadTin.Triangles.Count + cadTin.VerticalFaces.Count))"
    )
    assert '"TIN_CAD_EXISTING" : "TIN_CAD_DESIGN"' in single
    assert "HostPerformanceLog.Record(" in single


def test_host_proof_is_local_versioned_and_nonblocking():
    code = read("src/MiningVolume.Plugin2023/Services/HostPerformanceLog.cs")
    for token in (
        "SpecialFolder.LocalApplicationData",
        "autocad2023-perf.tsv",
        "BUILD_IDENTITY.txt",
        "SOURCE_SHA=",
        "assembly_mvid",
        "drawing_fingerprint",
        "batch_ms",
        "CultureInfo.InvariantCulture",
        "File.AppendAllText(",
    ):
        assert token in code, token
    assert "lock (Sync)" in code
    assert "catch" in code
    assert "No DWG filename" in code
