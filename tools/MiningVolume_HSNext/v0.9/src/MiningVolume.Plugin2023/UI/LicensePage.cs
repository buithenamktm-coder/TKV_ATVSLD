using System;
using System.Drawing;
using System.Windows.Forms;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    /// <summary>Simple offline Pass activation. Does not lock or remove CAD files.</summary>
    public sealed class LicensePage : UserControl
    {
        private readonly Label _status;
        private readonly TextBox _pass;
        private readonly Button _activate;

        public LicensePage()
        {
            Dock = DockStyle.Fill;
            Font = new Font("Arial", 9F);
            BackColor = Color.White;
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
                Padding = new Padding(10, 6, 10, 6)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(panel);

            panel.Controls.Add(new Label
            {
                Text = "Bản quyền NAMVECTA VOLUME 2.0",
                Dock = DockStyle.Fill, Font = new Font("Arial", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            _status = new Label { Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            panel.Controls.Add(_status, 0, 1);

            panel.Controls.Add(new Label
            {
                Text = "Nhập Pass kích hoạt offline",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            }, 0, 2);
            _pass = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true,
                Font = new Font("Arial", 9F) };
            panel.Controls.Add(_pass, 0, 3);

            var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            _activate = new Button { Text = "Kích hoạt", AutoSize = true, Height = 32 };
            _activate.Click += ActivateClick;
            var refresh = new Button { Text = "Kiểm tra", AutoSize = true, Height = 32 };
            refresh.Click += (s, e) => RefreshStatus();
            controls.Controls.Add(_activate);
            controls.Controls.Add(refresh);
            panel.Controls.Add(controls, 0, 4);

            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill, ForeColor = Color.DimGray,
                Text = "Dùng thử 15 ngày. Các Pass có thời hạn 1 tháng, 3 tháng, 6 tháng, " +
                    "1 năm hoặc vĩnh viễn. Không cần Internet hoặc mã máy. " +
                    "Nhập lại cùng Pass không tự gia hạn; dữ liệu DWG luôn được giữ nguyên.",
                AutoEllipsis = true
            }, 0, 5);
            RefreshStatus();
        }

        private void ActivateClick(object sender, EventArgs e)
        {
            try
            {
                string message = LicenseService.Activate(_pass.Text);
                _pass.Clear();
                MessageBox.Show(message, "NAMVECTA VOLUME – Kích hoạt",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không kích hoạt được",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void RefreshStatus()
        {
            var status = LicenseService.GetStatus();
            var expiry = status.ExpiresUtc.HasValue
                ? status.ExpiresUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : (status.State == LicenseState.Activated ? "Không hết hạn" : "");
            _status.Text = status.Description +
                (status.ActivatedUtc.HasValue
                    ? Environment.NewLine + "Kích hoạt: " +
                        status.ActivatedUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") +
                        " • Hết hạn: " + expiry
                    : "");
            _status.ForeColor = status.CanCalculate
                ? Color.FromArgb(34, 96, 62)
                : Color.FromArgb(160, 48, 48);
        }
    }
}