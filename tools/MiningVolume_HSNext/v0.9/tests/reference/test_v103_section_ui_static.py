from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def read(rel):
    return (ROOT / rel).read_text(encoding="utf-8")

def section_ctor():
    s = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    a = s.index("public SectionPage()")
    b = s.index("protected override void Dispose", a)
    return s[a:b]

def test_section_page_has_explicit_draw_sections_button():
    s = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    assert 'Button("XUẤT / VẼ MẶT CẮT", BuildProfiles)' in s
    assert 'draw.Name = "btnDrawSections";' in s
    assert 'Button("XEM TRƯỚC TUYẾN", Preview)' in s

def test_section_layout_is_single_explicit_vertical_grid_not_overlapping_top_docks():
    ctor = section_ctor()
    assert "var root = new TableLayoutPanel" in ctor
    assert "RowCount = 6" in ctor
    assert "root.RowStyles.Add" in ctor
    assert "BringToFront()" not in ctor

def test_all_section_parameters_are_visible_rows_before_actions():
    s = read("src/MiningVolume.Plugin2023/UI/SectionPage.cs")
    for token in [
        'Add(top, 2, "Khoảng cách mặt cắt, m"',
        'Add(top, 3, "Mức chia tầng, m"',
        'Add(top, 4, "Tính từ mức"',
        'Add(top, 5, "Đến mức"',
        'Add(top, 6, "Tỷ lệ ngang 1/"',
        'Add(top, 7, "Tỷ lệ đứng 1/"'
    ]:
        assert token in s

def test_runtime_selftest_checks_draw_button_at_minimum_palette_size():
    s = read("src/MiningVolume.Plugin2023/Services/SelfTestService.cs")
    assert "ui.Size = new System.Drawing.Size(580, 620);" in s
    assert 'FindByName(ui, "btnDrawSections")' in s
    assert "drawSections.Visible" in s
    assert "Nút XUẤT / VẼ MẶT CẮT hiển thị trong palette tối thiểu" in s
