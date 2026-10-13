from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(name):
    return (ROOT / name).read_text(encoding="utf-8")


def test_tt34_defaults_1000_but_custom_scales_are_editable():
    ui = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    service = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    renderer = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    assert 'Add(top, 8, "Tỷ lệ ngang 1/", _hScale' in ui
    assert 'Add(top, 9, "Tỷ lệ đứng 1/", _vScale' in ui
    assert "UpdateScaleAvailability()" in ui
    assert "_hScale.Enabled = custom;" in ui
    assert "_vScale.Enabled = custom;" in ui
    assert "st.HorizontalScale = (double)_hScale.Value;" in ui
    assert "st.VerticalScale = (double)_vScale.Value;" in ui
    assert "(double)_hScale.Value,\n                    (double)_vScale.Value," in ui
    assert "Math.Abs(horizontalScale - 1000.0) > 1e-9" in service
    assert "Math.Abs(verticalScale - 1000.0) > 1e-9" in service
    assert "double vExag = horizontalScale / verticalScale;" in renderer


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


def test_old_dwg_preserves_custom_scales_and_tt34_defaults():
    project = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    snap = read("src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs")
    assert "SectionParameterMode = state.SectionParameterMode" in project
    assert "SectionAxisElevationInterval = state.SectionAxisElevationInterval" in project
    assert "snap.SectionParameterMode == null" in project
    assert 'state.HorizontalScale = state.SectionParameterMode == "Tùy chỉnh"' in project
    assert 'state.VerticalScale = state.SectionParameterMode == "Tùy chỉnh"' in project
    assert "PositiveOr(snap.HorizontalScale, 1000.0)" in project
    assert "PositiveOr(snap.VerticalScale, 1000.0)" in project
    assert "state.SectionMapScale = 1000;" in project
    assert "DataMember(Order = 44)] public double SectionAxisElevationInterval" in snap


def test_elevation_axis_step_is_independent_from_earthwork_level_step():
    ui = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    section = ui.split("SectionWorkflowService.DrawProfiles(", 1)[1].split("drawWatch.Stop();", 1)[0]
    assert "(double)_axisStep.Value" in section
    assert "(double)_levelStep.Value" not in section
    assert "st.LevelStep = (double)_levelStep.Value;" in ui
    assert "st.SectionAxisElevationInterval = (double)_axisStep.Value;" in ui


def test_actual_custom_scales_and_user_parameters_are_in_report_metadata():
    volume = read("src/MiningVolume.Plugin2023/Services/ExportWorkflowService.cs")
    grid = read("src/MiningVolume.Plugin2023/Services/GridVolumeExcelReportBuilder.cs")
    mineral = read("src/MiningVolume.Plugin2023/Services/MineralVolumeExcelReportBuilder.cs")
    for content in [volume, grid, mineral]:
        assert '"Tỷ lệ mặt cắt (ngang / đứng)"' in content
        assert 'HorizontalScale.ToString("N0")' in content
        assert 'VerticalScale.ToString("N0")' in content
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


def test_manual_station_edits_cannot_falsely_retain_tt34_spacing_preset():
    workflow = read("src/MiningVolume.Plugin2023/Services/SectionWorkflowService.cs")
    assert workflow.count("ReconcileManualStationLimit(state, candidate);") == 3
    assert "ReconcileManualStationLimit(state, system);" in workflow
    policy = workflow.split("private static void ReconcileManualStationLimit(", 1)[1].split(
        "private static void DrawPreview(", 1
    )[0]
    assert '"TT34/2018/TT-BCT"' in policy
    assert "const double maxStationGap = 20.0;" in policy
    assert "boundaryOffsets.Min()" in policy and "boundaryOffsets.Max()" in policy
    assert "offsets[i] - offsets[i - 1] > maxStationGap + tolerance" in policy
    assert 'state.SectionParameterMode = "Tùy chỉnh";' in policy
    assert workflow.index("DrawPreview(candidate);") < workflow.index(
        "ReconcileManualStationLimit(state, candidate);"
    )
    assert workflow.index("ReconcileManualStationLimit(state, candidate);") < workflow.index(
        "state.SectionSystem = candidate;"
    )


def test_excel_distinguishes_requested_station_step_from_actual_manual_gap():
    report = read("src/MiningVolume.Plugin2023/Services/ExportWorkflowService.cs")
    assert '"Bước tuyến thiết lập, m"' in report
    assert '"Khoảng cách tuyến lớn nhất thực tế, m"' in report
    method = report.split("private static double? MaxActualSectionGap(", 1)[1].split(
        "private static ReportSheet BuildSourceXyzSheet(", 1
    )[0]
    assert "system.Lines.Select(x => x.Offset).OrderBy(x => x).ToArray();" in method
    assert "projected.Min()" in method and "projected.Max()" in method
    assert "stations[i] - stations[i - 1]" in method
    assert "return Math.Max(0.0, gap);" in method
