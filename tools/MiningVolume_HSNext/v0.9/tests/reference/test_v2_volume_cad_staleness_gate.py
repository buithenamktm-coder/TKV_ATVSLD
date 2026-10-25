"""Protect quantity recalculation from unsaved CAD grip edits.

VolumePage must reject stale tagged CAD section polylines BEFORE starting the
background numerical solve and AFTER returning to the AutoCAD host thread.
ExportPage already has its own preflight, which is not sufficient: wrong
intermediate quantities must not be shown as newly accepted calculations.
These contracts are source checks; the actual Polyline behavior is covered
in the AutoCAD-only SectionCadRegression group.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def src(path):
    return (ROOT / path).read_text(encoding="utf-8")


def calc():
    return src("src/MiningVolume.Plugin2023/UI/VolumePage.cs").split(
        "private async void CalculateClicked(", 1
    )[1].split("private void BindResult(", 1)[0]


def test_quantity_calculation_rejects_uncommitted_cad_grip_edits_twice():
    s = calc()
    gate = "EditableSectionCadService.EnsureProfilesMatch("
    assert s.count(gate) == 2, "Both start and pre-commit CAD validation are mandatory"
    first = s.index(gate)
    background = s.index("await Task.Run(")
    second = s.index(gate, first + len(gate))
    accepted = s.index("VolumeWorkflowService.Commit(")
    assert first < background < second < accepted
    assert "doc.Database, st.SectionProfiles.ToArray()" in s
    assert "doc.Database, profiles" in s
    assert "token.ThrowIfCancellationRequested();" in s[second:accepted]
    assert "regionRevision != st.TinRegionRevision" in s


def test_edited_geometry_is_compared_to_approved_profiles():
    s = src("src/MiningVolume.Plugin2023/Services/EditableSectionCadService.cs")
    assert "ReadEditedProfiles(db, approved, allowMissingUnedited: true)" in s
    assert "EditedSectionProfileBuilder.SameGeometry(approved, actual)" in s
    assert "Hãy bấm CẬP NHẬT MẶT CẮT" in s


def test_actual_autocad_host_regression_protects_manual_grip_edit():
    s = src("src/MiningVolume.Plugin2023/Services/SectionCadRegression.cs")
    assert 'RunGroup("Volume preflight vs CAD grip edit",' in s
    assert "VerifyVolumePreflightRejectsCadGripEdits" in s
    assert "EditableSectionCadService.EnsureProfilesMatch(db, profiles)" in s
    assert "poly.SetPointAt(" in s
    assert "Modified grip vertex silently reused stale quantities" in s
