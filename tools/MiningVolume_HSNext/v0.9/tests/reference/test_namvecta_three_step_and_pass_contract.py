"""Static contract checks for NAMVECTA VOLUME 2.0 review branch.

These checks are intentionally NOT AutoCAD/Windows runtime evidence.
Full integration and acceptance still require Work's host testing.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PLUGIN = ROOT / "src" / "MiningVolume.Plugin2023"


def source(relative):
    return (PLUGIN / relative).read_text(encoding="utf-8")


def test_three_step_navigation_keeps_advanced_commands():
    ui = source("UI/MainPaletteControl.cs")
    assert "_groups = { AppPage.Model, AppPage.Volume, AppPage.Export }" in ui
    assert '"1  Dữ liệu", "2  Tính toán", "3  Kết quả"' in ui
    assert "AppPage.QuickSection: targetGroup = AppPage.Volume" in ui
    assert "AppPage.Project: targetGroup = AppPage.Model" in ui
    assert "AppPage.License: targetGroup = AppPage.Export" in ui
    assert "imsat_logo_64.png" not in ui


def test_pass_offline_no_machine_identifier_in_activation_ui():
    license_service = source("Services/LicenseService.cs")
    license_ui = source("UI/LicensePage.cs")
    assert "public static string MachineId" not in license_service
    assert "RSACryptoServiceProvider" not in license_service
    assert "AddDays(15)" in license_service
    assert "AddMonths(TierMonths[tier])" in license_service
    assert "Pass không hợp lệ" in license_service
    assert "Nhập lại không gia hạn" in license_service
    assert license_service.count("A18AB4532DF") == 1
    assert "Nhập Pass kích hoạt offline" in license_ui
    assert "_machineId" not in license_ui
    assert ".mvlic" not in license_ui


def test_workbook_defaults_compact_but_formula_dependency_retained():
    export = source("Services/ExportWorkflowService.cs")
    ui = source("UI/ExportPage.cs")
    for field in ("IncludeSourceXyz", "IncludeSectionData",
                  "IncludeSectionAreas", "IncludeLevels", "IncludeWarnings"):
        assert f"{field} {{ get; set; }} = false" in export
    for field in ("IncludeIntervals", "IncludeSummary"):
        assert f"{field} {{ get; set; }} = true" in export
    assert "if (options.IncludeInfoSheet)" in export
    assert 'NewSheet("Tổng hợp"' in export
    assert 'NewSheet("Chi tiết tầng"' in export
    assert "BandSum" in export
    assert 'AddCheck(flow, "Tổng hợp toàn khu vực", true)' in ui
    assert "NAMVECTA_VOLUME_KhoiLuong_" in ui
