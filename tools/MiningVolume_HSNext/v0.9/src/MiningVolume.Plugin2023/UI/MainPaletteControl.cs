using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MiningVolume2023.UI
{
    public enum AppPage { Project, Data, Model, Section, Volume, Export }

    public sealed class MainPaletteControl : UserControl
    {
        private readonly Panel _content;
        private readonly Dictionary<AppPage, Control> _pages;
        private readonly Dictionary<AppPage, Button> _buttons = new Dictionary<AppPage, Button>();

        public MainPaletteControl()
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.Canvas;
            Font = new Font("Arial", 9F);

            var header = BuildHeader();
            var sidebar = BuildSidebar(out var nav);

            _content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = UiTheme.Canvas
            };

            Controls.Add(_content);
            Controls.Add(sidebar);
            Controls.Add(header);

            _pages = new Dictionary<AppPage, Control>
            {
                [AppPage.Project] = new ProjectPage(),
                [AppPage.Data] = new DataPage(),
                [AppPage.Model] = new ModelPage(),
                [AppPage.Section] = new SectionPage(),
                [AppPage.Volume] = new VolumePage(),
                [AppPage.Export] = new ExportPage()
            };

            foreach (var page in _pages.Values)
                UiTheme.ApplyPage(page);

            AddNav(nav, AppPage.Project, "0", "Dự án");
            AddNav(nav, AppPage.Data, "1", "Dữ liệu");
            AddNav(nav, AppPage.Model, "2", "TIN / Mô hình");
            AddNav(nav, AppPage.Section, "3", "Mặt cắt");
            AddNav(nav, AppPage.Volume, "4", "Khối lượng");
            AddNav(nav, AppPage.Export, "5", "Xuất Excel");

            ShowPage(AppPage.Project);
        }

        private static Panel BuildHeader()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 66,
                BackColor = UiTheme.Surface
            };

            int textLeft = 14;
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "imsat_logo_64.png");
                if (File.Exists(logoPath))
                {
                    var logo = new PictureBox
                    {
                        Image = Image.FromFile(logoPath),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Size = new Size(48, 48),
                        Location = new Point(12, 8),
                        BackColor = Color.Transparent
                    };
                    header.Controls.Add(logo);
                    textLeft = 70;
                }
            }
            catch { }

            header.Controls.Add(new Label
            {
                Text = "IMSAT VOLUME",
                AutoSize = true,
                Font = new Font("Arial", 14F, FontStyle.Bold),
                ForeColor = UiTheme.TextStrong,
                Location = new Point(textLeft, 8)
            });
            header.Controls.Add(new Label
            {
                Text = "Phát triển: Bùi Thế Nam  •  Điện thoại: 0967280686",
                AutoSize = true,
                Font = new Font("Arial", 8.5F),
                ForeColor = UiTheme.Muted,
                Location = new Point(textLeft + 1, 37)
            });

            header.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = UiTheme.Border
            });
            return header;
        }

        private static Panel BuildSidebar(out FlowLayoutPanel nav)
        {
            var sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 152,
                BackColor = UiTheme.Sidebar
            };

            var sectionTitle = new Label
            {
                Text = "QUY TRÌNH",
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(10, 10, 0, 0),
                Font = new Font("Arial", 8F, FontStyle.Bold),
                ForeColor = UiTheme.Muted
            };

            var footer = new Label
            {
                Text = "TKL  •  Hiện / ẩn IMSAT VOLUME",
                Dock = DockStyle.Bottom,
                Height = 42,
                Padding = new Padding(10, 9, 6, 6),
                Font = new Font("Arial", 7.75F),
                ForeColor = UiTheme.Muted,
                BackColor = UiTheme.Sidebar
            };

            nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(7, 6, 7, 6),
                BackColor = UiTheme.Sidebar
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
                Width = 138,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 4),
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Arial", 9F),
                ForeColor = UiTheme.Text,
                BackColor = UiTheme.Sidebar,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = UiTheme.SidebarHover;
            b.FlatAppearance.MouseDownBackColor = UiTheme.Soft;
            b.Click += (s, e) => ShowPage(page);
            parent.Controls.Add(b);
            _buttons[page] = b;
        }

        public void ShowPage(AppPage page)
        {
            if (!_pages.TryGetValue(page, out var control)) return;

            _content.SuspendLayout();
            _content.Controls.Clear();
            control.Dock = DockStyle.Fill;
            _content.Controls.Add(control);
            _content.ResumeLayout();

            foreach (var kv in _buttons)
            {
                bool active = kv.Key == page;
                kv.Value.BackColor = active ? UiTheme.Accent : UiTheme.Sidebar;
                kv.Value.ForeColor = active ? Color.White : UiTheme.Text;
                kv.Value.Font = new Font("Arial", 9F, active ? FontStyle.Bold : FontStyle.Regular);
            }
        }
    }
}
