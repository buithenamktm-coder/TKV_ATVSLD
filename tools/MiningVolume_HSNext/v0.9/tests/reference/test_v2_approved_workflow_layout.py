from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(relative):
    return (ROOT / relative).read_text(encoding="utf-8")


def test_three_main_groups_and_exactly_two_default_volume_methods():
    main = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    assert 'AddNav(nav, AppPage.Model, "1", "Dữ liệu")' in main
    assert 'AddNav(nav, AppPage.Volume, "2", "Tính toán")' in main
    assert 'AddNav(nav, AppPage.Export, "3", "Kết quả")' in main
    assert '[AppPage.QuickSection] = new SectionPage(true)' in main
    assert '[AppPage.License] = new ContactInfoPage()' in main
    assert 'case AppPage.Volume: return new[] { AppPage.Section, AppPage.Grid };' in main
    assert 'case AppPage.Model: return new[] { AppPage.Data, AppPage.Project };' in main
    assert 'AppPage.Volume, AppPage.QuickSection };' in main  # minerals reserved for later version


def test_model_contains_input_then_tin_management():
    p = read("src/MiningVolume.Plugin2023/UI/ModelWorkflowPage.cs")
    main = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    assert 'new DataPage' in p and 'new ModelPage' in p
    assert p.index("new DataPage") < p.index("new ModelPage")
    assert 'ShowPage(pages[0])' in main


def test_independent_quick_sections_and_volume_result_navigation():
    section = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    main = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    assert 'if (_quickOnly) primary.Controls.Add(quick);' in section
    assert 'new SectionPage(false, ShowPage)' in main
    assert 'new SectionPage(true)' in main
    assert 'VolumeWorkflowService.Commit(volume, from, to, step);' in section


def test_supported_cad_compile_matrix_is_2020_through_2026():
    workflow = (ROOT.parents[2] / ".github/workflows/imsat-cad-2025-2026-compat-ci.yml").read_text(encoding="utf-8")
    for year in range(2015, 2020):
        assert "ACAD" + str(year) + "_" not in workflow
    for year in (2020, 2021, 2022, 2024, 2025, 2026):
        assert "ACAD" + str(year) + "_" in workflow
    # AutoCAD 2023 is compiled in the dedicated add-in workflow.
    primary = (ROOT.parents[2] / ".github/workflows/build-miningvolume-autocad2023.yml").read_text(encoding="utf-8")
    assert "Build AutoCAD 2023 add-in" in primary
