using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Grid;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    /// <summary>Phương pháp lưới ô vuông, hoạt động độc lập với hệ mặt cắt.</summary>
    public sealed class GridPage : UserControl
    {
        private readonly Label _boundary;
        private readonly NumericUpDown _sizeX, _sizeY, _angle;
        private readonly Button _calculate, _cancel, _draw, _excel;
        private readonly CheckBox _labels;
        private readonly Label _status;
        private readonly Label _tinReuseStatus;
        private readonly DataGridView _cells;
        private bool _syncing;
        private bool _exportingExcel;
        private CancellationTokenSource _cts;
        private readonly TableLayoutPanel _rootLayout;
        private readonly TableLayoutPanel _settingsGrid;
        private readonly FlowLayoutPanel _actionPanel;
        private readonly Label _scopeCaption, _xCaption, _yCaption, _angleCaption;
        private readonly Button _pickBoundary;
        private bool _compactGrid;

        public GridPage()
        {
            Font = new Font("Arial", 9F);
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var root = _rootLayout = new TableLayoutPanel
            {
                Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,
                Padding=Padding.Empty,Margin=Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,46)); // shared HT/TK TIN status
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,80));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,83));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,56));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            Controls.Add(root);
            root.Controls.Add(new Label
            {
                Text="TÍNH KHỐI LƯỢNG THEO LƯỚI Ô VUÔNG",Dock=DockStyle.Fill,
                Font=new Font("Arial",11,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft
            },0,0);

            var settings=_settingsGrid=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=6,RowCount=2,Padding=new Padding(2)};
            for(int c=0;c<6;c++) settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F/6F));
            settings.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            settings.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            _boundary=new Label{Text="Chưa chọn đường bao",Dock=DockStyle.Fill,
                TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
            _scopeCaption=new Label{Text="Phạm vi",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};
            settings.Controls.Add(_scopeCaption,0,0);
            settings.Controls.Add(_boundary,1,0);
            settings.SetColumnSpan(_boundary,3);
            var pick=_pickBoundary=Btn("Chọn trên CAD",(s,e)=>ChooseBoundary());
            settings.Controls.Add(pick,4,0);settings.SetColumnSpan(pick,2);
            _sizeX=Num(20,0.1M,100000);
            _sizeY=Num(20,0.1M,100000);
            _angle=Num(0,-180,180);
            _xCaption=new Label{Text="Ô X (m)",Dock=DockStyle.Fill};
            settings.Controls.Add(_xCaption,0,1);
            settings.Controls.Add(_sizeX,1,1);
            _yCaption=new Label{Text="Ô Y (m)",Dock=DockStyle.Fill};
            settings.Controls.Add(_yCaption,2,1);
            settings.Controls.Add(_sizeY,3,1);
            _angleCaption=new Label{Text="Góc xoay",Dock=DockStyle.Fill};
            settings.Controls.Add(_angleCaption,4,1);
            settings.Controls.Add(_angle,5,1);
            _tinReuseStatus=new Label
            {
                Dock=DockStyle.Fill,
                AutoEllipsis=true,
                BackColor=UiTheme.Soft,
                ForeColor=UiTheme.TextStrong,
                Font=new Font("Arial",9.5F,FontStyle.Bold),
                Padding=new Padding(12,6,8,6),
                TextAlign=ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_tinReuseStatus,0,1);
            root.Controls.Add(settings,0,2);

            var actions=_actionPanel=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=true,Padding=new Padding(4,3,4,3)};
            _calculate=Btn("Tính khối lượng",async(s,e)=>await CalculateAsync());
            _cancel=Btn("Hủy tính",(s,e)=>_cts?.Cancel());
            _draw=Btn("Vẽ lưới CAD",(s,e)=>DrawCad());
            _excel=Btn("Xuất Excel",async(s,e)=>await ExportExcelAsync());
            _labels=new CheckBox{Text="Nhãn thể tích trên ô",Checked=false,
                AutoSize=true,Margin=new Padding(10,9,4,4)};
            actions.Controls.Add(_calculate);actions.Controls.Add(_cancel);
            actions.Controls.Add(_draw);actions.Controls.Add(_excel);actions.Controls.Add(_labels);
            root.Controls.Add(actions,0,3);

            _status=new Label{Dock=DockStyle.Fill,AutoEllipsis=true,
                Text="Chọn đường bao, nhập kích thước ô lưới rồi tính.",ForeColor=Color.DimGray,
                TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(6)};
            root.Controls.Add(_status,0,4);

            _cells=new DataGridView
            {
                Dock=DockStyle.Fill,ReadOnly=true,VirtualMode=true,
                RowHeadersVisible=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
                AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,
                SelectionMode=DataGridViewSelectionMode.FullRowSelect
            };
            foreach(var col in new[]{"Mã ô","I","J","S ô (m²)","S thiếu (m²)",
                "V đào (m³)","V đắp (m³)","V thuần (m³)","Trạng thái"})
                _cells.Columns.Add(col,col);
            _cells.CellValueNeeded+=CellValueNeeded;
            root.Controls.Add(_cells,0,5);

            _sizeX.ValueChanged+=ParametersChanged;
            _sizeY.ValueChanged+=ParametersChanged;
            _angle.ValueChanged+=ParametersChanged;
            ProjectState.Current.Changed+=ProjectChanged;
            Restore();
            SizeChanged += (sender,args) => ApplyResponsiveGridLayout();
            ApplyResponsiveGridLayout();
        }

        private void ApplyResponsiveGridLayout()
        {
            bool compact = ClientSize.Width < 560;
            if (_compactGrid == compact) return;
            _compactGrid = compact;
            _settingsGrid.SuspendLayout();
            try
            {
                _settingsGrid.Controls.Clear();
                _settingsGrid.RowStyles.Clear();
                _settingsGrid.ColumnStyles.Clear();
                _settingsGrid.ColumnCount = compact ? 2 : 6;
                _settingsGrid.RowCount = compact ? 5 : 2;
                if (compact)
                {
                    _settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
                    _settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    for (int k=0; k<5; k++)
                        _settingsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
                    _settingsGrid.Controls.Add(_scopeCaption,0,0);
                    _settingsGrid.SetColumnSpan(_boundary,1);
                    _settingsGrid.Controls.Add(_boundary,1,0);
                    _pickBoundary.Dock=DockStyle.Fill;
                    _settingsGrid.Controls.Add(_pickBoundary,0,1);
                    _settingsGrid.SetColumnSpan(_pickBoundary,2);
                    _settingsGrid.Controls.Add(_xCaption,0,2); _settingsGrid.Controls.Add(_sizeX,1,2);
                    _settingsGrid.Controls.Add(_yCaption,0,3); _settingsGrid.Controls.Add(_sizeY,1,3);
                    _settingsGrid.Controls.Add(_angleCaption,0,4); _settingsGrid.Controls.Add(_angle,1,4);
                }
                else
                {
                    for(int c=0;c<6;c++) _settingsGrid.ColumnStyles.Add(
                        new ColumnStyle(SizeType.Percent,100F/6F));
                    _settingsGrid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
                    _settingsGrid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
                    _settingsGrid.Controls.Add(_scopeCaption,0,0);
                    _settingsGrid.Controls.Add(_boundary,1,0);
                    _settingsGrid.SetColumnSpan(_boundary,3);
                    _settingsGrid.Controls.Add(_pickBoundary,4,0);
                    _settingsGrid.SetColumnSpan(_pickBoundary,2);
                    _settingsGrid.Controls.Add(_xCaption,0,1); _settingsGrid.Controls.Add(_sizeX,1,1);
                    _settingsGrid.Controls.Add(_yCaption,2,1); _settingsGrid.Controls.Add(_sizeY,3,1);
                    _settingsGrid.Controls.Add(_angleCaption,4,1); _settingsGrid.Controls.Add(_angle,5,1);
                }
                _rootLayout.RowStyles[2].Height = compact ? 184F : 80F;
                _rootLayout.RowStyles[3].Height = compact ? 148F : 83F;
            }
            finally { _settingsGrid.ResumeLayout(true); }
        }

        private static Button Btn(string text,EventHandler e)
        {
            var b=new Button{Text=text,AutoSize=true,Height=30,Margin=new Padding(2)};
            b.Click+=e; return b;
        }

        private static NumericUpDown Num(decimal value,decimal min,decimal max)
        {
            var n=new NumericUpDown{Minimum=min,Maximum=max,DecimalPlaces=2,Dock=DockStyle.Fill};
            n.Value=value;return n;
        }

        private void ProjectChanged(object sender,EventArgs e)
        {
            if(!IsHandleCreated)return;
            if(InvokeRequired)BeginInvoke(new Action(RefreshCells)); else RefreshCells();
        }

        private void Restore()
        {
            _syncing=true;
            var st=ProjectState.Current;
            _sizeX.Value=Math.Max(_sizeX.Minimum,Math.Min(_sizeX.Maximum,(decimal)st.GridSizeX));
            _sizeY.Value=Math.Max(_sizeY.Minimum,Math.Min(_sizeY.Maximum,(decimal)st.GridSizeY));
            _angle.Value=Math.Max(_angle.Minimum,Math.Min(_angle.Maximum,(decimal)st.GridRotationDegrees));
            _boundary.Text=string.IsNullOrWhiteSpace(st.GridBoundaryHandle)
                ?"Chưa chọn đường bao":"Handle: "+st.GridBoundaryHandle;
            _syncing=false;
            RefreshCells();
        }

        private static string TinReuseMessage()
        {
            var state=ProjectState.Current;
            bool existing=state.Existing.IsTinCurrent;
            bool design=state.Design.IsTinCurrent;
            if(existing && design)
                return "ĐÃ CÓ TIN HIỆN TRẠNG (" +
                    state.Existing.Tin.Triangles.Count.ToString("N0") +
                    ") + THIẾT KẾ (" + state.Design.Tin.Triangles.Count.ToString("N0") +
                    ") → Lưới ô vuông dùng lại trực tiếp; KHÔNG cần dựng TIN mới.";
            string missing = !existing && !design
                ? "hiện trạng và thiết kế"
                : !existing ? "hiện trạng" : "thiết kế";
            return "TIN " + missing + " chưa sẵn sàng hoặc đã thay đổi. " +
                "Chỉ tạo/cập nhật mô hình còn thiếu; TIN hợp lệ còn lại được giữ nguyên.";
        }

        private void ParametersChanged(object sender,EventArgs e)
        {
            if(_syncing)return;
            var st=ProjectState.Current;
            st.GridSizeX=(double)_sizeX.Value;
            st.GridSizeY=(double)_sizeY.Value;
            st.GridRotationDegrees=(double)_angle.Value;
            GridWorkflowService.Invalidate();
            RefreshCells();
        }

        private void ChooseBoundary()
        {
            var handle=SelectionService.PickClosedBoundary();
            if(handle==null)return;
            try
            {
                var polygon=BoundaryGeometryService.ReadBoundary(handle);
                if(polygon.Count<3)throw new InvalidOperationException("Đường bao không đủ đỉnh.");
                ProjectState.Current.GridBoundaryHandle=handle;
                _boundary.Text="Handle: "+handle;
                GridWorkflowService.Invalidate();
                RefreshCells();
            }
            catch(Exception ex){Error(ex,"Đường bao lưới");}
        }

        private async Task CalculateAsync()
        {
            if(_cts!=null || !LicenseUiGuard.Check())return;
            var st=ProjectState.Current;
            try
            {
                if(string.IsNullOrWhiteSpace(st.GridBoundaryHandle))
                    throw new InvalidOperationException("Hãy chọn đường bao ô lưới trên bình đồ CAD.");
                var activeDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (activeDoc == null)
                    throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
                string boundaryHandle = st.GridBoundaryHandle;
                var boundary=BoundaryGeometryService.ReadBoundary(boundaryHandle).ToArray();
                SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers:false);
                var options=new GridVolumeOptions
                {
                    Boundary=boundary,
                    Origin=boundary[0],
                    SizeX=(double)_sizeX.Value,SizeY=(double)_sizeY.Value,
                    RotationDegrees=(double)_angle.Value,
                    MaxCells=100000
                };
                var existing=st.Existing.Tin;var design=st.Design.Tin;
                long rx=st.Existing.Source.Revision,ry=st.Design.Source.Revision;
                _cts=new CancellationTokenSource();
                _calculate.Enabled=false;_cancel.Enabled=true;UseWaitCursor=true;
                var progress=new Progress<string>(s=>_status.Text=s);
                _status.Text="Đang tính giao TIN và lưới ô vuông...";
                var cts=_cts;
                var watch = Stopwatch.StartNew();
                var result=await Task.Run(()=>new GridVolumeCalculator().Calculate(existing,design,
                    options,(done,total)=>((IProgress<string>)progress).Report(
                        "Đang tính ô "+done.ToString("n0")+"/"+total.ToString("n0")),
                    cts.Token));
                cts.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(activeDoc, Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument) ||
                    !string.Equals(st.GridBoundaryHandle, boundaryHandle, StringComparison.OrdinalIgnoreCase) ||
                    !ReferenceEquals(st.Existing.Tin, existing) ||
                    !ReferenceEquals(st.Design.Tin, design) ||
                    options.SizeX != st.GridSizeX || options.SizeY != st.GridSizeY ||
                    options.RotationDegrees != st.GridRotationDegrees)
                    throw new InvalidOperationException(
                        "DWG, TIN, đường bao hoặc kích thước ô đã thay đổi trong lúc tính. Không dùng khối lượng cũ.");
                var latestBoundary=BoundaryGeometryService.ReadBoundary(boundaryHandle);
                if (latestBoundary.Count != boundary.Length ||
                    boundary.Where((point,index) => index < latestBoundary.Count &&
                        (Math.Abs(point.X-latestBoundary[index].X)>1e-7 ||
                         Math.Abs(point.Y-latestBoundary[index].Y)>1e-7)).Any())
                    throw new InvalidOperationException(
                        "Đường bao CAD đã được sửa trong khi tính lưới. Hãy tính lại theo đường bao mới.");
                GridWorkflowService.Commit(result,options,existing,design,rx,ry,boundaryHandle);
                watch.Stop();
                st.GridCalculatedUtc = DateTime.UtcNow;
                st.GridCalculationSeconds = watch.Elapsed.TotalSeconds;
                _status.Text=result.IsComplete
                    ? "Đã tính "+result.Cells.Count.ToString("n0")+" ô. Đào "+
                        result.TotalCutVolume.ToString("n2")+" m³; đắp "+
                        result.TotalFillVolume.ToString("n2")+" m³."
                    : result.AmbiguousCells > 0
                        ? "Đã tính phần hợp lệ; "+result.AmbiguousCells.ToString("n0")+
                          " ô ("+result.AmbiguousArea.ToString("n2")+
                          " m²) gặp mặt đứng nhiều Z: CHƯA CÓ KHỐI LƯỢNG XÁC NHẬN. " +
                          "Không coi các ô này là thể tích bằng 0."
                        : "Có "+result.TotalNoDataArea.ToString("n2")+
                          " m² chưa phủ đủ: "+result.MissingExistingCells.ToString("n0")+
                          " ô thiếu HT; "+result.MissingDesignCells.ToString("n0")+
                          " ô thiếu TK; "+result.IntersectionMismatchCells.ToString("n0")+
                          " ô hai TIN đều phủ nhưng không giao đúng. Chưa nghiệm thu.";
                RefreshCells();
            }
            catch(OperationCanceledException){_status.Text="Đã hủy tính lưới theo yêu cầu.";}
            catch(Exception ex){_status.Text="Không tính được lưới.";Error(ex,"Tính lưới");}
            finally
            {
                _cts?.Dispose();_cts=null;UseWaitCursor=false;
                _calculate.Enabled=true;_cancel.Enabled=false;
            }
        }

        private void DrawCad()
        {
            if (!LicenseUiGuard.Check()) return;
            try
            {
                if(!GridWorkflowService.IsCurrent)throw new InvalidOperationException(
                    "Lưới đã cũ hoặc chưa tính. Hãy tính lại trước khi vẽ lên CAD.");
                var doc=Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument ??
                    throw new InvalidOperationException("Không có bản vẽ AutoCAD.");
                GridWorkflowService.EnsureBoundaryCurrent(
                    BoundaryGeometryService.ReadBoundary(ProjectState.Current.GridBoundaryHandle));
                var grid = GridWorkflowService.Report;
                _draw.Enabled=false;
                UseWaitCursor=true;
                _status.Text="Đang vẽ "+grid.Cells.Count.ToString("n0")+" ô lưới lên CAD — Esc để hủy.";
                _status.Refresh();
                using(doc.LockDocument())
                using(var cadProgress = new SectionCadProgress(grid.Cells.Count, "xuất lưới ô vuông"))
                {
                    if(!ReferenceEquals(doc,Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument))
                        throw new InvalidOperationException("Đã chuyển DWG trước khi vẽ lưới.");
                    GridCadRenderer.Draw(doc.Database,grid,_labels.Checked,
                        (done,total) =>
                        {
                            cadProgress.Report(done);
                            _status.Text="Đang vẽ lưới CAD: "+done.ToString("n0")+
                                "/"+total.ToString("n0")+" ô — Esc để hủy.";
                            _status.Refresh(); // repaint only; never pump nested CAD events
                        }, pollCancellation: cadProgress.Poll);
                }
                doc.Editor.Regen();
                _status.Text="Đã vẽ "+grid.Cells.Count.ToString("n0")+
                    " ô lưới CAD trên layer riêng; giữ nguyên lưới trước.";
            }
            catch(OperationCanceledException)
            {
                _status.Text="Đã hủy xuất lưới CAD; lần xuất này đã được hoàn tác.";
            }
            catch(Exception ex){Error(ex,"Vẽ lưới CAD");}
            finally { UseWaitCursor=false; _draw.Enabled=GridWorkflowService.Report!=null; }
        }

        private async Task ExportExcelAsync()
        {
            if (_exportingExcel || !LicenseUiGuard.Check()) return;
            _exportingExcel=true;
            try
            {
                if(!GridWorkflowService.IsCurrent)throw new InvalidOperationException(
                    "Kết quả lưới chưa có hoặc đã cũ, hãy tính lại.");
                using(var dlg=new SaveFileDialog
                {
                    Filter="Excel Workbook (*.xlsx)|*.xlsx",DefaultExt="xlsx",
                    FileName="IMSAT_LuoiOVuong_"+DateTime.Now.ToString("yyyyMMdd_HHmm")+".xlsx",
                    OverwritePrompt=true
                })
                {
                    if(dlg.ShowDialog(this)!=DialogResult.OK)return;
                    // A partial grid may still be useful for field review,
                    // but never let an operator mistake its totals for complete
                    // approved excavation quantities.
                    var currentGrid = GridWorkflowService.Report;
                    if (currentGrid == null)
                        throw new InvalidOperationException("Không còn kết quả lưới hiện hành.");
                    if (!currentGrid.IsComplete)
                    {
                        var confirm = MessageBox.Show(
                            "Hệ lưới còn " + currentGrid.AmbiguousCells.ToString("n0") +
                            " ô nhiều Z và " + currentGrid.TotalNoDataArea.ToString("n2") +
                            " m² chưa xác nhận. Workbook sẽ ghi rõ TẠM TÍNH, các ô chưa có " +
                            "thể tích xác nhận để trống; không dùng tổng này làm khối lượng nghiệm thu. " +
                            "Anh có muốn xuất Excel để rà soát không?",
                            "Xuất lưới chưa đủ dữ liệu",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button2);
                        if (confirm != DialogResult.Yes) return;
                    }
                    // The Save As dialog can remain open while a CAD boundary is
                    // grip-edited. Re-read the actual polygon immediately before
                    // taking a workbook snapshot.
                    GridWorkflowService.EnsureBoundaryCurrent(
                        BoundaryGeometryService.ReadBoundary(ProjectState.Current.GridBoundaryHandle));
                    _excel.Enabled=false;UseWaitCursor=true;_status.Text="Đang dựng 6 sheet Excel...";
                    var report=await GridVolumeExcelReportBuilder.BuildAsync(GridWorkflowService.Report,GridWorkflowService.Options);
                    _status.Text="Đang ghi Excel...";
                    await Task.Run(()=>ExportWorkflowService.WriteXlsx(dlg.FileName,report));
                    _status.Text="Đã xuất "+dlg.FileName;
                }
            }
            catch(Exception ex){Error(ex,"Xuất lưới Excel");}
            finally{_exportingExcel=false;UseWaitCursor=false;_excel.Enabled=GridWorkflowService.Report!=null;}
        }

        private void RefreshCells()
        {
            var r=GridWorkflowService.Report;
            _tinReuseStatus.Text=TinReuseMessage();
            _tinReuseStatus.ForeColor=
                ProjectState.Current.Existing.IsTinCurrent && ProjectState.Current.Design.IsTinCurrent
                    ? Color.FromArgb(14,87,62) : UiTheme.TextStrong;
            _cells.RowCount=r?.Cells.Count??0;
            _cells.Invalidate();
            _draw.Enabled=r!=null;
            _excel.Enabled=r!=null && !_exportingExcel;
            _cancel.Enabled=_cts!=null;
        }

        private void CellValueNeeded(object sender,DataGridViewCellValueEventArgs e)
        {
            var r=GridWorkflowService.Report;
            if(r==null||e.RowIndex<0||e.RowIndex>=r.Cells.Count)return;
            var c=r.Cells[e.RowIndex];
            switch(e.ColumnIndex)
            {
                case 0:e.Value=c.CellCode;break;
                case 1:e.Value=c.I;break;
                case 2:e.Value=c.J;break;
                case 3:e.Value=c.Volume.CalculationArea.ToString("0.00");break;
                case 4:e.Value=c.Volume.NoDataArea.ToString("0.00");break;
                // A cell with zero covered area is unmeasured, not a
                // certified 0.00 m³. Preserve true partial volumes when
                // the overlap covers some nonzero portion of a cell.
                case 5:e.Value=c.IsVerticalConflict||c.Volume.CoveredArea<=1e-9
                    ?"—":c.Volume.CutVolume.ToString("0.00");break;
                case 6:e.Value=c.IsVerticalConflict||c.Volume.CoveredArea<=1e-9
                    ?"—":c.Volume.FillVolume.ToString("0.00");break;
                case 7:e.Value=c.IsVerticalConflict||c.Volume.CoveredArea<=1e-9
                    ?"—":c.Volume.NetVolume.ToString("0.00");break;
                case 8:e.Value=c.CoverageStatus;break;
            }
        }

        private static void Error(Exception ex,string title)=>
            MessageBox.Show(ex.Message,title,MessageBoxButtons.OK,MessageBoxIcon.Warning);

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                ProjectState.Current.Changed-=ProjectChanged;
                _cts?.Cancel();
            }
            base.Dispose(disposing);
        }
    }
}
