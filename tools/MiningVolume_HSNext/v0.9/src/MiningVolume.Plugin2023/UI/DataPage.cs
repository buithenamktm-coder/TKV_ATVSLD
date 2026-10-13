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
        private readonly Button _buildExisting;
        private readonly Button _buildDesign;
        private readonly Button _cancelBuild;
        private readonly Button _via4Preset;
        private CancellationTokenSource _buildCts;
        private readonly GroupBox _sourceBox;
        private readonly TableLayoutPanel _sourceTable;
        private readonly Control[] _sourceLabels = new Control[2];
        private readonly Control[] _sourceLoad = new Control[2];
        private readonly Control[] _sourceChoose = new Control[2];
        private bool _compactSourceLayout;

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

            var sourceBox = _sourceBox = new GroupBox
            {
                Text = "Nguồn dữ liệu",
                Width = 520,
                Height = 173,
                Margin = new Padding(0, 0, 0, 8)
            };
            var source = _sourceTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                Padding = new Padding(0)
            };
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 107));
            source.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            source.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            source.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            source.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

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
            for (int row=0; row<2; row++)
            {
                _sourceLabels[row] = source.GetControlFromPosition(0,row);
                _sourceLoad[row] = source.GetControlFromPosition(2,row);
                _sourceChoose[row] = source.GetControlFromPosition(3,row);
            }
            _via4Preset = Btn("Nạp mẫu Vỉa 4: ht / - nam4 / LO_TINHKL", (s, e) => LoadVia4Preset());
            source.Controls.Add(_via4Preset, 0, 2);
            source.SetColumnSpan(_via4Preset, 4);
            sourceBox.Controls.Add(source);
            body.Controls.Add(sourceBox);
            // Optional diagnostic only: a user never has to delete duplicate
            // survey points to use the default automatic TIN workflow.
            var review = Btn("Thống kê điểm trùng (kiểm tra tùy chọn)", (s, e) =>
                DuplicateXYConflictUi.Show(ProjectState.Current.ActiveRole));
            review.Width = 520;
            body.Controls.Add(review);

            var typeBox = new GroupBox
            {
                Text = "Loại dữ liệu tham gia TIN",
                Width = 520,
                Height = 106,
                Margin = new Padding(0, 0, 0, 8)
            };
            _types = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
                MultiColumn = true,
                ColumnWidth = 192,
                HorizontalScrollbar = false,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0),
                BackColor = UiTheme.Surface
            };
            foreach (var x in new[] { "POINT", "LINE", "LWPOLYLINE", "2D POLYLINE", "3D POLYLINE", "Đường đồng mức" })
                _types.Items.Add(x, true);
            var typePanel = new Panel { Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 8) };
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
                Height = 104,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            var refresh = Btn("Làm mới layer", (s, e) => RefreshLayers());
            refresh.Dock = DockStyle.Fill;
            refresh.Margin = new Padding(5, 5, 7, 5);
            actions.Controls.Add(refresh, 0, 1);

            _buildExisting = Btn("TẠO TIN HIỆN TRẠNG", async (s,e) => await BuildSingleTinAsync(ModelRole.Existing));
            _buildDesign = Btn("TẠO TIN THIẾT KẾ", async (s,e) => await BuildSingleTinAsync(ModelRole.Design));
            _buildExisting.Dock = DockStyle.Fill;
            _buildDesign.Dock = DockStyle.Fill;
            _buildExisting.Margin = new Padding(5,5,7,5);
            _buildDesign.Margin = new Padding(7,5,5,5);
            actions.Controls.Add(_buildExisting,0,0);
            actions.Controls.Add(_buildDesign,1,0);
            _cancelBuild = Btn("Hủy", (s, e) =>
            {
                if (_buildCts == null || _buildCts.IsCancellationRequested) return;
                _status.Text = "Đang yêu cầu hủy dựng TIN...";
                _buildCts.Cancel();
            });
            _cancelBuild.Dock = DockStyle.Fill;
            _cancelBuild.Margin = new Padding(7, 5, 5, 5);
            _cancelBuild.Enabled = false;
            actions.Controls.Add(_cancelBuild, 1, 1);

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
                ApplyResponsiveSourceLayout(width);
            };
            viewport.SizeChanged += (s, e) => resizeCards();
            Load += (s, e) => resizeCards();

            RefreshLayers();
            RefreshTinRegionStatus();
            RefreshBuildButtons();
        }

        private void ApplyResponsiveSourceLayout(int width)
        {
            bool compact = width < 560;
            if (_compactSourceLayout == compact) return;
            _compactSourceLayout = compact;
            _sourceTable.SuspendLayout();
            try
            {
                _sourceTable.Controls.Clear();
                _sourceTable.RowStyles.Clear();
                _sourceTable.ColumnStyles.Clear();
                _sourceTable.ColumnCount = compact ? 2 : 4;
                _sourceTable.RowCount = compact ? 5 : 3;
                if (compact)
                {
                    _sourceBox.Height = 233;
                    _sourceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
                    _sourceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
                    for (int k=0;k<5;k++)
                        _sourceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
                    _sourceTable.Controls.Add(_sourceLabels[0],0,0);
                    _sourceTable.Controls.Add(_existingLayer,1,0);
                    _sourceTable.Controls.Add(_sourceLoad[0],0,1);
                    _sourceTable.Controls.Add(_sourceChoose[0],1,1);
                    _sourceTable.Controls.Add(_sourceLabels[1],0,2);
                    _sourceTable.Controls.Add(_designLayer,1,2);
                    _sourceTable.Controls.Add(_sourceLoad[1],0,3);
                    _sourceTable.Controls.Add(_sourceChoose[1],1,3);
                    _sourceTable.SetColumnSpan(_via4Preset,2);
                    _sourceTable.Controls.Add(_via4Preset,0,4);
                }
                else
                {
                    _sourceBox.Height = 148;
                    _sourceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
                    _sourceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    _sourceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
                    _sourceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
                    _sourceTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                    _sourceTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                    _sourceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                    for (int row=0;row<2;row++)
                    {
                        _sourceTable.Controls.Add(_sourceLabels[row],0,row);
                        _sourceTable.Controls.Add(row==0 ? (Control)_existingLayer : _designLayer,1,row);
                        _sourceTable.Controls.Add(_sourceLoad[row],2,row);
                        _sourceTable.Controls.Add(_sourceChoose[row],3,row);
                    }
                    _sourceTable.SetColumnSpan(_via4Preset,4);
                    _sourceTable.Controls.Add(_via4Preset,0,2);
                }
            }
            finally { _sourceTable.ResumeLayout(true); }
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
                RefreshBuildButtons();
                _status.Text = loaded[0].ReusedExistingTin && loaded[1].ReusedExistingTin
                    ? "Vỉa 4 không thay đổi: GIỮ NGUYÊN TIN hiện trạng, thiết kế và kết quả " +
                      "khối lượng đã duyệt. Có thể chuyển ngay sang LƯỚI Ô VUÔNG để tính."
                    : $"Vỉa 4 đã nạp • Hiện trạng: {loaded[0].Summary} • Thiết kế: {loaded[1].Summary}. " +
                      "Chỉ các TIN có dữ liệu thay đổi mới cần dựng lại.";
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

        private void RefreshBuildButtons()
        {
            var st=ProjectState.Current;
            _buildExisting.Enabled=_buildCts==null && st.Existing.Source!=null && st.Existing.Source.Entities.Count>0;
            _buildDesign.Enabled=_buildCts==null && st.Design.Source!=null && st.Design.Source.Entities.Count>0;
        }

        private async Task BuildSingleTinAsync(ModelRole role)
        {
            if(_buildCts!=null || !LicenseUiGuard.Check())return;
            var st=ProjectState.Current;
            var activeDoc=Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if(activeDoc==null)return;
            SurfaceBuildResult build=null;
            IDisposable operation=null;
            _buildCts=new CancellationTokenSource();
            var token=_buildCts.Token;
            try
            {
                operation=SurfaceWorkflowService.EnterInteractiveBuild();
                RefreshBuildButtons();_cancelBuild.Enabled=true;
                _sourceBox.Enabled=false;_types.Enabled=false;
                var totalWatch=Stopwatch.StartNew();
                SetBuildStatus($"Đang dựng TIN {st.Get(role).Name}...");
                build=await BuildRoleWithConflictChoiceAsync(role,token);
                token.ThrowIfCancellationRequested();
                if(!ReferenceEquals(activeDoc,Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument))
                    throw new InvalidOperationException("Đã đổi DWG; không ghi TIN sang bản vẽ khác.");
                SetBuildStatus($"Đang ghi TIN {st.Get(role).Name} xuống AutoCAD; Esc để hủy...");
                var cadWatch=Stopwatch.StartNew();
                SurfaceWorkflowService.DrawTin(role,build);
                cadWatch.Stop();totalWatch.Stop();
                SetBuildStatus($"{st.Get(role).Name}: {build.TriangleCount:n0} tam giác. Ghi CAD {cadWatch.Elapsed.TotalSeconds:0.00}s; tổng {totalWatch.Elapsed.TotalSeconds:0.00}s.");
                int duplicateGroups = build.DuplicateXYGroupCount;
                int duplicateVertices = build.DuplicateXYSourceVertices;
                int sameXYZ = build.SameXYZDuplicateVertices;
                string review = duplicateGroups > 0 || sameXYZ > 0
                    ? $"\r\n{duplicateGroups:n0} nhóm XY nhiều Z / {duplicateVertices:n0} đỉnh nguồn; {sameXYZ:n0} đỉnh trùng XYZ. " +
                      "Không cần loại từng điểm thủ công. Đề nghị kiểm tra hệ thống điểm và đường đồng mức; " +
                      "xem/chỉnh mặt cắt rồi bấm CẬP NHẬT MẶT CẮT trước khi chốt khối lượng."
                    : string.Empty;
                MessageBox.Show(build.Summary + review + "\r\n\r\nTIN còn lại được giữ nguyên.",
                    "IMSAT VOLUME — TIN " + st.Get(role).Name,MessageBoxButtons.OK,
                    build.WarningCount>0?MessageBoxIcon.Warning:MessageBoxIcon.Information);
            }
            catch(OperationCanceledException) { _status.Text="Đã hủy. Giữ TIN trước thao tác và dữ liệu CAD gốc."; }
            catch(DuplicateXYConflictException ex) {
                MessageBox.Show("Không thể dựng topology TIN an toàn. Không yêu cầu loại từng điểm trùng thủ công.\r\n"+ex.Message,
                    "Kiểm tra topology nguồn",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
            catch(Exception ex) { MessageBox.Show(ex.Message,"Không tạo được TIN",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
            finally
            {
                if(build!=null && !ReferenceEquals(build.Tin,st.Get(role).Tin))
                    (build.Tin.Triangles as IDisposable)?.Dispose();
                operation?.Dispose();
                _buildCts.Dispose();_buildCts=null;
                _cancelBuild.Enabled=false;_sourceBox.Enabled=true;_types.Enabled=true;
                RefreshBuildButtons();
            }
        }

        private async Task<SurfaceBuildResult> BuildRoleWithConflictChoiceAsync(
            ModelRole role,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            // Duplicate XY with differing Z is an automatic 3D vertical-face
            // input, not a condition to interrupt work for manual deletions.
            // A true unsupported crossing still propagates as a validation
            // exception; never create plausible but incorrect CAD geometry.
            return await Task.Run(() => SurfaceWorkflowService.BuildCoreDetailed(
                role, message => SetBuildStatus(message), token), token);
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
                bool sameRegion = state.HasTinRegion &&
                    string.Equals(state.TinRegionHandle, handle, StringComparison.OrdinalIgnoreCase) &&
                    state.TinRegionPolygon.Count == polygon.Count;
                if (sameRegion)
                    for (int i = 0; i < polygon.Count; i++)
                        if (Math.Abs(state.TinRegionPolygon[i].X - polygon[i].X) > 1e-7 ||
                            Math.Abs(state.TinRegionPolygon[i].Y - polygon[i].Y) > 1e-7)
                        {
                            sameRegion = false;
                            break;
                        }
                if (sameRegion)
                {
                    RefreshTinRegionStatus();
                    _status.Text = "Vùng tạo TIN không thay đổi. GIỮ NGUYÊN hai TIN hiện có; " +
                        "tiếp tục tính mặt cắt hoặc lưới ô vuông mà không phải dựng lại.";
                    return;
                }
                state.TinRegionHandle = handle;
                state.TinRegionPolygon.Clear();
                state.TinRegionPolygon.AddRange(polygon);
                state.MarkTinRegionChanged();

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
            // No-op when there is no selected TIN clip. Previously merely
            // pressing "Bỏ vùng" incremented TinRegionRevision, instantly
            // making both unchanged, valid TINs appear stale to GridPage.
            if (!hadRegion && string.IsNullOrWhiteSpace(state.TinRegionHandle))
            {
                RefreshTinRegionStatus();
                _status.Text = "Chưa giới hạn vùng TIN; hai mô hình hiện có được giữ nguyên.";
                return;
            }
            state.TinRegionHandle = null;
            state.TinRegionPolygon.Clear();
            if (hadRegion) state.MarkTinRegionChanged();

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
                    (result.ReusedExistingTin
                        ? "TIN đã dựng được giữ nguyên; không cần tạo lại để tính lưới ô vuông."
                        : $"Nguồn có thể nằm trên nhiều layer. Đã chuẩn bị layer TIN: {result.OutputTinLayer}.");
                RefreshBuildButtons();
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
                    (r.ReusedExistingTin
                        ? "Dữ liệu không đổi → GIỮ NGUYÊN TIN đã dựng, dùng chung cho Mặt cắt/Lưới ô vuông."
                        : $"Đã chuẩn bị layer TIN: {r.OutputTinLayer}.");
                RefreshBuildButtons();
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
