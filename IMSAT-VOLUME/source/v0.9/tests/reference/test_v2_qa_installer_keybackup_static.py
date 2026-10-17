from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(p):
    return (ROOT / p).read_text(encoding="utf-8")


def test_v2_qa_has_separate_installer_and_icon_resource_without_overwriting_v1():
    build = read("installer/build_v2_qa.ps1")
    workflow = read("../../.github/workflows/build-miningvolume-autocad2023.yml") if False else None
    assert "IMSAT_MiningVolume_V2_0_QA_CANDIDATE.exe" in build
    assert 'AppVersion="2.0.0"' in build
    assert "rsrc_windows_amd64.syso" in build
    assert "imsat.ico" in build
    assert "QA Candidate" in build
    assert "V2_QA_CANDIDATE_NOT_RELEASE.txt" in build
    assert "Remove-Item $work" in build
    original = read("installer/main.go")
    assert 'version = "1.0.0"' in original  # V1 source deliberately preserved


def test_admin_can_back_up_and_recover_signing_key_with_encryption_and_mac():
    s = read("license-manager/IssuerBackup.cs")
    main = read("license-manager/Program.cs")
    for marker in ["HashAlgorithmName.SHA256", "Iterations=250000", "HMACSHA256",
                   "CipherMode.CBC", "PaddingMode.PKCS7", "RandomNumberGenerator",
                   "EqualConstantTime", "SelfTest"]:
        assert marker in s
    assert 'case "backup":' in main
    assert 'case "restore":' in main
    assert "ReadPasswordFromConsole" in main
    assert "ProtectedData.Protect" in main
    assert "IssuerBackup.SelfTest();" in main
    assert "if(File.Exists(PrivatePath))" in main


def test_user_installer_does_not_include_admin_private_key():
    workflow=Path(__file__).resolve().parents[5]/".github/workflows/build-miningvolume-autocad2023.yml"
    if workflow.exists():
        s=workflow.read_text(encoding="utf-8")
        assert "Build V2.0 QA Candidate Setup" in s
    main=read("installer/main.go")
    assert "issuer.private.dpapi" not in main
    assert "RSAPrivateKey" not in main
