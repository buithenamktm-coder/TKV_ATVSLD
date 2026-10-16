from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT/rel).read_text(encoding="utf-8")


def test_trial_is_15_consecutive_days_and_clock_rollback_blocks_calculation():
    s=read("src/MiningVolume.Plugin2023/Services/LicenseService.cs")
    assert "record.FirstUseUtc.AddDays(15)" in s
    assert "now < record.LastUseUtc.AddHours(-1)" in s
    assert "LicenseState.ClockRollback" in s
    assert "LicenseState.TrialExpired" in s
    assert "LicenseState.StorageError" in s
    assert "ProtectedData.Protect" in s
    assert "ProtectedData.Unprotect" in s
    assert "DataProtectionScope.CurrentUser" in s
    assert "Registry.CurrentUser" in s


def test_activation_uses_rsa_signature_and_machine_bound_payload_not_superkey():
    s=read("src/MiningVolume.Plugin2023/Services/LicenseService.cs")
    assert "rsa.VerifyData(" in s
    assert 'CryptoConfig.MapNameToOID("SHA256")' in s
    assert "string.Equals(payload.MachineId, MachineId" in s
    assert 'payload.Mode != "INTERNAL" && payload.Mode != "UNIT"' in s
    assert "private issuer key" in s
    assert "RsaPublicKeyXml = \"\"" in s
    assert "RSAPrivateKey" not in s


def test_license_page_entry_and_calculation_gates():
    e=read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    n=read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    p=read("src/MiningVolume.Plugin2023/UI/LicensePage.cs")
    assert 'CommandMethod("MV_LICENSE"' in e
    assert "LicenseService.GetStatus()" in e
    assert "new LicensePage()" in n
    assert "SAO CHÉP MÃ MÁY" in p
    assert "KÍCH HOẠT" in p
    for file in ["UI/ModelPage.cs","UI/SectionPage.cs","UI/VolumePage.cs",
                 "UI/GridPage.cs","UI/ExportPage.cs"]:
        assert "LicenseUiGuard.Check()" in read("src/MiningVolume.Plugin2023/"+file)
