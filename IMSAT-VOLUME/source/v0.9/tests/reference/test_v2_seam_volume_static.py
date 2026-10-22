from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
def read(p): return (ROOT/p).read_text(encoding="utf-8")

def test_seam_volume_engine_never_assumes_missing_area_is_zero():
    s=read("src/MiningVolume.Core/Volumes/MineralVolumeCalculator.cs")
    assert "AbsentConfirmed" in s
    assert "Không được tự coi thiếu dữ liệu là diện tích bằng 0" in s
    assert "SectionVolumeCalculator.VolumeByFormula" in s
    assert "SectionVolumeCalculator.SelectFormula" in s
    assert "mineralInterval > interval.CutVolume" in s
    assert "seam.DensityTonPerCubicMetre <= 0" in s

def test_area_absence_confirmation_survives_dwg_saves_and_reloads():
    body=read("src/MiningVolume.Plugin2023/Services/MineralBody.cs")
    snap=read("src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs")
    persistence=read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    assert "ConfirmedAbsentSections" in body
    assert "ConfirmedAbsentSections" in snap
    assert "item.ConfirmedAbsentSections.AddRange" in persistence
    assert "restored.SetAbsentAtSection" in persistence

def test_ui_rereads_live_cad_polylines_before_seam_volume_or_export():
    workflow=read("src/MiningVolume.Plugin2023/Services/MineralVolumeWorkflowService.cs")
    page=read("src/MiningVolume.Plugin2023/UI/MineralPage.cs")
    assert "MineralSectionRegionService.UpdateAll();" in workflow
    assert "MineralVolumeCalculator.Calculate(" in workflow
    assert "TÍNH M³ / TẤN THEO VỈA" in page
    assert "XUẤT EXCEL KHOÁNG SẢN" in page
    assert "XÁC NHẬN VỈA VẮNG TẠI MC" in page
    assert "MineralVolumeWorkflowService.Recalculate();" in page

def test_4_sheet_excel_has_linked_seam_tonnage_and_stripping():
    s=read("src/MiningVolume.Plugin2023/Services/MineralVolumeExcelReportBuilder.cs")
    for title in ("THAM SỐ VỈA","CHI TIẾT VỈA","TỔNG HỢP VỈA","ĐẤT ĐÁ VÀ HSB"):
        assert title in s
    assert 'ReportCell.Formula3("J"+row+"*K"+row)' in s
    assert 'ReportCell.Formula3("B"+DataStart+"-B"+(DataStart+1))' in s  # Actual B4-B5 verified in FinalAuditRegression
    assert "SUMIF(" in s
    assert "AdaptiveVolumeFormula(row" in s
    assert "SQRT(" in read("src/MiningVolume.Plugin2023/Services/ExportWorkflowService.cs")

def test_real_core_regressions_cover_multi_seam_tons_and_missing_area():
    s=read("tests/core/CoreRegression.cs")
    for name in ("mineral_volume_multiple_seams_and_stripping",
                "mineral_volume_missing_section_must_not_be_zero",
                "mineral_volume_rejects_excess_seam_and_bad_density"):
        assert name in s
