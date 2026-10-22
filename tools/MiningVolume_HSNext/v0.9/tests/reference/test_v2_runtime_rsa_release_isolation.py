"""V2.0 host QA and offline licensing must never be confused with V1.0 FINAL.

Static tests complement RSA issuer self-test and actual AutoCAD host tests.
No private key is generated, committed, or used by GitHub-hosted CI.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")


def test_v2_workflow_isolated_from_v1_and_rejects_unpinned_keys():
    wf = read("../../../.github/workflows/verify-imsat-volume-v2-autocad2023.yml")
    old = read("../../../.github/workflows/verify-miningvolume-autocad2023-runtime.yml")
    assert "runs-on: [self-hosted, Windows, X64, autocad2023]" in wf
    assert "default: miningvolume-v2-chatgpt-p0-continuation" in wf
    assert "git merge-base --is-ancestor" in wf
    assert "source is not part of V2" in wf
    assert "secrets.IMSAT_VOLUME_V2_RSA_PUBLIC_XML_B64" in wf
    assert "vars.IMSAT_VOLUME_V2_RSA_PUBLIC_SHA256" in wf
    assert "-ExpectedPublicKeySha256 $env:APPROVED_RSA_SHA256" in wf
    assert "-ProductVersion '2.0'" in wf
    assert "V2_RUNTIME_VERIFICATION.txt" in wf
    assert "V2_MVSELFTEST_RUNTIME.txt" in wf
    assert "V2_HOST_QA_NOT_RELEASE.txt" in wf
    assert "IMSAT_MiningVolume_V2_0_QA_CANDIDATE.exe" in wf
    assert "RealSignedPassActivation=NOT_TESTED" in wf
    assert "T01_T22_EndToEnd=NOT_TESTED" in wf
    assert "ReleaseStatus=BLOCKED" in wf
    assert "Setup_v1.0" not in wf
    assert "miningvolume-v1.0" not in wf
    assert "MiningVolume_HSNext_AutoCAD2023_Setup_v1.0_RUNTIME_PASS.exe" in old

    # Placeholder-sensitive code tests must run before provisioning the
    # source file with its public verifier on the isolated runner.
    assert wf.index("python -m pytest -q tests/reference") < wf.index(
        "& \"$env:MV_ROOT/release/provision_v2_public_key.ps1\""
    )


def test_public_key_provisioning_must_fail_closed_and_never_embed_private_xml():
    ps = read("release/provision_v2_public_key.ps1")
    assert "ExpectedPublicKeySha256" in ps
    assert "PublicKeyFile" in ps
    assert "RSACryptoServiceProvider" in ps
    assert "rsa.KeySize -lt 3072" in ps
    assert "rsa.PublicOnly -ne $true" in ps
    assert "ToXmlString($false)" in ps
    assert "SHA256" in ps
    assert "PublicKeySha256=" in ps
    assert "PrivateKeyIncluded=NO" in ps
    assert "SourceProvisioned=YES" in ps
    assert "RSA public key không khớp SHA-256" in ps
    assert 'internal const string RsaPublicKeyXml = "";' in ps
    assert "rsa.ToXmlString(true)" not in ps
    assert "issuer.private.dpapi" not in ps
    assert "[System.IO.File]::ReadAllText" in ps
    assert "File.WriteAllText" in ps
    assert "PublicXmlPath" not in ps
    assert "SourceProvisioned=YES" in read("release/prepare_v2_runtime_bundle.ps1")


def test_real_v2_runtime_has_version_specific_selftest():
    verifier = read("release/verify_autocad2023_runtime.ps1")
    test = read("src/MiningVolume.Plugin2023/Services/SelfTestService.cs")
    assert "[ValidateSet('1.0', '2.0')]" in verifier
    assert "AppVersion=" in verifier
    assert "MININGVOLUME_RUNTIME_PRODUCT_VERSION" in verifier
    assert "ProductVersion=" in verifier
    assert "IMSAT VOLUME V2.0 runtime self-test" in verifier
    assert "IMSAT VOLUME V2.0 runtime self-test" in test
    assert "MiningVolume HS-Next v1.0 runtime self-test" in test
    assert "MiningVolume HS-Next v1.0 runtime self-test" in verifier
    assert "MININGVOLUME_RUNTIME_PRODUCT_VERSION = $oldProductVersion" in verifier


def test_issuer_fingerprint_comes_from_public_only_not_issuer_private_material():
    issuer = read("license-manager/Program.cs")
    assert 'case "fingerprint":' in issuer
    assert "PrintPublicFingerprint()" in issuer
    assert "rsa.ToXmlString(false)" in issuer
    assert "IMSAT_VOLUME_V2_RSA_PUBLIC_SHA256=" in issuer
    assert "rsa.PublicOnly" in issuer
    assert "rsa.KeySize < 3072" in issuer


def test_v2_host_staging_never_mutates_v1_bundle_source():
    ps = read("release/prepare_v2_runtime_bundle.ps1")
    assert "Copy-Item -Path $source -Destination $Destination -Recurse -Force" in ps
    assert "AppVersion=\"2.0.0\"" in ps
    assert "Version=\"2.0.0\"" in ps
    assert "NOT RELEASE" in ps
    assert "ReleaseStatus=BLOCKED" not in ps  # host proof is not final
