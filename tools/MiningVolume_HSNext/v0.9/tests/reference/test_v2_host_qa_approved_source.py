"""Licensed host QA must accept PR54 source but reject arbitrary refs.

Static contract only: this does not execute AutoCAD or claim MVSELFTEST PASS.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
WORKFLOW = ROOT / ".github/workflows/verify-imsat-volume-v2-autocad2023.yml"


def read():
    return WORKFLOW.read_text(encoding="utf-8")


def test_host_qa_uses_only_explicit_approved_source_branches():
    s = read()
    block = s.split("- name: Verify V2 source and exact commit", 1)[1].split(
        "- name: Require real licensed AutoCAD 2023 installation", 1
    )[0]
    assert "'miningvolume-v2-chatgpt-p0-continuation'" in block
    assert "'namvecta-volume-v2-grid-accuracy-gate-20261025'" in block
    assert "git merge-base --is-ancestor $sha" in block
    assert "if (-not $authorized) { throw" in block
    assert block.index("if (-not $authorized)") < block.index("SOURCE_SHA=$sha")


def test_host_qa_does_not_issue_a_release_without_real_runtime_proof():
    s = read()
    assert "runs-on: [self-hosted, Windows, X64, autocad2023]" in s
    assert "verify_autocad2023_runtime.ps1" in s
    assert "-ProductVersion '2.0'" in s
    assert "Status=PASS" in s
    assert "T01_T22_EndToEnd=NOT_TESTED" in s
    assert "ReleaseStatus=BLOCKED" in s
