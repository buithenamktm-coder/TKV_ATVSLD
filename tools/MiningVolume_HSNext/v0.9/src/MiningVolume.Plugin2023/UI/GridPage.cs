using System;
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
        private readonly DataGridView _cells;
        private bool _syncing;
        private CancellationTokenSource _cts;

        public GridPage()
        {
            Font = new Font("Arial", 9F);
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var root = new TableLayoutPanel
            {
                Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,
                Padding=Padding.Empty,Margin=Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
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

            var settings=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=6,RowCount=2,Padding=new Padding(2)};
            for(int c=0;c<6;c++) settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F/6F));
            settings.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            settings.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            _boundary=new Label{Text="Chưa chọn đường bao",Dock=DockStyle.Fill,
                TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
            settings.Controls.Add(new Label{Text="Phạm vi",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
            settings.Controls.Add(_boundary,1,0);
            settings.SetColumnSpan(_boundary,3);
            var pick=Btn("CHỌN TRÊN CAD",(s,e)=>ChooseBoundary());
            settings.Controls.Add(pick,4,0);settings.SetColumnSpan(pick,2);
            _sizeX=Num(20,0.1M,100000);
            _sizeY=Num(20,0.1M,100000);
            _angle=Num(0,-180,180);
            settings.Controls.Add(new Label{Text="Ô X (m)",Dock=DockStyle.Fill},0,1);
            settings.Controls.Add(_sizeX,1,1);
            settings.Controls.Add(new Label{Text="Ô Y (m)",Dock=DockStyle.Fill},2,1);
            settings.Controls.Add(_sizeY,3,1);
            settings.Controls.Add(new Label{Text="Góc xoay",Dock=DockStyle.Fill},4,1);
            settings.Controls.Add(_angle,5,1);
            root.Controls.Add(settings,0,1);

            var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=true,Padding=new Padding(4,3,4,3)};
            _calculate=Btn("TÍNH LƯỚI",async(s,e)=>await CalculateAsync());
            _cancel=Btn("HỦY TÍNH",(s,e)=>_cts?.Cancel());
            _draw=Btn("VẼ LƯỚI CAD",(s,e)=>DrawCad());
            _excel=Btn("XUẤT EXCEL",async(s,e)=>await ExportExcelAsync());
            _labels=new CheckBox{Text="Nhãn thể tích trên ô",Checked=false,
                AutoSize=true,Margin=new Padding(10,9,4,4)};
            actions.Controls.Add(_calculate);actions.Controls.Add(_cancel);
            actions.Controls.Add(_draw);actions.Controls.Add(_excel);actions.Controls.Add(_labels);
            root.Controls.Add(actions,0,2);

            _status=new Label{Dock=DockStyle.Fill,AutoEllipsis=true,
                Text="Chọn đường bao, nhập kích thước ô lưới rồi tính.",ForeColor=Color.DimGray,
                TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(6)};
            root.Controls.Add(_status,0,3);

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
            root.Controls.Add(_cells,0,4);

            _sizeX.ValueChanged+=ParametersChanged;
            _sizeY.ValueChanged+=ParametersChanged;
            _angle.ValueChanged+=ParametersChanged;
            ProjectState.Current.Changed+=ProjectChanged;
            Restore();
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
            if(_cts!=null)return;
            var st=ProjectState.Current;
            try
            {
                if(string.IsNullOrWhiteSpace(st.GridBoundaryHandle))
                    throw new InvalidOperationException("Hãy chọn đường bao ô lưới trên bình đồ CAD.");
                var boundary=BoundaryGeometryService.ReadBoundary(st.GridBoundaryHandle).ToArray();
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
                var result=await Task.Run(()=>new GridVolumeCalculator().Calculate(existing,design,
                    options,(done,total)=>((IProgress<string>)progress).Report(
                        "Đang tính ô "+done.ToString("n0")+"/"+total.ToString("n0")),
                    cts.Token));
                GridWorkflowService.Commit(result,options,existing,design,rx,ry);
                _status.Text=result.IsComplete
                    ? "Đã tính "+result.Cells.Count.ToString("n0")+" ô. Đào "+
                        result.TotalCutVolume.ToString("n2")+" m³; đắp "+
                        result.TotalFillVolume.ToString("n2")+" m³."
                    : "Tính xong nhưng còn "+result.TotalNoDataArea.ToString("n2")+
                        " m² thiếu TIN. Chưa được nghiệm thu khối lượng.";
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
            try
            {
                if(!GridWorkflowService.IsCurrent)throw new InvalidOperationException(
                    "Lưới đã cũ hoặc chưa tính. Hãy tính lại trước khi vẽ lên CAD.");
                var doc=Application.DocumentManager.MdiActiveDocument ??
                    throw new InvalidOperationException("Không có bản vẽ AutoCAD.");
                using(doc.LockDocument())
                    GridCadRenderer.Draw(doc.Database,GridWorkflowService.Report,_labels.Checked);
                doc.Editor.Regen();
                _status.Text="Đã vẽ hệ lưới trên layer riêng; các lưới lần xuất trước được giữ nguyên.";
            }
            catch(Exception ex){Error(ex,"Vẽ lưới CAD");}
        }

        private async Task ExportExcelAsync()
        {
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
                    var report=GridVolumeExcelReportBuilder.Build(GridWorkflowService.Report,GridWorkflowService.Options);
                    _excel.Enabled=false;UseWaitCursor=true;_status.Text="Đang xuất 6 sheet Excel...";
                    await Task.Run(()=>ExportWorkflowService.WriteXlsx(dlg.FileName,report));
                    _status.Text="Đã xuất "+dlg.FileName;
                }
            }
            catch(Exception ex){Error(ex,"Xuất lưới Excel");}
            finally{UseWaitCursor=false;_excel.Enabled=true;}
        }

        private void RefreshCells()
        {
            var r=GridWorkflowService.Report;
            _cells.RowCount=r?.Cells.Count??0;
            _cells.Invalidate();
            _draw.Enabled=r!=null;
            _excel.Enabled=r!=null;
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
                case 5:e.Value=c.Volume.CutVolume.ToString("0.00");break;
                case 6:e.Value=c.Volume.FillVolume.ToString("0.00");break;
                case 7:e.Value=c.Volume.NetVolume.ToString("0.00");break;
                case 8:e.Value=c.Volume.IsComplete?"Đủ":"Thiếu TIN";break;
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
