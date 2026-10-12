from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def source(path):
    return (ROOT / path).read_text(encoding="utf-8")


def test_tkv_directory_and_external_manual_entry():
    catalog = source("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    ui = source("src/MiningVolume.Plugin2023/UI/ProjectPage.cs")
    project = source("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    for unit in [
        "Công ty Cổ phần Than Cao Sơn - TKV",
        "Công ty Cổ phần Than Đèo Nai - Cọc Sáu - TKV",
        "Công ty Cổ phần Than Hà Tu - Vinacomin",
    ]:
        assert unit in catalog
    assert 'public const string TkvOther = "Đơn vị TKV khác";' in catalog
    assert 'public const string OutsideTkv = "Đơn vị khác (ngoài TKV)";' in catalog
    assert "MiningUnitCatalog.TkvOther, MiningUnitCatalog.OutsideTkv" in ui
    assert "AutoCompleteMode.None" in ui
    assert "ComboBoxStyle.DropDown" in ui
    assert "MiningUnitCatalog.IsKnownGroup(snap.MiningUnitGroup)" in project


def test_organization_above_excel_title_without_shifting_formula_rows():
    report = source("src/MiningVolume.Plugin2023/Services/ExportWorkflowService.cs")
    writer = source("src/MiningVolume.Core/Reporting/SpreadsheetReport.cs")
    grid = source("src/MiningVolume.Plugin2023/Services/GridVolumeExcelReportBuilder.cs")
    assert 'TkvParentHeading = "TẬP ĐOÀN CÔNG NGHIỆP THAN - KHOÁNG SẢN VIỆT NAM"' in report
    assert 'string prefix = outside ? unit : TkvParentHeading + "\\n" + unit;' in report
    assert 'sheet.Title = prefix + "\\n" + sheet.OrganizationBaseTitle;' in report
    assert "ApplyOrganizationHeader(report, frozenGroup, frozenUnit);" in report
    assert "ApplyOrganizationHeader(report, ProjectState.Current);" not in report
    assert "ExportWorkflowService.ApplyOrganizationHeader(r, ProjectState.Current);" in grid
    mineral = source("src/MiningVolume.Plugin2023/Services/MineralVolumeExcelReportBuilder.cs")
    assert "ExportWorkflowService.ApplyOrganizationHeader(report, state);" in mineral
    assert "OrganizationBaseTitle" in writer
    assert "int titleLines = sheet.Title.Split('\\n').Length;" in writer
    assert "Math.Max(24, titleLines * 19 + 6)" in writer
    assert "if (state.MiningUnitGroup != MiningUnitCatalog.OutsideTkv)" in grid
    assert "ExportWorkflowService.TkvParentHeading" in grid
    assert '"=C5-C6"' in report
    assert 'const int firstDataRow = 4' in report
    assert "new[] { ReportCell.Text(\"Đơn vị tính toán\"" in report
