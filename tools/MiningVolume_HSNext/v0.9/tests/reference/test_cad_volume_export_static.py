from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(p):
    return (ROOT / p).read_text(encoding="utf-8")


def test_cad_volume_tables_are_native_and_not_images():
    s = read("src/MiningVolume.Plugin2023/Services/CadVolumeTableService.cs")
    assert "new Autodesk.AutoCAD.DatabaseServices.Table" in s
    assert "table.GenerateLayout();" in s
    assert "model.AppendEntity(table);" in s
    assert 'internal const string OutputLayer = "MV_KL_BANG";' in s
    assert "result.Intervals.Count + 1" in s
    assert "result.Levels.Count + 1" in s
    assert "result.TotalCutVolume" in s
    assert "result.TotalFillVolume" in s
    assert "tr.Commit();" in s
    assert "ent.Erase()" not in s


def test_export_page_supports_cad_xlsx_or_both_without_forcing_xlsx():
    s = read("src/MiningVolume.Plugin2023/UI/ExportPage.cs")
    assert "Xuất bảng lên CAD" in s
    assert "Xuất file Excel" in s
    assert "bool exportCad = _toCad.Checked" in s
    assert "bool exportExcel = _toExcel.Checked" in s
    assert "if (!exportCad && !exportExcel)" in s
    assert "if (exportExcel)" in s
    assert "if (exportCad)" in s
    assert "CadVolumeTableService.Insert" in s
    assert "ExportWorkflowService.WriteXlsx" in s
    # Recheck the authoritative CAD section paths and approved volume before
    # either output; the guard also rejects a DWG/TIN switch mid-export.
    assert "EnsureExportInputsCurrent(doc, st, currentResult, approvedProfiles," in s
    assert "!ReferenceEquals(expectedVolume, state.VolumeResult)" in s
    assert "EditableSectionCadService.EnsureProfilesMatch(doc.Database, expectedProfiles)" in s
