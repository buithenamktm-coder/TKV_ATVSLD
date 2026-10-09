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
                Padding = new Padding(18, 14, 18, 18),
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
                Height = 86,
                BackColor = UiTheme.Sidebar
            };

            int textLeft = 16;
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "imsat_logo_64.png");
                if (File.Exists(logoPath))
                {
                    var logo = new PictureBox
                    {
                        Image = Image.FromFile(logoPath),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Size = new Size(58, 58),
                        Location = new Point(14, 13),
                        BackColor = Color.Transparent
                    };
                    header.Controls.Add(logo);
                    textLeft = 82;
                }
            }
            catch { }

            header.Controls.Add(new Label
            {
                Text = "IMSAT MINING VOLUME",
                AutoSize = true,
                Font = new Font("Arial", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(textLeft, 9)
            });
            header.Controls.Add(new Label
            {
                Text = "Mine Survey & Earthwork • AutoCAD",
                AutoSize = true,
                Font = new Font("Arial", 8.5F),
                ForeColor = Color.FromArgb(190, 204, 216),
                Location = new Point(textLeft + 1, 35)
            });
            header.Controls.Add(new Label
            {
                Text = "Phát triển: Bùi Thế Nam  •  Điện thoại: 0967280686",
                AutoSize = true,
                Font = new Font("Arial", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(122, 213, 181),
                Location = new Point(textLeft + 1, 58)
            });
            return header;
        }

        private static Panel BuildSidebar(out FlowLayoutPanel nav)
        {
            var sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 178,
                BackColor = UiTheme.Sidebar
            };

            var sectionTitle = new Label
            {
                Text = "QUY TRÌNH",
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(12, 12, 0, 0),
                Font = new Font("Arial", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(151, 169, 184)
            };

            var footer = new Label
            {
                Text = "Lệnh nhanh:  TKL\r\nHiện / ẩn bảng MiningVolume",
                Dock = DockStyle.Bottom,
                Height = 62,
                Padding = new Padding(12, 8, 6, 6),
                Font = new Font("Arial", 8F),
                ForeColor = Color.FromArgb(151, 169, 184),
                BackColor = Color.FromArgb(23, 35, 47)
            };

            nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 7, 8, 7),
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
                Width = 162,
                Height = 46,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(9, 0, 0, 0),
                Font = new Font("Arial", 9F),
                ForeColor = Color.FromArgb(224, 231, 237),
                BackColor = UiTheme.Sidebar,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = UiTheme.SidebarHover;
            b.FlatAppearance.MouseDownBackColor = UiTheme.AccentDark;
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
                kv.Value.ForeColor = active ? Color.White : Color.FromArgb(224, 231, 237);
                kv.Value.Font = new Font("Arial", 9F, active ? FontStyle.Bold : FontStyle.Regular);
            }
        }
    }
}
