from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT/rel).read_text(encoding="utf-8")


def test_trial_is_15_consecutive_days_and_clock_rollback_blocks_calculation():
    s=read("src/MiningVolume.Plugin2023/Services/LicenseService.cs")
    assert "trial.FirstUseUtc.AddDays(15)" in s
    assert "now < last.AddHours(-1)" in s
    assert "LicenseState.ClockRollback" in s
    assert "LicenseState.TrialExpired" in s
    assert "LicenseState.StorageError" in s
    assert "ProtectedData.Protect" in s
    assert "ProtectedData.Unprotect" in s
    assert "DataProtectionScope.CurrentUser" in s
    assert "Registry.CurrentUser" in s


def test_activation_uses_five_fixed_offline_passes_without_machine_binding():
    import re
    s=read("src/MiningVolume.Plugin2023/Services/LicenseService.cs")
    assert len(re.findall(r'"[A-F0-9]{64}"', s)) == 5
    assert "TierMonths = { 1, 3, 6, 12, 0 }" in s
    assert "RSACryptoServiceProvider" not in s
    assert "public static string MachineId" not in s
    assert "IdentifyPass(pass.Trim())" in s
    assert "MergeActivation(first as ActivationLedger, second as ActivationLedger)" in s
    assert "Nhập lại không gia hạn" in s


def test_license_page_entry_and_calculation_gates():
    e=read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    n=read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    p=read("src/MiningVolume.Plugin2023/UI/LicensePage.cs")
    assert 'CommandMethod("MV_LICENSE"' in e
    assert "LicenseService.GetStatus()" in e
    assert "new ContactInfoPage()" in n
    assert "new LicensePage" in read("src/MiningVolume.Plugin2023/UI/ContactInfoPage.cs")
    assert "SAO CHÉP MÃ MÁY" not in p
    assert "UseSystemPasswordChar = true" in p
    assert "Kích hoạt" in p
    for file in ["UI/ModelPage.cs","UI/SectionPage.cs","UI/VolumePage.cs",
                 "UI/GridPage.cs","UI/ExportPage.cs"]:
        assert "LicenseUiGuard.Check()" in read("src/MiningVolume.Plugin2023/"+file)
