from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(p):
    return (ROOT / p).read_text(encoding="utf-8")


def test_view3d_never_triggers_1_6m_cad_face_rescan_or_orbit_automatically():
    view = read("src/MiningVolume.Plugin2023/Services/Tin3DViewService.cs")
    body = view.split("public static void Start(ModelRole role)", 1)[1].split(
        "private static (double cx", 1
    )[0]
    assert "EnsureTinReady(role, synchronizeCadLayer: false)" in body
    assert "TinCadIsPreview" in body and "TinCadFaceCount != faceCount" in body
    assert "private const int LargeTinFaceThreshold = 200000;" in view
    assert "MessageBoxButtons.YesNo" in body
    assert 'AcApp.SetSystemVariable("VSCURRENT", "2D Wireframe")' in body
    assert "SurfaceWorkflowService.SetTinVisible(" not in body
    assert "doc.Editor.Regen();" not in body
    assert "SendStringToExecute" not in body
    assert "3DORBIT " not in body
    assert "doc.Editor.SetCurrentView(view);" in body
    assert "public static void Stop()" in view
    assert "_previousView.Dispose()" in view
    assert "ProjectState.Current.NotifyChanged();" in view
    page = read("src/MiningVolume.Plugin2023/UI/ModelPage.cs")
    assert "Tin3DViewService.IsActive" in page
    assert "Tin3DViewService.Stop()" in page
    assert '"Trở về 2D"' in page


def test_section_direction_is_perpendicular_to_two_selected_cad_points():
    builder = read("src/MiningVolume.Core/Sections/SectionSystemBuilder.cs")
    page = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    select = read("src/MiningVolume.Plugin2023/Services/SelectionService.cs")
    assert "public static Vec2 PerpendicularToSelectedDirection(Vec2 selected)" in builder
    assert "new Vec2(-selected.Y / length, selected.X / length)" in builder
    assert "SectionSystemBuilder.PerpendicularToSelectedDirection(r.Vector)" in page
    assert "SectionDirection = sectionDirection;" in page
    assert "Hướng MC (vuông góc)" in page
    assert "phương chuẩn" in select


def test_two_section_elevation_scales_mirror_exactly():
    draw = read("src/MiningVolume.Plugin2023/Services/SectionCadRenderer.cs")
    assert "double rightAxisX = x0 + width;" in draw
    assert "new Point3d(rightAxisX, y0 + height, 0)" in draw
    assert "new Point3d(rightAxisX - 1.5, yy, 0)" in draw
    assert "new Point3d(rightAxisX + 1.5, yy, 0)" in draw
    assert "new Point3d(rightAxisX + 4.0, yy - 1.5, 0)" in draw
    assert "string elevation = z.ToString" in draw
    assert "new Point3d(rightAxisX + 2, y0 + height + 3, 0)" in draw


def test_high_contrast_live_palette_and_correct_seam_ore_label():
    theme = read("src/MiningVolume.Plugin2023/UI/UiTheme.cs")
    palette = read("src/MiningVolume.Plugin2023/UI/MainPaletteControl.cs")
    section = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert "new GlassPanel(true)" in palette
    assert "UiTheme.ChromeText" in palette
    assert "UiTheme.ChromeBottom" in palette
    assert palette.count("Vỉa than/quặng") >= 1
    assert "AppPage.Mineral" in palette and "GetGroupPages" in palette
    assert 'AddNav(nav, AppPage.Model, "1", "Dữ liệu")' in palette
    assert '[AppPage.QuickSection] = new SectionPage(true)' in palette
    assert '"Vỉa / Quặng"' not in palette
    assert "grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(31, 105, 183)" in theme
    assert "grid.DefaultCellStyle.SelectionForeColor = Color.White" in theme
    assert "private readonly bool _dark;" in theme
    assert "Color.FromArgb(212, 230, 248)" in theme
    assert "Text = \"HỆ MẶT CẮT\"" in section
    assert "Font = new Font(\"Arial\", 9.5F, FontStyle.Bold)" in section
