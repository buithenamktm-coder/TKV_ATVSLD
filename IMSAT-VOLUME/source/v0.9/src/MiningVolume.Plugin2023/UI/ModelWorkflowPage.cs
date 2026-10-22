using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiningVolume2023.UI
{
    /// <summary>
    /// One continuous model workflow: import survey geometry, then inspect
    /// the resulting current/design TINs. Both original controls are reused.
    /// </summary>
    public sealed class ModelWorkflowPage : UserControl
    {
        private readonly TabControl _steps;
        public ModelWorkflowPage()
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.Canvas;
            Font = new Font("Arial", 9F);
            _steps = new TabControl { Dock = DockStyle.Fill };
            var data = new TabPage("1. Nạp dữ liệu và tạo TIN");
            var model = new TabPage("2. Quản lý mô hình TIN");
            data.Controls.Add(new DataPage { Dock = DockStyle.Fill });
            model.Controls.Add(new ModelPage { Dock = DockStyle.Fill });
            _steps.TabPages.Add(data);
            _steps.TabPages.Add(model);
            Controls.Add(_steps);
        }

        public void ShowInput() { _steps.SelectedIndex = 0; }
        public void ShowModel() { _steps.SelectedIndex = 1; }
    }
}
