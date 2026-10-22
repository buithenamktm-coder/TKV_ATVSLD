"""The trusted V2 branch must package an installer with the owner's pinned
RSA *public-only* verifier, never a CI-generated issuer private key.

Pull-request builds remain isolated/keyless and must never be advertised as
production-licensed artifacts. Real AutoCAD MVSELFTEST is still a separate gate.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT.parents[2] / ".github/workflows/build-miningvolume-autocad2023.yml"


def test_trusted_v2_push_uses_owner_public_only_key_before_build():
    s = WORKFLOW.read_text(encoding="utf-8")
    assert "if: github.event_name == 'push' && github.ref == 'refs/heads/miningvolume-v2-chatgpt-p0-continuation'" in s
    assert "secrets.IMSAT_VOLUME_V2_RSA_PUBLIC_XML_B64" in s
    assert "vars.IMSAT_VOLUME_V2_RSA_PUBLIC_SHA256" in s
    assert "-ExpectedPublicKeySha256 $env:V2_ISSUER_PUBLIC_SHA256" in s
    assert "provision_v2_public_key.ps1" in s
    assert "No installable signed-license QA artifact" in s
    # .ps1 does not set LASTEXITCODE. Hosted Windows CI previously pinned
    # the correct RSA key, then falsely failed on an unset $LASTEXITCODE.
    assert "if (-not $?) { throw 'V2 pinned RSA public key provision failed.' }" in s
    assert "if ($LASTEXITCODE -ne 0) { throw 'V2 pinned RSA public key provision failed.' }" not in s
    assert "Remove-Item $publicXml -Force" in s
    assert "IMSAT_REQUIRE_APPROVED_PUBLIC_KEY" in s
    assert "RSA_PUBLIC_VERIFIER_PINNED_SHA256=" in s
    assert s.index("python -m pytest -q tests/reference") < s.index(
        "- name: Provision pinned RSA PUBLIC verifier"
    ) < s.index("- name: Restore .NET Framework solution") < s.index(
        "- name: Build AutoCAD 2023 add-in"
    )
    assert "tools/MiningVolume_HSNext/v0.9/dist/V2_LICENSE_PUBLIC_KEY_PROOF.txt" in s


def test_build_v2_qa_requires_pinned_approval_receipt_in_trusted_pipeline():
    s = (ROOT / "installer/build_v2_qa.ps1").read_text(encoding="utf-8-sig")
    assert "V2_LICENSE_PUBLIC_KEY_PROOF.txt" in s
    assert "PublicKeySha256=" in s
    assert "SourceProvisioned=YES" in s
    assert "PrivateKeyIncluded=NO" in s
    assert "RSA_PUBLIC_VERIFIER_PINNED_SHA256=" in s
    assert "IMSAT_REQUIRE_APPROVED_PUBLIC_KEY -eq '1'" in s
    assert "refusing" not in s.lower() or "BLOCKED:" in s
    assert "TRIAL_ONLY: PUBLIC_KEY_NOT_PROVISIONED" in s
    assert "NOT A RELEASE" in s
    assert "Runtime AutoCAD host acceptance: NOT VERIFIED." in s


def test_user_license_secret_is_never_read_or_exported_by_ci():
    workflow = WORKFLOW.read_text(encoding="utf-8")
    provisioning = (ROOT / "release/provision_v2_public_key.ps1").read_text(
        encoding="utf-8-sig"
    )
    assert "issuer.private.dpapi" not in workflow
    assert "imsbackup" not in workflow
    assert "ToXmlString($true)" not in workflow
    assert "rsa.PublicOnly" in provisioning
    assert "ToXmlString($false)" in provisioning
