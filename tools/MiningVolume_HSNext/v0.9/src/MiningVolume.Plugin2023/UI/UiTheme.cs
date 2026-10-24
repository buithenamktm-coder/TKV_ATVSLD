using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MiningVolume2023.UI
{
    /// <summary>
    /// Visual system shared by every MiningVolume palette page.
    /// Keeps WinForms controls native/reliable while giving the add-in a
    /// consistent professional CAD-tool appearance.
    /// </summary>
    internal static class UiTheme
    {
        public static readonly Color Canvas = Color.FromArgb(239, 245, 251);
        public static readonly Color Surface = Color.White;
        public static readonly Color Sidebar = Color.FromArgb(28, 49, 76);
        public static readonly Color SidebarHover = Color.FromArgb(45, 78, 111);
        public static readonly Color Accent = Color.FromArgb(17, 96, 180);
        public static readonly Color AccentDark = Color.FromArgb(11, 71, 142);
        public static readonly Color TextStrong = Color.FromArgb(20, 36, 56);
        public static readonly Color Text = Color.FromArgb(30, 49, 71);
        public static readonly Color Muted = Color.FromArgb(70, 87, 105);
        public static readonly Color Border = Color.FromArgb(158, 187, 216);
        public static readonly Color Soft = Color.FromArgb(224, 237, 249);
        public static readonly Color Danger = Color.FromArgb(157, 31, 43);
        // CAD-tool buttons: clear outline against light panels, dark labels,
        // blue only for primary commands. Preserve the agreed vertical layout.
        public static readonly Color ButtonBorder = Color.FromArgb(93, 122, 152);
        public static readonly Color ButtonSecondary = Color.FromArgb(212, 230, 248);
        public static readonly Color ButtonText = Color.FromArgb(24, 43, 61);
        // Liquid Glass-inspired visual system. Opaque, composited-once surfaces
        // deliberately replace expensive live desktop blur inside AutoCAD.
        // Dark foreground/solid command fills maintain technical readability.
        public static readonly Color GlassSurface = Color.FromArgb(244, 249, 254);
        // High-contrast Liquid Glass chrome for AutoCAD dark model-space themes.
        public static readonly Color ChromeTop = Color.FromArgb(43, 73, 109);
        public static readonly Color ChromeBottom = Color.FromArgb(22, 40, 65);
        public static readonly Color ChromeText = Color.FromArgb(245, 250, 255);
        public static readonly Color ChromeMuted = Color.FromArgb(203, 223, 242);
        public static readonly Color ChromeEdge = Color.FromArgb(85, 122, 158);
        public static readonly Color GlassTop = Color.FromArgb(250, 253, 255);
        public static readonly Color GlassBottom = Color.FromArgb(225, 239, 250);
        public static readonly Color GlassEdge = Color.FromArgb(161, 190, 216);
        public static readonly Color GlassHighlight = Color.FromArgb(115, 255, 255, 255);


        public static void ApplyPage(Control root)
        {
            if (root == null) return;
            root.BackColor = Canvas;
            root.ForeColor = Text;
            root.Font = new Font("Arial", 9.5F);
            ApplyChildren(root);
        }

        private static void ApplyChildren(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is Button button) StyleButton(button);
                else if (c is DataGridView grid) StyleGrid(grid);
                else if (c is GroupBox group) StyleGroup(group);
                else if (c is TextBox textBox) StyleTextBox(textBox);
                else if (c is ComboBox combo) StyleInput(combo);
                else if (c is NumericUpDown numeric) StyleInput(numeric);
                else if (c is CheckedListBox checkedList) StyleList(checkedList);
                else if (c is ListBox list) StyleList(list);
                else if (c is CheckBox check) StyleCheck(check);
                else if (c is Label label) StyleLabel(label);
                else if (c is TabPage tabPage)
                {
                    tabPage.BackColor = Surface;
                    tabPage.ForeColor = Text;
                }
                else if (c is TableLayoutPanel || c is FlowLayoutPanel || c is Panel)
                {
                    if (c.BackColor == Color.White || c.BackColor == SystemColors.Control)
                        c.BackColor = Surface;
                    c.ForeColor = Text;
                }

                ApplyChildren(c);
            }
        }

        private static void StyleLabel(Label label)
        {
            if (!label.AutoSize) label.AutoEllipsis = true;
            if (label.Font.Bold && label.Font.Size >= 10.5F)
            {
                label.Font = new Font("Arial", 12F, FontStyle.Bold);
                label.ForeColor = TextStrong;
            }
            else if (label.ForeColor == Color.DimGray || label.ForeColor == SystemColors.GrayText)
            {
                label.ForeColor = Muted;
            }
            else if (label.ForeColor == SystemColors.ControlText || label.ForeColor == Color.Black)
            {
                label.ForeColor = Text;
            }

            if (label.BackColor == SystemColors.Control)
                label.BackColor = Color.Transparent;
        }

        private static void StyleGroup(GroupBox group)
        {
            group.BackColor = Color.FromArgb(247, 251, 255);
            group.ForeColor = TextStrong;
            group.Font = new Font("Arial", 9.5F, FontStyle.Bold);
            group.Padding = new Padding(6, 7, 6, 6);
        }

        private static void StyleButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            // Native WinForms 1px border clips at the rounded Region edge,
            // leaving black/white slivers on high-DPI AutoCAD palettes.
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.BorderColor = ButtonBorder;
            button.Font = new Font("Arial", 9F, FontStyle.Regular);
            button.Text = SentenceCaseButton(button.Text);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            // Never enlarge compact Palette buttons: 28px controls must fit 32px rows.
            if (button.MinimumSize.Height > 28)
                button.MinimumSize = new Size(button.MinimumSize.Width, 28);
            // Real whitespace between controls, not a larger clickable
            // surface touching adjacent buttons on short palettes.
            // Calculation preview and draw need a visible gap (>=6px),
            // but all other Palette buttons keep their compact margins.
            if (button.Name == "btnPreviewSections" ||
                button.Name == "btnDrawSections")
                button.Margin = new Padding(4, 2, 4, 2);
            else
                button.Margin = new Padding(2, 2, 2, 2);
            button.Padding = new Padding(4, 1, 4, 1);
            button.AutoEllipsis = true;
            button.UseCompatibleTextRendering = true;
            ApplyRoundedButtonMask(button);

            string text = (button.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (IsPrimary(text))
            {
                button.BackColor = Accent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = AccentDark;
                button.FlatAppearance.MouseOverBackColor = AccentDark;
                button.FlatAppearance.MouseDownBackColor = AccentDark;
            }
            else if (text.StartsWith("XÓA") || text.StartsWith("LOẠI ") || text.StartsWith("HỦY"))
            {
                button.BackColor = Color.FromArgb(255, 235, 237);
                button.ForeColor = Danger;
                button.FlatAppearance.BorderColor = Danger;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(253, 234, 235);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(249, 222, 224);
            }
            else
            {
                // A subtle blue tint and a defined border keep secondary actions
                // visible on the light palette without making every button dominant.
                button.BackColor = ButtonSecondary;
                button.ForeColor = ButtonText;
                button.FlatAppearance.BorderColor = ButtonBorder;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(190, 218, 245);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(168, 202, 237);
            }
        }

        // Sentence-case action labels, preserving AutoCAD/engineering acronyms.
        // Capitalization happens once at initialization, never in repaint.
        internal static string SentenceCaseButton(string original)
        {
            string value = (original ?? string.Empty).Trim();
            if (value.Length == 0) return value;
            bool hasLetter = false, allUpper = true;
            foreach (char ch in value)
            {
                if (!char.IsLetter(ch)) continue;
                hasLetter = true;
                if (char.IsLower(ch)) { allUpper = false; break; }
            }
            if (!hasLetter || !allUpper) return value;
            string lower = value.ToLowerInvariant();
            for (int i = 0; i < lower.Length; i++)
                if (char.IsLetter(lower[i]))
                {
                    lower = lower.Substring(0, i) + char.ToUpperInvariant(lower[i]) +
                        lower.Substring(i + 1);
                    break;
                }
            return Regex.Replace(lower,
                @"(?<!\p{L})(imsat|tin|cad|dwg|excel|ht|tk|2d|3d|z|mvlic)(?!\p{L})",
                m => string.Equals(m.Value, "excel", StringComparison.OrdinalIgnoreCase)
                    ? "Excel" : m.Value.ToUpperInvariant(),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Bo tròn góc 9 px ở kích thước Windows/Palette hiện tại. Recompute
        /// Region sau Resize để không bị cắt chữ khi DPI hoặc cửa sổ thay đổi.
        /// Không vẽ nút thành ảnh; WinForms Button giữ nguyên tương tác.
        /// </summary>
        internal static void ApplyRoundedButtonMask(Button button)
        {
            if (button == null) return;
            button.Resize -= RefreshButtonMask;
            button.Resize += RefreshButtonMask;
            button.HandleCreated -= RefreshButtonMask;
            button.HandleCreated += RefreshButtonMask;
            RefreshButtonMask(button, EventArgs.Empty);
        }

        private static void RefreshButtonMask(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button == null || button.Width < 12 || button.Height < 12) return;
            int radius = Math.Min(9, Math.Min(button.Width, button.Height) / 3);
            int diameter = radius * 2;
            using (var path = new GraphicsPath())
            {
                var rect = new Rectangle(0, 0, button.Width, button.Height);
                path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
                path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
                path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                var previous = button.Region;
                button.Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }

        private static bool IsPrimary(string text)
        {
            return text.StartsWith("TẠO ") ||
                   text.StartsWith("TÍNH ") ||
                   text.StartsWith("XUẤT ") ||
                   text.StartsWith("LƯU PROJECT") ||
                   text.StartsWith("CẬP NHẬT MẶT CẮT") ||
                   text.StartsWith("XEM TRƯỚC");
        }

        private static void StyleTextBox(TextBox box)
        {
            box.Font = new Font("Arial", 9.5F);
            box.ForeColor = TextStrong;
            box.BackColor = box.ReadOnly ? Soft : Surface;
            box.BorderStyle = BorderStyle.FixedSingle;
        }

        private static void StyleInput(Control input)
        {
            input.Font = new Font("Arial", 9.5F);
            input.ForeColor = TextStrong;
            input.BackColor = Surface;
        }

        private static void StyleList(Control list)
        {
            list.Font = new Font("Arial", 9.5F);
            list.ForeColor = TextStrong;
            list.BackColor = Surface;
        }

        private static void StyleCheck(CheckBox check)
        {
            check.Font = new Font("Arial", 9.5F);
            check.ForeColor = Text;
            check.BackColor = Color.Transparent;
        }

        private static void StyleGrid(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.GridColor = Border;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.ColumnHeadersDefaultCellStyle.BackColor = AccentDark;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 9.5F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = AccentDark;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(31, 105, 183);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(244, 249, 254);
            grid.ColumnHeadersHeight = Math.Max(grid.ColumnHeadersHeight, 34);
            grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 27);
            grid.RowHeadersVisible = false;
        }
    }

    /// <summary>
    /// Lightweight, non-blurring Liquid Glass-style panel for static palette
    /// chrome only. The native controls, DWG viewport and TIN rendering remain
    /// untouched; this paints once on resize/exposure rather than per CAD frame.
    /// </summary>
    internal sealed class GlassPanel : Panel
    {
        private readonly bool _dark;

        public GlassPanel() : this(false) { }

        public GlassPanel(bool dark)
        {
            _dark = dark;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = dark ? UiTheme.ChromeBottom : UiTheme.GlassTop;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var rect = ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using (var fill = new LinearGradientBrush(
                rect, _dark ? UiTheme.ChromeTop : UiTheme.GlassTop,
                _dark ? UiTheme.ChromeBottom : UiTheme.GlassBottom,
                LinearGradientMode.Vertical))
                e.Graphics.FillRectangle(fill, rect);
            // Frosted highlight and a precise separator evoke a glass surface
            // without Windows DWM transparency or per-frame blur operations.
            int shineHeight = Math.Max(1, Math.Min(14, rect.Height / 4));
            using (var shine = new LinearGradientBrush(
                new Rectangle(0, 0, rect.Width, shineHeight),
                _dark ? Color.FromArgb(53, 255, 255, 255) : UiTheme.GlassHighlight,
                Color.FromArgb(0, 255, 255, 255),
                LinearGradientMode.Vertical))
                e.Graphics.FillRectangle(shine, 0, 0, rect.Width, shineHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 2 || Height < 2) return;
            using (var pen = new Pen(_dark ? UiTheme.ChromeEdge : UiTheme.GlassEdge))
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}
