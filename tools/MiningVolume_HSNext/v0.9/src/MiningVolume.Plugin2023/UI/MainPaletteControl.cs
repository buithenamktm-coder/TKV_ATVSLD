using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    public enum AppPage { Project, Data, Model, Section, Volume, Grid, Mineral, Export, License, QuickSection }

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
        private readonly ComboBox _subNavigation;
        private readonly Panel _subNavigationPanel;
        private readonly AppPage[] _groups = { AppPage.Model, AppPage.Volume, AppPage.Export };
        private AppPage _currentGroup = AppPage.Project;
        private bool _updatingNav;
        private readonly HashSet<AppPage> _expandedGroups = new HashSet<AppPage>();
        private readonly Button _advancedVolumeButton;

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
            _compactNavigation.Items.AddRange(new object[] { "1  Dữ liệu", "2  Tính toán", "3  Kết quả" });
            _compactNavigation.SelectedIndexChanged += (sender, args) =>
            {
                if (!_updatingNav && _compactNavigation.SelectedIndex >= 0)
                    ShowGroup(_groups[_compactNavigation.SelectedIndex]);
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

            _subNavigation = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Arial", 9F), BackColor = UiTheme.Surface, ForeColor = UiTheme.TextStrong };
            _subNavigation.SelectedIndexChanged += (sender, args) =>
            {
                if (_updatingNav || _subNavigation.SelectedIndex < 0) return;
                var pages = VisiblePages(_currentGroup);
                if (_subNavigation.SelectedIndex < pages.Length) ShowPage(pages[_subNavigation.SelectedIndex]);
            };
            _subNavigationPanel = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(12, 5, 12, 5), BackColor = UiTheme.Canvas };
            _advancedVolumeButton = new Button {
                Dock = DockStyle.Right, Width = 96, Text = "Tùy chọn",
                Visible = false, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 9F)
            };
            _advancedVolumeButton.FlatAppearance.BorderSize = 0;
            _advancedVolumeButton.Click += (sender, args) => {
                if (!_expandedGroups.Add(_currentGroup))
                    _expandedGroups.Remove(_currentGroup);
                ShowGroup(_currentGroup);
            };
            _subNavigationPanel.Controls.Add(_subNavigation);
            _subNavigationPanel.Controls.Add(_advancedVolumeButton);
            Controls.Add(_content);
            Controls.Add(_subNavigationPanel);
            Controls.Add(_compactHeader);
            Controls.Add(_sidebar);
            Controls.Add(header);

            var modelWorkflow = new ModelWorkflowPage();
            _pages = new Dictionary<AppPage, Control>
            {
                [AppPage.Project] = new ProjectPage(),
                [AppPage.Data] = modelWorkflow,
                [AppPage.Model] = modelWorkflow,
                [AppPage.Section] = new SectionPage(false, ShowPage),
                [AppPage.QuickSection] = new SectionPage(true),
                [AppPage.Volume] = new VolumePage(),
                [AppPage.Grid] = new GridPage(),
                [AppPage.Mineral] = new MineralPage(),
                [AppPage.Export] = new ExportPage(),
                [AppPage.License] = new ContactInfoPage()
            };

            foreach (var page in new HashSet<Control>(_pages.Values))
                UiTheme.ApplyPage(page);

            AddNav(nav, AppPage.Model, "1", "Dữ liệu");
            AddNav(nav, AppPage.Volume, "2", "Tính toán");
            AddNav(nav, AppPage.Export, "3", "Kết quả");

            ShowGroup(AppPage.Model);
            SizeChanged += (sender, args) => ApplyResponsiveLayout();
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            // AutoCAD docked-left palettes commonly have only 320-350 px.
            // Replace the permanent side menu with a compact TOP selector,
            // giving narrow windows their full width for vertically scrolling
            // workflow pages. No page is recreated; form values are preserved.
            bool compact = ClientSize.Width < 560;
            if (_compact != compact)
            {
                _compact = compact;
                _sidebar.Visible = !compact;
                _compactHeader.Visible = compact;
                _updatingNav = true;
                _compactNavigation.SelectedIndex = Array.IndexOf(_groups, _currentGroup);
                _updatingNav = false;
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

            // Do not ship the former IMSAT logo under a NAMVECTA title.
            // Work must add the approved PA3 asset from the user's brand DOCX
            // before the NAMVECTA release acceptance gate can pass.
            int textLeft = 14;

            // Paint the title directly on the frosted header instead of
            // placing transparent WinForms Labels over a UserPaint panel.
            // The latter caused opaque rectangular patches behind the title
            // in AutoCAD's palette host (as shown in the field QA capture).
            const string title = "NAMVECTA VOLUME";
            string buildLabel = "V2.0 QA · " + RuntimeBuildIdentity.ShortTag();
            const string subtitle = "Phát triển: Bùi Thế Nam  •  ĐT/Zalo: 0967280686";
            int left = textLeft;
            header.Paint += (sender, args) =>
            {
                args.Graphics.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using (var big = new Font("Arial", 15F, FontStyle.Bold))
                using (var small = new Font("Arial", 9F))
                using (var white = new SolidBrush(UiTheme.ChromeText))
                using (var muted = new SolidBrush(UiTheme.ChromeMuted))
                {
                    args.Graphics.DrawString(title, big, white, left, 12);
                    args.Graphics.DrawString(subtitle, small, muted, left + 1, 43);
                    using (var buildFont = new Font("Arial", 7F, FontStyle.Bold))
                        args.Graphics.DrawString(buildLabel, buildFont, white, left + 1, 61);
                }
            };

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
                Text = "TKL  •  Hiện / ẩn NAMVECTA VOLUME",
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
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 5),
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
            b.Click += (s, e) => ShowGroup(page);
            parent.Controls.Add(b);
            _buttons[page] = b;
        }

        private static AppPage[] GetGroupPages(AppPage group)
        {
            switch (group)
            {
                case AppPage.Model: return new[] { AppPage.Data };
                case AppPage.Volume: return new[] { AppPage.Section, AppPage.Grid };
                case AppPage.Export: return new[] { AppPage.Export };
                default: return new[] { AppPage.Data };
            }
        }

        private AppPage[] VisiblePages(AppPage group)
        {
            if (!_expandedGroups.Contains(group)) return GetGroupPages(group);
            switch (group)
            {
                case AppPage.Model: return new[] { AppPage.Data, AppPage.Project };
                case AppPage.Volume: return new[] { AppPage.Section, AppPage.Grid, AppPage.Volume, AppPage.QuickSection, AppPage.Mineral };
                case AppPage.Export: return new[] { AppPage.Export, AppPage.License };
                default: return GetGroupPages(group);
            }
        }

        private static string PageLabel(AppPage page)
        {
            switch (page)
            {
                case AppPage.Data: return "TIN hiện trạng / thiết kế";
                case AppPage.Project: return "Thông tin dự án / đơn vị";
                case AppPage.Model: return "Xây dựng TIN";
                case AppPage.Section: return "Theo mặt cắt";
                case AppPage.Volume: return "Chi tiết khối lượng mặt cắt";
                case AppPage.Grid: return "Theo lưới ô vuông";
                case AppPage.Mineral: return "Vỉa than/quặng";
                case AppPage.Export: return "Xuất CAD / Excel";
                case AppPage.QuickSection: return "Vẽ nhanh mặt cắt";
                case AppPage.License: return "Giới thiệu / bản quyền";
                default: return page.ToString();
            }
        }

        public void ShowGroup(AppPage group)
        {
            _currentGroup = group;
            var pages = VisiblePages(group);
            _advancedVolumeButton.Visible = true;
            _advancedVolumeButton.Text = _expandedGroups.Contains(group) ? "Thu gọn" : "Tùy chọn";
            _subNavigation.Items.Clear();
            foreach (var page in pages) _subNavigation.Items.Add(PageLabel(page));
            _subNavigationPanel.Visible = true;
            _subNavigation.Visible = pages.Length > 1;
            _subNavigation.SelectedIndex = 0;
            int groupIndex = Array.IndexOf(_groups, group);
            if (groupIndex >= 0) _compactNavigation.SelectedIndex = groupIndex;
            _updatingNav = false;
            ShowPage(pages[0]);
        }

        public void ShowPage(AppPage page)
        {
            if (!_pages.TryGetValue(page, out var control)) return;

            // Public MV_* commands can open a legacy page directly.
            // Keep the simplified 5-group navigation in sync with that page.
            AppPage targetGroup;
            switch (page)
            {
                case AppPage.Data:
                case AppPage.Model:
                case AppPage.Project: targetGroup = AppPage.Model; break;
                case AppPage.Section:
                case AppPage.Volume:
                case AppPage.Grid:
                case AppPage.Mineral:
                case AppPage.QuickSection: targetGroup = AppPage.Volume; break;
                case AppPage.Export:
                case AppPage.License: targetGroup = AppPage.Export; break;
                default: targetGroup = AppPage.Model; break;
            }
            // Legacy MV_* commands keep access to expert pages while the
            // ordinary user sees only the three main workflow steps.
            if (Array.IndexOf(VisiblePages(targetGroup), page) < 0)
                _expandedGroups.Add(targetGroup);
            if (_currentGroup != targetGroup)
            {
                _currentGroup = targetGroup;
                _updatingNav = true;
                _subNavigation.Items.Clear();
                foreach (var member in VisiblePages(targetGroup))
                    _subNavigation.Items.Add(PageLabel(member));
                _subNavigationPanel.Visible = true;
                _subNavigation.Visible = _subNavigation.Items.Count > 1;
                _updatingNav = false;
            }

            if (control is ModelWorkflowPage workflow)
            {
                if (page == AppPage.Data) workflow.ShowInput();
                else workflow.ShowModel();
            }
            _currentPage = page;
            _content.SuspendLayout();
            _content.Controls.Clear();
            control.Dock = DockStyle.Fill;
            _content.Controls.Add(control);
            _content.ResumeLayout();
            _updatingNav = true;
            _advancedVolumeButton.Visible = true;
            _advancedVolumeButton.Text = _expandedGroups.Contains(_currentGroup) ? "Thu gọn" : "Tùy chọn";
            _subNavigation.Visible = VisiblePages(_currentGroup).Length > 1;
            _updatingNav = true;
            _subNavigation.Items.Clear();
            foreach (var member in VisiblePages(_currentGroup))
                _subNavigation.Items.Add(PageLabel(member));
            var groupPages = VisiblePages(_currentGroup);
            int subIndex = Array.IndexOf(groupPages, page);
            if (subIndex >= 0) _subNavigation.SelectedIndex = subIndex;
            int groupIndex = Array.IndexOf(_groups, _currentGroup);
            if (groupIndex >= 0) _compactNavigation.SelectedIndex = groupIndex;
            _updatingNav = false;
            ApplyResponsiveLayout();

            foreach (var kv in _buttons)
            {
                bool active = kv.Key == _currentGroup;
                kv.Value.BackColor = active ? UiTheme.Accent : UiTheme.Sidebar;
                kv.Value.ForeColor = active ? Color.White : UiTheme.ChromeText;
                // Active navigation is indicated by the fill only.
                // A white border is clipped at WinForms rounded corners.
                kv.Value.FlatAppearance.BorderSize = 0;
                kv.Value.FlatAppearance.BorderColor = active ? UiTheme.ChromeMuted : UiTheme.Sidebar;
                kv.Value.Font = new Font("Arial", 9.5F, active ? FontStyle.Bold : FontStyle.Regular);
            }
        }
    }
}
