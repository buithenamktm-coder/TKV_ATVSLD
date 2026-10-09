from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPORT = ROOT / "src/MiningVolume.Core/Reporting/SpreadsheetReport.cs"
EXPORT_SERVICE = ROOT / "src/MiningVolume.Plugin2023/Services/ExportWorkflowService.cs"
EXPORT_UI = ROOT / "src/MiningVolume.Plugin2023/UI/ExportPage.cs"


def read(p):
    return p.read_text(encoding="utf-8")


def test_writer_has_xlsx_package_parts_and_no_excel_com():
    s = read(REPORT)
    for token in [
        "[Content_Types].xml", "xl/workbook.xml", "xl/styles.xml",
        "xl/worksheets/sheet", "ZipArchive", "Arial", "thin"
    ]:
        assert token in s
    assert "Microsoft.Office.Interop.Excel" not in s
    assert "Excel.Application" not in s


def test_writer_uses_arial_12_and_no_background_fill():
    s = read(REPORT)
    assert 'x.WriteAttributeString("val", "12")' in s
    assert 'x.WriteAttributeString("val", "Arial")' in s
    assert 'patternType", "none"' in s
    assert 'patternType", "gray125"' in s


def test_export_contains_required_sheets():
    s = read(EXPORT_SERVICE)
    required = [
        'NewSheet("Thông tin"',
        'NewSheet("Dữ liệu XYZ"',
        'NewSheet("Dữ liệu mặt cắt"',
        'NewSheet("Thông số mặt cắt"',
        'NewSheet("Khối lượng chi tiết"',
        'NewSheet("Tổng hợp theo tầng"',
        'NewSheet("Tổng khối"',
        'NewSheet("Cảnh báo"',
    ]
    for token in required:
        assert token in s


def test_export_has_developer_and_contact_fields():
    s = read(EXPORT_SERVICE)
    ui = read(EXPORT_UI)
    assert "DeveloperName" in s and "DeveloperContact" in s
    assert "Người phát triển phần mềm" in ui
    assert "Địa chỉ liên hệ / email / điện thoại" in ui


def test_export_ui_requires_volume_result_and_uses_save_dialog():
    s = read(EXPORT_UI)
    assert "SaveFileDialog" in s
    assert "st.VolumeResult != null" in s
    assert "st.SectionProfiles.Count > 0" in s
    assert "Task.Run" in s


def test_report_control_totals_are_present():
    s = read(EXPORT_SERVICE)
    assert "Sai lệch kiểm soát đào" in s
    assert "Sai lệch kiểm soát đắp" in s
    assert 'ReportCell.Formula3("=C5-C8")' in s
    assert 'ReportCell.Formula3("=C6-C9")' in s
    assert "ReportCell.Formula2" in s
    assert "SumFormula" in s


def test_volume_detail_exports_mid_section_and_formula():
    s = read(EXPORT_SERVICE)
    for token in ["CutAreaMid", "FillAreaMid", "FormulaName(x.CutFormula)", "FormulaName(x.FillFormula)"]:
        assert token in s

def test_xlsx_writer_emits_autofilter_before_mergecells_per_ooxml_schema():
    s = read(REPORT)
    worksheet = s[s.index("private static void WriteWorksheet"):s.index("private static void Merge")]
    assert worksheet.index('x.WriteStartElement("autoFilter")') < worksheet.index('x.WriteStartElement("mergeCells")')
    assert "OOXML worksheet schema requires autoFilter before mergeCells" in worksheet

def test_developer_identity_is_fixed_and_readonly():
    service = read(EXPORT_SERVICE)
    ui = read(EXPORT_UI)
    assert 'FixedDeveloperName = "Bùi Thế Nam"' in service
    assert 'FixedDeveloperContact = "Điện thoại: 0967280686"' in service
    assert 'ReadOnly = true' in ui
    assert 'Text = ExportOptions.FixedDeveloperName' in ui
    assert 'Text = ExportOptions.FixedDeveloperContact' in ui
    assert "TextChanged += ReportInfoChanged" not in ui


def test_writer_blanks_nonfinite_numeric_values_and_self_validates_package():
    s = read(REPORT)
    assert "double.IsNaN(number)" in s
    assert "double.IsInfinity(number)" in s
    assert "ValidatePackage(filePath, report.Sheets.Count)" in s
    assert 'zip.GetEntry(path)' in s
    assert "new XmlDocument()" in s


def test_xlsx_writer_supports_real_formulas_and_recalculation():
    s = read(REPORT)
    for token in [
        "FormulaInteger", "Formula2", "Formula3",
        'x.WriteElementString("f", formula)',
        'x.WriteStartElement("calcPr")',
        'x.WriteAttributeString("calcMode", "auto")',
        'x.WriteAttributeString("fullCalcOnLoad", "1")',
    ]:
        assert token in s


def test_export_links_summary_to_detail_and_level_sheets():
    s = read(EXPORT_SERVICE)
    assert "'Khối lượng chi tiết'!H" in s
    assert "'Khối lượng chi tiết'!L" in s
    assert "'Tổng hợp theo tầng'!E" in s
    assert "'Tổng hợp theo tầng'!F" in s
    assert 'AdaptiveVolumeFormula(excelRow, "D", "E", "G")' in s
    assert 'AdaptiveVolumeFormula(excelRow, "D", "I", "K")' in s
    assert 'SQRT(' in s
    assert 'ABS(' in s and 'MAX(' in s and 'MIN(' in s
