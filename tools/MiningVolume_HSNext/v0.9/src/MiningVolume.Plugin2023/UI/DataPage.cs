using System;
using System.Collections.Generic;
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
            BackColor = Color.White;
            Controls.Add(new Label { Text = "DỮ LIỆU ĐẦU VÀO", Dock = DockStyle.Top, Height = 32, Font = new Font("Arial", 11F, FontStyle.Bold) });

            var body = new TableLayoutPanel { Dock = DockStyle.Top, Height = 330, ColumnCount = 3, RowCount = 8, Padding = new Padding(4) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));

            _existingLayer = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _designLayer = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            AddRow(body, 0, "Hiện trạng", _existingLayer, Btn("Nạp dữ liệu", (s, e) => LoadModel(ModelRole.Existing)));
            AddRow(body, 1, "Thiết kế", _designLayer, Btn("Nạp dữ liệu", (s, e) => LoadModel(ModelRole.Design)));

            _types = new CheckedListBox { Dock = DockStyle.Fill, Height = 120, CheckOnClick = true };
            foreach (var x in new[] { "POINT", "LINE", "LWPOLYLINE", "2D POLYLINE", "3D POLYLINE", "Đường đồng mức" }) _types.Items.Add(x, true);
            body.Controls.Add(new Label { Text = "Loại dữ liệu", Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft }, 0, 2);
            body.Controls.Add(_types, 1, 2);
            body.SetColumnSpan(_types, 2);

            var refresh = Btn("Làm mới danh sách layer", (s, e) => RefreshLayers());
            body.Controls.Add(refresh, 1, 4);
            body.SetColumnSpan(refresh, 2);

            _status = new Label
            {
                Text = "Chưa nạp dữ liệu",
                Dock = DockStyle.Fill,
                AutoSize = true,
                ForeColor = Color.DimGray
            };
            body.Controls.Add(_status, 1, 5);
            body.SetColumnSpan(_status, 2);

            _buildPair = Btn("TẠO CẶP TIN HIỆN TRẠNG + THIẾT KẾ", async (s, e) => await BuildPairTinAsync());
            _buildPair.Font = new Font("Arial", 9F, FontStyle.Bold);
            _buildPair.Enabled = false;
            body.Controls.Add(_buildPair, 1, 6);
            body.SetColumnSpan(_buildPair, 2);

            Controls.Add(body);
            body.BringToFront();
            RefreshLayers();
            RefreshPairButton();
        }

        private static Button Btn(string text, EventHandler h)
        {
            var b = new Button { Text = text, Dock = DockStyle.Fill, Height = 28 };
            b.Click += h;
            return b;
        }

        private static void AddRow(TableLayoutPanel p, int r, string label, Control main, Control button)
        {
            p.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r);
            p.Controls.Add(main, 1, r);
            p.Controls.Add(button, 2, r);
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
                _status.Text = "Đang kiểm tra và dựng cặp TIN hiện trạng / thiết kế...";
                _status.Refresh();

                var builds = await Task.Run(() => SurfaceWorkflowService.BuildPairCoreDetailed());
                SurfaceWorkflowService.DrawTinPair(builds);

                _status.Text =
                    $"Cặp TIN hợp lệ: {st.Existing.TinLayer} = {builds[0].TriangleCount:n0} tam giác; " +
                    $"{st.Design.TinLayer} = {builds[1].TriangleCount:n0} tam giác.";

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

        private void LoadModel(ModelRole role)
        {
            var cb = role == ModelRole.Existing ? _existingLayer : _designLayer;
            if (string.IsNullOrWhiteSpace(cb.Text)) { MessageBox.Show("Chọn layer trước khi nạp.", "Mining Volume"); return; }
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