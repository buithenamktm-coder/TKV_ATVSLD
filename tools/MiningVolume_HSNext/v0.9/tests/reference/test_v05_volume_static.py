from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def read(rel): return (ROOT/rel).read_text(encoding='utf-8')


def test_volume_core_is_real_and_no_placeholder():
    s=read('src/MiningVolume.Core/Volumes/SectionVolumeCalculator.cs')
    b=read('src/MiningVolume.Core/Volumes/SectionBandAreaCalculator.cs')
    assert 'AreaDifferenceThreshold' in read('src/MiningVolume.Core/Volumes/VolumeModels.cs')
    assert 'SelectFormula' in s
    assert 'VolumeFormulaKind.Frustum' in s
    assert 'VolumeFormulaKind.Pyramid' in s
    assert 'SectionBandAreaCalculator' in s
    assert 'ClipAbove' in b and 'ClipBelow' in b and 'Area(' in b
    assert 'TODO' not in s+b


def test_volume_page_has_detail_and_level_tables():
    s=read('src/MiningVolume.Plugin2023/UI/VolumePage.cs')
    assert 'Chi tiết giữa các mặt cắt' in s
    assert 'Tổng hợp theo tầng' in s
    assert 'Fđ giữa' in s and 'V đào, m³' in s and 'V đắp, m³' in s
    assert 'TÍNH / CẬP NHẬT KHỐI LƯỢNG' in s
    assert 'Task.Run' in s


def test_volume_result_is_invalidated_when_surface_or_sections_change():
    a=read('src/MiningVolume.Plugin2023/Services/SurfaceWorkflowService.cs')
    b=read('src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs')
    assert 'VolumeResult = null' in a
    assert b.count('VolumeResult = null') >= 4


def test_project_state_stores_volume_result():
    s=read('src/MiningVolume.Plugin2023/Services/ProjectState.cs')
    assert 'VolumeResult VolumeResult' in s


def test_bundle_stays_locked_to_autocad_2023():
    s=read('bundle/MiningVolume2023.bundle/PackageContents.xml')
    assert 'AppVersion=' in s
    assert 'SeriesMin="R24.2"' in s and 'SeriesMax="R24.2"' in s


def test_level_parameters_invalidate_stale_volume_immediately():
    s=read('src/MiningVolume.Plugin2023/UI/SectionPage.cs')
    assert '_levelStep.ValueChanged += VolumeParameterChanged' in s
    assert '_fromLevel.ValueChanged += VolumeParameterChanged' in s
    assert '_toLevel.ValueChanged += VolumeParameterChanged' in s
    assert 'st.VolumeResult = null' in s

def test_v1_adaptive_volume_rule_is_visible_in_ui_and_workflow():
    calc=read('src/MiningVolume.Core/Volumes/SectionVolumeCalculator.cs')
    ui=read('src/MiningVolume.Plugin2023/UI/VolumePage.cs')
    wf=read('src/MiningVolume.Plugin2023/Services/VolumeWorkflowService.cs')
    assert 'relativeDifference <= areaDifferenceThreshold' in calc
    assert 'Math.Sqrt(a1 * a2)' in calc
    assert 'Hình chóp cụt' in ui and 'chênh ≤ 40%' in ui
    assert 'AreaDifferenceThreshold = 0.40' in wf
