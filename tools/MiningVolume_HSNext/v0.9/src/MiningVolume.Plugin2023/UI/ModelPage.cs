using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MiningVolume.Core.Model;
using MiningVolume.Core.Surface;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    public sealed class ModelPage : UserControl
    {
        private sealed class RowRef
        {
            public SourceEntity Entity;
            public ModelVertex Vertex;
        }

        private readonly DataGridView _grid;
        private readonly ComboBox _model;
        private readonly Label _info;
        private readonly ProgressBar _progress;
        private readonly Button _build;
        private readonly Button _toggleTin;
        private readonly List<RowRef> _rows = new List<RowRef>();
        private bool _busy;

        public ModelPage()
        {
            Font = new Font("Arial", 9F);
            BackColor = Color.White;
            Controls.Add(new Label { Text = "QUẢN LÝ MÔ HÌNH X - Y - Z", Dock = DockStyle.Top, Height = 32, Font = new Font("Arial", 11F, FontStyle.Bold) });

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, WrapContents = false, Padding = new Padding(0, 2, 0, 2) };
            top.Controls.Add(new Label { Text = "Mô hình:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
            _model = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            _model.Items.AddRange(new object[] { "Hiện trạng", "Thiết kế" });
            _model.SelectedIndex = ProjectState.Current.ActiveRole == ModelRole.Existing ? 0 : 1;
            _model.SelectedIndexChanged += (s, e) => { ProjectState.Current.ActiveRole = CurrentRole; RebuildRows(); };
            top.Controls.Add(_model);
            top.Controls.Add(Button("Loại điểm", (s, e) => DisableSelectedVertices()));
            top.Controls.Add(Button("Loại đối tượng", (s, e) => DisableSelectedEntities()));
            top.Controls.Add(Button("Khôi phục", (s, e) => RestoreSelected()));
            top.Controls.Add(Button("Khôi phục tất cả", (s, e) => RestoreAll()));
            top.Controls.Add(Button("Sửa Z", (s, e) => EditZ()));
            top.Controls.Add(Button("Zoom tới", (s, e) => ZoomSelected()));
            Controls.Add(top);
            top.BringToFront();

            var second = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, WrapContents = false, Padding = new Padding(0, 2, 0, 2) };
            _build = Button("LƯU / CẬP NHẬT MÔ HÌNH", async (s, e) => await BuildTinAsync());
            _build.Width = 210;
            _toggleTin = Button("Ẩn TIN", (s, e) => ToggleTin());
            _toggleTin.Width = 90;
            second.Controls.Add(_build);
            second.Controls.Add(_toggleTin);
            _progress = new ProgressBar { Width = 150, Height = 24, Style = ProgressBarStyle.Marquee, Visible = false, MarqueeAnimationSpeed = 25 };
            second.Controls.Add(_progress);
            Controls.Add(second);
            second.BringToFront();

            _info = new Label { Dock = DockStyle.Bottom, Height = 34, Text = "Chưa có dữ liệu.", ForeColor = Color.DimGray };
            Controls.Add(_info);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                VirtualMode = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
            };
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Use", HeaderText = "Dùng" });
            foreach (var c in new[] { "Loại", "Layer", "Handle", "Đỉnh", "X", "Y", "Z" }) _grid.Columns.Add(c, c);
            _grid.CellValueNeeded += GridCellValueNeeded;
            Controls.Add(_grid);
            _grid.BringToFront();

            ProjectState.Current.Changed += StateChanged;
            RebuildRows();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ProjectState.Current.Changed -= StateChanged;
            base.Dispose(disposing);
        }

        private ModelRole CurrentRole => _model.SelectedIndex == 1 ? ModelRole.Design : ModelRole.Existing;
        private ModelSession CurrentSession => ProjectState.Current.Get(CurrentRole);

        private static Button Button(string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(2) };
            b.Click += click;
            return b;
        }

        private void StateChanged(object sender, EventArgs e)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(new Action(RebuildRows)); else RebuildRows();
        }

        private void RebuildRows()
        {
            _rows.Clear();
            var s = CurrentSession;
            if (s.Source != null)
                foreach (var entity in s.Source.Entities)
                    foreach (var vertex in entity.Vertices)
                        _rows.Add(new RowRef { Entity = entity, Vertex = vertex });
            _grid.RowCount = _rows.Count;
            _grid.Invalidate();
            UpdateInfo();
        }

        private void UpdateInfo()
        {
            var s = CurrentSession;
            string tin = s.Tin == null ? "TIN: chưa tạo" : $"TIN: {s.Tin.Triangles.Count:n0} tam giác";
            _info.Text = $"{s.Name} • Layer: {s.Layer ?? "-"} • {s.EntityCount:n0} đối tượng • {s.ActiveVertexCount:n0}/{s.VertexCount:n0} đỉnh đang dùng • {tin}";
            _toggleTin.Text = s.TinVisible ? "Ẩn TIN" : "Hiện TIN";
            _toggleTin.Enabled = s.Tin != null && !_busy;
        }

        private void GridCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
            var r = _rows[e.RowIndex];
            bool enabled = r.Entity.IsEnabled && r.Vertex.IsEnabled;
            switch (_grid.Columns[e.ColumnIndex].Name)
            {
                case "Use": e.Value = enabled; break;
                case "Loại": e.Value = TypeName(r.Entity.Type); break;
                case "Layer": e.Value = r.Entity.Layer; break;
                case "Handle": e.Value = r.Entity.Handle; break;
                case "Đỉnh": e.Value = r.Vertex.Index; break;
                case "X": e.Value = r.Vertex.Position.X.ToString("0.###"); break;
                case "Y": e.Value = r.Vertex.Position.Y.ToString("0.###"); break;
                case "Z": e.Value = r.Vertex.Position.Z.ToString("0.###"); break;
            }
        }

        private static string TypeName(SourceEntityType type)
        {
            switch (type)
            {
                case SourceEntityType.Point: return "Point";
                case SourceEntityType.Line: return "Line";
                case SourceEntityType.LwPolyline: return "LWPolyline";
                case SourceEntityType.Polyline2d: return "2D Polyline";
                case SourceEntityType.Polyline3d: return "3D Polyline";
                case SourceEntityType.Contour: return "Đồng mức";
                default: return type.ToString();
            }
        }

        private IEnumerable<RowRef> SelectedRows()
        {
            return _grid.SelectedRows.Cast<DataGridViewRow>()
                .Where(x => x.Index >= 0 && x.Index < _rows.Count)
                .Select(x => _rows[x.Index]);
        }

        private void DisableSelectedVertices()
        {
            foreach (var r in SelectedRows()) CurrentSession.Source.SetVertexEnabled(r.Entity.Id, r.Vertex.Index, false);
            ProjectState.Current.NotifyChanged();
        }

        private void DisableSelectedEntities()
        {
            foreach (var id in SelectedRows().Select(r => r.Entity.Id).Distinct()) CurrentSession.Source.RemoveEntityFromModel(id);
            ProjectState.Current.NotifyChanged();
        }

        private void RestoreSelected()
        {
            foreach (var entity in SelectedRows().Select(r => r.Entity).Distinct())
            {
                entity.SetEnabled(true);
                foreach (var v in entity.Vertices) v.Reset();
            }
            CurrentSession.Source.Touch();
            ProjectState.Current.NotifyChanged();
        }

        private void RestoreAll()
        {
            if (CurrentSession.Source == null) return;
            foreach (var entity in CurrentSession.Source.Entities)
            {
                entity.SetEnabled(true);
                foreach (var v in entity.Vertices) v.Reset();
            }
            CurrentSession.Source.Touch();
            ProjectState.Current.NotifyChanged();
        }

        private void EditZ()
        {
            var row = SelectedRows().FirstOrDefault();
            if (row == null) { MessageBox.Show("Chọn một đỉnh cần sửa cao độ.", "Mining Volume"); return; }
            using (var form = new NumericInputDialog("Sửa cao độ Z", "Cao độ Z mới:", row.Vertex.Position.Z))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                CurrentSession.Source.OverrideVertexZ(row.Entity.Id, row.Vertex.Index, form.Value);
                ProjectState.Current.NotifyChanged();
            }
        }

        private void ZoomSelected()
        {
            var row = SelectedRows().FirstOrDefault();
            if (row != null) CadViewService.ZoomToHandle(row.Entity.Handle);
        }

        private async Task BuildTinAsync()
        {
            if (_busy) return;
            var role = CurrentRole;
            try
            {
                SetBusy(true, "Đang kiểm tra dữ liệu và dựng TIN...");
                var tin = await Task.Run(() => SurfaceWorkflowService.BuildCore(role));
                SurfaceWorkflowService.DrawTin(role, tin);
                _info.Text = $"Đã cập nhật {ProjectState.Current.Get(role).Name}: {tin.Triangles.Count:n0} tam giác TIN.";
            }
            catch (SurfaceValidationException ex)
            {
                ShowValidation(ex.Issues);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không cập nhật được mô hình", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { SetBusy(false, null); }
        }

        private void ShowValidation(IReadOnlyList<ValidationIssue> issues)
        {
            int errors = issues.Count(x => x.Severity == ValidationSeverity.Error);
            int warnings = issues.Count(x => x.Severity == ValidationSeverity.Warning);
            var lines = issues.Take(30).Select(x => $"[{x.Severity}] {x.Code}: {x.Message}");
            string suffix = issues.Count > 30 ? $"\r\n... còn {issues.Count - 30:n0} cảnh báo/lỗi khác." : string.Empty;
            MessageBox.Show($"Không dựng TIN. Có {errors:n0} lỗi, {warnings:n0} cảnh báo.\r\n\r\n" + string.Join("\r\n", lines) + suffix,
                "Kiểm tra dữ liệu mô hình", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ToggleTin()
        {
            var s = CurrentSession;
            if (s.Tin == null) return;
            SurfaceWorkflowService.SetTinVisible(CurrentRole, !s.TinVisible);
            UpdateInfo();
        }

        private void SetBusy(bool busy, string message)
        {
            _busy = busy;
            _build.Enabled = !busy;
            _toggleTin.Enabled = !busy && CurrentSession.Tin != null;
            _model.Enabled = !busy;
            _grid.Enabled = !busy;
            _progress.Visible = busy;
            if (!string.IsNullOrWhiteSpace(message)) _info.Text = message;
            if (!busy) UpdateInfo();
        }
    }

    internal sealed class NumericInputDialog : Form
    {
        private readonly NumericUpDown _value;
        public double Value => (double)_value.Value;
        public NumericInputDialog(string title, string prompt, double current)
        {
            Text = title;
            Font = new Font("Arial", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 330;
            Height = 150;
            Controls.Add(new Label { Text = prompt, Left = 12, Top = 15, Width = 105 });
            _value = new NumericUpDown { Left = 120, Top = 12, Width = 180, DecimalPlaces = 3, Minimum = -1000000, Maximum = 1000000, Value = Clamp(current) };
            Controls.Add(_value);
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 140, Top = 55, Width = 75 };
            var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, Left = 225, Top = 55, Width = 75 };
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }
        private static decimal Clamp(double value)
        {
            if (value < -1000000) value = -1000000;
            if (value > 1000000) value = 1000000;
            return (decimal)value;
        }
    }
}