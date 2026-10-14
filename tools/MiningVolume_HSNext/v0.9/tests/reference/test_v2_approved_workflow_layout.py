from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(relative):
    return (ROOT / relative).read_text(encoding="utf-8")


def test_five_main_groups_and_exactly_two_default_volume_methods():
    main = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    assert 'AddNav(nav, AppPage.Project, "1", "Dự án")' in main
    assert 'AddNav(nav, AppPage.Model, "2", "Mô hình")' in main
    assert 'AddNav(nav, AppPage.Volume, "3", "Tính khối lượng")' in main
    assert 'AddNav(nav, AppPage.QuickSection, "4", "Vẽ nhanh mặt cắt")' in main
    assert 'AddNav(nav, AppPage.License, "5", "Thông tin liên hệ")' in main
    assert 'case AppPage.Volume: return new[] { AppPage.Section, AppPage.Grid };' in main
    assert 'case AppPage.Model: return new[] { AppPage.Model };' in main
    assert 'AppPage.Mineral, AppPage.Export' in main  # preserved behind advanced view


def test_model_contains_input_then_tin_management():
    p = read("src/MiningVolume.Plugin2023/UI/ModelWorkflowPage.cs")
    main = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    assert 'new DataPage' in p and 'new ModelPage' in p
    assert p.index("new DataPage") < p.index("new ModelPage")
    assert 'ShowPage(group == AppPage.Model ? AppPage.Data : pages[0])' in main


def test_independent_quick_sections_and_volume_result_navigation():
    section = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    main = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    assert 'if (_quickOnly)\n                primary.Controls.Add(quick, 2, 0);' in section
    assert 'new SectionPage(false, ShowPage)' in main
    assert 'new SectionPage(true)' in main
    assert '_navigate(AppPage.Volume)' in section
