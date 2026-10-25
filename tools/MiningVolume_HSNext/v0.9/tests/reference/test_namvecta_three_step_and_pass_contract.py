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
    assert "AppPage.QuickSection: targetGroup = AppPage.Model" in ui
    assert "case AppPage.Project:" in ui
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
    assert license_service.count("E18AB4532DF") == 1
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
    assert 'NewSheet("01 Tổng hợp"' in export
    assert 'NewSheet("Chi tiết tầng"' in export
    assert "BandSum" in export
    assert 'AddCheck(flow, "Tổng hợp toàn khu vực", true)' in ui
    assert "NAMVECTA_VOLUME_KhoiLuong_" in ui

def test_project_restore_user_messages_use_namvecta_brand():
    persistence = source("Services/ProjectPersistenceService.cs")
    assert "Bản vẽ chưa có Project NAMVECTA VOLUME." in persistence
    assert "Project IMSAT VOLUME" not in persistence

def test_remaining_user_facing_runtime_strings_use_namvecta_brand():
    cad_table = source("Services/CadVolumeTableService.cs")
    mean_z = source("Services/MeanZPreviewWorkflowService.cs")
    mineral_table = source("Services/MineralCadTableService.cs")
    mineral_section = source("Services/MineralSectionRegionService.cs")
    section_progress = source("Services/SectionCadProgress.cs")
    tin_view = source("Services/Tin3DViewService.cs")
    tin_progress = source("Services/TinCadProgress.cs")
    duplicate_xy = source("UI/DuplicateXYConflictUi.cs")

    assert "NAMVECTA VOLUME – TỔNG HỢP KHỐI LƯỢNG" in cad_table
    assert "NAMVECTA VOLUME;TIN THU NGHIEM TRUNG BINH Z" in mean_z
    assert "NAMVECTA VOLUME – KHOÁNG SẢN VÀ ĐẤT ĐÁ" in mineral_table
    assert "NAMVECTA VOLUME 2.0 để tạo liên kết" in mineral_section
    assert '"NAMVECTA: " + operation' in section_progress
    assert "NAMVECTA VOLUME - VIEW TIN 3D" in tin_view
    assert '"NAMVECTA: ghi " + layerName' in tin_progress
    assert "NAMVECTA VOLUME — Điểm trùng tọa độ" in duplicate_xy

    # Keep legacy storage/interoperability identifiers separately; they are not UI branding.
    forbidden = (
        "IMSAT VOLUME – TỔNG HỢP KHỐI LƯỢNG",
        "IMSAT MININGVOLUME;TIN THU NGHIEM TRUNG BINH Z",
        "IMSAT – KHOÁNG SẢN VÀ ĐẤT ĐÁ",
        "bằng IMSAT VOLUME V2.0",
        '"IMSAT: " + operation',
        "IMSAT VOLUME - VIEW TIN 3D",
        '"IMSAT: ghi " + layerName',
        "IMSAT VOLUME — Điểm trùng tọa độ",
    )
    combined = "\n".join((cad_table, mean_z, mineral_table, mineral_section,
                           section_progress, tin_view, tin_progress, duplicate_xy))
    for legacy in forbidden:
        assert legacy not in combined

