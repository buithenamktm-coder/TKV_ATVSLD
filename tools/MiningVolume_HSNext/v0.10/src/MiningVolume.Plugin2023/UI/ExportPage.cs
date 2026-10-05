using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MiningVolume.Core.Reporting;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    public sealed class ExportPage : UserControl
    {
        private readonly CheckBox _xyz;
        private readonly CheckBox _sectionData;
        private readonly CheckBox _sectionArea;
        private readonly CheckBox _detail;
        private readonly CheckBox _levels;
        private readonly CheckBox _summary;
        private readonly CheckBox _warnings;
        private readonly TextBox _developer;
        private readonly TextBox _contact;
        private readonly Button _export;
        private readonly Label _status;

        public ExportPage()
        {
            Font = new Font("Arial", 9F);
            BackColor = Color.White;

            Controls.Add(new Label
            {
                Text = "XUẤT BẢNG EXCEL",
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Arial", 11F, FontStyle.Bold)
            });

            var developerBox = new GroupBox { Text = "Thông tin người phát triển / liên hệ", Dock = DockStyle.Top, Height = 108 };
            var devTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(6) };
            devTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            devTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            devTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            devTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            _developer = new TextBox { Dock = DockStyle.Fill, Text = ProjectState.Current.DeveloperName, Font = new Font("Arial", 9F) };
            _contact = new TextBox { Dock = DockStyle.Fill, Text = ProjectState.Current.DeveloperContact, Font = new Font("Arial", 9F) };
            devTable.Controls.Add(LabelCell("Người phát triển phần mềm"), 0, 0);
            devTable.Controls.Add(_developer, 1, 0);
            devTable.Controls.Add(LabelCell("Địa chỉ liên hệ / email / điện thoại"), 0, 1);
            devTable.Controls.Add(_contact, 1, 1);
            developerBox.Controls.Add(devTable);
            Controls.Add(developerBox);
            developerBox.BringToFront();

            var box = new GroupBox { Text = "Nội dung xuất", Dock = DockStyle.Top, Height = 222 };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(7) };
            _xyz = AddCheck(flow, "Dữ liệu X-Y-Z mô hình hiện trạng và thiết kế");
            _sectionData = AddCheck(flow, "Dữ liệu hình học mặt cắt X-Y-Z");
            _sectionArea = AddCheck(flow, "Thông số + diện tích đào, đắp từng mặt cắt");
            _detail = AddCheck(flow, "Khối lượng chi tiết giữa các mặt cắt");
            _levels = AddCheck(flow, "Tổng hợp khối lượng theo tầng / mức");
            _summary = AddCheck(flow, "Tổng hợp toàn khu vực");
            _warnings = AddCheck(flow, "Cảnh báo và ghi chú kiểm soát");
            box.Controls.Add(flow);
            Controls.Add(box);
            box.BringToFront();

            Controls.Add(new Label
            {
                Text = "Excel: Arial 12 • không tô nền • toàn bộ bảng đóng khung • số liệu 2–3 chữ số thập phân theo loại dữ liệu.",
                Dock = DockStyle.Top,
                Height = 42,
                ForeColor = Color.DimGray,
                Padding = new Padding(4, 6, 4, 4)
            });

            var bottom = new TableLayoutPanel { Dock = DockStyle.Top, Height = 72, ColumnCount = 2, Padding = new Padding(4) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            _status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, Text = "Cần thành lập mặt cắt và tính khối lượng trước khi xuất." };
            _export = new Button { Text = "XUẤT EXCEL", Dock = DockStyle.Top, Height = 38, Font = new Font("Arial", 10F, FontStyle.Bold) };
            _export.Click += ExportClicked;
            bottom.Controls.Add(_status, 0, 0);
            bottom.Controls.Add(_export, 1, 0);
            Controls.Add(bottom);
            bottom.BringToFront();

            _developer.TextChanged += ReportInfoChanged;
            _contact.TextChanged += ReportInfoChanged;
            ProjectState.Current.Changed += StateChanged;
            RefreshState();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ProjectState.Current.Changed -= StateChanged;
            base.Dispose(disposing);
        }

        private async void ExportClicked(object sender, EventArgs e)
        {
            var st = ProjectState.Current;
            if (st.VolumeResult == null || st.SectionProfiles.Count == 0)
            {
                MessageBox.Show("Chưa có đủ mặt cắt và kết quả khối lượng để xuất.", "Xuất Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new SaveFileDialog
            {
                Title = "Xuất báo cáo MiningVolume",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = "MiningVolume_KhoiLuong_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var opt = new ExportOptions
                {
                    IncludeSourceXyz = _xyz.Checked,
                    IncludeSectionData = _sectionData.Checked,
                    IncludeSectionAreas = _sectionArea.Checked,
                    IncludeIntervals = _detail.Checked,
                    IncludeLevels = _levels.Checked,
                    IncludeSummary = _summary.Checked,
                    IncludeWarnings = _warnings.Checked,
                    DeveloperName = _developer.Text?.Trim() ?? string.Empty,
                    DeveloperContact = _contact.Text?.Trim() ?? string.Empty
                };

                try
                {
                    _export.Enabled = false;
                    UseWaitCursor = true;
                    _status.Text = "Đang lập workbook và ghi file Excel...";
                    var report = await Task.Run(() => ExportWorkflowService.BuildReport(st, opt));
                    await Task.Run(() => SimpleXlsxWriter.Write(dlg.FileName, report));
                    _status.Text = "Đã xuất: " + dlg.FileName;
                    MessageBox.Show("Đã xuất báo cáo Excel thành công.\n\n" + dlg.FileName, "MiningVolume 2023", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    _status.Text = "Xuất Excel chưa thành công.";
                    MessageBox.Show(ex.Message, "Xuất Excel", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    UseWaitCursor = false;
                    RefreshState();
                }
            }
        }

        private void ReportInfoChanged(object sender, EventArgs e)
        {
            ProjectState.Current.DeveloperName = _developer.Text?.Trim() ?? string.Empty;
            ProjectState.Current.DeveloperContact = _contact.Text?.Trim() ?? string.Empty;
        }

        private void StateChanged(object sender, EventArgs e)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(new Action(RefreshState)); else RefreshState();
        }

        private void RefreshState()
        {
            var st = ProjectState.Current;
            if (!_developer.Focused) _developer.Text = st.DeveloperName ?? string.Empty;
            if (!_contact.Focused) _contact.Text = st.DeveloperContact ?? string.Empty;
            bool ready = st.VolumeResult != null && st.SectionProfiles.Count > 0;
            _export.Enabled = ready;
            if (ready)
                _status.Text = $"Sẵn sàng xuất: {st.SectionProfiles.Count:n0} mặt cắt; {st.VolumeResult.Intervals.Count:n0} khoảng; {st.VolumeResult.Levels.Count:n0} tầng/mức.";
            else
                _status.Text = "Cần thành lập mặt cắt và tính khối lượng trước khi xuất.";
        }

        private static CheckBox AddCheck(Control parent, string text)
        {
            var c = new CheckBox { Text = text, Checked = true, AutoSize = true, Margin = new Padding(4, 3, 4, 3) };
            parent.Controls.Add(c);
            return c;
        }

        private static Label LabelCell(string text) => new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Arial", 9F, FontStyle.Bold)
        };
    }
}