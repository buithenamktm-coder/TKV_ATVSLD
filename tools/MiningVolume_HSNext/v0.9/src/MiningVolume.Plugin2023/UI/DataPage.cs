using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MiningVolume.Core.Model;
using MiningVolume.Core.Surface;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    public sealed class DataPage : UserControl
    {
        private readonly ComboBox _existingLayer;
        private readonly ComboBox _designLayer;
        private readonly CheckedListBox _types;
        private readonly Label _status;
        private readonly Label _tinRegionStatus;
        private readonly Button _buildPair;
        private readonly Button _cancelBuild;
        private readonly Button _via4Preset;
        private CancellationTokenSource _buildCts;

        public DataPage()
        {
            Font = new Font("Arial", 9F);
            BackColor = UiTheme.Canvas;
            AutoScroll = false;
            Padding = new Padding(0);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 10, 12, 6)
            };
            header.Controls.Add(new Label
            {
                Text = "DỮ LIỆU ĐẦU VÀO",
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 11.5F, FontStyle.Bold),
                ForeColor = UiTheme.TextStrong,
                TextAlign = ContentAlignment.MiddleLeft
            });

            var body = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8, 8, 8, 10),
                BackColor = UiTheme.Canvas
            };

            var sourceBox = new GroupBox
            {
                Text = "Nguồn dữ liệu",
                Width = 520,
                Height = 148,
                Margin = new Padding(0, 0, 0, 8)
            };
            var source = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                Padding = new Padding(0)
            };
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            source.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            source.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            source.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            _existingLayer = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(3, 5, 6, 5) };
            _designLayer = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(3, 5, 6, 5) };
            AddRow(
                source, 0, "Hiện trạng", _existingLayer,
                Btn("Nạp layer", (s, e) => LoadModel(ModelRole.Existing)),
                Btn("Chọn trên CAD", (s, e) => SelectModel(ModelRole.Existing)));
            AddRow(
                source, 1, "Thiết kế", _designLayer,
                Btn("Nạp layer", (s, e) => LoadModel(ModelRole.Design)),
                Btn("Chọn trên CAD", (s, e) => SelectModel(ModelRole.Design)));
            _via4Preset = Btn("Nạp mẫu Vỉa 4: ht / - nam4 / LO_TINHKL", (s, e) => LoadVia4Preset());
            source.Controls.Add(_via4Preset, 0, 2);
            source.SetColumnSpan(_via4Preset, 4);
            sourceBox.Controls.Add(source);
            body.Controls.Add(sourceBox);

            var typeBox = new GroupBox
            {
                Text = "Loại dữ liệu tham gia TIN",
                Width = 520,
                Height = 96,
                Margin = new Padding(0, 0, 0, 8)
            };
            _types = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
                MultiColumn = true,
                ColumnWidth = 165,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0),
                BackColor = UiTheme.Surface
            };
            foreach (var x in new[] { "POINT", "LINE", "LWPOLYLINE", "2D POLYLINE", "3D POLYLINE", "Đường đồng mức" })
                _types.Items.Add(x, true);
            var typePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 8) };
            typePanel.Controls.Add(_types);
            typeBox.Controls.Add(typePanel);
            body.Controls.Add(typeBox);

            var regionBox = new GroupBox
            {
                Text = "Phạm vi tạo TIN",
                Width = 520,
                Height = 102,
                Margin = new Padding(0, 0, 0, 8)
            };
            var regionLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(0)
            };
            regionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            regionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            regionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            regionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _tinRegionStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.Muted,
                Text = "Toàn bộ dữ liệu đã nạp."
            };
            regionLayout.Controls.Add(_tinRegionStatus, 0, 0);
            regionLayout.SetColumnSpan(_tinRegionStatus, 2);
            regionLayout.Controls.Add(
                Btn("Chọn vùng trên CAD", (s, e) => SelectTinRegion()), 0, 1);
            regionLayout.Controls.Add(
                Btn("Dùng toàn bộ", (s, e) => ClearTinRegion()), 1, 1);
            regionBox.Controls.Add(regionLayout);
            body.Controls.Add(regionBox);

            var statusBox = new GroupBox
            {
                Text = "Trạng thái mô hình",
                Width = 520,
                Height = 80,
                Margin = new Padding(0, 0, 0, 8)
            };
            _status = new Label
            {
                Text = "Chưa nạp dữ liệu.",
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.Muted,
                Padding = new Padding(2),
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusBox.Controls.Add(_status);
            body.Controls.Add(statusBox);

            var actions = new TableLayoutPanel
            {
                Width = 520,
                Height = 44,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));

            var refresh = Btn("Làm mới layer", (s, e) => RefreshLayers());
            refresh.Dock = DockStyle.Fill;
            refresh.Margin = new Padding(0, 0, 6, 0);
            actions.Controls.Add(refresh, 0, 0);

            _buildPair = Btn("TẠO CẶP TIN", async (s, e) => await BuildPairTinAsync());
            _buildPair.Font = new Font("Arial", 9F, FontStyle.Bold);
            _buildPair.Dock = DockStyle.Fill;
            _buildPair.Margin = new Padding(0);
            _buildPair.Enabled = false;
            actions.Controls.Add(_buildPair, 1, 0);

            _cancelBuild = Btn("Hủy", (s, e) =>
            {
                if (_buildCts == null || _buildCts.IsCancellationRequested) return;
                _status.Text = "Đang yêu cầu hủy dựng TIN...";
                _buildCts.Cancel();
            });
            _cancelBuild.Dock = DockStyle.Fill;
            _cancelBuild.Margin = new Padding(6, 0, 0, 0);
            _cancelBuild.Enabled = false;
            actions.Controls.Add(_cancelBuild, 2, 0);

            body.Controls.Add(actions);

            // A dedicated scroll viewport prevents the page title from
            // overlapping the top-docked flow panel at small palette sizes.
            var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            viewport.Controls.Add(body);
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                Margin = new Padding(0), Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0);
            viewport.Margin = new Padding(0);
            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(viewport, 0, 1);
            Controls.Add(layout);

            Action resizeCards = () =>
            {
                int width = Math.Max(1, viewport.ClientSize.Width -
                    SystemInformation.VerticalScrollBarWidth - body.Padding.Horizontal - 2);
                foreach (Control card in body.Controls) card.Width = width;
                _types.ColumnWidth = Math.Max(120, (_types.ClientSize.Width - 4) / 2);
            };
            viewport.SizeChanged += (s, e) => resizeCards();
            Load += (s, e) => resizeCards();

            RefreshLayers();
            RefreshTinRegionStatus();
            RefreshPairButton();
        }

        private static Button Btn(string text, EventHandler h)
        {
            var b = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Height = 34,
                MinimumSize = new Size(0, 34),
                Margin = new Padding(3, 4, 3, 4),
                UseCompatibleTextRendering = true
            };
            b.Click += h;
            return b;
        }

        private static void AddRow(TableLayoutPanel p, int r, string label, Control main, Control layerButton, Control selectButton)
        {
            p.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r);
            p.Controls.Add(main, 1, r);
            p.Controls.Add(layerButton, 2, r);
            p.Controls.Add(selectButton, 3, r);
        }

        private void RefreshLayers()
        {
            try
            {
                var layers = LayerService.GetSourceLayers();
                var state = ProjectState.Current;
                string existing = state.Existing.Layer;
                string design = state.Design.Layer;
                bool via4 = layers.Exists(x => string.Equals(x, "ht", StringComparison.OrdinalIgnoreCase)) &&
                    layers.Exists(x => string.Equals(x, "- nam4", StringComparison.OrdinalIgnoreCase));
                if (via4 && _existingLayer.Items.Count == 0 && string.IsNullOrEmpty(existing)) existing = "ht";
                if (via4 && _designLayer.Items.Count == 0 && string.IsNullOrEmpty(design)) design = "- nam4";
                Fill(_existingLayer, layers, existing);
                Fill(_designLayer, layers, design);
                _via4Preset.Enabled = via4 && layers.Exists(x => string.Equals(x, "LO_TINHKL", StringComparison.OrdinalIgnoreCase));
                _status.Text = layers.Count.ToString("n0") + " layer trong bản vẽ.";
            }
            catch (Exception ex)
            {
                _status.Text = "Không đọc được layer: " + ex.Message;
            }
        }

        private void LoadVia4Preset()
        {
            if (_buildCts != null) return;
            try
            {
                _via4Preset.Enabled = false;
                _status.Text = "Đang nạp Vỉa 4: ht → hiện trạng, - nam4 → thiết kế, LO_TINHKL → phạm vi tính...";
                _status.Refresh();
                var loaded = SurfaceWorkflowService.LoadVia4Preset(AllowedTypes());
                RefreshLayers();
                RefreshTinRegionStatus();
                RefreshPairButton();
                _status.Text = $"Vỉa 4 đã nạp • Hiện trạng: {loaded[0].Summary} • Thiết kế: {loaded[1].Summary}. Phạm vi: LO_TINHKL.";
            }
            catch (Exception ex) { _status.Text = "Không nạp được mẫu Vỉa 4: " + ex.Message; }
            finally { _via4Preset.Enabled = true; }
        }

        private static void Fill(ComboBox cb, List<string> values, string preferred)
        {
            var old = string.IsNullOrWhiteSpace(preferred) ? cb.Text : preferred;
            cb.Items.Clear();
            foreach (var x in values) cb.Items.Add(x);
            if (cb.Items.Count > 0)
            {
                var i = cb.FindStringExact(old);
                cb.SelectedIndex = i >= 0 ? i : 0;
            }
        }

        private ISet<SourceEntityType> AllowedTypes()
        {
            var set = new HashSet<SourceEntityType>();
            foreach (var item in _types.CheckedItems)
            {
                switch (item.ToString())
                {
                    case "POINT": set.Add(SourceEntityType.Point); break;
                    case "LINE": set.Add(SourceEntityType.Line); break;
                    case "LWPOLYLINE": set.Add(SourceEntityType.LwPolyline); break;
                    case "2D POLYLINE": set.Add(SourceEntityType.Polyline2d); break;
                    case "3D POLYLINE": set.Add(SourceEntityType.Polyline3d); break;
                    case "Đường đồng mức": set.Add(SourceEntityType.Contour); break;
                }
            }
            return set;
        }

        private void RefreshPairButton()
        {
            var st = ProjectState.Current;
            _buildPair.Enabled =
                st.Existing.Source != null && st.Existing.Source.Entities.Count > 0 &&
                st.Design.Source != null && st.Design.Source.Entities.Count > 0;
        }

        private async Task BuildPairTinAsync()
        {
            var st = ProjectState.Current;
            if (st.Existing.Source == null || st.Existing.Source.Entities.Count == 0 ||
                st.Design.Source == null || st.Design.Source.Entities.Count == 0)
            {
                MessageBox.Show(
                    "Phải nạp đủ dữ liệu Hiện trạng và Thiết kế trước khi tạo cặp TIN.",
                    "IMSAT VOLUME - TIN",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _buildCts?.Dispose();
            _buildCts = new CancellationTokenSource();
            var token = _buildCts.Token;

            try
            {
                _buildPair.Enabled = false;
                _cancelBuild.Enabled = true;
                UseWaitCursor = true;
                var totalWatch = Stopwatch.StartNew();

                SetBuildStatus(
                    st.HasTinRegion
                        ? $"Đang dựng TIN hiện trạng trong vùng chọn ({st.TinRegionPolygon.Count:n0} đỉnh biên)..."
                        : $"Đang dựng TIN hiện trạng từ {st.Existing.ActiveVertexCount:n0} đỉnh...");
                var existing = await BuildRoleWithConflictChoiceAsync(ModelRole.Existing, token);

                SetBuildStatus(
                    $"Hiện trạng xong ({existing.TriangleCount:n0} tam giác, {existing.CoreMilliseconds / 1000.0:0.00}s). " +
                    $"Đang dựng TIN thiết kế từ {st.Design.ActiveVertexCount:n0} đỉnh...");
                var design = await BuildRoleWithConflictChoiceAsync(ModelRole.Design, token);

                token.ThrowIfCancellationRequested();
                var builds = new[] { existing, design };
                var cadWatch = Stopwatch.StartNew();
                SurfaceWorkflowService.DrawTinPair(builds, message => SetBuildStatus(message));
                cadWatch.Stop();
                totalWatch.Stop();

                SetBuildStatus(
                    $"Cặp TIN hợp lệ: {st.Existing.TinLayer} = {existing.TriangleCount:n0}; " +
                    $"{st.Design.TinLayer} = {design.TriangleCount:n0} tam giác. " +
                    $"Ghi CAD {cadWatch.Elapsed.TotalSeconds:0.00}s; tổng {totalWatch.Elapsed.TotalSeconds:0.00}s.");

                MessageBox.Show(
                    "ĐÃ TẠO ĐỦ 2 TIN DÙNG CHO TÍNH KHỐI LƯỢNG\r\n\r\n" +
                    $"Hiện trạng → {st.Existing.TinLayer}: {builds[0].Summary}\r\n" +
                    $"Thiết kế → {st.Design.TinLayer}: {builds[1].Summary}",
                    "IMSAT VOLUME - Cặp TIN hợp lệ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                _status.Text = "Đã hủy dựng TIN theo yêu cầu. Dữ liệu nguồn không bị thay đổi.";
            }
            catch (SurfaceValidationException ex)
            {
                var lines = new List<string>();
                foreach (var issue in ex.Issues)
                    lines.Add($"[{issue.Severity}] {issue.Code}: {issue.Message}");
                MessageBox.Show(
                    $"Không dựng được TIN {ex.ModelName}.\r\n\r\n" +
                    string.Join("\r\n", lines.GetRange(0, Math.Min(20, lines.Count))),
                    "Kiểm tra dữ liệu TIN",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _status.Text = $"TIN {ex.ModelName} chưa hợp lệ.";
            }
            catch (Exception ex)
            {
                string detail = ex.Message;
                if (ex.InnerException != null && !string.IsNullOrWhiteSpace(ex.InnerException.Message) &&
                    !detail.Contains(ex.InnerException.Message))
                    detail += "\r\n\r\nChi tiết: " + ex.InnerException.Message;

                MessageBox.Show(
                    detail,
                    "IMSAT VOLUME - Không tạo được cặp TIN",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                _status.Text = "Chưa tạo được cặp TIN. Xem thông báo để biết lỗi dữ liệu hoặc phép dựng.";
            }
            finally
            {
                UseWaitCursor = false;
                _cancelBuild.Enabled = false;
                _buildCts.Dispose();
                _buildCts = null;
                RefreshPairButton();
            }
        }

        private async Task<SurfaceBuildResult> BuildRoleWithConflictChoiceAsync(
            ModelRole role,
            CancellationToken token)
        {
            var policy = DuplicateXYConflictPolicy.Stop;
            var session = ProjectState.Current.Get(role);

            while (true)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    return await Task.Run(() =>
                        SurfaceWorkflowService.BuildCoreDetailed(
                            role,
                            message => SetBuildStatus(message),
                            token,
                            policy), token);
                }
                catch (DuplicateXYConflictException ex)
                {
                    token.ThrowIfCancellationRequested();

                    var chosen = DuplicateXYConflictUi.Ask(this, ex);
                    if (!chosen.HasValue)
                        throw new OperationCanceledException(
                            "Người dùng dừng tạo TIN do dữ liệu trùng XY khác Z.",
                            token);

                    policy = chosen.Value;
                    SetBuildStatus(
                        $"Đang dựng lại TIN {session.Name}: " +
                        (policy == DuplicateXYConflictPolicy.UseUpper
                            ? "dùng đỉnh trên (Z lớn hơn) tại vị trí trùng XY..."
                            : "dùng đỉnh dưới (Z nhỏ hơn) tại vị trí trùng XY..."));
                }
            }
        }

        private void SetBuildStatus(string message)
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(SetBuildStatus), message); } catch { }
                return;
            }
            _status.Text = message;
            _status.Refresh();
        }

        private void SelectTinRegion()
        {
            var handle = SelectionService.PickClosedBoundary();
            if (string.IsNullOrWhiteSpace(handle))
            {
                _status.Text = "Đã hủy chọn vùng tạo TIN.";
                return;
            }

            try
            {
                var polygon = BoundaryGeometryService.ReadBoundary(handle);
                if (polygon == null || polygon.Count < 3)
                    throw new InvalidOperationException("Vùng tạo TIN không có đủ đỉnh hợp lệ.");

                var state = ProjectState.Current;
                state.TinRegionHandle = handle;
                state.TinRegionPolygon.Clear();
                state.TinRegionPolygon.AddRange(polygon);

                // Thay đổi phạm vi tạo TIN làm cả hai TIN cũ mất hiệu lực.
                SurfaceWorkflowService.InvalidateTin(
                    ModelRole.Existing, clearCadLayer: true, notify: false);
                SurfaceWorkflowService.InvalidateTin(
                    ModelRole.Design, clearCadLayer: true, notify: false);
                state.NotifyChanged();

                RefreshTinRegionStatus();
                _status.Text =
                    $"Đã chọn vùng tạo TIN chung cho Hiện trạng + Thiết kế: " +
                    $"{polygon.Count:n0} đỉnh biên. TIN chỉ dùng trong vùng này; " +
                    "phần mềm tự giữ một dải dữ liệu đệm kỹ thuật quanh biên để nội suy ổn định.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Không chọn được vùng tạo TIN",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                _status.Text = "Chọn vùng tạo TIN thất bại.";
            }
        }

        private void ClearTinRegion()
        {
            var state = ProjectState.Current;
            bool hadRegion = state.HasTinRegion;
            state.TinRegionHandle = null;
            state.TinRegionPolygon.Clear();

            if (hadRegion)
            {
                SurfaceWorkflowService.InvalidateTin(
                    ModelRole.Existing, clearCadLayer: true, notify: false);
                SurfaceWorkflowService.InvalidateTin(
                    ModelRole.Design, clearCadLayer: true, notify: false);
                state.NotifyChanged();
            }

            RefreshTinRegionStatus();
            _status.Text = "Phạm vi tạo TIN: dùng toàn bộ dữ liệu đã nạp.";
        }

        private void RefreshTinRegionStatus()
        {
            var state = ProjectState.Current;
            _tinRegionStatus.Text = state.HasTinRegion
                ? $"Vùng CAD: {state.TinRegionPolygon.Count:n0} đỉnh biên • áp dụng cho cả 2 TIN"
                : "Toàn bộ dữ liệu đã nạp.";
        }

        private void SelectModel(ModelRole role)
        {
            var allowed = AllowedTypes();
            if (allowed.Count == 0)
            {
                MessageBox.Show("Chọn ít nhất một loại dữ liệu.", "Mining Volume");
                return;
            }

            var session = ProjectState.Current.Get(role);
            var picked = SelectionService.PickSurfaceEntities(session.Name);
            if (picked == null || picked.Count == 0)
            {
                _status.Text = "Đã hủy chọn đối tượng cho " + session.Name + ".";
                return;
            }

            try
            {
                UseWaitCursor = true;
                _status.Text = $"Đang nạp {picked.Count:n0} đối tượng đã chọn cho {session.Name}...";
                _status.Refresh();

                var result = SurfaceWorkflowService.LoadSelection(role, picked, allowed);
                ProjectState.Current.ActiveRole = role;
                _status.Text =
                    $"Đã nạp {session.Name} bằng chọn trực tiếp: {result.Summary}. " +
                    $"Nguồn có thể nằm trên nhiều layer. Đã chuẩn bị layer TIN: {result.OutputTinLayer}.";
                RefreshPairButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không nạp được đối tượng đã chọn", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _status.Text = "Nạp dữ liệu bằng chọn trực tiếp thất bại.";
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void LoadModel(ModelRole role)
        {
            var cb = role == ModelRole.Existing ? _existingLayer : _designLayer;
            if (string.IsNullOrWhiteSpace(cb.Text))
            {
                MessageBox.Show("Chọn layer trước khi nạp, hoặc dùng nút 'Chọn trên CAD'.", "Mining Volume");
                return;
            }
            var allowed = AllowedTypes();
            if (allowed.Count == 0) { MessageBox.Show("Chọn ít nhất một loại dữ liệu.", "Mining Volume"); return; }

            try
            {
                UseWaitCursor = true;
                _status.Text = "Đang đọc layer " + cb.Text + "...";
                _status.Refresh();
                var r = SurfaceWorkflowService.LoadLayer(role, cb.Text, allowed);
                ProjectState.Current.ActiveRole = role;
                _status.Text =
                    $"Đã nạp {ProjectState.Current.Get(role).Name}: {r.Summary}. " +
                    $"Đã chuẩn bị layer TIN: {r.OutputTinLayer}.";
                RefreshPairButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không nạp được dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _status.Text = "Nạp dữ liệu thất bại.";
            }
            finally { UseWaitCursor = false; }
        }
    }
}