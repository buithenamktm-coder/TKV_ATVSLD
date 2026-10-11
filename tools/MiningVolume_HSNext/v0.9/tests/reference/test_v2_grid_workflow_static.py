from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def read(p):
    return (ROOT/p).read_text(encoding="utf-8")


def test_v2_grid_page_exposes_calculate_draw_excel_cancel():
    s=read("src/MiningVolume.Plugin2023/UI/GridPage.cs")
    assert "CHỌN TRÊN CAD" in s
    assert "TÍNH LƯỚI" in s
    assert "HỦY TÍNH" in s
    assert "VẼ LƯỚI CAD" in s
    assert "XUẤT EXCEL" in s
    assert "GridVolumeCalculator().Calculate" in s
    assert "GridWorkflowService.Commit" in s
    assert "GridCadRenderer.Draw" in s
    assert "GridVolumeExcelReportBuilder.Build" in s
    assert "SaveFileDialog" in s


def test_v2_grid_result_stale_guard_and_dwg_parameters():
    source=read("src/MiningVolume.Plugin2023/Services/GridWorkflowService.cs")
    project=read("src/MiningVolume.Plugin2023/Services/ProjectState.cs")
    snap=read("src/MiningVolume.Plugin2023/Services/ProjectSnapshot.cs")
    persistence=read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    assert "ReferenceEquals(_existing, state.Existing.Tin)" in source
    assert "state.Existing.Source.Revision == _revExisting" in source
    for field in ["GridBoundaryHandle","GridSizeX","GridSizeY","GridRotationDegrees"]:
        assert field in project and field in snap and field in persistence


def test_v2_grid_cad_uses_separate_layers_for_cut_fill_mixed_and_nodata():
    s=read("src/MiningVolume.Plugin2023/Services/GridCadRenderer.cs")
    for text in ["MV_LUOI_DAO","MV_LUOI_DAP","MV_LUOI_HONHOP","MV_LUOI_THIEU_DL"]:
        assert text in s
    assert "new Polyline" in s
    assert "new DBText" in s
    assert "Closed=true" in s
    assert "tr.Commit();" in s


def test_six_sheet_grid_excel_formula_links_nodes_and_totals():
    s=read("src/MiningVolume.Plugin2023/Services/GridVolumeExcelReportBuilder.cs")
    for sheet in ["THÔNG TIN","CAO ĐỘ NÚT LƯỚI","CHI TIẾT Ô LƯỚI",
                  "BẢNG LƯỚI TỔNG HỢP","TỔNG HỢP KHỐI LƯỢNG","CHI TIẾT TÍNH TOÁN"]:
        assert sheet in s
    assert 'ReportCell.Formula3("G"+row+"-H"+row)' in s
    assert "'CHI TIẾT Ô LƯỚI'!" in s
    assert "ReportCell.Formula3" in s
    assert "chỉ sửa Excel" in s


def test_grid_menu_added_without_removing_sections():
    p=read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    e=read("src/MiningVolume.Plugin2023/EntryPoint.cs")
    assert "[AppPage.Grid] = new GridPage()" in p
    assert "AppPage.Section" in p
    assert "AppPage.Volume" in p
    assert 'CommandMethod("MV_GRID"' in e
