from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return (ROOT/path).read_text(encoding="utf-8")


def test_quick_section_selects_selected_routes_not_parallel_volume_stations():
    pick = read("src/MiningVolume.Plugin2023/Services/SelectionService.cs")
    core = read("src/MiningVolume.Core/Sections/QuickSectionProfileBuilder.cs")
    page = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    cad = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    assert "PickQuickSectionRoutes()" in pick
    assert "entity is Polyline3d" in pick
    assert "entity is Polyline2d" in pick
    assert "entity is Polyline polyline" in pick
    assert "Math.Abs(polyline.GetBulgeAt(i))" in pick
    assert "Vẽ mặt cắt nhanh" in page
    assert 'quick.Name = "btnQuickSections";' in page
    assert "SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers: false)" in page
    assert "new QuickSectionProfileBuilder()" in page
    assert "new TinSectionSampler()" in page
    # A long TIN sample must not use an old CAD drawing gap if the user
    # changes layout settings while the background calculation is running.
    assert "st.SectionLayoutGap != layoutGap" in page
    assert "selected.Select((route, i)" in page
    assert "SectionCadRenderer.DrawQuickProfiles(" in page
    assert "st.SectionSystem =" not in page.split(
        "private async void DrawQuickSections(", 1)[1].split(
        "private async void UpdateEditedProfiles(", 1
    )[0]
    assert "new SectionSpan(start, start + len)" in core
    assert "total + segment.S0" in core
    assert "total + segment.S1" in core
    assert "new SectionSpan(0, total)" in core
    out = cad.split("public static void DrawQuickProfiles(", 1)[1].split(
        "private static void ValidatePreviewOwnership(", 1
    )[0]
    for layer in ["MV_MC_NHANH_HT", "MV_MC_NHANH_TK",
                  "MV_MC_NHANH_TRUC", "MV_MC_NHANH_CHU"]:
        assert layer in out
    assert "EraseOnLayers" not in out
    assert "VolumeWorkflowService" not in out
    assert "AddProfilePolylines" in out
    assert "exaggeration,ht,tk, tagForEditing: false" in out
    assert "if (tagForEditing)" in cad
    assert "StampSection(a, section" in cad
    # Dedicated QUICK layers cannot be interpreted as official Grip sections.
    assert "StampSection(" not in out


def test_professional_math_fractions_are_visible_without_making_word_equation_claims():
    formula = read("src/MiningVolume.Plugin2023/UI/ProfessionalFormulaPanel.cs")
    volume = read("src/MiningVolume.Plugin2023/UI/VolumePage.cs")
    assert "OnPaint(PaintEventArgs e)" in formula
    assert 'new Font("Cambria Math", 12F' in formula
    assert "√F₁F₂" in formula
    assert 'string denominator = row == 0 ? "2" : "3"' in formula
    assert "e.Graphics.DrawLine(pen, fractionX + 3" in formula
    assert "new ProfessionalFormulaPanel()" in volume
    assert "Tự chọn theo F1-F2: 1 đầu = 0" not in volume


def test_buttons_have_breathing_room_and_are_not_all_caps():
    theme = read("src/MiningVolume.Plugin2023/UI/UiTheme.cs")
    palette = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    data = read("src/MiningVolume.Plugin2023/UI/DataPage.cs")
    project = read("src/MiningVolume.Plugin2023/UI/ProjectPage.cs")
    assert "button.Text = SentenceCaseButton(button.Text)" in theme
    assert "button.Margin = new Padding(2, 2, 2, 2);" in theme
    assert "button.Padding = new Padding(4, 1, 4, 1)" in theme
    assert "button.FlatAppearance.BorderSize = 0;" in theme
    assert "kv.Value.FlatAppearance.BorderSize = 0;" in palette
    assert "_sourceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));" in data
    assert "bool compact = width < 720;" in data
    assert "_sourceBox.Height = 182;" in data
    assert "new RowStyle(SizeType.Absolute, 38)" in data
    assert "Height = 182," in data
    assert "actions.Controls.Add(_buildExisting);" in data
    assert "actions.Controls.Add(_buildDesign);" in data
    assert "buttons.Height = ClientSize.Width < 650 ? 140 : 62;" in project


def test_custom_horizontal_and_vertical_scales_persist_and_match_excel():
    page = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    cad = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    restore = read("src/MiningVolume.Plugin2023/Services/ProjectPersistenceService.cs")
    assert "_hScale.Enabled = custom;" in page
    assert "_vScale.Enabled = custom;" in page
    assert "(double)_hScale.Value,\n                    (double)_vScale.Value" in page
    assert "double vExag = horizontalScale / verticalScale;" in cad
    assert 'state.HorizontalScale = state.SectionParameterMode == "Tùy chỉnh"' in restore
    for path in ["ExportWorkflowService.cs",
                 "GridVolumeExcelReportBuilder.cs",
                 "MineralVolumeExcelReportBuilder.cs"]:
        report = read("src/MiningVolume.Plugin2023/Services/" + path)
        assert "HorizontalScale.ToString" in report
        assert "VerticalScale.ToString" in report


def test_branding_and_discard_excel_tip_only():
    license_page = read("src/MiningVolume.Plugin2023/UI/LicensePage.cs")
    export = read("src/MiningVolume.Plugin2023/UI/ExportPage.cs")
    assert 'Text = "Bản quyền NAMVECTA VOLUME 2.0"' in license_page
    assert "Excel: Arial 12 • bảng đóng khung" not in export
    assert "ExportClicked" in export


def test_quick_section_layout_gap_is_validated_before_cad_transaction():
    cad = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    body = cad.split("public static void DrawQuickProfiles(", 1)[1].split(
        "// A user can set the CAD elevation-axis step", 1
    )[0]
    guard = "if (double.IsNaN(layoutGap) || double.IsInfinity(layoutGap) || layoutGap < 30.0)"
    assert guard in body
    assert body.index(guard) < body.index("db.TransactionManager.StartTransaction()")
    assert "throw new ArgumentOutOfRangeException(nameof(layoutGap)" in body
