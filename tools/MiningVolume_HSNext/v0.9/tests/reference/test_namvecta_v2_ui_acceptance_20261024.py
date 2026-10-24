"""14-item field UI round acceptance contract; NOT a live AutoCAD UI acceptance.

The original features are preserved internally unless explicitly deprecated.
Tests check the normal end-user workflow and guard against regressions.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / "src" / "MiningVolume.Plugin2023" / "UI"


def read(name):
    return (UI / name).read_text(encoding="utf-8")


def test_data_buttons_compact_and_layer_refresh_above_sources():
    data = read("DataPage.cs")
    assert data.index('Btn("Làm mới layer"') < data.index('var sourceBox = _sourceBox')
    assert 'Height = 28' in data
    assert 'var actions = _actionsPanel = new FlowLayoutPanel' in data
    assert 'var refresh = Btn("Làm mới layer"' not in data
    assert 'source.Controls.Add(_via4Preset' not in data
    assert 'body.Controls.Add(_via4Preset' not in data


def test_data_source_types_two_columns_no_horizontal_scroll():
    data = read("DataPage.cs")
    assert "MultiColumn = true" in data
    assert "HorizontalScrollbar = false" in data
    assert "_types.ColumnWidth = Math.Max(90," in data
    assert "_sourceTable.RowCount = compact ? 4 : 2" in data


def test_tin_management_exactly_two_visible_actions():
    model = read("ModelPage.cs")
    assert 'Button("Xem TIN", (s, e) => ViewTin3D())' in model
    assert 'Button("Xóa toàn bộ TIN", (s, e) => DeleteAllTins())' in model
    # Reopened DWGs have 3DFACEs even when TinCadFaceCount is not restored.
    assert "TinCadRenderer.InspectLayers(doc.Database, names)" in model
    assert "SurfaceWorkflowService.DeleteAllTinsStoredInCurrentDrawing()" in model
    assert "Dữ liệu khảo sát" not in model or "đường đồng mức" in model
    assert "operations.Controls.Add(_view3D);" in model
    assert "operations.Controls.Add(deleteAll);" in model
    for obsolete in (
        "top.Controls.Add(Button(\"Loại điểm\"",
        "second.Controls.Add(_meanZBuild)",
        "second.Controls.Add(_build)",
        "Controls.Add(_grid);",
    ):
        assert obsolete not in model


def test_data_navigation_direct_buttons_and_license_nav():
    menu = read("MainPaletteControl.cs")
    assert 'Text = "TIN hiện trạng / thiết kế"' in menu
    assert 'Text = "Thông tin dự án / đơn vị"' in menu
    assert '_tinTab.Click += (s,e) => ShowPage(AppPage.Data);' in menu
    assert '_projectTab.Click += (s,e) => ShowPage(AppPage.Project);' in menu
    assert '_dataButtons.Visible = group == AppPage.Model;' in menu
    assert 'Name = "NamvectaLicenseNav"' in menu
    assert 'Text = "Thông tin & Bản quyền"' in menu
    assert 'compactLicense.Click += (s,e) => ShowPage(AppPage.License);' in menu
    assert 'case AppPage.Model: return new[] { AppPage.Data, AppPage.Project, AppPage.QuickSection };' in menu
    assert '_quickSectionTab.Click += (s,e) => ShowPage(AppPage.QuickSection);' in menu
    assert '_advancedVolumeButton' not in menu
    assert 'Text = "Tùy chọn"' not in menu
    assert 'AppPage.Mineral };' not in menu


def test_section_smaller_controls_and_volume_edited_profiles():
    section = read("SectionPage.cs")
    assert 'Text = t, AutoSize = true, Height = 28' in section
    assert 'Button("Tính toán khối lượng trên mặt cắt", UpdateEditedProfiles)' in section
    assert 'Button("Vỉa than/quặng"' not in section
    assert 'Button("Tính khối lượng", (sender, args) => _navigate(AppPage.Volume))' not in section
    assert 'EditableSectionCadService.ReadEditedProfiles' in section
    assert 'VolumeWorkflowService.CalculateCore(' in section
    assert 'VolumeWorkflowService.Commit(volume, from, to, step);' in section
    assert "compact ? 102F : 112F" in section


def test_wheel_never_changes_spinner_value():
    spin = read("NoWheelNumericUpDown.cs")
    assert 'protected override void OnMouseWheel(MouseEventArgs e)' in spin
    assert 'scroll.AutoScrollPosition = new Point(' in spin
    assert 'base.OnMouseWheel(e)' not in spin
    for file in ("SectionPage.cs", "GridPage.cs", "ProjectPage.cs", "ModelPage.cs"):
        src = read(file)
        assert "new NoWheelNumericUpDown" in src
        assert "new NumericUpDown" not in src


def test_scope_not_clipped_and_offline_pass_refreshes():
    volume = read("VolumePage.cs")
    assert 'AddInfo(info, 2, "Tổng khối lượng", _totals);' in volume
    assert "info.Height = 94;" in volume
    contact = read("ContactInfoPage.cs")
    license = read("LicensePage.cs")
    assert "RuntimeBuildIdentity.ShortTag()" in contact
    assert "_license.RefreshStatus()" in contact
    assert "Bùi Thế Nam" in contact
    assert "0967280686" in contact
    assert "Nhập Pass kích hoạt offline" in license
    assert "status.ExpiresUtc" in license
