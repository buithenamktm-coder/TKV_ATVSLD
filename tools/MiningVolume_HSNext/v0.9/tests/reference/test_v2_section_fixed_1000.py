from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(name):
    return (ROOT / name).read_text(encoding="utf-8")


def test_both_section_axes_fixed_1000_even_in_custom_mode():
    ui = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    service = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    renderer = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    assert 'Add(top, 8, "Tỷ lệ ngang 1/ (cố định)", _hScale' in ui
    assert 'Add(top, 9, "Tỷ lệ đứng 1/ (cố định)", _vScale' in ui
    assert "_hScale.Enabled = false" in ui
    assert "_vScale.Enabled = false" in ui
    assert "st.HorizontalScale = 1000;" in ui
    assert "st.VerticalScale = 1000;" in ui
    assert "1000.0,\n                    1000.0,\n                    (double)_axisStep.Value" in ui
    assert "Math.Abs(horizontalScale - 1000.0) > 1e-9" in service
    assert "Math.Abs(verticalScale - 1000.0) > 1e-9" in service
    assert "Math.Abs(horizontalScale - 1000.0) > 1e-9" in renderer
    assert "Math.Abs(verticalScale - 1000.0) > 1e-9" in renderer


def test_user_can_change_other_section_calculation_and_plot_parameters():
    ui = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    state = read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    assert '_parameterMode.Items.AddRange(new object[]' in ui
    assert '"TT34/2018/TT-BCT", "Tùy chỉnh"' in ui
    assert "Add(top, 3, \"Khoảng cách mặt cắt, m\", _spacing" in ui
    assert "Add(top, 4, \"Bước chia trục Z, m\", _axisStep" in ui
    assert "Add(top, 5, \"Mức chia tầng tính KL, m\", _levelStep" in ui
    assert "Add(top, 6, \"Tính từ mức\", _fromLevel" in ui
    assert "Add(top, 7, \"Đến mức\", _toLevel" in ui
    assert "_spacing = Num(20, 0.1M, 10000)" in ui
    assert "_axisStep = Num(20, 0.1M, 1000)" in ui
    assert "(_spacing.Value > 20 || _axisStep.Value != 20)" in ui
    assert "public double SectionAxisElevationInterval { get; set; } = 20.0;" in state
    assert "public double LevelStep { get; set; } = 5.0;" in state
    assert "HorizontalScale / VerticalScale" not in ui


def test_old_dwg_preserves_user_defined_other_settings_but_normalizes_scales():
    project = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    snap = read("src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs")
    assert "SectionParameterMode = state.SectionParameterMode" in project
    assert "SectionAxisElevationInterval = state.SectionAxisElevationInterval" in project
    assert "snap.SectionParameterMode == null" in project
    assert "state.HorizontalScale = 1000.0;" in project
    assert "state.VerticalScale = 1000.0;" in project
    assert "state.SectionMapScale = 1000;" in project
    assert "Project cũ dùng tỷ lệ mặt cắt khác 1:1.000" in project
    assert "DataMember(Order = 44)] public double SectionAxisElevationInterval" in snap


def test_elevation_axis_step_is_independent_from_earthwork_level_step():
    ui = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    section = ui.split("SectionWorkflowService.DrawProfiles(", 1)[1].split("drawWatch.Stop();", 1)[0]
    assert "(double)_axisStep.Value" in section
    assert "(double)_levelStep.Value" not in section
    assert "st.LevelStep = (double)_levelStep.Value;" in ui
    assert "st.SectionAxisElevationInterval = (double)_axisStep.Value;" in ui


def test_fixed_1000_scale_and_user_parameters_are_in_report_metadata():
    volume = read("src/MiningVolume.Plugin2023/Services/ExportWorkflowService.cs")
    grid = read("src/MiningVolume.Plugin2023/Services/GridVolumeExcelReportBuilder.cs")
    mineral = read("src/MiningVolume.Plugin2023/Services/MineralVolumeExcelReportBuilder.cs")
    for content in [volume, grid, mineral]:
        assert '"Tỷ lệ mặt cắt (ngang / đứng)"' in content
        assert '"1:1.000 / 1:1.000"' in content
        assert '"Bước chia trục Z, m"' in content
        assert "SectionAxisElevationInterval" in content


def test_changing_station_spacing_invalidates_old_section_geometry_and_volume():
    ui = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    part = ui.split("private void GeneralParameterChanged(", 1)[1].split(
        "private void SaveParameters(", 1
    )[0]
    assert "Math.Abs(st.SectionSpacing - newSpacing) > 1e-9" in part
    assert "if (geometryChanged)" in part
    for statement in [
        "st.SectionSystem = null;",
        "st.SectionProfiles.Clear();",
        "st.VolumeResult = null;",
        'Bấm XEM TRƯỚC TUYẾN',
        "if (geometryChanged) st.NotifyChanged();",
    ]:
        assert statement in part
    assert part.index("st.SectionSystem = null;") < part.index("st.SectionSpacing = newSpacing;")
    # Display-only parameters (Z ticks / CAD layout gap) must not
    # reset the official volume or the sampled section system.
    assert "st.SectionAxisElevationInterval = (double)_axisStep.Value;" in part
    assert "st.SectionLayoutGap = (double)_layoutGap.Value;" in part
    assert "if (geometryChanged)" in part
    other = ui.split("private void VolumeParameterChanged(", 1)[1].split(
        "private void ParameterModeChanged(", 1
    )[0]
    assert "st.VolumeResult = null;" in other
