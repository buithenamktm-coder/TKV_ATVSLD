from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def test_ribbon_is_direct_gui_not_command_string_dispatch():
    s = read("src/MiningVolume.Plugin2023/RibbonBuilder.cs")
    assert "SendStringToExecute" not in s
    assert "EntryPoint.Open(AppPage.Data)" in s
    assert "EntryPoint.Open(AppPage.Model)" in s
    assert "EntryPoint.Open(AppPage.Section)" in s
    assert "EntryPoint.Open(AppPage.Volume)" in s
    assert "EntryPoint.Open(AppPage.Export)" in s

def test_user_pages_have_real_controls():
    data = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    model = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    section = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    volume = read("src/MiningVolume.Plugin2023/UI/VolumePage.cs")
    export = read("src/MiningVolume.Plugin2023/UI/ExportPage.cs")
    assert "ComboBox" in data and "LoadModel" in data
    assert "DataGridView" in model and "BuildTinAsync" in model
    assert "NumericUpDown" in section and "Preview" in section and "BuildProfiles" in section
    assert "TÍNH / CẬP NHẬT KHỐI LƯỢNG" in volume
    assert "SaveFileDialog" in export and "XUẤT EXCEL" in export

def test_cli_commands_are_not_used_by_ribbon():
    ribbon = read("src/MiningVolume.Plugin2023/RibbonBuilder.cs")
    for cmd in ["MV_DATA", "MV_MODEL", "MV_SECTION", "MV_VOLUME", "MV_EXPORT", "MVOPEN"]:
        assert cmd not in ribbon
