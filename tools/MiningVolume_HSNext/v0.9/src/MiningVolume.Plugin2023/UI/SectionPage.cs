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
        private readonly ComboBox _parameterMode;
        private readonly NumericUpDown _axisStep;
        private readonly NumericUpDown _layoutGap;
        private readonly NumericUpDown _levelStep;
        private readonly NumericUpDown _fromLevel;
        private readonly NumericUpDown _toLevel;
        private readonly NumericUpDown _hScale;
        private readonly NumericUpDown _vScale;
        private readonly DataGridView _grid;
        private readonly Label _status;
        private bool _syncing;
        private readonly bool _quickOnly;
        private readonly Action<AppPage> _navigate;
        private bool _compactParameters;
        private readonly TableLayoutPanel _rootLayout;
        private readonly TableLayoutPanel _parameterLayout;
        private readonly Control[] _parameterLabels = new Control[11];
        private readonly Control[] _parameterValues = new Control[11];
        private readonly Control[] _parameterActions = new Control[11];

        public SectionPage() : this(false, null) { }

        public SectionPage(bool quickOnly) : this(quickOnly, null) { }

        public SectionPage(bool quickOnly, Action<AppPage> navigate)
        {
            _quickOnly = quickOnly;
            _navigate = navigate;
            Font = new Font("Arial", 9F);
            BackColor = Color.White;

            // Use one explicit vertical layout. Do not stack Top-docked controls with
            // BringToFront: on a narrow/short AutoCAD palette that can overlap rows and
            // hide the primary section-output button.
            var root = _rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.White
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));   // title
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 440F));  // scrollable parameters
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));   // spaced primary actions
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));   // edit tools + CAD profile update
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));   // multi-line status
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // grid
            Controls.Add(root);

            var title = new Label
            {
                Text = "HỆ MẶT CẮT",
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 12F, FontStyle.Bold),
                ForeColor = UiTheme.TextStrong,
                Padding = new Padding(12, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(title, 0, 0);

            var top = _parameterLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 11,
                Padding = new Padding(10, 8, 10, 8),
                Margin = new Padding(0, 4, 0, 6),
                AutoScroll = true, // compact 13-row parameters stay reachable in a 620px palette
                BackColor = Color.FromArgb(246, 250, 255)
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 192F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            for (int i = 0; i < 11; i++)
                top.RowStyles.Add(new RowStyle(SizeType.Absolute, i < 2 ? 54F : 35F));

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
            _parameterMode = new ComboBox {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _parameterMode.Items.AddRange(new object[] {
                "TT34/2018/TT-BCT", "Tùy chỉnh"
            });
            _axisStep = Num(20, 0.1M, 1000);
            _layoutGap = Num(90, 30, 10000);
            _levelStep = Num(5, 0.1M, 1000);
            _fromLevel = Num(0, -10000, 10000);
            _toLevel = Num(0, -10000, 10000);
            _hScale = Num(1000, 1, 100000);
            _vScale = Num(1000, 1, 100000);
            // TT34 starts 1:1000/1:1000; Custom enables independent editing.
            _hScale.Enabled = false;
            _vScale.Enabled = false;

            Add(top, 0, "Đường bao", _boundary, Button("Chọn trên CAD", PickBoundary));
            Add(top, 1, "Hướng MC (vuông góc)", _direction, Button("Chọn 2 điểm", PickDirection));
            Add(top, 2, "Thông số theo", _parameterMode, new Panel());
            Add(top, 3, "Khoảng cách mặt cắt, m", _spacing, new Panel());
            Add(top, 4, "Bước chia trục Z, m", _axisStep, new Panel());
            Add(top, 5, "Mức chia tầng tính KL, m", _levelStep, new Panel());
            Add(top, 6, "Tính từ mức", _fromLevel, new Panel());
            Add(top, 7, "Đến mức", _toLevel, new Panel());
            Add(top, 8, "Tỷ lệ ngang 1/", _hScale, new Panel());
            Add(top, 9, "Tỷ lệ đứng 1/", _vScale, new Panel());
            Add(top, 10, "Giãn cách khi xuất CAD", _layoutGap, new Panel());
            for (int i = 0; i < 11; i++)
            {
                _parameterLabels[i] = top.GetControlFromPosition(0, i);
                _parameterValues[i] = top.GetControlFromPosition(1, i);
                _parameterActions[i] = top.GetControlFromPosition(2, i);
            }
            root.Controls.Add(top, 0, 1);

            // Primary workflow actions must always remain visible.
            var primary = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(10, 7, 10, 7),
                Margin = Padding.Empty
            };
            primary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            primary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
            primary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
            var preview = Button("Xem trước tuyến", Preview);
            preview.Name = "btnPreviewSections";
            preview.Dock = DockStyle.Fill;
            preview.AutoSize = false;
            preview.Font = new Font("Arial", 9F, FontStyle.Bold);
            var draw = Button("Xuất / Vẽ mặt cắt", BuildProfiles);
            draw.Name = "btnDrawSections";
            draw.Dock = DockStyle.Fill;
            draw.AutoSize = false;
            draw.Font = new Font("Arial", 9F, FontStyle.Bold);
            var quick = Button("Vẽ mặt cắt nhanh", DrawQuickSections);
            quick.Name = "btnQuickSections";
            quick.Dock = DockStyle.Fill;
            quick.AutoSize = false;
            primary.Controls.Add(preview, 0, 0);
            primary.Controls.Add(draw, 1, 0);
            if (_quickOnly)
                primary.Controls.Add(quick, 2, 0);
            else
            {
                // The quick section belongs to independent group 4, not volume calculation.
                primary.ColumnStyles[0].Width = 50F;
                primary.ColumnStyles[1].Width = 50F;
                primary.ColumnStyles[2].Width = 0F;
                primary.ColumnStyles[2].SizeType = SizeType.Percent;
            }
            root.Controls.Add(primary, 0, 2);

            var tools = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(10, 9, 10, 9),
                Margin = Padding.Empty
            };
            tools.Controls.Add(Button("Thêm tuyến", AddLine));
            tools.Controls.Add(Button("Dịch tuyến", MoveLine));
            tools.Controls.Add(Button("Xóa tuyến", DeleteLine));
            tools.Controls.Add(Button("Xóa preview", (s, e) => SectionWorkflowService.ClearPreview()));
            var updateEdited = Button("Cập nhật mặt cắt", UpdateEditedProfiles);
            updateEdited.Name = "btnUpdateEditedSections";
            updateEdited.Font = new Font("Arial", 9F, FontStyle.Bold);
            updateEdited.MinimumSize = new Size(175, 36);
            tools.Controls.Add(updateEdited);
            if (!_quickOnly && _navigate != null)
            {
                tools.Controls.Add(Button("Tính khối lượng", (sender, args) => _navigate(AppPage.Volume)));
                tools.Controls.Add(Button("Vỉa than/quặng", (sender, args) => _navigate(AppPage.Mineral)));
                tools.Controls.Add(Button("Xuất CAD / Excel", (sender, args) => _navigate(AppPage.Export)));
            }
            root.Controls.Add(tools, 0, 3);

            _status = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Chưa tạo hệ mặt cắt.",
                ForeColor = UiTheme.Text,
                BackColor = UiTheme.Soft,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 5, 8, 5)
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
            _parameterMode.SelectedIndexChanged += ParameterModeChanged;
            _spacing.ValueChanged += GeneralParameterChanged;
            _axisStep.ValueChanged += GeneralParameterChanged;
            _layoutGap.ValueChanged += GeneralParameterChanged;
            _hScale.ValueChanged += GeneralParameterChanged;
            _vScale.ValueChanged += GeneralParameterChanged;
            SizeChanged += (sender, args) =>
            {
                ApplyResponsiveParameterLayout();
                AdjustParameterHeightToAvailableSpace();
            };
            ApplyResponsiveParameterLayout();
            AdjustParameterHeightToAvailableSpace();
            if (quickOnly)
            {
                title.Text = "Vẽ nhanh mặt cắt";
                preview.Visible = false;
                draw.Visible = false;
                primary.SetColumnSpan(quick, 3);
                tools.Visible = false;
                _grid.Visible = false;
                _rootLayout.RowStyles[1].Height = 155F;
                _rootLayout.RowStyles[3].Height = 0F;
                _rootLayout.RowStyles[5].Height = 0F;
                for (int i = 0; i < 11; i++)
                {
                    bool show = i == 8 || i == 9 || i == 10;
                    _parameterLabels[i].Visible = show;
                    _parameterValues[i].Visible = show;
                    _parameterActions[i].Visible = false;
                    _parameterLayout.RowStyles[i].Height = show ? 39F : 0F;
                }
                _hScale.Enabled = true;
                _vScale.Enabled = true;
                _status.Text = "Chọn tuyến trên CAD để vẽ mặt cắt nhanh từ TIN.";
            }
        }

        private void AdjustParameterHeightToAvailableSpace()
        {
            if (_rootLayout == null) return;
            if (_quickOnly) { _rootLayout.RowStyles[1].Height = 155F; return; }
            // Reserve usable space for the table of section results even on
            // 620px Windows palettes; the parameter panel scrolls internally.
            int visibleHeight = Math.Max(500, ClientSize.Height);
            int reserved = 38 + 58 + 110 + 46 + Math.Max(105, visibleHeight / 5);
            float maxParameters = Math.Max(210, visibleHeight - reserved);
            float preferred = 440F;
            _rootLayout.RowStyles[1].Height = Math.Min(preferred, maxParameters);
        }

        private void ApplyResponsiveParameterLayout()
        {
            bool compact = ClientSize.Width < 560;
            if (_compactParameters == compact) return;
            _compactParameters = compact;
            _parameterLayout.SuspendLayout();
            try
            {
                // Reparent the SAME input controls: do not discard user values.
                _parameterLayout.Controls.Clear();
                _parameterLayout.RowStyles.Clear();
                _parameterLayout.ColumnStyles.Clear();
                _parameterLayout.ColumnCount = compact ? 2 : 3;
                _parameterLayout.RowCount = compact ? 13 : 11;
                if (compact)
                {
                    _parameterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 174F));
                    _parameterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    for (int k = 0; k < 13; k++)
                        _parameterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, k < 4 ? 48F : 35F));
                }
                else
                {
                    _parameterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 192F));
                    _parameterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    _parameterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
                    for (int k = 0; k < 11; k++)
                        _parameterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, k < 2 ? 54F : 35F));
                }
                for (int i = 0; i < 11; i++)
                {
                    int row = compact ? i < 2 ? 2 * i : i + 2 : i;
                    _parameterLayout.Controls.Add(_parameterLabels[i], 0, row);
                    _parameterLayout.Controls.Add(_parameterValues[i], 1, row);
                    if (compact)
                    {
                        if (i < 2)
                        {
                            var choose = _parameterActions[i];
                            choose.Dock = DockStyle.Fill;
                            _parameterLayout.Controls.Add(choose, 0, row + 1);
                            _parameterLayout.SetColumnSpan(choose, 2);
                        }
                    }
                    else
                    {
                        _parameterActions[i].Dock = DockStyle.Fill;
                        _parameterLayout.SetColumnSpan(_parameterActions[i], 1);
                        _parameterLayout.Controls.Add(_parameterActions[i], 2, row);
                    }
                }
                // Height is bounded by available palette space below.
            }
            finally { _parameterLayout.ResumeLayout(true); }
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
        private static Button Button(string t, EventHandler h) { var b = new Button { Text = t, AutoSize = true, Height = 38, Margin = new Padding(6, 4, 6, 4) }; b.Click += h; return b; }
        private static void Add(TableLayoutPanel p, int r, string label, Control c, Control b) { p.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, AutoEllipsis = true,
                Font = new Font("Arial", 9.5F, FontStyle.Bold), ForeColor = UiTheme.Text,
                TextAlign = ContentAlignment.MiddleLeft }, 0, r); p.Controls.Add(c, 1, r); p.Controls.Add(b, 2, r); }

        private void PickBoundary(object s, EventArgs e)
        {
            var h = SelectionService.PickClosedBoundary();
            if (h == null) return;
            ProjectState.Current.BoundaryHandle = h;
            ProjectState.Current.SectionSystem = null;
            ProjectState.Current.SectionProfiles.Clear();
            ProjectState.Current.VolumeResult = null;
            _boundary.Text = "Handle: " + h;
            ProjectState.Current.NotifyChanged();
        }

        private void PickDirection(object s, EventArgs e)
        {
            var r = SelectionService.PickDirection();
            if (r == null) return;
            // The two CAD points indicate the REFERENCE baseline, not the
            // direction along a section. Sections cross it at 90 degrees.
            var sectionDirection = SectionSystemBuilder.PerpendicularToSelectedDirection(r.Vector);
            ProjectState.Current.SectionDirection = sectionDirection;
            ProjectState.Current.SectionSystem = null;
            ProjectState.Current.SectionProfiles.Clear();
            ProjectState.Current.VolumeResult = null;
            _direction.Text = DirectionText(sectionDirection) + " (vuông góc đường chọn)";
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
            if (!LicenseUiGuard.Check()) return;
            var button = s as Button;
            try
            {
                // Building again replaces the CAD output layers. Protect
                // manually grip-edited geometry even if the user has not pressed
                // "CẬP NHẬT MẶT CẮT" yet. Default is to KEEP the original.
                var activeDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (activeDoc == null)
                    throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
                if (EditableSectionCadService.HasEditableProfiles(activeDoc.Database) &&
                    MessageBox.Show(
                        "Bản vẽ đang có hệ mặt cắt CAD có thể đã được hiệu chỉnh thủ công. " +
                        "Thành lập lại sẽ THAY THẾ các đường mặt cắt và xóa các chỉnh sửa trên hệ mặt cắt cũ.\r\n\r\n" +
                        "Hãy lưu một bản sao DWG trước khi thực hiện. Anh có chắc chắn muốn vẽ lại không?",
                        "Xác nhận thay thế mặt cắt CAD",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                {
                    _status.Text = "Đã giữ nguyên hệ mặt cắt CAD và khối lượng hiện có.";
                    return;
                }
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
                IProgress<string> progress = new Progress<string>(message =>
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
                if (!ReferenceEquals(activeDoc, Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument) ||
                    !ReferenceEquals(system, state.SectionSystem) ||
                    !ReferenceEquals(existing, state.Existing.Tin) ||
                    !ReferenceEquals(design, state.Design.Tin))
                    throw new InvalidOperationException(
                        "Bản vẽ, hệ mặt cắt hoặc TIN đã đổi trong khi tính. Không xuất kết quả cũ sang bản vẽ khác.");
                int valid = profiles.Count(x => x.HasData);
                _status.Text =
                    $"Tính xong {profiles.Count:n0} mặt cắt ({valid:n0} đủ hai TIN) trong {watch.Elapsed.TotalSeconds:0.0}s. " +
                    "Chọn điểm chèn trên CAD.";
                _status.Refresh();

                var ins = SelectionService.PickInsertionPoint("Chọn điểm chèn hệ mặt cắt: ");
                if (!ins.HasValue) return;
                if (!ReferenceEquals(activeDoc, Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument) ||
                    !ReferenceEquals(system, state.SectionSystem) ||
                    !ReferenceEquals(existing, state.Existing.Tin) ||
                    !ReferenceEquals(design, state.Design.Tin))
                    throw new InvalidOperationException(
                        "Bản vẽ hoặc mô hình đã đổi trong khi chọn điểm chèn. Đã giữ nguyên kết quả cũ.");

                var drawWatch = Stopwatch.StartNew();
                _status.Text = $"Đang vẽ mặt cắt 0/{profiles.Count:n0} xuống AutoCAD...";
                _status.Refresh();
                long lastDrawUiTick = 0;
                SectionWorkflowService.DrawProfiles(
                    ins.Value,
                    (double)_hScale.Value,
                    (double)_vScale.Value,
                    (double)_axisStep.Value,
                    (done, total) =>
                    {
                        // Refreshing WinForms on each CAD entity batch forces
                        // expensive synchronous repaints during a locked drawing
                        // transaction. Display progress at most four times/sec.
                        // Never pump DoEvents in a document transaction.
                        long now = Stopwatch.GetTimestamp();
                        if (done != total && (now - lastDrawUiTick) < Stopwatch.Frequency / 4)
                            return;
                        lastDrawUiTick = now;
                        _status.Text = $"Đang vẽ mặt cắt {done:n0}/{total:n0} xuống AutoCAD...";
                        _status.Refresh();
                    },
                    (double)_layoutGap.Value,
                    profiles);
                drawWatch.Stop();
                // Only commit the newly sampled profiles after CAD drawing has
                // succeeded. Canceling the insertion point leaves prior
                // manually accepted profiles and volumes untouched.
                SectionWorkflowService.CommitProfiles(profiles);

                // The old default 0..100 cannot intersect elevations such as
                // +330..+380 in KL mb.dwg. Automatically match the real range
                // only when the ENTIRE requested range is outside the sampled
                // elevations; make the adjustment visible and editable.
                string autoRangeNote = "";
                var measurable = profiles.Where(x=>x!=null && x.HasData).ToArray();
                if (measurable.Length > 0)
                {
                    double minZ=measurable.Min(x=>x.MinZ);
                    double maxZ=measurable.Max(x=>x.MaxZ);
                    double requestedLow=Math.Min(state.FromLevel,state.ToLevel);
                    double requestedHigh=Math.Max(state.FromLevel,state.ToLevel);
                    if (requestedHigh<minZ-1e-7 || requestedLow>maxZ+1e-7 ||
                        Math.Abs(state.FromLevel-state.ToLevel)<1e-7)
                    {
                        double zStep=(double)_levelStep.Value;
                        double from=Math.Floor(minZ/zStep)*zStep;
                        double to=Math.Ceiling(maxZ/zStep)*zStep;
                        if(to<=from)to=from+zStep;
                        _syncing=true;
                        try
                        {
                            _fromLevel.Value=Clamp(_fromLevel,from);
                            _toLevel.Value=Clamp(_toLevel,to);
                            state.FromLevel=(double)_fromLevel.Value;
                            state.ToLevel=(double)_toLevel.Value;
                        }
                        finally { _syncing=false; }
                        autoRangeNote=$" Đã tự đặt mức tính theo TIN: {state.FromLevel:0.###} đến {state.ToLevel:0.###} m.";
                        state.NotifyChanged();
                    }
                }

                _status.Text =
                    $"Đã thành lập {profiles.Count:n0} mặt cắt; {valid:n0} mặt cắt có đủ dữ liệu hai TIN. " +
                    $"Tính {watch.Elapsed.TotalSeconds:0.0}s; vẽ {drawWatch.Elapsed.TotalSeconds:0.0}s." + autoRangeNote;
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


        /// <summary>
        /// Independent plan-picked quick sections. The existing parallel
        /// section system, approved profile corrections and volume results
        /// are NEVER modified by this workflow.
        /// </summary>
        private async void DrawQuickSections(object sender, EventArgs e)
        {
            if (!LicenseUiGuard.Check()) return;
            var button = sender as Button;
            var st = ProjectState.Current;
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            try
            {
                if (doc == null)
                    throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
                SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers: false);
                var selected = SelectionService.PickQuickSectionRoutes();
                if (selected == null || selected.Count == 0) return;
                var insertion = SelectionService.PickInsertionPoint(
                    "Chọn điểm chèn các mặt cắt nhanh (không ảnh hưởng mặt cắt tính khối lượng): ");
                if (!insertion.HasValue) return;

                var ht = st.Existing.Tin;
                var tk = st.Design.Tin;
                long hRevision = st.Existing.Source.Revision;
                long tRevision = st.Design.Source.Revision;
                long regionRevision = st.TinRegionRevision;
                double hScale = st.HorizontalScale, vScale = st.VerticalScale;
                double axisStep = st.SectionAxisElevationInterval;
                double layoutGap = st.SectionLayoutGap;
                if (button != null) button.Enabled = false;
                UseWaitCursor = true;
                _status.Text = "Đang tra TIN theo " + selected.Count +
                    " tuyến mặt cắt nhanh độc lập...";
                var watch = Stopwatch.StartNew();
                var profiles = await Task.Run(() =>
                {
                    var sampler = new TinSectionSampler();
                    var builder = new QuickSectionProfileBuilder();
                    return selected.Select((route, i) =>
                        builder.Build(i + 1, "MCN-" + (i+1).ToString("00") +
                            " (" + route.SourceHandle + ")", route.Vertices,
                            ht, tk, sampler)).ToArray();
                });
                if (!ReferenceEquals(doc,
                        Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument) ||
                    !ReferenceEquals(ht, st.Existing.Tin) ||
                    !ReferenceEquals(tk, st.Design.Tin) ||
                    !st.Existing.IsTinCurrent || !st.Design.IsTinCurrent ||
                    st.Existing.Source.Revision != hRevision ||
                    st.Design.Source.Revision != tRevision ||
                    st.TinRegionRevision != regionRevision ||
                    st.HorizontalScale != hScale || st.VerticalScale != vScale ||
                    st.SectionAxisElevationInterval != axisStep ||
                    st.SectionLayoutGap != layoutGap)
                    throw new InvalidOperationException(
                        "Bản vẽ hoặc TIN đã thay đổi khi lấy mặt cắt nhanh. " +
                        "Chưa ghi kết quả CAD; hãy chọn lại tuyến.");
                var valid = profiles.Where(p => p.HasData).ToArray();
                if (valid.Length == 0)
                    throw new InvalidOperationException(
                        "Các tuyến đã chọn không có đoạn giao đủ dữ liệu của hai TIN.");
                using (doc.LockDocument())
                    SectionCadRenderer.DrawQuickProfiles(doc.Database, valid,
                        insertion.Value, hScale, vScale, axisStep, layoutGap);
                watch.Stop();
                _status.Text = "Đã vẽ mặt cắt nhanh thành công.";
            }
            catch (Exception ex)
            {
                _status.Text = "Chưa vẽ được mặt cắt nhanh: " + ex.Message;
                MessageBox.Show(ex.Message, "Vẽ mặt cắt nhanh",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                UseWaitCursor = false;
                if (button != null && !button.IsDisposed) button.Enabled = true;
            }
        }

        private async void UpdateEditedProfiles(object sender, EventArgs e)
        {
            if (!LicenseUiGuard.Check()) return;
            var button = sender as Button;
            var state = ProjectState.Current;
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            try
            {
                if (doc == null) throw new InvalidOperationException("Không có bản vẽ AutoCAD.");
                if (state.SectionProfiles.Count == 0)
                    throw new InvalidOperationException("Hãy xuất/vẽ hệ mặt cắt trước khi hiệu chỉnh.");
                if (Math.Abs(state.FromLevel - state.ToLevel) < 1e-9)
                    throw new InvalidOperationException("Hãy thiết lập 'Tính từ mức' và 'Đến mức' trước khi cập nhật khối lượng.");
                if (button != null) button.Enabled = false;
                UseWaitCursor = true;
                SectionWorkflowService.EnsureProfileInputsReady();
                var oldProfiles = state.SectionProfiles.ToArray();
                // Read CAD in its drawing thread before doing pure geometry in Task.Run.
                var edited = EditableSectionCadService.ReadEditedProfiles(doc.Database, oldProfiles);
                var sectionSystem = state.SectionSystem;
                var existing = state.Existing.Tin;
                var design = state.Design.Tin;
                double from = state.FromLevel, to = state.ToLevel, step = state.LevelStep;
                var watch = Stopwatch.StartNew();
                _status.Text = "Đang tính lại diện tích đào/đắp và khối lượng từ mặt cắt đã sửa trên CAD...";
                var volume = await Task.Run(() => VolumeWorkflowService.CalculateCore(
                    sectionSystem, edited, existing, design, from, to, step));
                if (!ReferenceEquals(doc, Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument) ||
                    !ReferenceEquals(sectionSystem, state.SectionSystem) ||
                    !ReferenceEquals(existing, state.Existing.Tin) ||
                    !ReferenceEquals(design, state.Design.Tin) ||
                    from != state.FromLevel || to != state.ToLevel || step != state.LevelStep ||
                    oldProfiles.Length != state.SectionProfiles.Count ||
                    oldProfiles.Where((v, i) => !ReferenceEquals(v, state.SectionProfiles[i])).Any())
                    throw new InvalidOperationException("Dữ liệu CAD/TIN đã đổi trong lúc tính. Hãy cập nhật lại.");
                // Commit only after all geometry, volume and CAD annotation steps succeed.
                using (doc.LockDocument())
                {
                    var latest=EditableSectionCadService.ReadEditedProfiles(doc.Database,oldProfiles);
                    if(!EditedSectionProfileBuilder.SameGeometry(edited,latest))
                        throw new InvalidOperationException("Đường mặt cắt đã đổi trong lúc tính. Hãy cập nhật lại.");
                    EditableSectionCadService.UpdateAreaLabels(doc.Database, edited);
                }
                SectionWorkflowService.CommitProfiles(edited);
                VolumeWorkflowService.Commit(volume, from, to, step);
                watch.Stop();
                state.VolumeCalculatedUtc = DateTime.UtcNow;
                state.VolumeCalculationSeconds = watch.Elapsed.TotalSeconds;
                doc.Editor.Regen();
                _status.Text = "Đã cập nhật " + edited.Count.ToString("n0") +
                    " mặt cắt và tính lại khối lượng: đào " + volume.TotalCutVolume.ToString("n2") +
                    " m³; đắp " + volume.TotalFillVolume.ToString("n2") + " m³.";
                RefreshGrid();
            }
            catch (Exception ex)
            {
                _status.Text = "Chưa cập nhật mặt cắt: " + ex.Message;
                MessageBox.Show(ex.Message, "Cập nhật mặt cắt CAD", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void UpdateScaleAvailability()
        {
            bool custom = string.Equals(_parameterMode.SelectedItem as string,
                "Tùy chỉnh", StringComparison.Ordinal);
            _hScale.Enabled = custom;
            _vScale.Enabled = custom;
        }

        private void ParameterModeChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            if (string.Equals(_parameterMode.SelectedItem as string,
                "TT34/2018/TT-BCT", StringComparison.Ordinal))
            {
                _syncing = true;
                try
                {
                    _spacing.Value = 20;
                    _axisStep.Value = 20;
                    _hScale.Value = 1000;
                    _vScale.Value = 1000;
                }
                finally { _syncing = false; }
            }
            UpdateScaleAvailability();
            GeneralParameterChanged(sender, e);
        }

        private void GeneralParameterChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            var st = ProjectState.Current;
            if (string.Equals(_parameterMode.SelectedItem as string,
                "TT34/2018/TT-BCT", StringComparison.Ordinal) &&
                (_spacing.Value > 20 || _axisStep.Value != 20 ||
                 _hScale.Value != 1000 || _vScale.Value != 1000))
            {
                _syncing = true;
                try { _parameterMode.SelectedItem = "Tùy chỉnh"; }
                finally { _syncing = false; }
            }
            double newSpacing = (double)_spacing.Value;
            bool geometryChanged = Math.Abs(st.SectionSpacing - newSpacing) > 1e-9;
            // The preview geometry is generated at the accepted station step.
            // Editing that step must never leave an apparently current 20-m
            // line system while the UI/Excel says 10 m. Plot-only parameters
            // (Z-axis marks, drawing separation) do not change volume geometry.
            if (geometryChanged)
            {
                st.SectionSystem = null;
                st.SectionProfiles.Clear();
                st.VolumeResult = null;
                _status.Text = "Đã đổi bước tuyến. Bấm 'Xem trước tuyến', rồi vẽ lại mặt cắt.";
            }
            st.SectionSpacing = newSpacing;
            st.SectionAxisElevationInterval = (double)_axisStep.Value;
            st.SectionMapScale = 1000;
            st.SectionParameterMode = _parameterMode.SelectedItem as string ?? "Tùy chỉnh";
            st.SectionLayoutGap = (double)_layoutGap.Value;
            st.HorizontalScale = (double)_hScale.Value;
            st.VerticalScale = (double)_vScale.Value;
            if (geometryChanged) st.NotifyChanged();
        }

        private void SaveParameters()
        {
            var st = ProjectState.Current;
            st.SectionSpacing = (double)_spacing.Value;
            st.SectionAxisElevationInterval = (double)_axisStep.Value;
            st.SectionParameterMode = _parameterMode.SelectedItem as string ?? "Tùy chỉnh";
            st.SectionMapScale = 1000;
            st.SectionLayoutGap = (double)_layoutGap.Value;
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
            _parameterMode.SelectedItem = st.SectionParameterMode == "TT34/2018/TT-BCT"
                ? "TT34/2018/TT-BCT" : "Tùy chỉnh";
            _spacing.Value = Clamp(_spacing, st.SectionSpacing);
            _axisStep.Value = Clamp(_axisStep, st.SectionAxisElevationInterval);
            _layoutGap.Value = Clamp(_layoutGap, st.SectionLayoutGap);
            _levelStep.Value = Clamp(_levelStep, st.LevelStep);
            _fromLevel.Value = Clamp(_fromLevel, st.FromLevel);
            _toLevel.Value = Clamp(_toLevel, st.ToLevel);
            _hScale.Value = Clamp(_hScale, st.HorizontalScale);
            _vScale.Value = Clamp(_vScale, st.VerticalScale);
            UpdateScaleAvailability();
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
            var st = ProjectState.Current;
            _grid.SuspendLayout();
            try
            {
            _grid.Rows.Clear();
            if (st.SectionSystem == null) return;
            var profileByName = st.SectionProfiles
                .Where(p => p != null && p.Line != null)
                .GroupBy(p => p.Line.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var line in st.SectionSystem.Lines)
            {
                SectionProfile p;
                profileByName.TryGetValue(line.Name, out p);
                _grid.Rows.Add(line.Name, line.Offset.ToString("0.###"), line.TotalLength.ToString("0.###"),
                    p == null ? "-" : p.CutArea.ToString("0.00"), p == null ? "-" : p.FillArea.ToString("0.00"),
                    p == null ? "Chưa tính" :
                    !p.HasData ? "Thiếu dữ liệu TIN" :
                    p.Warnings.Any(w => w.StartsWith("MẶT ĐỨNG NHIỀU Z:", StringComparison.OrdinalIgnoreCase))
                        ? "CẦN KIỂM TRA MC" : "OK");
            }
            }
            finally { _grid.ResumeLayout(); }
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