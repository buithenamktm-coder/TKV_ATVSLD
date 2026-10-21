"""Source-contract checks only; AutoCAD interaction requires host testing."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PAGE = ROOT / "src/MiningVolume.Plugin2023/UI/SectionPage.cs"

def method(start, end):
    source = PAGE.read_text(encoding="utf-8")
    return source[source.index(start):source.index(end)]

def assert_revision_gate(block):
    for condition in (
        "!state.Existing.IsTinCurrent",
        "!state.Design.IsTinCurrent",
        "existingRevision != state.Existing.Source.Revision",
        "designRevision != state.Design.Source.Revision",
        "regionRevision != state.TinRegionRevision",
    ):
        assert condition in block
    assert "throw new InvalidOperationException" in block

def test_sampling_captures_revisions_before_worker_and_rechecks_before_prompt():
    body = method("private async void BuildProfiles", "private async void DrawQuickSections")
    worker = body.index("await Task.Run")
    for snapshot in ("long existingRevision =", "long designRevision =", "long regionRevision ="):
        assert body.index(snapshot) < worker
    assert_revision_gate(body[worker:body.index("SelectionService.PickInsertionPoint")])

def test_sampling_rechecks_revisions_after_insertion_prompt_before_cad_write():
    body = method("private async void BuildProfiles", "private async void DrawQuickSections")
    assert_revision_gate(body[body.index("SelectionService.PickInsertionPoint"):body.index("SectionWorkflowService.DrawProfiles")])

def test_edited_profile_update_rejects_stale_tin_before_labels_and_result_commit():
    body = method("private async void UpdateEditedProfiles", "private void VolumeParameterChanged")
    worker = body.index("await Task.Run")
    for snapshot in ("long existingRevision =", "long designRevision =", "long regionRevision ="):
        assert body.index(snapshot) < worker
    assert_revision_gate(body[worker:body.index("EditableSectionCadService.UpdateAreaLabels")])
    assert body.index("EditableSectionCadService.UpdateAreaLabels") < body.index("SectionWorkflowService.CommitProfiles")
