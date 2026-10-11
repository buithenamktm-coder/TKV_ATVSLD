using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
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
        private readonly Button _buildBoth;
        private readonly Button _toggleTin;
        private readonly Button _view3D;
        private readonly Button _meanZBuild;
        private readonly Button _meanZCancel;
        private CancellationTokenSource _meanZCts;
        private readonly List<RowRef> _rows = new List<RowRef>();
        private bool _busy;

        public ModelPage()
        {
            Font = new Font("Arial", 9F);
            BackColor = Color.White;
            Controls.Add(new Label { Text = "MÔ HÌNH TIN HIỆN TRẠNG / THIẾT KẾ", Dock = DockStyle.Top, Height = 32, Font = new Font("Arial", 11F, FontStyle.Bold) });

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, WrapContents = false, Padding = new Padding(0, 2, 0, 2) };
            top.Controls.Add(new Label { Text = "Mô hình:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
            _model = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            _model.Items.AddRange(new object[] { "Hiện trạng", "Thiết kế" });
            _model.SelectedIndex = ProjectState.Current.ActiveRole == ModelRole.Existing ? 0 : 1;
            _model.SelectedIndexChanged += (s, e) =>
            {
                ProjectState.Current.ActiveRole = CurrentRole;
                RebuildRows();
            };
            top.Controls.Add(_model);
            top.Controls.Add(Button("Loại điểm", (s, e) => DisableSelectedVertices()));
            top.Controls.Add(Button("Loại đối tượng", (s, e) => DisableSelectedEntities()));
            top.Controls.Add(Button("Khôi phục", (s, e) => RestoreSelected()));
            top.Controls.Add(Button("Khôi phục tất cả", (s, e) => RestoreAll()));
            top.Controls.Add(Button("Sửa Z", (s, e) => EditZ()));
            top.Controls.Add(Button("Zoom tới", (s, e) => ZoomSelected()));
            Controls.Add(top);
            top.BringToFront();

            var second = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 122,
                WrapContents = true,
                Padding = new Padding(0, 2, 0, 2),
                AutoScroll = false
            };
            _build = Button("TẠO / CẬP NHẬT TIN", async (s, e) => await BuildTinAsync());
            _build.Width = 220;
            _buildBoth = Button("TẠO / CẬP NHẬT CẢ HAI TIN", async (s, e) => await BuildBothTinAsync());
            _buildBoth.Width = 205;
            _toggleTin = Button("Ẩn TIN", (s, e) => ToggleTin());
            _toggleTin.Width = 90;
            _view3D = Button("VIEW TIN 3D", (s, e) => ViewTin3D());
            _view3D.Width = 115;
            second.Controls.Add(_build);
            second.Controls.Add(_buildBoth);
            second.Controls.Add(_toggleTin);
            second.Controls.Add(_view3D);
            _meanZBuild = Button("TIN Z TRUNG BÌNH – THỬ", async (s,e) => await BuildMeanZPreviewAsync());
            _meanZBuild.Width = 224;
            _meanZCancel = Button("HỦY TIN THỬ", (s,e) => _meanZCts?.Cancel());
            _meanZCancel.Width = 118;
            _meanZCancel.Enabled = false;
            second.Controls.Add(_meanZBuild);
            second.Controls.Add(_meanZCancel);
            _progress = new ProgressBar { Width = 150, Height = 24, Style = ProgressBarStyle.Marquee, Visible = false, MarqueeAnimationSpeed = 25 };
            second.Controls.Add(_progress);
            Controls.Add(second);
            second.BringToFront();

            _info = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                Text = "Chưa có dữ liệu.",
                ForeColor = Color.DimGray,
                AutoEllipsis = true
            };
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
            if (disposing)
            {
                _meanZCts?.Cancel();
                ProjectState.Current.Changed -= StateChanged;
            }
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
            _build.Text = CurrentRole == ModelRole.Existing
                ? "TẠO / CẬP NHẬT TIN HIỆN TRẠNG"
                : "TẠO / CẬP NHẬT TIN THIẾT KẾ";

            _info.Text =
                $"{s.Name} • nguồn: {s.Layer ?? "-"} • {s.EntityCount:n0} đối tượng • {s.ActiveVertexCount:n0}/{s.VertexCount:n0} đỉnh đang dùng" +
                Environment.NewLine +
                SurfaceWorkflowService.TinStatusText(ModelRole.Existing) + "   |   " +
                SurfaceWorkflowService.TinStatusText(ModelRole.Design);

            _toggleTin.Text = s.TinVisible ? "Ẩn TIN" : "Hiện TIN";
            _toggleTin.Enabled = s.Tin != null && !_busy;
            _view3D.Enabled = s.IsTinCurrent && !_busy;
            _build.Enabled = !_busy && s.Source != null && s.Source.Entities.Count > 0;
            _meanZBuild.Enabled = !_busy && s.Source != null && s.Source.Entities.Count > 0;
            _meanZCancel.Enabled = _busy && _meanZCts != null;
            _buildBoth.Enabled = !_busy &&
                ProjectState.Current.Existing.Source != null && ProjectState.Current.Existing.Source.Entities.Count > 0 &&
                ProjectState.Current.Design.Source != null && ProjectState.Current.Design.Source.Entities.Count > 0;
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
            var rows = SelectedRows().ToList();
            if (rows.Count == 0) return;
            var source=CurrentSession.Source;long revision=source.Revision;
            if(!ModelEditConfirmation.Show(this,"Xác nhận loại điểm",
                "Bạn có chắc chắn muốn loại điểm này khỏi mô hình không?"+
                "\r\nSố điểm chọn: "+rows.Count+"\r\n"+
                string.Join("\r\n",rows.Take(8).Select(r=>"Handle "+r.Entity.Handle+" / đỉnh "+r.Vertex.Index+
                    " / Z "+r.Vertex.Position.Z))+"\r\nChỉ loại khỏi mô hình; CAD gốc giữ nguyên."))return;
            if(!ReferenceEquals(source,CurrentSession.Source)||source.Revision!=revision)return;
            foreach (var r in rows) CurrentSession.Source.SetVertexEnabled(r.Entity.Id, r.Vertex.Index, false);
            SurfaceWorkflowService.InvalidateTin(CurrentRole, clearCadLayer: true);
        }

        private void DisableSelectedEntities()
        {
            var ids = SelectedRows().Select(r => r.Entity.Id).Distinct().ToList();
            if (ids.Count == 0) return;
            foreach (var id in ids) CurrentSession.Source.RemoveEntityFromModel(id);
            SurfaceWorkflowService.InvalidateTin(CurrentRole, clearCadLayer: true);
        }

        private void RestoreSelected()
        {
            foreach(var row in SelectedRows().ToList()) {
                CurrentSession.Source.RestoreEntity(row.Entity.Id);
                CurrentSession.Source.RestoreVertex(row.Entity.Id,row.Vertex.Index);
            }
            SurfaceWorkflowService.InvalidateTin(CurrentRole, clearCadLayer: true);
        }

        private void RestoreAll()
        {
            if (CurrentSession.Source == null) return;
            foreach (var entity in CurrentSession.Source.Entities)
            {
                entity.SetEnabled(true);
            }
            CurrentSession.Source.RestoreVertices(CurrentSession.Source.Entities.SelectMany(e=>e.Vertices
                .Where(v=>!v.IsEnabled||v.Position.Z!=v.Original.Z)
                .Select(v=>new KeyValuePair<string,int>(e.Id,v.Index))));
            CurrentSession.Source.Touch();
            SurfaceWorkflowService.InvalidateTin(CurrentRole, clearCadLayer: true);
        }

        private void EditZ()
        {
            var row = SelectedRows().FirstOrDefault();
            if (row == null) { MessageBox.Show("Chọn một đỉnh cần sửa cao độ.", "Mining Volume"); return; }
            var source=CurrentSession.Source;var role=CurrentRole;long revision=source.Revision;
            using (var form = new NumericInputDialog("Sửa cao độ Z", "Cao độ Z mới:", row.Vertex.Position.Z))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                if(!ModelEditConfirmation.Show(this,"Xác nhận sửa Z",
                    "Handle "+row.Entity.Handle+" / đỉnh "+row.Vertex.Index+"\r\nZ cũ "+row.Vertex.Position.Z+
                    " → Z mới "+form.Value+"\r\nChỉ sửa mô hình; CAD gốc giữ nguyên."))return;
                if(CurrentRole!=role || !ReferenceEquals(source,CurrentSession.Source) || source.Revision!=revision)return;
                source.OverrideVertexZ(row.Entity.Id, row.Vertex.Index, form.Value);
                SurfaceWorkflowService.InvalidateTin(CurrentRole, clearCadLayer: true);
            }
        }

        private void ZoomSelected()
        {
            var row = SelectedRows().FirstOrDefault();
            if (row != null) CadViewService.ZoomToHandle(row.Entity.Handle);
        }

        private async Task BuildTinAsync()
        {
            if (_busy || !LicenseUiGuard.Check()) return;
            var role = CurrentRole;
            try
            {
                SetBusy(true, $"Đang kiểm tra dữ liệu và dựng TIN {ProjectState.Current.Get(role).Name}...");
                SurfaceBuildResult build;
                try { build = await Task.Run(() => SurfaceWorkflowService.BuildCoreDetailed(role)); }
                catch (DuplicateXYConflictException ex) { DuplicateXYConflictUi.Show(role, ex.Report); return; }
                SurfaceWorkflowService.DrawTin(role, build);
                MessageBox.Show(
                    $"TIN {ProjectState.Current.Get(role).Name} đã tạo thành công.\r\n\r\n" +
                    $"Layer TIN: {ProjectState.Current.Get(role).TinLayer}\r\n" +
                    build.Summary,
                    "IMSAT VOLUME - TIN",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (SurfaceValidationException ex)
            {
                ShowValidation(ex.ModelName, ex.Issues);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không tạo được TIN", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { SetBusy(false, null); }
        }

        private async Task BuildBothTinAsync()
        {
            if (_busy || !LicenseUiGuard.Check()) return;
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

            try
            {
                SetBusy(true, "Đang dựng đồng thời cặp TIN hiện trạng / thiết kế...");
                SurfaceBuildResult[] builds;
                try { builds = await Task.Run(() => SurfaceWorkflowService.BuildPairCoreDetailed()); }
                catch (DuplicateXYConflictException ex) { DuplicateXYConflictUi.Ask(this, ex); return; }

                SurfaceWorkflowService.DrawTinPair(builds);
                MessageBox.Show(
                    "ĐÃ TẠO ĐỦ 2 TIN DÙNG CHO TÍNH KHỐI LƯỢNG\r\n\r\n" +
                    $"Hiện trạng → {st.Existing.TinLayer}: {builds[0].Summary}\r\n" +
                    $"Thiết kế → {st.Design.TinLayer}: {builds[1].Summary}",
                    "IMSAT VOLUME - Cặp TIN hợp lệ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (SurfaceValidationException ex)
            {
                ShowValidation(ex.ModelName, ex.Issues);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Không tạo được cặp TIN", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { SetBusy(false, null); }
        }

        /// <summary>
        /// Experimental mean-Z TIN does not alter either verified TIN or any
        /// official section/volume state. Only writes a TEST_ONLY preview layer.
        /// </summary>
        private async Task BuildMeanZPreviewAsync()
        {
            if (_busy || !LicenseUiGuard.Check()) return;
            var role=CurrentRole;
            var startingDocument=Autodesk.AutoCAD.ApplicationServices.Application
                .DocumentManager.MdiActiveDocument;
            if(startingDocument==null)
            {
                MessageBox.Show(this,"Chưa mở bản vẽ AutoCAD.","IMSAT VOLUME",
                    MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(this,
                "TIN THỬ NGHIỆM – TRUNG BÌNH Z\\r\\n\\r\\n"+
                "Gộp tất cả đỉnh trùng XY từ đối tượng đang dùng bằng Z trung bình. "+
                "Đường breakline KHÔNG được giữ như cạnh bắt buộc. "+
                "Chỉ vẽ trên layer riêng TEST_ONLY, không thay TIN hiện trạng/thiết kế chính thức.\\r\\n\\r\\n"+
                "KẾT QUẢ KHÔNG DÙNG TÍNH KHỐI LƯỢNG. Tiếp tục?",
                "IMSAT VOLUME – TIN thử nghiệm",
                MessageBoxButtons.YesNo,MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2)!=DialogResult.Yes) return;

            _meanZCts=new CancellationTokenSource();
            SetBusy(true,"Đang chuẩn hóa trùng XY và dựng TIN thử nghiệm...");
            MeanZPreviewWorkflowService.PreviewResult result=null;
            try
            {
                var token=_meanZCts.Token;
                Action<string> progress = m => {
                    if(IsDisposed || !IsHandleCreated)return;
                    try { BeginInvoke(new Action(()=>{
                        if(!IsDisposed) _info.Text="TIN Z trung bình THỬ: "+m;
                    })); } catch(InvalidOperationException){}
                };
                result=await Task.Run(() =>
                    MeanZPreviewWorkflowService.Build(role, progress, token), token);
                token.ThrowIfCancellationRequested();
                if (Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                    != startingDocument)
                    throw new InvalidOperationException(
                        "Bản vẽ CAD đã chuyển trong khi dựng TIN thử nghiệm; không ghi vào bản vẽ khác.");
                int count=MeanZPreviewWorkflowService.Draw(result);
                string info=
                    "Đã tạo TIN THỬ NGHIỆM trên layer: "+result.Layer+
                    "\\r\\nĐỉnh nguồn: "+result.Analysis.OriginalVertexCount.ToString("n0")+
                    "; đỉnh sau gộp: "+result.Analysis.Prepared.Sites.Count.ToString("n0")+
                    "\\r\\nNhóm XY trùng: "+result.Analysis.DuplicateGroupCount.ToString("n0")+
                    "; nhóm khác Z: "+result.Analysis.Conflicts.Count.ToString("n0")+
                    "\\r\\nBreakline KHÔNG ràng buộc: "+result.Analysis.IgnoredBreaklineSegments.ToString("n0")+
                    "\\r\\nSố tam giác toàn bộ: "+result.TotalTriangles.ToString("n0")+
                    "; số mặt CAD minh họa: "+count.ToString("n0")+
                    (result.DrawIsSampled?" (đã lấy mẫu hiển thị để không treo CAD)":"")+
                    "\\r\\n\\r\\nKHÔNG DÙNG LƯỚI NÀY ĐỂ TÍNH KHỐI LƯỢNG."+
                    "\\r\\nCó xuất báo cáo chi tiết các vị trí Z đã gộp sang CSV không?";
                if(MessageBox.Show(this,info,"TIN Z TB – chỉ so sánh HSMo",
                    MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)
                {
                    using(var dlg=new SaveFileDialog
                    {
                        Filter="CSV phân tích trùng XY (*.csv)|*.csv",
                        DefaultExt="csv",AddExtension=true,OverwritePrompt=true,
                        FileName="IMSAT_TIN_Z_TB_"+(role==ModelRole.Existing?"HT":"TK")+
                            "_THU_"+DateTime.Now.ToString("yyyyMMdd_HHmm")+".csv"
                    })
                        if(dlg.ShowDialog(this)==DialogResult.OK)
                            MeanZPreviewWorkflowService.ExportCsv(result,dlg.FileName);
                }
            }
            catch(MeanZReviewRequiredException ex)
            {
                // A >3 m conflict is a REVIEW result, not a triangulation crash.
                // Open the grouped source-vertex editor on the same drawing.
                // Its own asynchronous rescan verifies current model revision
                // before enabling any Edit-Z or Exclude-Vertex action.
                if (!IsDisposed &&
                    Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument
                        == startingDocument)
                {
                    Autodesk.AutoCAD.ApplicationServices.Application.ShowModelessDialog(
                        new MeanZConflictReviewForm(role, ex.Analysis));
                }
                else if(!IsDisposed)
                {
                    MessageBox.Show(this,
                        "Bản vẽ CAD đã chuyển khi phát hiện chênh Z > 3 m. " +
                        "Hãy trở lại đúng DWG và quét lại dữ liệu.",
                        "TIN thử nghiệm – cần kiểm tra cao độ",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch(OperationCanceledException)
            {
                MessageBox.Show(this,"Đã hủy dựng TIN Z trung bình. Không thay TIN chính thức.",
                    "IMSAT VOLUME",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
            catch(Exception ex)
            {
                MessageBox.Show(this,ex.Message,"Không dựng được TIN Z trung bình THỬ",
                    MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
            finally
            {
                result?.Dispose();
                _meanZCts?.Dispose();_meanZCts=null;
                if (!IsDisposed) SetBusy(false,null);
            }
        }

        private void ShowValidation(string modelName, IReadOnlyList<ValidationIssue> issues)
        {
            int errors = issues.Count(x => x.Severity == ValidationSeverity.Error);
            int warnings = issues.Count(x => x.Severity == ValidationSeverity.Warning);
            var lines = issues.Take(30).Select(x => $"[{x.Severity}] {x.Code}: {x.Message}");
            string suffix = issues.Count > 30 ? $"\r\n... còn {issues.Count - 30:n0} cảnh báo/lỗi khác." : string.Empty;
            MessageBox.Show(
                $"Không dựng TIN {modelName}. Có {errors:n0} lỗi, {warnings:n0} cảnh báo.\r\n\r\n" +
                string.Join("\r\n", lines) + suffix,
                $"Kiểm tra dữ liệu {modelName}",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void ViewTin3D()
        {
            if (_busy) return;
            try
            {
                Tin3DViewService.Start(CurrentRole);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Xem TIN 3D", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
            _build.Enabled = !busy && CurrentSession.Source != null && CurrentSession.Source.Entities.Count > 0;
            _buildBoth.Enabled = !busy &&
                ProjectState.Current.Existing.Source != null && ProjectState.Current.Existing.Source.Entities.Count > 0 &&
                ProjectState.Current.Design.Source != null && ProjectState.Current.Design.Source.Entities.Count > 0;
            _toggleTin.Enabled = !busy && CurrentSession.Tin != null;
            _meanZBuild.Enabled = !busy && CurrentSession.Source != null && CurrentSession.Source.Entities.Count > 0;
            _meanZCancel.Enabled = busy && _meanZCts != null;
            _view3D.Enabled = !busy && CurrentSession.IsTinCurrent;
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