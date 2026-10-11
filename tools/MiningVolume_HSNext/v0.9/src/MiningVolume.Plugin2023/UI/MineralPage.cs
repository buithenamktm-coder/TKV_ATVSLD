using System;
using System.Drawing;
using System.Linq;
using MiningVolume.Core.Sections;
using System.Windows.Forms;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    /// <summary>
    /// Quản lý và gán tên nhiều vỉa. Khi thiếu một bình đồ: phần mềm vẫn chỉ
    /// tính đào đắp thuần túy; không suy ra thể tích khoáng sản từ Polyline 2D.
    /// </summary>
    public sealed class MineralPage : UserControl
    {
        private readonly DataGridView _grid;
        private readonly NumericUpDown _density;
        private readonly TextBox _densitySource;
        private readonly Label _status;
        private bool _syncing;

        public MineralPage()
        {
            Dock = DockStyle.Fill;
            Font = new Font("Arial", 9F);
            BackColor = Color.White;

            var header = new Label
            {
                Text = "QUẢN LÝ VỈA THAN / THÂN QUẶNG",
                Dock = DockStyle.Top,
                Height = 34,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Arial", 11F, FontStyle.Bold)
            };
            Controls.Add(header);

            var commands = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 112,
                WrapContents = true, Padding = new Padding(4, 3, 4, 3)
            };
            commands.Controls.Add(Action("THÊM VỈA", AddBody));
            commands.Controls.Add(Action("ĐỔI TÊN", RenameBody));
            commands.Controls.Add(Action("XÓA MỀM", DeleteBody));
            commands.Controls.Add(Action("CHỌN LỘ VỈA HIỆN TRẠNG", (s,e) => Pick(ModelRole.Existing)));
            commands.Controls.Add(Action("CHỌN LỘ VỈA THIẾT KẾ", (s,e) => Pick(ModelRole.Design)));
            commands.Controls.Add(Action("BỎ CHỌN HIỆN TRẠNG", (s,e) => Clear(ModelRole.Existing)));
            commands.Controls.Add(Action("BỎ CHỌN THIẾT KẾ", (s,e) => Clear(ModelRole.Design)));
            commands.Controls.Add(Action("GÁN VÙNG TRÊN MẶT CẮT", (s,e) => AssignSectionRegion()));
            commands.Controls.Add(Action("CẬP NHẬT MẶT CẮT", (s,e) => UpdateSectionRegions()));
            Controls.Add(commands);
            commands.BringToFront();

            var attrs = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 44, ColumnCount = 4, Padding = new Padding(4)
            };
            attrs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            attrs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            attrs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            attrs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            attrs.Controls.Add(new Label { Text = "Thể trọng (t/m³):", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _density = new NumericUpDown { Dock = DockStyle.Fill, DecimalPlaces = 3,
                Minimum = 0, Maximum = 30, Increment = 0.01M };
            attrs.Controls.Add(_density, 1, 0);
            attrs.Controls.Add(new Label { Text = "Nguồn thể trọng:", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft }, 2, 0);
            _densitySource = new TextBox { Dock = DockStyle.Fill };
            attrs.Controls.Add(_densitySource, 3, 0);
            Controls.Add(attrs);
            attrs.BringToFront();

            _status = new Label
            {
                Dock = DockStyle.Bottom, Height = 56, ForeColor = Color.DimGray,
                Text = "Chỉ tính khoáng sản sau khi chọn đủ lộ vỉa và có hình học 3D/mặt cắt hợp lệ.",
                Padding = new Padding(6, 4, 4, 4)
            };
            Controls.Add(_status);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            foreach (var title in new[] { "Mã vỉa", "Tên vỉa/thân quặng", "Lộ vỉa HT",
                "Lộ vỉa TK", "Thể trọng", "Trạng thái" })
                _grid.Columns.Add(title, title);
            Controls.Add(_grid);
            _grid.BringToFront();

            _grid.SelectionChanged += (s,e) => ShowSelected();
            _density.ValueChanged += (s,e) => SaveDensity();
            _densitySource.Leave += (s,e) => SaveDensity();
            ProjectState.Current.Changed += Changed;
            RefreshView();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ProjectState.Current.Changed -= Changed;
            base.Dispose(disposing);
        }

        private static Button Action(string text, EventHandler handler)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 29, Margin = new Padding(2) };
            button.Click += handler;
            return button;
        }

        private MineralBody Selected()
        {
            if (_grid.SelectedRows.Count < 1) return null;
            var id = _grid.SelectedRows[0].Tag as string;
            return ProjectState.Current.MineralBodies.FirstOrDefault(b => b.Id == id && !b.IsDeleted);
        }

        private static string PromptName(string title, string initial)
        {
            using (var form = new Form
            {
                Text = title, Font = new Font("Arial", 9F),
                FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false, MinimizeBox = false, Width = 405, Height = 145
            })
            {
                var box = new TextBox { Left = 12, Top = 12, Width = 365, Text = initial ?? "" };
                var ok = new Button { Left = 202, Top = 54, Width = 80,
                    Text = "Đồng ý", DialogResult = DialogResult.OK };
                var cancel = new Button { Left = 294, Top = 54, Width = 82,
                    Text = "Hủy", DialogResult = DialogResult.Cancel };
                form.Controls.Add(box); form.Controls.Add(ok); form.Controls.Add(cancel);
                form.AcceptButton = ok; form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? box.Text : null;
            }
        }

        private void AddBody(object sender, EventArgs e)
        {
            string title = PromptName("Thêm vỉa than/thân quặng",
                "Vỉa " + (ProjectState.Current.MineralBodies.Count + 1));
            if (title == null) return;
            try
            {
                MineralBody.AddNew(ProjectState.Current.MineralBodies, title);
                ProjectState.Current.NotifyChanged();
            }
            catch (Exception ex) { Alert(ex); }
        }

        private void RenameBody(object sender, EventArgs e)
        {
            var body = Selected();
            if (body == null) { MissingSelection(); return; }
            string name = PromptName("Đổi tên vỉa/thân quặng", body.Name);
            if (name == null) return;
            try
            {
                body.Rename(ProjectState.Current.MineralBodies, name);
                ProjectState.Current.NotifyChanged();
            }
            catch (Exception ex) { Alert(ex); }
        }

        private void DeleteBody(object sender, EventArgs e)
        {
            var body = Selected();
            if (body == null) { MissingSelection(); return; }
            if (MessageBox.Show("Xóa mềm " + body.Name + "? Dữ liệu nguồn CAD không bị xóa.",
                "Quản lý vỉa", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            body.IsDeleted = true;
            ProjectState.Current.NotifyChanged();
        }

        private void Pick(ModelRole role)
        {
            var body = Selected();
            if (body == null) { MissingSelection(); return; }
            var handles = SelectionService.PickMineralOutcropHandles(body.Name, role);
            if (handles == null || handles.Count == 0) return;
            body.AddTraces(role, handles);
            ProjectState.Current.NotifyChanged();
        }

        private void Clear(ModelRole role)
        {
            var body = Selected();
            if (body == null) { MissingSelection(); return; }
            if (MessageBox.Show("Bỏ liên kết đường lộ vỉa trên bình đồ đã chọn? Không xóa đối tượng CAD.",
                "Quản lý vỉa", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            body.ClearTraces(role);
            ProjectState.Current.NotifyChanged();
        }

        private string ChooseSection()
        {
            var profiles = ProjectState.Current.SectionProfiles
                .Where(x => x.HasData).OrderBy(x => x.Line.Index).ToList();
            if (profiles.Count == 0)
            {
                MessageBox.Show("Chưa có mặt cắt có đủ hai TIN.", "IMSAT VOLUME");
                return null;
            }
            using (var dialog = new Form
            {
                Text = "Chọn mặt cắt để gán vùng khoáng sản",
                Font = new Font("Arial",9F),
                Width = 420,Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,MinimizeBox = false
            })
            {
                var list = new ComboBox
                {
                    Left = 15,Top = 12,Width = 378,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                list.Items.AddRange(profiles.Select(x=>x.Line.Name).Cast<object>().ToArray());
                list.SelectedIndex=0;
                var yes = new Button { Text = "Đồng ý",Left = 214,Top = 49,Width = 80,DialogResult = DialogResult.OK };
                var no = new Button { Text = "Hủy",Left = 303,Top = 49,Width = 80,DialogResult = DialogResult.Cancel };
                dialog.Controls.Add(list);dialog.Controls.Add(yes);dialog.Controls.Add(no);
                dialog.AcceptButton = yes; dialog.CancelButton = no;
                return dialog.ShowDialog(this)==DialogResult.OK ? list.SelectedItem as string : null;
            }
        }

        private void AssignSectionRegion()
        {
            var body = Selected();
            if (body == null) { MissingSelection(); return; }
            if (!body.HasPairedOutcrops)
            {
                MessageBox.Show("Vỉa này chưa chọn đủ đường lộ vỉa hiện trạng và thiết kế.",
                    "IMSAT VOLUME",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            string sectionName = ChooseSection();
            if (sectionName == null) return;
            try
            {
                // Người dùng dựng vùng kín bằng PLINE trong khung mặt cắt trước khi gán.
                var handle = SelectionService.PickClosedBoundary();
                if (handle == null) return;
                var profile = ProjectState.Current.SectionProfiles.First(p => p.Line.Name == sectionName);
                double area = MineralSectionRegionService.AssignRegion(handle,body,profile);
                _status.Text = "Đã gán "+body.Name+" vào "+sectionName+
                    "; F="+area.ToString("0.00")+" m². Bấm CẬP NHẬT MẶT CẮT để đọc lại toàn bộ.";
            }
            catch(Exception ex){Alert(ex);}
        }

        private void UpdateSectionRegions()
        {
            try
            {
                var result=MineralSectionRegionService.UpdateAll();
                double area=result.Sum(x=>x.AreaSquareMetres);
                _status.Text="Đã cập nhật "+result.Count+" vùng khoáng sản; tổng diện tích theo các mặt cắt = "+
                    area.ToString("0.00")+" m². Chưa phải thể tích m³/tấn.";
                MessageBox.Show(_status.Text+Environment.NewLine+
                    "Diện tích theo vỉa đã kiểm tra nằm trong phần đào, không chồng lấn. "+
                    "Tính thể tích/tấn chỉ được thực hiện khi đủ thông tin hình học giữa các mặt cắt.",
                    "CẬP NHẬT MẶT CẮT",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
            catch(Exception ex){Alert(ex);}
        }

        private void SaveDensity()
        {
            if (_syncing) return;
            var body = Selected();
            if (body == null) return;
            body.DensityTonPerCubicMetre = (double)_density.Value;
            body.DensitySource = _densitySource.Text.Trim();
        }

        private void Changed(object sender, EventArgs e)
        {
            if (!IsHandleCreated) return;
            if (InvokeRequired) BeginInvoke(new Action(RefreshView)); else RefreshView();
        }

        private void RefreshView()
        {
            string id = Selected()?.Id;
            _syncing = true;
            _grid.Rows.Clear();
            foreach (var body in ProjectState.Current.MineralBodies.Where(b => !b.IsDeleted))
            {
                int row = _grid.Rows.Add(body.Id.Substring(0, Math.Min(8, body.Id.Length)),
                    body.Name, body.ExistingOutcropHandles.Count,
                    body.DesignOutcropHandles.Count,
                    body.DensityTonPerCubicMetre > 0 ? body.DensityTonPerCubicMetre.ToString("0.000") : "Chưa nhập",
                    body.StateText);
                _grid.Rows[row].Tag = body.Id;
            }
            if (_grid.Rows.Count > 0)
            {
                int row = 0;
                for (int i = 0; i < _grid.Rows.Count; i++)
                    if (string.Equals(_grid.Rows[i].Tag as string, id, StringComparison.Ordinal))
                    { row = i; break; }
                _grid.Rows[row].Selected = true;
                _grid.CurrentCell = _grid.Rows[row].Cells[1];
            }
            _syncing = false;
            ShowSelected();
            _status.Text = ProjectState.Current.HasMineralInput
                ? "Đã chọn đủ lộ vỉa cho ít nhất một vỉa. Chưa được tính tấn nếu thiếu hình học khoáng sản."
                : "CHẾ ĐỘ ĐÀO ĐẮP THUẦN TÚY – chưa có cặp đường lộ vỉa hiện trạng/thiết kế hợp lệ.";
        }

        private void ShowSelected()
        {
            if (_syncing) return;
            _syncing = true;
            var b = Selected();
            _density.Enabled = b != null;
            _densitySource.Enabled = b != null;
            _density.Value = b == null ? 0 : Math.Max(_density.Minimum, Math.Min(_density.Maximum, (decimal)b.DensityTonPerCubicMetre));
            _densitySource.Text = b?.DensitySource ?? "";
            _syncing = false;
        }

        private void MissingSelection()
        {
            MessageBox.Show("Hãy chọn một vỉa/thân quặng trong bảng trước.", "Quản lý vỉa",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Alert(Exception e) =>
            MessageBox.Show(e.Message, "Quản lý vỉa/thân quặng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
