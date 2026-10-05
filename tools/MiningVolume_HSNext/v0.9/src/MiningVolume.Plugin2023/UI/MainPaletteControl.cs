using System.Collections.Generic;
using System.Drawing;
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
            BackColor = Color.White;
            Font = new Font("Arial", 9F);

            var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.FromArgb(245, 245, 245) };
            header.Controls.Add(new Label { Text = "MINING VOLUME 2023", AutoSize = true, Font = new Font("Arial", 12F, FontStyle.Bold), Location = new Point(14, 10) });
            header.Controls.Add(new Label { Text = "HS-Next • Mine Survey & Earthwork • AutoCAD 2023", AutoSize = true, Location = new Point(15, 35), ForeColor = Color.DimGray });
            Controls.Add(header);

            var nav = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 145, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(7, 10, 7, 7), BackColor = Color.FromArgb(238, 238, 238) };
            Controls.Add(nav);
            _content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.White };
            Controls.Add(_content);
            _content.BringToFront();

            _pages = new Dictionary<AppPage, Control>
            {
                [AppPage.Project] = new ProjectPage(),
                [AppPage.Data] = new DataPage(),
                [AppPage.Model] = new ModelPage(),
                [AppPage.Section] = new SectionPage(),
                [AppPage.Volume] = new VolumePage(),
                [AppPage.Export] = new ExportPage()
            };

            AddNav(nav, AppPage.Project, "0. Dự án");
            AddNav(nav, AppPage.Data, "1. Dữ liệu");
            AddNav(nav, AppPage.Model, "2. TIN / Mô hình");
            AddNav(nav, AppPage.Section, "3. Mặt cắt");
            AddNav(nav, AppPage.Volume, "4. Khối lượng");
            AddNav(nav, AppPage.Export, "5. Xuất Excel");
            ShowPage(AppPage.Project);
        }

        private void AddNav(Control parent, AppPage page, string text)
        {
            var b = new Button { Text = text, Width = 124, Height = 42, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(2, 2, 2, 6) };
            b.FlatAppearance.BorderColor = Color.Silver;
            b.Click += (s, e) => ShowPage(page);
            parent.Controls.Add(b);
            _buttons[page] = b;
        }

        public void ShowPage(AppPage page)
        {
            if (!_pages.TryGetValue(page, out var control)) return;
            _content.Controls.Clear();
            control.Dock = DockStyle.Fill;
            _content.Controls.Add(control);
            foreach (var kv in _buttons)
            {
                kv.Value.BackColor = kv.Key == page ? Color.White : Color.FromArgb(238, 238, 238);
                kv.Value.Font = new Font("Arial", 9F, kv.Key == page ? FontStyle.Bold : FontStyle.Regular);
            }
        }
    }
}