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
    assert "tin.VerticalFaces, tin.VerticalConflicts" in workflow


def test_user_sees_warning_not_manual_point_removal():
    ui = read("MiningVolume.Plugin2023/UI/DataPage.cs")
    assert "duplicateGroups = existing.DuplicateXYGroupCount + design.DuplicateXYGroupCount" in ui
    assert "duplicateVertices = existing.DuplicateXYSourceVertices + design.DuplicateXYSourceVertices" in ui
    assert "Không cần loại từng điểm thủ công" in ui
    assert "sameXYZ = existing.SameXYZDuplicateVertices + design.SameXYZDuplicateVertices" in ui
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
