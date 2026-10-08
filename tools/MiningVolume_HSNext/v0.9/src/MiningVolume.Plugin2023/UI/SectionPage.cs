using System;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
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
            Font = new Font("Arial", 9F);
            BackColor = Color.White;

            // Use one explicit vertical layout. Do not stack Top-docked controls with
            // BringToFront: on a narrow/short AutoCAD palette that can overlap rows and
            // hide the primary section-output button.
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.White
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));   // title
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 258F));  // parameters
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));   // primary actions
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));   // edit tools
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));   // status
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // grid
            Controls.Add(root);

            var title = new Label
            {
                Text = "HỆ MẶT CẮT",
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(title, 0, 0);

            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 8,
                Padding = new Padding(4, 2, 4, 2),
                Margin = Padding.Empty,
                BackColor = Color.White
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
            for (int i = 0; i < 8; i++)
                top.RowStyles.Add(new RowStyle(SizeType.Percent, 12.5F));

            _boundary = new Label
            {
                Text = "Chưa chọn",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _direction = new Label
            {
                Text = "Chưa chọn",
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
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
            root.Controls.Add(top, 0, 1);

            // Primary workflow actions must always remain visible.
            var primary = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(4, 3, 4, 3),
                Margin = Padding.Empty
            };
            primary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            primary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            var preview = Button("XEM TRƯỚC TUYẾN", Preview);
            preview.Dock = DockStyle.Fill;
            preview.AutoSize = false;
            preview.Font = new Font("Arial", 9F, FontStyle.Bold);
            var draw = Button("XUẤT / VẼ MẶT CẮT", BuildProfiles);
            draw.Name = "btnDrawSections";
            draw.Dock = DockStyle.Fill;
            draw.AutoSize = false;
            draw.Font = new Font("Arial", 9F, FontStyle.Bold);
            primary.Controls.Add(preview, 0, 0);
            primary.Controls.Add(draw, 1, 0);
            root.Controls.Add(primary, 0, 2);

            var tools = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4, 2, 4, 2),
                Margin = Padding.Empty
            };
            tools.Controls.Add(Button("Thêm tuyến", AddLine));
            tools.Controls.Add(Button("Dịch tuyến", MoveLine));
            tools.Controls.Add(Button("Xóa tuyến", DeleteLine));
            tools.Controls.Add(Button("Xóa preview", (s, e) => SectionWorkflowService.ClearPreview()));
            root.Controls.Add(tools, 0, 3);

            _status = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chưa tạo hệ mặt cắt.",
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(7, 0, 0, 0)
            };
            root.Controls.Add(_status, 0, 4);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Margin = new Padding(0)
            };
            foreach (var col in new[] { "Tên", "Offset", "Chiều dài", "F đào", "F đắp", "Trạng thái" })
                _grid.Columns.Add(col, col);
            root.Controls.Add(_grid, 0, 5);

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

        private async void BuildProfiles(object s, EventArgs e)
        {
            var button = s as Button;
            try
            {
                if (button != null) button.Enabled = false;
                UseWaitCursor = true;
                EnsureSystem();
                SaveParameters();

                _status.Text = "Đang kiểm tra trạng thái hai TIN...";
                _status.Refresh();
                SectionWorkflowService.EnsureProfileInputsReady();

                var state = ProjectState.Current;
                var system = state.SectionSystem;
                var existing = state.Existing.Tin;
                var design = state.Design.Tin;
                var watch = Stopwatch.StartNew();
                var progress = new Progress<string>(message =>
                {
                    _status.Text = message;
                });

                var profiles = await Task.Run(() =>
                    SectionWorkflowService.BuildProfilesCore(
                        system,
                        existing,
                        design,
                        (done, total, name) =>
                            progress.Report($"Đang tính mặt cắt {done:n0}/{total:n0}: {name}...")));

                watch.Stop();
                SectionWorkflowService.CommitProfiles(profiles);
                int valid = profiles.Count(x => x.HasData);
                _status.Text =
                    $"Tính xong {profiles.Count:n0} mặt cắt ({valid:n0} đủ hai TIN) trong {watch.Elapsed.TotalSeconds:0.0}s. " +
                    "Chọn điểm chèn trên CAD.";
                _status.Refresh();

                var ins = SelectionService.PickInsertionPoint("Chọn điểm chèn hệ mặt cắt: ");
                if (!ins.HasValue) return;

                _status.Text = "Đang vẽ hệ mặt cắt xuống AutoCAD...";
                _status.Refresh();
                SectionWorkflowService.DrawProfiles(
                    ins.Value,
                    (double)_hScale.Value,
                    (double)_vScale.Value,
                    (double)_levelStep.Value);

                _status.Text =
                    $"Đã thành lập {profiles.Count:n0} mặt cắt; {valid:n0} mặt cắt có đủ dữ liệu hai TIN. " +
                    $"Thời gian tính {watch.Elapsed.TotalSeconds:0.0}s.";
                RefreshGrid();
            }
            catch (Exception ex)
            {
                _status.Text = "Không thành lập được mặt cắt.";
                MessageBox.Show(ex.Message, "Không thành lập được mặt cắt", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                if (button != null) button.Enabled = true;
            }
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