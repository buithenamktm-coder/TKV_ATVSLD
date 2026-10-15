using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    /// <summary>15-day trial status and offline activation. Does not block CAD file access.</summary>
    public sealed class LicensePage : UserControl
    {
        private readonly Label _status;
        private readonly TextBox _machineId;
        private readonly TextBox _key;
        private readonly Button _activate;

        public LicensePage()
        {
            Dock = DockStyle.Fill;
            Font = new Font("Arial", 9F);
            BackColor = Color.White;
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9,
                Padding = new Padding(14, 8, 14, 8)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            Controls.Add(panel);

            panel.Controls.Add(new Label
            {
                Text = "Bản quyền IMSAT VOLUME",
                Dock = DockStyle.Fill, Font = new Font("Arial", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            _status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true };
            panel.Controls.Add(_status, 0, 1);

            panel.Controls.Add(new Label
            {
                Text = "Mã yêu cầu kích hoạt của máy này", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 2);
            _machineId = new TextBox { Dock = DockStyle.Fill, ReadOnly = true,
                Text = LicenseService.MachineId };
            panel.Controls.Add(_machineId, 0, 3);
            var copy = new Button { Text = "SAO CHÉP MÃ MÁY", Width = 175, Height = 33 };
            copy.Click += (s,e) => { Clipboard.SetText(_machineId.Text); };
            var copyPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            copyPanel.Controls.Add(copy);
            panel.Controls.Add(copyPanel, 0, 4);

            panel.Controls.Add(new Label
            {
                Text = "Nhập key ký số hoặc dán nội dung file .mvlic", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 5);
            _key = new TextBox { Dock = DockStyle.Fill, Multiline = true,
                ScrollBars = ScrollBars.Vertical };
            panel.Controls.Add(_key, 0, 6);

            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            var load = new Button { Text = "ĐỌC FILE .MVLIC", AutoSize = true, Height = 34 };
            load.Click += (s,e) =>
            {
                using (var dlg = new OpenFileDialog
                {
                    Filter = "IMSAT License (*.mvlic)|*.mvlic|Text (*.txt)|*.txt",
                    CheckFileExists = true
                })
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        if (new FileInfo(dlg.FileName).Length > 32768)
                        {
                            MessageBox.Show("File giấy phép vượt giới hạn 32 KB.");
                            return;
                        }
                        _key.Text = File.ReadAllText(dlg.FileName).Trim();
                    }
            };
            _activate = new Button { Text = "KÍCH HOẠT", AutoSize = true, Height = 34 };
            _activate.Click += ActivateClick;
            var refresh = new Button { Text = "KIỂM TRA", AutoSize = true, Height = 34 };
            refresh.Click += (s,e) => RefreshStatus();
            tools.Controls.Add(load); tools.Controls.Add(_activate); tools.Controls.Add(refresh);
            panel.Controls.Add(tools, 0, 7);

            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill, ForeColor = Color.DimGray,
                Text = "Dùng thử 15 ngày liên tục từ lần đầu mở IMSAT VOLUME. " +
                    "Khi hết hạn, bản vẽ và dữ liệu DWG vẫn được giữ nguyên. " +
                    "Super Key chỉ thuộc License Manager của người cấp, không nhập tại đây.",
                AutoEllipsis = true
            }, 0, 8);
            RefreshStatus();
        }

        private void ActivateClick(object sender, EventArgs e)
        {
            try
            {
                string message = LicenseService.Activate(_key.Text);
                _key.Clear();
                MessageBox.Show(message, "IMSAT VOLUME – Kích hoạt",
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
            _status.Text = status.Description + Environment.NewLine +
                (status.CanCalculate ? "Các chức năng tính toán được sử dụng." :
                    "Không thể tính toán mới trước khi kích hoạt hợp lệ.");
            _status.ForeColor = status.CanCalculate ? Color.FromArgb(34, 96, 62) : Color.FromArgb(160, 48, 48);
        }
    }
}
