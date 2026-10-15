using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiningVolume2023.UI
{
    /// <summary>
    /// Typeset three exact formulas used by SectionVolumeCalculator.
    /// Uses real stacked fractions and a radicand overbar (not a/b text).
    /// Draws only when the palette is exposed/resized: no AutoCAD redraw cost.
    /// </summary>
    internal sealed class ProfessionalFormulaPanel : Control
    {
        public ProfessionalFormulaPanel()
        {
            Height = 130;
            Dock = DockStyle.Top;
            BackColor = Color.FromArgb(247, 251, 255);
            ForeColor = UiTheme.TextStrong;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var regular = new Font("Cambria Math", 12F, FontStyle.Regular))
            using (var small = new Font("Arial", 9F, FontStyle.Regular))
            using (var pen = new Pen(ForeColor, 1.15F))
            using (var brush = new SolidBrush(ForeColor))
            {
                string[] captions = {
                    "Chênh lệch diện tích ≤ 40%",
                    "Một đầu có diện tích bằng 0",
                    "Chênh lệch diện tích > 40%"
                };
                for (int row = 0; row < 3; row++)
                {
                    float y = 5 + row * 40;
                    float fractionX = 32;
                    e.Graphics.DrawString("V =", regular, brush, 8, y + 6);
                    string numerator = row == 2 ? "L(F₁ + F₂ + √F₁F₂)" : "L(F₁ + F₂)";
                    string denominator = row == 0 ? "2" : "3";
                    float numeratorWidth = e.Graphics.MeasureString(numerator, regular).Width;
                    float fracWidth = Math.Max(148F, numeratorWidth + 12);
                    using (var format = new StringFormat { Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center })
                    {
                        var nx = new RectangleF(fractionX, y, fracWidth, 21);
                        e.Graphics.DrawString(numerator, regular, brush, nx, format);
                        e.Graphics.DrawLine(pen, fractionX + 3, y + 22,
                            fractionX + fracWidth - 3, y + 22);
                        e.Graphics.DrawString(denominator, regular, brush,
                            new RectangleF(fractionX, y + 21, fracWidth, 17), format);
                    }
                    float captionX = fractionX + fracWidth + 22;
                    if (Width > captionX + 165)
                        e.Graphics.DrawString(captions[row], small, brush, captionX, y + 12);
                }
            }
        }
    }
}
