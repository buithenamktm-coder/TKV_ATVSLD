from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]

def read(p):
    return (ROOT/p).read_text(encoding="utf-8")


def test_seam_tables_are_real_editable_autocad_tables_not_images():
    src=read("src/MiningVolume.Plugin2023/Services/MineralCadTableService.cs")
    page=read("src/MiningVolume.Plugin2023/UI/MineralPage.cs")
    assert "var tab = new Table" in src
    assert "tab.SetRowHeight" in src
    assert "tr.AddNewlyCreatedDBObject(t,true);" in src
    assert "MV_KHOANGSAN_BANG" in src
    assert "Tổng đào (m³)" in src
    assert "Đất đá bóc (m³)" in src
    assert "Hệ số bóc (m³/m³)" in src
    assert "Hệ số bóc (m³/tấn)" in src
    assert "XUẤT BẢNG LÊN CAD" in page
    assert "MineralVolumeWorkflowService.Recalculate();" in page
    assert "MineralCadTableService.Insert" in page
    assert "LicenseUiGuard.Check()" in page


def test_combined_v2_license_and_mineral_are_visible_in_same_palette():
    p=read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    entry=read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert "new MineralPage()" in p
    assert "new ContactInfoPage()" in p
    assert "new LicensePage()" in read("src/MiningVolume.Plugin2023/UI/ContactInfoPage.cs")
    assert 'CommandMethod("MV_MINERAL"' in entry
    assert 'CommandMethod("MV_LICENSE"' in entry
    assert "LicenseService.GetStatus();" in entry
