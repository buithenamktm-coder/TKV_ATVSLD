from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def test_export_gap_is_distinct_from_survey_section_spacing():
    ui = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    state = read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    assert 'Add(top, 3, "Khoảng cách mặt cắt, m", _spacing' in ui
    assert 'Add(top, 10, "Giãn cách khi xuất CAD", _layoutGap' in ui
    assert '_layoutGap = Num(90, 30, 10000);' in ui
    assert 'SectionLayoutGap { get; set; } = 90.0' in state
    assert 'SectionSpacing { get; set; } = 20.0' in state


def test_renderer_uses_same_gap_for_prior_batched_and_current_profiles():
    renderer = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    workflow = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    ui = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert 'currentY -= (prevTopZ - prevBaseZ) * vExag + layoutGap;' in renderer
    assert 'currentY -= height + layoutGap;' in renderer
    assert 'currentY -= height + 45.0;' not in renderer
    assert 'double layoutGap = 90.0' in renderer
    assert re.search(r"start\s*==\s*0\s*,\s*layoutGap\s*,\s*nextY\s*,\s*meter\.Poll\s*,\s*createdByExport\.Add\s*,\s*erasedByExport\.Add\s*\)", workflow)
    assert 'nextY = SectionCadRenderer.ReplaceProfilesBatched(' in workflow
    assert '!batchStartY.HasValue && k < startIndex' in renderer
    # The newly drawn profiles are passed explicitly so an insertion cancel
    # does not replace the previously approved section/volume state.
    assert '(double)_layoutGap.Value,\n                    profiles);' in ui
    assert 'double.IsNaN(layoutGap)' in renderer
    assert 'double.IsInfinity(layoutGap)' in renderer


def test_layout_gap_persisted_without_project_format_break():
    snap = read("src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs")
    project = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    ui = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert 'FormatVersion { get; set; } = 2' in snap
    assert '[DataMember(Order = 24)] public double SectionLayoutGap' in snap
    assert 'SectionLayoutGap = state.SectionLayoutGap' in project
    assert 'PositiveOr(snap.SectionLayoutGap, 90)' in project
    assert 'st.SectionLayoutGap = (double)_layoutGap.Value;' in ui
    assert '_layoutGap.Value = Clamp(_layoutGap, st.SectionLayoutGap)' in ui
