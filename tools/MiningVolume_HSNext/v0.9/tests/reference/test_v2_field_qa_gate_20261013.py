"""Field QA gates derived from the actual 13 Oct AutoCAD screenshots.

These source assertions complement, but DO NOT replace, AutoCAD-host UAT.
"""
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]


def src(name):
    return (ROOT / "src" / name).read_text(encoding="utf-8")


def test_boundary_clipped_true_3d_faces_use_same_validator_as_cad_renderer():
    upright = src("MiningVolume.Core/Surface/VerticalFaceBuilder.cs")
    renderer = src("MiningVolume.Plugin2023/Services/TinCadRenderer.cs")
    core = (ROOT / "tests/core/CoreRegression.cs").read_text(encoding="utf-8")
    assert "IsValidCadFace(cut)" in upright
    assert "x.X == y.X && x.Y == y.Y" in upright
    assert "x.Z != y.Z" in upright
    assert "HasThreeDimensionalArea(face.A, face.B, face.C)" in upright
    assert "VerticalFaceBuilder.IsValidCadFace(t)" in renderer
    assert "vertical_3d_face_accepts_tiny_true_height_and_narrow_clip" in core
    assert "Clipped curtain" not in renderer  # checked using exact production gate


def test_export_does_not_fail_for_missing_imsat_logo_for_any_unit():
    export = src("MiningVolume.Plugin2023/Services/ExportWorkflowService.cs")
    logo = src("MiningVolume.Plugin2023/Services/XlsxBrandingInjector.cs")
    assert "report.IncludeImsatLogo = !outside;" in export
    assert "if (report.IncludeImsatLogo)" in export
    assert "catch (Exception ex) when (!(ex is OutOfMemoryException)" in export
    assert "SimpleXlsxWriter.Write(staging, report);" in export
    assert "if (!report.IncludeImsatLogo) return;" in logo
    assert 'GetManifestResourceStream("MiningVolume2023.Assets.imsat_logo_64.png")' in logo
    assert "if (logo == null) return;" in logo
    assert '"Đơn vị cấp trên", TkvParentHeading' in export


def test_native_cad_quantity_table_matches_structural_sample_not_a_flat_list():
    cad = src("MiningVolume.Plugin2023/Services/CadVolumeTableService.cs")
    assert "Autodesk.AutoCAD.DatabaseServices.Table" in cad
    assert "TableGap = 28.0" in cad
    assert "MergeCells(CellRange.Create(interval, 0, 0, 0, columns - 1))" in cad
    assert "MergeCells(CellRange.Create(interval, 1, 4, 1, 7))" in cad
    assert "MergeCells(CellRange.Create(interval, 1, 8, 1, 10))" in cad
    assert '"Diện tích (m²)"' in cad and '"Khối lượng (m³)"' in cad
    assert '"Thứ tự", "MC đầu", "MC cuối"' in cad
    assert 'interval.Cells[totalRow, 0].TextString = "Tổng";' in cad
    assert 'FileName = "arial.ttf"' in cad
    assert "table.GenerateLayout();" in cad
    assert "CadVolumeTableService.Insert(" in src("MiningVolume.Plugin2023/UI/ExportPage.cs")


def test_approved_ui_uses_sentence_case_no_white_menu_border_or_boxed_header():
    theme = src("MiningVolume.Plugin2023/UI/UiTheme.cs")
    palette = src("MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    section = src("MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert "button.Text = SentenceCaseButton(button.Text)" in theme
    assert "button.FlatAppearance.BorderSize = 0;" in theme
    assert "kv.Value.FlatAppearance.BorderSize = 0;" in palette
    assert "header.Paint += (sender, args)" in palette
    assert 'const string title = "IMSAT VOLUME";' in palette
    assert "args.Graphics.DrawString(title, big, white, left, 12)" in palette
    assert "top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F))" in section
    assert "_parameterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F))" in section
    assert "i < 2 ? 54F : 35F" in section


def test_quick_section_and_custom_ratio_do_not_corrupt_volume_stations():
    section = src("MiningVolume.Plugin2023/UI/SectionPage.cs")
    quick = section.split("private async void DrawQuickSections(",1)[1].split(
        "private async void UpdateEditedProfiles(",1)[0]
    assert "SelectionService.PickQuickSectionRoutes()" in quick
    assert "SectionCadRenderer.DrawQuickProfiles(" in quick
    assert "st.SectionSystem =" not in quick
    assert "st.VolumeResult =" not in quick
    assert "(double)_hScale.Value" in section
    assert "(double)_vScale.Value" in section
    assert "_hScale.Enabled = custom;" in section
    assert "_vScale.Enabled = custom;" in section


def test_grid_reuses_tin_and_volume_has_rendered_math():
    grid = src("MiningVolume.Plugin2023/UI/GridPage.cs")
    assert "EnsureBothTinsReady(synchronizeCadLayers:false)" in grid
    assert "KHÔNG cần dựng TIN mới" in grid
    eq = src("MiningVolume.Plugin2023/UI/ProfessionalFormulaPanel.cs")
    assert '"Cambria Math"' in eq and '"L(F₁ + F₂ + √F₁F₂)"' in eq
    assert "e.Graphics.DrawLine(pen, fractionX + 3" in eq


def test_excel_hint_is_gone_and_user_brand_name_is_imsat_volume():
    ui = src("MiningVolume.Plugin2023/UI/ExportPage.cs")
    assert "Excel: Arial 12 • bảng đóng khung" not in ui
    title = src("MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    entry = src("MiningVolume.Plugin2023/EntryPoint.cs")
    assert '"IMSAT VOLUME"' in title
    assert 'PaletteSet("IMSAT VOLUME"' in entry



def test_axis_tick_density_is_guarded_before_creating_cad_geometry():
    """Very fine axis ticks on deep pits must fail clearly, not freeze CAD."""
    renderer = src("MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    assert "private const int MaxElevationTicksPerProfile = 250;" in renderer
    assert "EnsureElevationTickBudget(baseZ, topZ, levelStep, p.Line.Name);" in renderer
    assert "EnsureElevationTickBudget(baseZ, topZ, elevationStep, profile.Line.Name);" in renderer
    assert "ticks > MaxElevationTicksPerProfile + 0.000001" in renderer
    assert "Bước chia tầng tính khối lượng không thay đổi." in renderer
    # The original independently chosen CAD axis step must never be silently
    # enlarged: otherwise the user sees a section at the wrong annotation step.
    assert "EnsureElevationTickBudget(baseZ, topZ, levelStep, p.Line.Name);" in renderer.split(
        "for (int pi = startIndex;", 1)[1].split(
        "AddLine(ms, tr", 1)[0]
    )
    assert "EnsureElevationTickBudget(baseZ, topZ, elevationStep, profile.Line.Name);" in renderer.split(
        "public static void DrawQuickProfiles(", 1)[1].split(
        "AddLine(ms,tr,new Point3d", 1)[0]
    )
