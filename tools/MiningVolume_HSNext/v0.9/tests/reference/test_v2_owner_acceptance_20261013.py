"""Owner-reported 2026-10-13 QA blockers; static guards supplement
the C# geometry and XLSX package integration tests. AutoCAD interactive
acceptance on the owner's DWG remains separate.
"""
from pathlib import Path

R = Path(__file__).resolve().parents[2]


def source(p):
    return (R / ("src/" + p)).read_text(encoding="utf-8")


def test_3d_upright_face_clip_and_cad_agree_on_all_vertical_edge_orders():
    core = source("MiningVolume.Core/Surface/VerticalFaceBuilder.cs")
    cad = source("MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    assert "public static bool IsValidCadFace(Triangle3 face)" in core
    for text in ("VerticalPair(face.A, face.B, face.C)",
                 "VerticalPair(face.B, face.C, face.A)",
                 "VerticalPair(face.C, face.A, face.B)"):
        assert text in core
    assert "VerticalFaceBuilder.IsValidCadFace(t)" in cad
    assert "Mặt đứng 3D không phải tam giác hợp lệ" in cad
    assert "VerticalFaceBuilder.ClipToBoundary" in source(
        "MiningVolume.Surface/ConformingTinBuilder.cs")


def test_excel_outside_tkv_never_gets_logo_and_missing_asset_not_fatal():
    report = source("MiningVolume.Core/Reporting/SpreadsheetReport.cs")
    export = source("MiningVolume.Plugin2023/Services/ExportWorkflowService.cs")
    logo = source("MiningVolume.Plugin2023/Services/XlsxBrandingInjector.cs")
    runner = (R / "tests/core/run_core.py").read_text(encoding="utf-8")
    assert "public bool IncludeImsatLogo { get; set; } = true;" in report
    assert "report.IncludeImsatLogo = !outside;" in export
    assert "if (report.IncludeImsatLogo)" in export
    assert "SimpleXlsxWriter.Write(staging, report, cancellationToken);" in export
    assert "if (!report.IncludeImsatLogo) return;" in logo
    assert 'GetManifestResourceStream("MiningVolume2023.Assets.imsat_logo_64.png")' in logo
    assert "if (logo == null) return;" in logo
    assert "rf14_embedded_logo_missing_file_and_outside_tkv_both_export" in (
        R / "tests/core/FinalAuditRegression.cs").read_text(encoding="utf-8")
    assert 'MiningVolume2023.Assets.imsat_logo_64.png' in runner


def test_cad_table_matches_grouped_reference_but_never_invents_road_quantities():
    cad = source("MiningVolume.Plugin2023/Services/CadVolumeTableService.cs")
    assert 'BẢNG TỔNG HỢP KHỐI LƯỢNG THEO MẶT CẮT' in cad
    assert 'interval.MergeCells(CellRange.Create(interval, 0, 0, 0, columns - 1))' in cad
    assert 'interval.MergeCells(CellRange.Create(interval, 1, 4, 1, 7))' in cad
    assert 'interval.MergeCells(CellRange.Create(interval, 1, 8, 1, 10))' in cad
    assert 'interval.Cells[1, 4].TextString = "Diện tích (m²)"' in cad
    assert 'interval.Cells[1, 8].TextString = "Khối lượng (m³)"' in cad
    assert 'interval.Cells[totalRow, 0].TextString = "Tổng"' in cad
    assert 'interval.Cells[totalRow, 10].TextString = F(result.NetVolume)' in cad
    assert 'FileName = "arial.ttf"' in cad
    assert 'new Autodesk.AutoCAD.DatabaseServices.Table' in cad


def test_ui_fix_header_gap_compact_data_types_and_button_case():
    chrome = source("MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    data = source("MiningVolume.Plugin2023/UI/DataPage.cs")
    theme = source("MiningVolume.Plugin2023/UI/UiTheme.cs")
    mineral = source("MiningVolume.Plugin2023/UI/MineralPage.cs")
    # Header title/subtitle are owner-painted: WinForms transparent labels
    # previously left solid rectangles inside AutoCAD's palette host.
    assert 'header.Paint += (sender, args)' in chrome
    assert 'args.Graphics.DrawString(title, big, white, left, 12)' in chrome
    assert 'const string title = "IMSAT VOLUME";' in chrome
    assert "kv.Value.FlatAppearance.BorderSize = 0;" in chrome
    assert "HorizontalScrollbar = false" in data
    assert "ColumnWidth = 148" in data
    assert "button.Text = SentenceCaseButton(button.Text);" in theme
    assert "button.FlatAppearance.BorderSize = 0;" in theme
    assert "Math.Max(5, button.Margin.Left)" in theme
    assert "Height = 212" in mineral
    assert "AutoScroll = true" in mineral
    sections = source("MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert "top.RowStyles.Add(new RowStyle(SizeType.Absolute, i < 2 ? 54F : 35F))" in sections
    assert "_parameterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, k < 4 ? 48F : 35F))" in sections
    assert "float preferred = 440F;" in sections
    assert "Bản quyền IMSAT VOLUME" in source("MiningVolume.Plugin2023/UI/LicensePage.cs")


def test_professional_formula_quick_section_and_custom_scales():
    panel = source("MiningVolume.Plugin2023/UI/ProfessionalFormulaPanel.cs")
    page = source("MiningVolume.Plugin2023/UI/SectionPage.cs")
    quick = source("MiningVolume.Core/Sections/QuickSectionProfileBuilder.cs")
    assert 'new Font("Cambria Math", 12F' in panel
    assert "e.Graphics.DrawLine(pen" in panel
    assert "new ProfessionalFormulaPanel()" in source("MiningVolume.Plugin2023/UI/VolumePage.cs")
    assert '_hScale.Enabled = custom;' in page and '_vScale.Enabled = custom;' in page
    assert 'quick.Name = "btnQuickSections"' in page
    assert 'new SectionSpan(0, total)' in quick
    assert "state.SectionSystem = " not in page.split(
        "private async void DrawQuickSections(", 1)[1].split(
        "private async void UpdateEditedProfiles(", 1)[0]


def test_export_tip_removed_and_excel_logic_preserved():
    page = source("MiningVolume.Plugin2023/UI/ExportPage.cs")
    assert "Excel: Arial 12 • bảng đóng khung" not in page
    assert "ExportWorkflowService.WriteValidatedXlsxAsync" in page
    assert "CadVolumeTableService.Insert" in page
