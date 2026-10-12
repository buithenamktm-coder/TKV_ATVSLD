"""Release guards for HSMo-style duplicate XY / multiple-Z TIN mode.

These checks target AutoCAD integration, while the C# native core regressions
exercise actual constrained triangulation and section geometry.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "src"


def read(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_auto_vertical_mode_keeps_strict_core_opt_in_and_preserves_all_z():
    options = read("MiningVolume.Core/Surface/SurfaceContracts.cs")
    preparer = read("MiningVolume.Core/Surface/SurfaceInputPreparer.cs")
    workflow = read("MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    builder = read("MiningVolume.Surface/ConformingTinBuilder.cs")
    assert "public bool PreserveDuplicateXYVerticalFaces { get; set; }" in options
    assert "PreserveDuplicateXYVerticalFaces = true" in workflow
    assert "result.VerticalConflictGroups.AddRange(groups);" in preparer
    assert "if (p.Z < old.Z) unique[match]" in preparer
    assert "VerticalFaceBuilder.Build(" in builder
    assert "verticalFaces, input.VerticalConflictGroups" in builder


def test_cad_writes_actual_faces_and_counts_vertical_geometry():
    render = read("MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    workflow = read("MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs")
    assert "foreach (var t in tin.VerticalFaces)" in render
    assert "ValidateVerticalFace(t);" in render
    assert "cadTin.Triangles.Count + cadTin.VerticalFaces.Count" in workflow
    assert "var cadTin = tin;" in workflow
    assert "cadTin.Triangles.Count + cadTin.VerticalFaces.Count" in workflow
    assert "foreach (var t in tin.VerticalFaces)" in render


def test_user_sees_warning_not_manual_point_removal():
    ui = read("MiningVolume.Plugin2023/UI/DataPage.cs")
    assert "duplicateGroups = build.DuplicateXYGroupCount" in ui
    assert "duplicateVertices = build.DuplicateXYSourceVertices" in ui
    assert "Không cần loại từng điểm thủ công" in ui
    assert "sameXYZ = build.SameXYZDuplicateVertices" in ui
    assert "CẬP NHẬT MẶT CẮT" in ui


def test_unreviewed_3d_vertical_faces_cannot_be_used_for_final_volume():
    workflow = read("MiningVolume.Plugin2023/Services/VolumeWorkflowService.cs")
    sampler = read("MiningVolume.Core/Sections/TinSectionSampler.cs")
    page = read("MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert "(existing.RequiresSectionReview || design.RequiresSectionReview)" in workflow
    assert "p.HasData && !p.IsUserEdited" in workflow
    assert "AnnotateVerticalConflicts(" in sampler
    assert "ĐIỂM TRÙNG XY:" in sampler
    assert "CẦN KIỂM TRA MC" in page

def test_standard_tin_creation_never_launches_manual_point_deletion():
    ui = read("MiningVolume.Plugin2023/UI/DataPage.cs")
    build = ui.split("private async Task<SurfaceBuildResult> BuildRoleWithConflictChoiceAsync(",1)[1].split(
        "private void SetBuildStatus(",1)[0]
    assert "BuildCoreDetailed(" in build
    assert "DuplicateXYConflictUi.Show(" not in build
    assert "OperationCanceledException(" not in build
    assert "Cần xử lý nguồn xung đột và quét lại" not in build
    # The legacy editor remains available only on demand; not a release gate.
    assert 'Btn("Thống kê điểm trùng (kiểm tra tùy chọn)"' in ui
    warning = ui.split("catch(DuplicateXYConflictException ex)",1)[1].split(
        "catch(Exception ex)",1)[0]
    assert "Không yêu cầu loại từng điểm trùng thủ công" in warning
    assert "Không thể dựng topology TIN" in warning


def test_post_tin_warning_is_counted_not_manual_confirmation():
    ui = read("MiningVolume.Plugin2023/UI/DataPage.cs")
    success = ui.split("int duplicateGroups = build.DuplicateXYGroupCount",1)[1].split(
        "catch(OperationCanceledException)",1)[0]
    assert "int duplicateVertices = build.DuplicateXYSourceVertices" in success
    assert "int sameXYZ = build.SameXYZDuplicateVertices" in success
    assert "Đề nghị kiểm tra hệ thống điểm và đường đồng mức" in success
    assert "Không cần loại từng điểm thủ công." in success
    assert "MessageBoxButtons.OK" in success

def test_generated_breakline_crossing_is_no_longer_blocked_by_missing_source_vertices():
    preparer = read("MiningVolume.Core/Surface/SurfaceInputPreparer.cs")
    assert "g.Sources.Count(x => !x.Generated && x.VertexIndex >= 0) >= 2" not in preparer
    assert "result.VerticalConflictGroups.AddRange(groups);" in preparer
    assert "foreach (var group in result.VerticalConflictGroups)" in preparer
    assert "result.Sites.Add(new Vec3(group.Position.X, group.Position.Y, z));" in preparer
    assert "RelaxConflictedBreaklines(result, options, cancellationToken);" in preparer
    assert "result.RelaxedBreaklineConstraints += removed;" in preparer


def test_local_relaxation_is_diagnosed_not_source_deleted():
    code = read("MiningVolume.Core/Surface/SurfaceInputPreparer.cs")
    helper = code.split("private static void RelaxConflictedBreaklines(",1)[1].split(
        "public PreparedSurfaceInput PrepareRaw(",1)[0]
    assert "result.Breaklines.Clear();" in helper
    assert "result.Breaklines.AddRange(safe);" in helper
    assert "result.RelaxedBreaklineConstraints += removed;" in helper
    assert "CAD gốc và mọi Z vẫn giữ nguyên" in helper
    assert "RemoveEntityFromModel" not in helper
