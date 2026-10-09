using System;
using System.Drawing;
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
        public static readonly Color Canvas = Color.FromArgb(242, 242, 247);
        public static readonly Color Surface = Color.White;
        public static readonly Color Sidebar = Color.FromArgb(248, 248, 250);
        public static readonly Color SidebarHover = Color.FromArgb(235, 235, 240);
        public static readonly Color Accent = Color.FromArgb(0, 122, 255);
        public static readonly Color AccentDark = Color.FromArgb(0, 102, 214);
        public static readonly Color TextStrong = Color.FromArgb(28, 28, 30);
        public static readonly Color Text = Color.FromArgb(58, 58, 60);
        public static readonly Color Muted = Color.FromArgb(99, 99, 102);
        public static readonly Color Border = Color.FromArgb(209, 209, 214);
        public static readonly Color Soft = Color.FromArgb(229, 229, 234);
        public static readonly Color Danger = Color.FromArgb(255, 59, 48);

        public static void ApplyPage(Control root)
        {
            if (root == null) return;
            root.BackColor = Canvas;
            root.ForeColor = Text;
            root.Font = new Font("Arial", 9F);
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
                label.Font = new Font("Arial", 11.5F, FontStyle.Bold);
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
            group.BackColor = Surface;
            group.ForeColor = TextStrong;
            group.Font = new Font("Arial", 9F, FontStyle.Bold);
            group.Padding = new Padding(10, 8, 10, 10);
        }

        private static void StyleButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Border;
            button.Font = new Font("Arial", 9F, button.Font.Bold ? FontStyle.Bold : FontStyle.Regular);
            button.Cursor = Cursors.Hand;
            if (button.MinimumSize.Height < 34)
                button.MinimumSize = new Size(button.MinimumSize.Width, 34);
            button.UseCompatibleTextRendering = true;

            string text = (button.Text ?? string.Empty).Trim().ToUpperInvariant();
            if (IsPrimary(text))
            {
                button.BackColor = Accent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = AccentDark;
                button.FlatAppearance.MouseOverBackColor = AccentDark;
                button.FlatAppearance.MouseDownBackColor = AccentDark;
            }
            else if (text.StartsWith("XÓA"))
            {
                button.BackColor = Surface;
                button.ForeColor = Danger;
                button.FlatAppearance.BorderColor = Color.FromArgb(226, 190, 190);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(252, 241, 241);
            }
            else
            {
                button.BackColor = Surface;
                button.ForeColor = TextStrong;
                button.FlatAppearance.MouseOverBackColor = Soft;
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(225, 233, 239);
            }
        }

        private static bool IsPrimary(string text)
        {
            return text.StartsWith("TẠO ") ||
                   text.StartsWith("TÍNH ") ||
                   text.StartsWith("XUẤT ") ||
                   text.StartsWith("LƯU PROJECT") ||
                   text.StartsWith("XEM TRƯỚC");
        }

        private static void StyleTextBox(TextBox box)
        {
            box.Font = new Font("Arial", 9F);
            box.ForeColor = TextStrong;
            box.BackColor = box.ReadOnly ? Soft : Surface;
            box.BorderStyle = BorderStyle.FixedSingle;
        }

        private static void StyleInput(Control input)
        {
            input.Font = new Font("Arial", 9F);
            input.ForeColor = TextStrong;
            input.BackColor = Surface;
        }

        private static void StyleList(Control list)
        {
            list.Font = new Font("Arial", 9F);
            list.ForeColor = TextStrong;
            list.BackColor = Surface;
        }

        private static void StyleCheck(CheckBox check)
        {
            check.Font = new Font("Arial", 9F);
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
            grid.ColumnHeadersDefaultCellStyle.BackColor = Soft;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextStrong;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Soft;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextStrong;
            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 255);
            grid.DefaultCellStyle.SelectionForeColor = TextStrong;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 250);
            grid.RowHeadersVisible = false;
        }
    }
}
