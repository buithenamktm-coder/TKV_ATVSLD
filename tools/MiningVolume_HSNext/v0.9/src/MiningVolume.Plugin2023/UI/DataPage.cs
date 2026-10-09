using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MiningVolume.Core.Model;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    public sealed class DataPage : UserControl
    {
        private readonly ComboBox _existingLayer;
        private readonly ComboBox _designLayer;
        private readonly CheckedListBox _types;
        private readonly Label _status;
        private readonly Button _buildPair;

        public DataPage()
        {
            Font = new Font("Arial", 9F);
            BackColor = UiTheme.Canvas;
            AutoScroll = true;
            Padding = new Padding(0);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = UiTheme.Surface,
                Padding = new Padding(16, 10, 16, 8)
            };
            header.Controls.Add(new Label
            {
                Text = "DỮ LIỆU ĐẦU VÀO",
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Arial", 12F, FontStyle.Bold),
                ForeColor = UiTheme.TextStrong
            });
            header.Controls.Add(new Label
            {
                Text = "Nạp theo layer hoặc chọn trực tiếp POINT / LINE / POLYLINE trên bản vẽ.",
                Dock = DockStyle.Bottom,
                Height = 22,
                Font = new Font("Arial", 8.75F),
                ForeColor = UiTheme.Muted
            });

            var body = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(14, 14, 14, 18),
                BackColor = UiTheme.Canvas
            };

            var sourceBox = new GroupBox
            {
                Text = "Nguồn dữ liệu",
                Width = 520,
                Height = 112,
                Margin = new Padding(0, 0, 0, 12)
            };
            var source = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                Padding = new Padding(10, 8, 10, 8)
            };
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            source.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            source.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

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
            sourceBox.Controls.Add(source);
            body.Controls.Add(sourceBox);

            var typeBox = new GroupBox
            {
                Text = "Loại dữ liệu tham gia TIN",
                Width = 520,
                Height = 174,
                Margin = new Padding(0, 0, 0, 12)
            };
            _types = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0)
            };
            foreach (var x in new[] { "POINT", "LINE", "LWPOLYLINE", "2D POLYLINE", "3D POLYLINE", "Đường đồng mức" })
                _types.Items.Add(x, true);
            var typePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 10) };
            typePanel.Controls.Add(_types);
            typeBox.Controls.Add(typePanel);
            body.Controls.Add(typeBox);

            var statusBox = new GroupBox
            {
                Text = "Trạng thái mô hình",
                Width = 520,
                Height = 104,
                Margin = new Padding(0, 0, 0, 12)
            };
            _status = new Label
            {
                Text = "Chưa nạp dữ liệu.",
                Dock = DockStyle.Fill,
                ForeColor = UiTheme.Muted,
                Padding = new Padding(10, 8, 10, 8),
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusBox.Controls.Add(_status);
            body.Controls.Add(statusBox);

            var actions = new TableLayoutPanel
            {
                Width = 520,
                Height = 92,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            var refresh = Btn("Làm mới danh sách layer", (s, e) => RefreshLayers());
            refresh.Dock = DockStyle.Fill;
            refresh.Margin = new Padding(0, 0, 0, 6);
            actions.Controls.Add(refresh, 0, 0);

            _buildPair = Btn("TẠO CẶP TIN HIỆN TRẠNG + THIẾT KẾ", async (s, e) => await BuildPairTinAsync());
            _buildPair.Font = new Font("Arial", 9F, FontStyle.Bold);
            _buildPair.Dock = DockStyle.Fill;
            _buildPair.Margin = new Padding(0);
            _buildPair.Enabled = false;
            actions.Controls.Add(_buildPair, 0, 1);
            body.Controls.Add(actions);

            body.SizeChanged += (s, e) =>
            {
                int width = Math.Max(420, body.ClientSize.Width - body.Padding.Horizontal - 4);
                sourceBox.Width = width;
                typeBox.Width = width;
                statusBox.Width = width;
                actions.Width = width;
            };

            Controls.Add(body);
            Controls.Add(header);
            header.BringToFront();

            RefreshLayers();
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
                Fill(_existingLayer, layers, ProjectState.Current.Existing.Layer);
                Fill(_designLayer, layers, ProjectState.Current.Design.Layer);
                _status.Text = layers.Count.ToString("n0") + " layer trong bản vẽ.";
            }
            catch (Exception ex)
            {
                _status.Text = "Không đọc được layer: " + ex.Message;
            }
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
                    "MiningVolume - TIN",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _buildPair.Enabled = false;
                UseWaitCursor = true;
                var totalWatch = Stopwatch.StartNew();

                _status.Text = $"Đang dựng TIN hiện trạng từ {st.Existing.ActiveVertexCount:n0} đỉnh...";
                _status.Refresh();
                var existing = await Task.Run(() => SurfaceWorkflowService.BuildCoreDetailed(ModelRole.Existing));

                _status.Text =
                    $"Hiện trạng xong ({existing.TriangleCount:n0} tam giác, {existing.CoreMilliseconds / 1000.0:0.00}s). " +
                    $"Đang dựng TIN thiết kế từ {st.Design.ActiveVertexCount:n0} đỉnh...";
                _status.Refresh();
                var design = await Task.Run(() => SurfaceWorkflowService.BuildCoreDetailed(ModelRole.Design));

                var builds = new[] { existing, design };
                var cadWatch = Stopwatch.StartNew();
                SurfaceWorkflowService.DrawTinPair(builds, message =>
                {
                    _status.Text = message;
                    _status.Refresh();
                    Application.DoEvents();
                });
                cadWatch.Stop();
                totalWatch.Stop();

                _status.Text =
                    $"Cặp TIN hợp lệ: {st.Existing.TinLayer} = {existing.TriangleCount:n0}; " +
                    $"{st.Design.TinLayer} = {design.TriangleCount:n0} tam giác. " +
                    $"Ghi CAD {cadWatch.Elapsed.TotalSeconds:0.00}s; tổng {totalWatch.Elapsed.TotalSeconds:0.00}s.";

                MessageBox.Show(
                    "ĐÃ TẠO ĐỦ 2 TIN DÙNG CHO TÍNH KHỐI LƯỢNG\r\n\r\n" +
                    $"Hiện trạng → {st.Existing.TinLayer}: {builds[0].Summary}\r\n" +
                    $"Thiết kế → {st.Design.TinLayer}: {builds[1].Summary}",
                    "MiningVolume - Cặp TIN hợp lệ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                MessageBox.Show(ex.Message, "Không tạo được cặp TIN", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _status.Text = "Chưa tạo được cặp TIN.";
            }
            finally
            {
                UseWaitCursor = false;
                RefreshPairButton();
            }
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