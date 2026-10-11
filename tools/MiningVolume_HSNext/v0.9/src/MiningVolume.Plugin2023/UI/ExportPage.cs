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
        private readonly CheckBox _toCad;
        private readonly CheckBox _toExcel;
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
            _developer = new TextBox { Dock = DockStyle.Fill, Text = ExportOptions.FixedDeveloperName, ReadOnly = true, Font = new Font("Arial", 9F) };
            _contact = new TextBox { Dock = DockStyle.Fill, Text = ExportOptions.FixedDeveloperContact, ReadOnly = true, Font = new Font("Arial", 9F) };
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
                Text = "Excel: Arial 12 • bảng đóng khung • công thức liên kết thật • logo IMSAT ở sheet báo cáo chính.",
                Dock = DockStyle.Top,
                Height = 42,
                ForeColor = Color.DimGray,
                Padding = new Padding(4, 6, 4, 4)
            });

            var targets = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 35, WrapContents = false, Padding = new Padding(7, 4, 4, 0) };
            _toCad = new CheckBox { Text = "Xuất bảng lên CAD", AutoSize = true, Checked = false, Margin = new Padding(4, 4, 20, 4) };
            _toExcel = new CheckBox { Text = "Xuất file Excel", AutoSize = true, Checked = true, Margin = new Padding(4, 4, 20, 4) };
            targets.Controls.Add(_toCad);
            targets.Controls.Add(_toExcel);
            Controls.Add(targets);
            targets.BringToFront();

            var bottom = new TableLayoutPanel { Dock = DockStyle.Top, Height = 72, ColumnCount = 2, Padding = new Padding(4) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            _status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DimGray, Text = "Cần thành lập mặt cắt và tính khối lượng trước khi xuất." };
            _export = new Button { Text = "XUẤT EXCEL / CAD", Dock = DockStyle.Top, Height = 38, Font = new Font("Arial", 10F, FontStyle.Bold) };
            _export.Click += ExportClicked;
            bottom.Controls.Add(_status, 0, 0);
            bottom.Controls.Add(_export, 1, 0);
            Controls.Add(bottom);
            bottom.BringToFront();

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
            bool exportCad = _toCad.Checked;
            bool exportExcel = _toExcel.Checked;
            if (!exportCad && !exportExcel)
            {
                MessageBox.Show("Chọn ít nhất một hình thức xuất: CAD hoặc Excel.", "IMSAT VOLUME",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (st.VolumeResult == null || st.SectionProfiles.Count == 0)
            {
                MessageBox.Show("Chưa có đủ mặt cắt và kết quả khối lượng để xuất.", "IMSAT VOLUME",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Prompt CAD only if the user explicitly requests CAD output.
            Autodesk.AutoCAD.Geometry.Point3d? cadInsertion = null;
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (exportCad)
            {
                if (doc == null) { MessageBox.Show("Không có bản vẽ AutoCAD đang hoạt động."); return; }
                cadInsertion = SelectionService.PickInsertionPoint("Chọn điểm chèn bảng khối lượng lên CAD: ");
                if (!cadInsertion.HasValue) return;
            }

            string excelPath = null;
            if (exportExcel)
            {
                using (var dlg = new SaveFileDialog
                {
                    Title = "Xuất báo cáo IMSAT VOLUME",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    DefaultExt = "xlsx",
                    AddExtension = true,
                    OverwritePrompt = true,
                    FileName = "IMSAT_VOLUME_KhoiLuong_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx"
                })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    excelPath = dlg.FileName;
                }
            }

            var opt = new ExportOptions
            {
                IncludeSourceXyz = _xyz.Checked,
                IncludeSectionData = _sectionData.Checked,
                IncludeSectionAreas = _sectionArea.Checked,
                IncludeIntervals = _detail.Checked,
                IncludeLevels = _levels.Checked,
                IncludeSummary = _summary.Checked,
                IncludeWarnings = _warnings.Checked,
                DeveloperName = ExportOptions.FixedDeveloperName,
                DeveloperContact = ExportOptions.FixedDeveloperContact
            };
            var currentResult = st.VolumeResult;
            bool excelWritten = false;
            try
            {
                _export.Enabled = false;
                UseWaitCursor = true;
                if (exportCad)
                    SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers: true);

                if (exportExcel)
                {
                    _status.Text = "Đang lập workbook và ghi file Excel...";
                    var report = await Task.Run(() => ExportWorkflowService.BuildReport(st, opt));
                    await Task.Run(() => ExportWorkflowService.WriteXlsx(excelPath, report));
                    excelWritten = true;
                }

                if (exportCad)
                {
                    if (!ReferenceEquals(doc, Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument))
                        throw new InvalidOperationException("Bản vẽ AutoCAD đã thay đổi trong lúc xuất; không chèn bảng vào bản vẽ khác.");
                    if (!ReferenceEquals(currentResult, st.VolumeResult))
                        throw new InvalidOperationException("Khối lượng đã thay đổi trong lúc xuất; hãy xuất lại để tránh dùng bảng cũ.");
                    _status.Text = "Đang chèn bảng khối lượng TABLE vào AutoCAD...";
                    using (doc.LockDocument())
                        CadVolumeTableService.Insert(doc.Database, currentResult, cadInsertion.Value);
                }

                _status.Text = exportExcel && exportCad ? "Đã xuất bảng CAD và file Excel." :
                    exportCad ? "Đã xuất bảng khối lượng lên AutoCAD." : "Đã xuất: " + excelPath;
                MessageBox.Show(_status.Text, "IMSAT VOLUME", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _status.Text = excelWritten
                    ? "Excel đã xuất, nhưng thao tác CAD chưa hoàn tất."
                    : "Xuất khối lượng chưa thành công.";
                MessageBox.Show(_status.Text + Environment.NewLine + ex.Message,
                    "Xuất khối lượng", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                RefreshState();
            }
        }

        private void StateChanged(object sender, EventArgs e)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(new Action(RefreshState)); else RefreshState();
        }

        private void RefreshState()
        {
            var st = ProjectState.Current;
            _developer.Text = ExportOptions.FixedDeveloperName;
            _contact.Text = ExportOptions.FixedDeveloperContact;
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