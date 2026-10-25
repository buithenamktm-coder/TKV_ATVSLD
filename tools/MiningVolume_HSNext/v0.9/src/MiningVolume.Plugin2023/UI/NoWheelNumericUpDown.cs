using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiningVolume2023.UI
{
    /// <summary>
    /// CAD quantity/scale inputs must NEVER change on mouse wheel.
    /// Only spinner arrows and keyboard editing can change their value.
    /// When nested inside a scrollable Palette panel, wheel scrolls the panel.
    /// </summary>
    internal sealed class NoWheelNumericUpDown : NumericUpDown
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            for (Control parent = Parent; parent != null; parent = parent.Parent)
            {
                var scroll = parent as ScrollableControl;
                if (scroll == null || !scroll.AutoScroll || !scroll.VerticalScroll.Visible)
                    continue;
                int current = -scroll.AutoScrollPosition.Y;
                int lines = SystemInformation.MouseWheelScrollLines;
                if (lines < 1 || lines > 15) lines = 3;
                int target = Math.Max(0, current - Math.Sign(e.Delta) * lines * 18);
                scroll.AutoScrollPosition = new Point(-scroll.AutoScrollPosition.X, target);
                return;
            }
            // Outside a scrollable panel: swallow the wheel, do not adjust Value.
        }
    }
}
