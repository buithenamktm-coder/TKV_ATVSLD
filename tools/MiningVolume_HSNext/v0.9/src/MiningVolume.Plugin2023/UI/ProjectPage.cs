using System;
using System.Drawing;
using System.Windows.Forms;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    public sealed class ProjectPage : UserControl
    {
        private readonly Label _drawing;
        private readonly Label _saved;
        private readonly Label _summary;
        private readonly Label _status;
        private readonly Button _save;
        private readonly Button _load;
        private readonly Button _delete;
        private readonly CheckBox _autoOpen;

        public ProjectPage()
        {
            Font = new Font("Arial", 9F);
            BackColor = Color.White;
            Controls.Add(new Label { Text = "DỰ ÁN / PHIÊN LÀM VIỆC", Dock = DockStyle.Top, Height = 32, Font = new Font("Arial", 11F, FontStyle.Bold) });

            var box = new GroupBox { Text = "Project lưu trong bản vẽ DWG", Dock = DockStyle.Top, Height = 215 };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(8) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _drawing = ValueLabel(); _saved = ValueLabel(); _summary = ValueLabel();
            t.Controls.Add(KeyLabel("Bản vẽ hiện tại"), 0, 0); t.Controls.Add(_drawing, 1, 0);
            t.Controls.Add(KeyLabel("Project trong DWG"), 0, 1); t.Controls.Add(_saved, 1, 1);
            t.Controls.Add(KeyLabel("Trạng thái"), 0, 2); t.Controls.Add(_summary, 1, 2);
            t.Controls.Add(new Label { Text = "Dữ liệu được lưu gồm layer nguồn, điểm/đối tượng đã loại, Z đã sửa, trạng thái TIN, ranh, hướng, hệ mặt cắt đã hiệu chỉnh, mức/tầng, tỷ lệ và thông tin báo cáo. TIN/mặt cắt/khối lượng được dựng lại từ dữ liệu nguồn khi nạp Project.", Dock = DockStyle.Fill, AutoSize = true, ForeColor = Color.DimGray }, 0, 3);
            t.SetColumnSpan(t.GetControlFromPosition(0, 3), 2);
            box.Controls.Add(t);
            Controls.Add(box); box.BringToFront();

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(4, 7, 4, 4) };
            _save = Btn("LƯU PROJECT VÀO DWG", SaveProject); _save.Width = 175;
            _load = Btn("NẠP LẠI PROJECT", LoadProject); _load.Width = 145;
            _delete = Btn("XÓA PROJECT ĐÃ LƯU", DeleteProject); _delete.Width = 165;
            buttons.Controls.Add(_save); buttons.Controls.Add(_load); buttons.Controls.Add(_delete);
            Controls.Add(buttons); buttons.BringToFront();

            var preferences = new GroupBox { Text = "Tùy chọn giao diện", Dock = DockStyle.Top, Height = 64 };
            _autoOpen = new CheckBox
            {
                Text = "Tự động mở bảng IMSAT VOLUME khi khởi động AutoCAD",
                AutoSize = true,
                Left = 12,
                Top = 24,
                Checked = UserSettingsService.AutoOpenPalette
            };
            _autoOpen.CheckedChanged += AutoOpenChanged;
            preferences.Controls.Add(_autoOpen);
            Controls.Add(preferences); preferences.BringToFront();

            _status = new Label { Dock = DockStyle.Top, Height = 44, Padding = new Padding(7, 5, 4, 4), ForeColor = Color.DimGray, Text = "Project được lưu trực tiếp trong DWG, không cần file phụ bên ngoài." };
            Controls.Add(_status); _status.BringToFront();

            ProjectState.Current.Changed += StateChanged;
            RefreshState();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ProjectState.Current.Changed -= StateChanged;
            base.Dispose(disposing);
        }

        private void SaveProject(object sender, EventArgs e)
        {
            try
            {
                SetBusy(true, "Đang lưu Project vào DWG...");
                var saved = ProjectPersistenceService.SaveCurrentProject();
                _status.Text = "Đã lưu Project vào DWG lúc " + saved.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + ".";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Lưu Project", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { SetBusy(false, null); RefreshState(); }
        }

        private void LoadProject(object sender, EventArgs e)
        {
            try
            {
                SetBusy(true, "Đang nạp Project và dựng lại mô hình...");
                var r = ProjectPersistenceService.LoadCurrentProject(true);
                if (!r.Found) { _status.Text = r.Message; return; }
                _status.Text = r.Message;
                if (r.Warnings.Count > 0)
                    MessageBox.Show(string.Join("\r\n", r.Warnings.GetRange(0, Math.Min(30, r.Warnings.Count))) + (r.Warnings.Count > 30 ? "\r\n..." : string.Empty), "Cảnh báo khi nạp Project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Nạp Project", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { SetBusy(false, null); RefreshState(); }
        }

        private void DeleteProject(object sender, EventArgs e)
        {
            if (!ProjectPersistenceService.HasSavedProject()) { _status.Text = "Bản vẽ chưa có Project đã lưu."; return; }
            if (MessageBox.Show("Xóa dữ liệu Project IMSAT VOLUME đã lưu trong DWG?\r\nCác đối tượng CAD và dữ liệu nguồn không bị xóa.", "Xóa Project", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { ProjectPersistenceService.DeleteSavedProject(); _status.Text = "Đã xóa Project đã lưu khỏi DWG."; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Xóa Project", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            RefreshState();
        }

        private void AutoOpenChanged(object sender, EventArgs e)
        {
            try
            {
                UserSettingsService.AutoOpenPalette = _autoOpen.Checked;
                _status.Text = _autoOpen.Checked
                    ? "Đã bật tự động mở bảng IMSAT VOLUME cho các lần khởi động AutoCAD sau."
                    : "Đã tắt tự động mở bảng IMSAT VOLUME. Khi cần, mở từ Ribbon/menu IMSAT VOLUME.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Tùy chọn IMSAT VOLUME", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _autoOpen.CheckedChanged -= AutoOpenChanged;
                _autoOpen.Checked = UserSettingsService.AutoOpenPalette;
                _autoOpen.CheckedChanged += AutoOpenChanged;
            }
        }

        private void StateChanged(object sender, EventArgs e)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(new Action(RefreshState)); else RefreshState();
        }

        private void RefreshState()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            _drawing.Text = doc == null ? "-" : doc.Name;
            bool has = false;
            try { has = ProjectPersistenceService.HasSavedProject(); } catch { }
            var st = ProjectState.Current;
            _saved.Text = has ? (st.LastProjectSavedUtc.HasValue ? "Có • " + st.LastProjectSavedUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") : "Có") : "Chưa lưu";
            string tinHt = st.Existing.IsTinCurrent ? $"OK {st.Existing.Tin.Triangles.Count:n0} tam giác" : "CHƯA HỢP LỆ";
            string tinTk = st.Design.IsTinCurrent ? $"OK {st.Design.Tin.Triangles.Count:n0} tam giác" : "CHƯA HỢP LỆ";
            _summary.Text =
                $"TIN HT: {tinHt} • TIN TK: {tinTk} • " +
                $"MC: {(st.SectionSystem?.Lines.Count ?? 0):n0} • " +
                $"Khối lượng: {(st.VolumeResult == null ? "chưa tính" : "đã tính")}";
            _load.Enabled = has;
            _delete.Enabled = has;
        }

        private void SetBusy(bool busy, string text)
        {
            _save.Enabled = !busy; _load.Enabled = !busy; _delete.Enabled = !busy;
            UseWaitCursor = busy;
            if (!string.IsNullOrWhiteSpace(text)) _status.Text = text;
            _status.Refresh();
        }

        private static Button Btn(string text, EventHandler click) { var b = new Button { Text = text, Height = 30, Margin = new Padding(2) }; b.Click += click; return b; }
        private static Label KeyLabel(string text) => new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Arial", 9F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        private static Label ValueLabel() => new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    }
}