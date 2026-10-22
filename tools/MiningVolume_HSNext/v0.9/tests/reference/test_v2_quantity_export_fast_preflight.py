"""Regression contracts for high-density TIN drawing quantity export.

The official numerical result must remain guarded by source identity and the
user-edited section polylines, without scanning millions of unrelated CAD
3DFACEs merely to insert an existing-result quantity Block.
These source checks do not replace the AutoCAD 2023 host acceptance.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def source(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_quantity_block_does_not_force_full_tin_face_reconstruction():
    page = source("src/MiningVolume.Plugin2023/UI/ExportPage.cs")
    export = page.split("private async void ExportClicked(", 1)[1].split(
        "private static void EnsureExportInputsCurrent(", 1
    )[0]
    assert "EnsureBothTinsReady(synchronizeCadLayers: true)" not in export
    assert "CadVolumeTableService.Insert(" in export
    # Preflight must still enforce TIN revision, boundary and section edits.
    preflight = page.split("private static void EnsureExportInputsCurrent(", 1)[1].split(
        "private void StateChanged(", 1
    )[0]
    assert "EnsureBothTinsReady(synchronizeCadLayers: false)" in preflight
    assert "EditableSectionCadService.EnsureProfilesMatch(" in preflight
    assert "state.Existing.Source.Revision != existingRevision" in preflight
    assert "state.Design.Source.Revision != designRevision" in preflight
    assert "ReferenceEquals(expectedVolume, state.VolumeResult)" in preflight
    assert "if (exportExcel)" in export.split("if (exportCad)", 1)[-1]


def test_edited_profile_preflight_is_one_scan_and_skips_cad_faces():
    code = source("src/MiningVolume.Plugin2023/Services/EditableSectionCadService.cs")
    checked = code.split("internal static void EnsureProfilesMatch(", 1)[1].split(
        "internal static IReadOnlyList<SectionProfile> ReadEditedProfiles(", 1
    )[0]
    assert "ReadEditedProfiles(db, approved, allowMissingUnedited: true)" in checked
    assert "HasEditableProfiles(db)" not in checked
    assert "EditedSectionProfileBuilder.SameGeometry(approved, actual)" in checked

    scan = code.split("internal static IReadOnlyList<SectionProfile> ReadEditedProfiles(", 1)[1].split(
        "internal static void UpdateAreaLabels(", 1
    )[0]
    assert "id.ObjectClass.IsDerivedFrom(polyClass)" in scan
    assert scan.index("id.ObjectClass.IsDerivedFrom(polyClass)") < scan.index(
        "tr.GetObject(id, OpenMode.ForRead)"
    )
    assert "Decode(poly, data)" in scan
    assert "p.IsUserEdited" in scan
    assert "if (grouped.Count == 0 && allowMissingUnedited)" in scan
    assert "Không tìm thấy đường CAD hiệu chỉnh" in scan
