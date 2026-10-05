using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Sections;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    public sealed class SectionPage : UserControl
    {
        private readonly Label _boundary;
        private readonly Label _direction;
        private readonly NumericUpDown _spacing;
        private readonly NumericUpDown _levelStep;
        private readonly NumericUpDown _fromLevel;
        private readonly NumericUpDown _toLevel;
        private readonly NumericUpDown _hScale;
        private readonly NumericUpDown _vScale;
        private readonly DataGridView _grid;
        private readonly Label _status;
        private bool _syncing;

        public SectionPage()
        {
            Font = new Font("Arial", 9F); BackColor = Color.White;
            Controls.Add(new Label { Text = "HỆ MẶT CẮT", Dock = DockStyle.Top, Height = 32, Font = new Font("Arial", 11F, FontStyle.Bold) });

            var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 285, ColumnCount = 3, RowCount = 9, Padding = new Padding(4) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            _boundary = new Label { Text = "Chưa chọn", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _direction = new Label { Text = "Chưa chọn", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _spacing = Num(20, 0.1M, 10000);
            _levelStep = Num(5, 0.1M, 1000);
            _fromLevel = Num(0, -10000, 10000);
            _toLevel = Num(0, -10000, 10000);
            _hScale = Num(1000, 1, 100000);
            _vScale = Num(500, 1, 100000);

            Add(top, 0, "Đường bao", _boundary, Button("Chọn trên CAD", PickBoundary));
            Add(top, 1, "Hướng mặt cắt", _direction, Button("Chọn 2 điểm", PickDirection));
            Add(top, 2, "Khoảng cách mặt cắt, m", _spacing, new Panel());
            Add(top, 3, "Mức chia tầng, m", _levelStep, new Panel());
            Add(top, 4, "Tính từ mức", _fromLevel, new Panel());
            Add(top, 5, "Đến mức", _toLevel, new Panel());
            Add(top, 6, "Tỷ lệ ngang 1/", _hScale, new Panel());
            Add(top, 7, "Tỷ lệ đứng 1/", _vScale, new Panel());
            var preview = Button("XEM TRƯỚC TUYẾN", Preview);
            preview.Font = new Font("Arial", 9F, FontStyle.Bold);
            top.Controls.Add(preview, 1, 8);
            top.SetColumnSpan(preview, 2);
            Controls.Add(top); top.BringToFront();

            var tools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(4, 2, 4, 2) };
            tools.Controls.Add(Button("Thêm tuyến", AddLine));
            tools.Controls.Add(Button("Dịch tuyến", MoveLine));
            tools.Controls.Add(Button("Xóa tuyến", DeleteLine));
            tools.Controls.Add(Button("Xóa preview", (s, e) => SectionWorkflowService.ClearPreview()));
            var build = Button("THÀNH LẬP MẶT CẮT", BuildProfiles);
            build.Font = new Font("Arial", 9F, FontStyle.Bold); build.Width = 190;
            tools.Controls.Add(build);
            Controls.Add(tools); tools.BringToFront();

            _status = new Label { Dock = DockStyle.Top, Height = 28, Text = "Chưa tạo hệ mặt cắt.", ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(7, 0, 0, 0) };
            Controls.Add(_status); _status.BringToFront();

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            foreach (var c in new[] { "Tên", "Offset", "Chiều dài", "F đào", "F đắp", "Trạng thái" }) _grid.Columns.Add(c, c);
            Controls.Add(_grid); _grid.BringToFront();

            ProjectState.Current.Changed += OnStateChanged;
            RestoreState();
            _levelStep.ValueChanged += VolumeParameterChanged;
            _fromLevel.ValueChanged += VolumeParameterChanged;
            _toLevel.ValueChanged += VolumeParameterChanged;
            _spacing.ValueChanged += GeneralParameterChanged;
            _hScale.ValueChanged += GeneralParameterChanged;
            _vScale.ValueChanged += GeneralParameterChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ProjectState.Current.Changed -= OnStateChanged;
            base.Dispose(disposing);
        }

        private static NumericUpDown Num(decimal v, decimal min, decimal max)
        {
            var n = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                DecimalPlaces = 2,
                Dock = DockStyle.Fill,
                ThousandsSeparator = true
            };
            n.Value = Math.Max(min, Math.Min(max, v));
            return n;
        }
        private static Button Button(string t, EventHandler h) { var b = new Button { Text = t, AutoSize = true, Height = 28, Margin = new Padding(2) }; b.Click += h; return b; }
        private static void Add(TableLayoutPanel p, int r, string label, Control c, Control b) { p.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r); p.Controls.Add(c, 1, r); p.Controls.Add(b, 2, r); }

        private void PickBoundary(object s, EventArgs e)
        {
            var h = SelectionService.PickClosedBoundary();
            if (h == null) return;
            ProjectState.Current.BoundaryHandle = h;
            ProjectState.Current.SectionSystem = null;
            ProjectState.Current.SectionProfiles.Clear();
            _boundary.Text = "Handle: " + h;
            ProjectState.Current.NotifyChanged();
        }

        private void PickDirection(object s, EventArgs e)
        {
            var r = SelectionService.PickDirection();
            if (r == null) return;
            ProjectState.Current.SectionDirection = r.Vector;
            ProjectState.Current.SectionSystem = null;
            ProjectState.Current.SectionProfiles.Clear();
            _direction.Text = DirectionText(r.Vector);
            ProjectState.Current.NotifyChanged();
        }

        private void Preview(object s, EventArgs e)
        {
            try
            {
                if (!ProjectState.Current.SectionDirection.HasValue) throw new InvalidOperationException("Chưa chọn hướng mặt cắt bằng 2 điểm trên CAD.");
                SaveParameters();
                var sys = SectionWorkflowService.Preview((double)_spacing.Value, ProjectState.Current.SectionDirection.Value);
                _status.Text = $"Đã tạo {sys.Lines.Count:n0} tuyến mặt cắt trên bình đồ." + (sys.Warnings.Count > 0 ? $" Có {sys.Warnings.Count:n0} cảnh báo đường bao lõm." : string.Empty);
                RefreshGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Không tạo được hệ mặt cắt", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void AddLine(object s, EventArgs e)
        {
            try
            {
                EnsureSystem();
                var p = SelectionService.PickPlanPoint("Chọn điểm mà tuyến mặt cắt mới sẽ đi qua: ");
                if (!p.HasValue) return;
                SectionWorkflowService.AddLineAtPoint(p.Value);
                RefreshGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Thêm tuyến", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void MoveLine(object s, EventArgs e)
        {
            try
            {
                string name = SelectedLineName();
                if (name == null) { MessageBox.Show("Chọn một tuyến trong bảng trước khi dịch.", "Mining Volume"); return; }
                var p = SelectionService.PickPlanPoint($"Chọn điểm mới mà {name} sẽ đi qua: ");
                if (!p.HasValue) return;
                SectionWorkflowService.MoveLineToPoint(name, p.Value);
                RefreshGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Dịch tuyến", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void DeleteLine(object s, EventArgs e)
        {
            try
            {
                string name = SelectedLineName();
                if (name == null) { MessageBox.Show("Chọn một tuyến trong bảng trước khi xóa.", "Mining Volume"); return; }
                SectionWorkflowService.DeleteLine(name);
                RefreshGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Xóa tuyến", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void BuildProfiles(object s, EventArgs e)
        {
            try
            {
                EnsureSystem();
                SaveParameters();
                var profiles = SectionWorkflowService.BuildProfiles();
                var ins = SelectionService.PickInsertionPoint("Chọn điểm chèn hệ mặt cắt: ");
                if (!ins.HasValue) return;
                SectionWorkflowService.DrawProfiles(ins.Value, (double)_hScale.Value, (double)_vScale.Value, (double)_levelStep.Value);
                int valid = profiles.Count(x => x.HasData);
                _status.Text = $"Đã thành lập {profiles.Count:n0} mặt cắt; {valid:n0} mặt cắt có đủ dữ liệu hai TIN.";
                RefreshGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Không thành lập được mặt cắt", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }


        private void VolumeParameterChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            var st = ProjectState.Current;
            st.LevelStep = (double)_levelStep.Value;
            st.FromLevel = (double)_fromLevel.Value;
            st.ToLevel = (double)_toLevel.Value;
            st.VolumeResult = null;
            st.NotifyChanged();
        }

        private void GeneralParameterChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            var st = ProjectState.Current;
            st.SectionSpacing = (double)_spacing.Value;
            st.HorizontalScale = (double)_hScale.Value;
            st.VerticalScale = (double)_vScale.Value;
        }

        private void SaveParameters()
        {
            var st = ProjectState.Current;
            st.SectionSpacing = (double)_spacing.Value;
            st.LevelStep = (double)_levelStep.Value;
            st.FromLevel = (double)_fromLevel.Value;
            st.ToLevel = (double)_toLevel.Value;
            st.HorizontalScale = (double)_hScale.Value;
            st.VerticalScale = (double)_vScale.Value;
        }

        private void RestoreState()
        {
            _syncing = true;
            var st = ProjectState.Current;
            _boundary.Text = string.IsNullOrWhiteSpace(st.BoundaryHandle) ? "Chưa chọn" : "Handle: " + st.BoundaryHandle;
            if (st.SectionDirection.HasValue) _direction.Text = DirectionText(st.SectionDirection.Value);
            _spacing.Value = Clamp(_spacing, st.SectionSpacing);
            _levelStep.Value = Clamp(_levelStep, st.LevelStep);
            _fromLevel.Value = Clamp(_fromLevel, st.FromLevel);
            _toLevel.Value = Clamp(_toLevel, st.ToLevel);
            _hScale.Value = Clamp(_hScale, st.HorizontalScale);
            _vScale.Value = Clamp(_vScale, st.VerticalScale);
            _syncing = false;
            RefreshGrid();
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(new Action(RestoreState)); else RestoreState();
        }

        private void RefreshGrid()
        {
            _grid.Rows.Clear();
            var st = ProjectState.Current;
            if (st.SectionSystem == null) return;
            foreach (var line in st.SectionSystem.Lines)
            {
                SectionProfile p = st.SectionProfiles.FirstOrDefault(x => x.Line.Name == line.Name);
                _grid.Rows.Add(line.Name, line.Offset.ToString("0.###"), line.TotalLength.ToString("0.###"),
                    p == null ? "-" : p.CutArea.ToString("0.00"), p == null ? "-" : p.FillArea.ToString("0.00"),
                    p == null ? "Chưa tính" : p.HasData ? "OK" : "Thiếu dữ liệu TIN");
            }
        }

        private string SelectedLineName()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            return Convert.ToString(_grid.SelectedRows[0].Cells[0].Value);
        }

        private static string DirectionText(Vec2 v)
        {
            double a = Math.Atan2(v.Y, v.X) * 180.0 / Math.PI;
            if (a < 0) a += 360.0;
            return $"Az = {a:0.00}°  (dX={v.X:0.###}; dY={v.Y:0.###})";
        }

        private static decimal Clamp(NumericUpDown n, double value)
        {
            decimal v = (decimal)Math.Max((double)n.Minimum, Math.Min((double)n.Maximum, value));
            return v;
        }

        private static void EnsureSystem()
        {
            if (ProjectState.Current.SectionSystem == null) throw new InvalidOperationException("Chưa có hệ mặt cắt. Hãy bấm Xem trước tuyến trước.");
        }
    }
}