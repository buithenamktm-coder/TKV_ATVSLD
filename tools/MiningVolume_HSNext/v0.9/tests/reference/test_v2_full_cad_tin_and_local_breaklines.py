from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_large_tin_does_not_use_stride_sampled_mesh_as_official_geometry():
    store = read("src/MiningVolume.Surface/TiledConformingTinBuilder.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "PreviewTriangles=Array.Empty<Triangle3>()" in store
    assert "preview.Add(triangle)" not in store
    pair = workflow.split("public static void DrawTinPair(", 1)[1].split(
        "public static void DrawTin(ModelRole role, SurfaceBuildResult build)", 1
    )[0]
    assert "var ht = existing.Tin;" in pair
    assert "var tk = design.Tin;" in pair
    assert "TinCadRenderer.ReplacePair(" in pair
    draw = workflow.split("private static void DrawTinInternal(", 1)[1].split(
        "private static void AdoptTin(", 1
    )[0]
    assert "var cadTin = tin;" in draw
    assert "bool cadPreview = false;" in draw
    assert "CadSurface(" not in workflow
    assert "BuildCadPreview(" not in workflow
    assert "200000" not in workflow.split(
        "public static void EnsureTinLayerSynchronized(", 1
    )[1].split("public static string TinStatusText(", 1)[0]
    assert "TinCadRenderer.Replace(" in draw
    assert "int expectedCadFaces = checked(session.Tin.Triangles.Count +" in workflow
    assert "count == expectedCadFaces && !session.TinCadIsPreview" in workflow


def test_topology_fallback_never_clears_all_unrelated_breaklines():
    source = read("src/MiningVolume.Core/Surface/SurfaceInputPreparer.cs")
    fallback = source.split(
        "private static void RecoverConflictingContourTopology(", 1
    )[1].split("private sealed class SplitMark", 1)[0]
    assert "unsafeEdges.Add(i);" in fallback
    assert "unsafeEdges.Add(j);" in fallback
    assert "if (!unsafeEdges.Contains(i)) safe.Add(constraints[i]);" in fallback
    assert "LOCALLY_RELAXED_UNRESOLVED_TOPOLOGY" in fallback
    assert "result.Breaklines.Clear()" not in fallback
    assert "constraints.AddRange(safe);" in fallback
    assert "TOO_FEW_POINTS" not in fallback
    tests = read("tests/core/FinalAuditRegression.cs")
    assert "p0_unresolved_breaklines_are_relaxed_only_at_conflict" in tests
    assert "p0_unrecoverable_validation_error_must_not_clear_constraints" in tests


def test_rebuilding_lost_cad_tin_layer_preserves_accepted_calculations():
    workflow = read("src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    sync = workflow.split("public static void EnsureTinLayerSynchronized(", 1)[1].split(
        "public static string TinStatusText(", 1
    )[0]
    assert "TinCadRenderer.Replace(doc.Database," in sync
    assert "int expectedCadFaces = checked(session.Tin.Triangles.Count +" in sync
    assert "if (written != expectedCadFaces)" in sync
    assert "session.TinCadFaceCount = written;" in sync
    assert "session.TinCadIsPreview = false;" in sync
    assert "if (!session.TinVisible)" in sync
    assert "DrawTinInternal(" not in sync
    assert "AdoptTin(" not in sync
    assert "GridWorkflowService.Invalidate(" not in sync
    assert "state.SectionProfiles.Clear(" not in sync
    assert "state.VolumeResult = null" not in sync
    assert "state.VolumeCalculatedUtc = null" not in sync
