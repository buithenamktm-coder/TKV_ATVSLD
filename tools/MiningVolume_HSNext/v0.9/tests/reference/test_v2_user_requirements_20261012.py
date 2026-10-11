"""Regression guards for owner-approved V2.0 workflow additions.
Static checks supplement C# CoreRegression; never replace real AutoCAD acceptance.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(relative):
    return (ROOT / relative).read_text(encoding="utf-8")


def test_palette_hides_for_all_interactive_cad_prompts_and_returns_on_esc():
    selection = read("src/MiningVolume.Plugin2023/Services/SelectionService.cs")
    assert "internal sealed class CadSelectionPaletteScope : IDisposable" in selection
    assert "using (CadSelectionPaletteScope.Hide())" in selection
    assert selection.count("CadSelectionPaletteScope.Hide()") >= 6
    assert "if (!_restore" in selection
    assert "ReferenceEquals(Application.DocumentManager.MdiActiveDocument, _document)" in selection
    assert "try { _palette.Visible = true; }" in selection


def test_dock_left_palette_uses_compact_vertical_scroll_layout():
    palette = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    entry = read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert "bool compact = ClientSize.Width < 650;" in palette
    assert "_compactHeader.Visible = compact;" in palette
    assert "_sidebar.Visible = !compact;" in palette
    assert "AutoScroll = true" in palette
    assert "MinimumSize = new System.Drawing.Size(360, 450)" in entry


def test_user_edited_sections_feed_area_volume_and_dwg_restore():
    cad = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    reader = read("src/MiningVolume.Plugin2023/Services/EditableSectionCadService.cs")
    page = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    restore = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    core = read("src/MiningVolume.Core/Sections/EditedSectionProfileBuilder.cs")
    assert "EditRegApp" in cad and "StampSection(a, section" in cad
    assert "GetXDataForApplication(SectionCadRenderer.EditRegApp)" in reader
    assert "EditedSectionProfileBuilder.Rebuild(orig, ht, tk)" in reader
    assert "UpdateAreaLabels" in reader
    assert "CẬP NHẬT MẶT CẮT" in page
    assert "VolumeWorkflowService.CalculateCore(" in page
    assert "SectionWorkflowService.CommitProfiles(edited)" in page
    assert "VolumeWorkflowService.Commit(volume" in page
    assert "EditableSectionCadService.ReadEditedProfiles(" in restore
    assert "không được thay đổi" not in core.lower() or "không" in core.lower()


def test_project_unit_and_period_saved_to_dwg_and_excel():
    p = read("src/MiningVolume.Plugin2023/UI/ProjectPage.cs")
    state = read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    snap = read("src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs")
    persistence = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    excel = read("src/MiningVolume.Plugin2023/Services/ExportWorkflowService.cs")
    for field in ("MiningUnitName", "MiningUnitGroup", "VolumePeriodType", "VolumePeriodYear"):
        assert field in state and field in snap and field in persistence
    for label in ("Tháng", "Quý", "Năm"):
        assert label in state
    assert "DropDownStyle = ComboBoxStyle.DropDown" in p
    assert "Đơn vị khai thác" in excel
    assert "Kỳ tính khối lượng" in excel


def test_excel_keeps_calculation_time_distinct_from_export_time_and_adds_signature():
    output = read("src/MiningVolume.Plugin2023/Services/ExportWorkflowService.cs")
    volume = read("src/MiningVolume.Plugin2023/UI/VolumePage.cs")
    assert "Stopwatch.StartNew()" in volume
    assert "VolumeCalculatedUtc = DateTime.UtcNow" in volume
    assert 'AddKV(s, "Thời gian xuất Excel"' in output
    assert 'AddKV(s, "Thời điểm tính khối lượng"' in output
    assert "NGƯỜI TÍNH TOÁN" in output
    assert "NGƯỜI KIỂM TRA" in output
    assert 'ReportCell.Formula2("=C5-C6"' in output


def test_excel_uses_owner_ariel_border_no_background_style_and_correct_contact():
    # The name used in the source is Arial, not 'Ariel'.
    writer = read("src/MiningVolume.Core/Reporting/SpreadsheetReport.cs")
    excel = read("src/MiningVolume.Plugin2023/Services/ExportWorkflowService.cs")
    assert 'x.WriteAttributeString("val", "12")' in writer
    assert 'x.WriteAttributeString("val", "Arial")' in writer
    assert 'Border(x, true)' in writer
    assert '"patternType", "none"' in writer
    assert 'FixedDeveloperContact = "ĐT/Zalo: 0967280686"' in excel


def test_setup_uninstaller_preserves_user_projects_and_removes_addin():
    inst = read("installer/main.go")
    qa = read("installer/build_v2_qa.ps1")
    assert 'if hasArg(args, "--uninstall")' in inst
    assert "os.RemoveAll(target)" in inst
    assert '"reg", "delete", uninstallKey' in inst
    assert "cleanupInstalledSetupLater" in inst
    assert "Gỡ IMSAT VOLUME.lnk" in inst
    assert "DWG, Project" in inst
    assert "main.version=2.0.0" in qa
    assert ".Replace('Uninstall" not in qa  # Same bundle uses one upgrade/uninstall registration
