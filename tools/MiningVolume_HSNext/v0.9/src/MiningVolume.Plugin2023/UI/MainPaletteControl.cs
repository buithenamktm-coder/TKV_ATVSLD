using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MiningVolume2023.UI
{
    public enum AppPage { Project, Data, Model, Section, Volume, Grid, Mineral, Export, License }

    public sealed class MainPaletteControl : UserControl
    {
        private readonly Panel _content;
        private readonly Panel _sidebar;
        private readonly Panel _compactHeader;
        private readonly ComboBox _compactNavigation;
        private readonly Dictionary<AppPage, Control> _pages;
        private bool _compact;
        private AppPage _currentPage;
        private readonly Dictionary<AppPage, Button> _buttons = new Dictionary<AppPage, Button>();

        public MainPaletteControl()
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.Canvas;
            Font = new Font("Arial", 9F);

            var header = BuildHeader();
            _sidebar = BuildSidebar(out var nav);
            _compactNavigation = new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Arial", 9F, FontStyle.Bold),
                BackColor = UiTheme.Surface, ForeColor = UiTheme.TextStrong
            };
            _compactNavigation.Items.AddRange(new object[] {
                "Dự án", "Dữ liệu", "TIN / Mô hình", "Mặt cắt",
                "Khối lượng", "Lưới ô vuông", "Vỉa than/quặng", "Xuất bảng", "Bản quyền"
            });
            _compactNavigation.SelectedIndexChanged += (sender, args) =>
            {
                if (_compactNavigation.SelectedIndex >= 0)
                    ShowPage((AppPage)_compactNavigation.SelectedIndex);
            };
            _compactHeader = new GlassPanel(true)
            {
                Dock = DockStyle.Top, Height = 46, Visible = false,
                BackColor = UiTheme.ChromeBottom, Padding = new Padding(12, 6, 12, 6)
            };
            _compactHeader.Controls.Add(_compactNavigation);

            _content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 12, 14, 12),
                BackColor = UiTheme.Canvas,
                AutoScroll = true
            };

            Controls.Add(_content);
            Controls.Add(_compactHeader);
            Controls.Add(_sidebar);
            Controls.Add(header);

            _pages = new Dictionary<AppPage, Control>
            {
                [AppPage.Project] = new ProjectPage(),
                [AppPage.Data] = new DataPage(),
                [AppPage.Model] = new ModelPage(),
                [AppPage.Section] = new SectionPage(),
                [AppPage.Volume] = new VolumePage(),
                [AppPage.Grid] = new GridPage(),
                [AppPage.Mineral] = new MineralPage(),
                [AppPage.Export] = new ExportPage(),
                [AppPage.License] = new LicensePage()
            };

            foreach (var page in _pages.Values)
                UiTheme.ApplyPage(page);

            AddNav(nav, AppPage.Project, "0", "Dự án");
            AddNav(nav, AppPage.Data, "1", "Dữ liệu");
            AddNav(nav, AppPage.Model, "2", "TIN / Mô hình");
            AddNav(nav, AppPage.Section, "3", "Mặt cắt");
            AddNav(nav, AppPage.Volume, "4", "Khối lượng");
            AddNav(nav, AppPage.Grid, "5", "Lưới ô vuông");
            AddNav(nav, AppPage.Mineral, "6", "Vỉa than/quặng");
            AddNav(nav, AppPage.Export, "7", "Xuất bảng");
            AddNav(nav, AppPage.License, "8", "Bản quyền");

            ShowPage(AppPage.Project);
            SizeChanged += (sender, args) => ApplyResponsiveLayout();
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            // AutoCAD docked-left palettes commonly have only 350-480 px.
            // Replace the permanent side menu with a compact TOP selector,
            // giving narrow windows their full width for vertically scrolling
            // workflow pages. No page is recreated; form values are preserved.
            bool compact = ClientSize.Width < 730;
            if (_compact != compact)
            {
                _compact = compact;
                _sidebar.Visible = !compact;
                _compactHeader.Visible = compact;
                _compactNavigation.SelectedIndex = (int)_currentPage;
            }
            var page = _content.Controls.Count == 0 ? null : _content.Controls[0];
            if (page == null) return;
            _content.SuspendLayout();
            if (_compact)
            {
                page.Dock = DockStyle.None;
                page.Location = new Point(0, 0);
                page.Width = Math.Max(310, _content.ClientSize.Width - 20);
                page.Height = Math.Max(800, _content.ClientSize.Height - 12);
            }
            else
            {
                page.Dock = DockStyle.Fill;
            }
            _content.ResumeLayout();
        }

        private static Panel BuildHeader()
        {
            var header = new GlassPanel(true)
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = UiTheme.ChromeBottom
            };

            int textLeft = 14;
            try
            {
                // Load from this add-in, not acad.exe's application directory.
                using (var stream = typeof(MainPaletteControl).Assembly.GetManifestResourceStream(
                    "MiningVolume2023.Assets.imsat_logo_64.png"))
                {
                    if (stream != null)
                    {
                        Image logoImage;
                        using (var original = Image.FromStream(stream))
                            logoImage = new Bitmap(original);
                        var logo = new PictureBox
                        {
                            Image = logoImage,
                            SizeMode = PictureBoxSizeMode.Zoom,
                            Size = new Size(48, 48),
                            Location = new Point(15, 14),
                            BackColor = Color.Transparent
                        };
                        logo.Disposed += (s, e) => logoImage.Dispose();
                        header.Controls.Add(logo);
                        textLeft = 76;
                    }
                }
            }
            catch { }

            header.Controls.Add(new Label
            {
                Text = "IMSAT VOLUME",
                AutoSize = true,
                Font = new Font("Arial", 15F, FontStyle.Bold),
                ForeColor = UiTheme.ChromeText,
                Location = new Point(textLeft, 12)
            });
            header.Controls.Add(new Label
            {
                Text = "Phát triển: Bùi Thế Nam  •  ĐT/Zalo: 0967280686",
                AutoSize = true,
                Font = new Font("Arial", 9F),
                ForeColor = UiTheme.ChromeMuted,
                Location = new Point(textLeft + 1, 43)
            });

            header.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = UiTheme.ChromeEdge
            });
            return header;
        }

        private static Panel BuildSidebar(out FlowLayoutPanel nav)
        {
            var sidebar = new GlassPanel(true)
            {
                Dock = DockStyle.Left,
                Width = 180,
                BackColor = UiTheme.ChromeBottom
            };

            var sectionTitle = new Label
            {
                Text = "QUY TRÌNH",
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(14, 16, 0, 0),
                Font = new Font("Arial", 9F, FontStyle.Bold),
                ForeColor = UiTheme.ChromeMuted
            };

            var footer = new Label
            {
                Text = "TKL  •  Hiện / ẩn IMSAT VOLUME",
                Dock = DockStyle.Bottom,
                Height = 56,
                Padding = new Padding(14, 12, 6, 6),
                Font = new Font("Arial", 8.5F),
                ForeColor = UiTheme.ChromeMuted,
                BackColor = UiTheme.ChromeBottom
            };

            nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 8, 8, 6),
                BackColor = UiTheme.ChromeBottom
            };

            sidebar.Controls.Add(nav);
            sidebar.Controls.Add(footer);
            sidebar.Controls.Add(sectionTitle);
            return sidebar;
        }

        private void AddNav(Control parent, AppPage page, string number, string text)
        {
            var b = new Button
            {
                Text = number + "   " + text,
                Width = 164,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(11, 0, 0, 0),
                Font = new Font("Arial", 9.5F),
                ForeColor = UiTheme.ChromeText,
                BackColor = UiTheme.Sidebar,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            UiTheme.ApplyRoundedButtonMask(b);
            b.FlatAppearance.MouseOverBackColor = UiTheme.SidebarHover;
            b.FlatAppearance.MouseDownBackColor = UiTheme.AccentDark;
            b.Click += (s, e) => ShowPage(page);
            parent.Controls.Add(b);
            _buttons[page] = b;
        }

        public void ShowPage(AppPage page)
        {
            if (!_pages.TryGetValue(page, out var control)) return;

            _currentPage = page;
            _content.SuspendLayout();
            _content.Controls.Clear();
            control.Dock = DockStyle.Fill;
            _content.Controls.Add(control);
            _content.ResumeLayout();
            if (_compactNavigation.SelectedIndex != (int)page)
                _compactNavigation.SelectedIndex = (int)page;
            ApplyResponsiveLayout();

            foreach (var kv in _buttons)
            {
                bool active = kv.Key == page;
                kv.Value.BackColor = active ? UiTheme.Accent : UiTheme.Sidebar;
                kv.Value.ForeColor = active ? Color.White : UiTheme.ChromeText;
                kv.Value.FlatAppearance.BorderSize = active ? 1 : 0;
                kv.Value.FlatAppearance.BorderColor = active ? UiTheme.ChromeMuted : UiTheme.Sidebar;
                kv.Value.Font = new Font("Arial", 9.5F, active ? FontStyle.Bold : FontStyle.Regular);
            }
        }
    }
}
