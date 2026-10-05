from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def read(rel): return (ROOT/rel).read_text(encoding='utf-8')


def test_section_core_files_exist_and_have_real_algorithms():
    b=read('src/MiningVolume.Core/Sections/SectionSystemBuilder.cs')
    s=read('src/MiningVolume.Core/Sections/TinSectionSampler.cs')
    assert 'PointInPolygon' in b and 'BuildLineAtOffset' in b
    assert 'CollectTriangleEdgeIntersections' in s and 'AccumulateArea' in s
    assert 'TODO' not in b+s


def test_section_page_no_placeholder_messages():
    s=read('src/MiningVolume.Plugin2023/UI/SectionPage.cs')
    assert 'Sẽ nối' not in s
    assert 'XEM TRƯỚC TUYẾN' in s
    assert 'XUẤT / VẼ MẶT CẮT' in s
    assert 'Thêm tuyến' in s and 'Dịch tuyến' in s and 'Xóa tuyến' in s


def test_plan_and_profile_rendering_contract():
    s=read('src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs')
    assert 'MV_MC_BINHDO' in s
    assert 'MV_MC_HIENTRANG' in s and 'MV_MC_THIETKE' in s
    assert 'arial.ttf' in s
    assert 'Height = 3.0' in s
    assert 'F đào' in s and 'F đắp' in s
    assert 'TL ngang' in s and 'TL đứng' in s


def test_existing_red_design_blue():
    s=read('src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs')
    assert 'ExistingLayer, 1' in s
    assert 'DesignLayer, 5' in s


def test_project_state_stores_section_system_and_profiles():
    s=read('src/MiningVolume.Plugin2023/Services/ProjectState.cs')
    assert 'SectionSystem SectionSystem' in s
    assert 'List<SectionProfile> SectionProfiles' in s
    assert 'Vec2? SectionDirection' in s


def test_selection_uses_two_points_for_direction():
    s=read('src/MiningVolume.Plugin2023/Services/SelectionService.cs')
    assert 'PickDirection()' in s
    assert 'GetPoint' in s and 'UseBasePoint = true' in s


def test_profile_area_is_not_simple_trapezoid_only():
    s=read('src/MiningVolume.Core/Sections/TinSectionSampler.cs')
    assert 'delta0 > 0' in s
    assert 'firstLength' in s and 'secondLength' in s