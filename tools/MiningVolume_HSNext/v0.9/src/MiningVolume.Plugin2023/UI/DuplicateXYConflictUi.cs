using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MiningVolume.Core.Surface;
using MiningVolume2023.Services;
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MiningVolume2023.UI
{
    internal static class DuplicateXYConflictUi
    {
        // Compatibility entry point: conflict cannot authorize automatic elevation selection.
        public static DuplicateXYConflictPolicy? Ask(IWin32Window owner, DuplicateXYConflictException ex)
        {
            var role = (ex?.ModelName ?? "").StartsWith(ProjectState.Current.Design.Name, StringComparison.OrdinalIgnoreCase)
                ? ModelRole.Design : ModelRole.Existing;
            Show(role, ex?.Report);
            return null;
        }
        public static void Show(ModelRole role, ConflictReport report = null)
            => CadApplication.ShowModelessDialog(new ConflictReviewForm(role, report));
    }

    internal sealed class ConflictReviewForm : Form
    {
        private sealed class Row { public ConflictGroup Group; public ConflictSource Source; }
        private readonly ModelRole _role;
        private readonly Autodesk.AutoCAD.ApplicationServices.Document _document;
        private readonly DataGridView _grid;
        private readonly Label _status;
        private readonly TextBox _filter;
        private readonly Button _scan, _exclude;
        private readonly System.Windows.Forms.Timer _documentWatch;
        private readonly List<Row> _rows = new List<Row>();
        private CancellationTokenSource _scanCts;
        private ConflictReport _report;
        private MiningVolume.Core.Model.SurfaceModel _source;
        private long _revision;

        public ConflictReviewForm(ModelRole role, ConflictReport report)
        {
            _role=role; _report=report;
            _document=CadApplication.DocumentManager.MdiActiveDocument;
            _source=ProjectState.Current.Get(role).Source; _revision=_source.Revision;
            Text="IMSAT VOLUME — Điểm trùng tọa độ — "+ProjectState.Current.Get(role).Name;
            Font=new Font("Arial",9F); Size=new Size(1040,590); MinimumSize=new Size(760,430);
            StartPosition=FormStartPosition.CenterScreen;
            _status=new Label{Dock=DockStyle.Bottom,Height=58,Padding=new Padding(8)};
            var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(6)};
            _scan=new Button{Text="Quét lại",AutoSize=true,Height=34};
            _scan.Click+=async(s,e)=>await ScanAsync();
            var zoom=new Button{Text="Zoom nguồn",AutoSize=true,Height=34};
            zoom.Click+=(s,e)=>Run(()=>{var row=Selected();if(row!=null)CadViewService.ZoomToHandle(row.Source.Handle);});
            _exclude=new Button{Text="Loại toàn bộ đối tượng khỏi mô hình",AutoSize=true,Height=34};
            _exclude.Click+=async(s,e)=>await ExcludeAsync();
            var cancel=new Button{Text="Hủy quét",AutoSize=true,Height=34};
            cancel.Click+=(s,e)=>_scanCts?.Cancel();
            _filter=new TextBox{Width=230}; _filter.TextChanged+=(s,e)=>BindRows();
            bar.Controls.AddRange(new Control[]{_scan,zoom,_exclude,cancel,new Label{Text="Lọc nhóm/layer/loại/handle",AutoSize=true},_filter});
            _grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
                AutoGenerateColumns=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,
                VirtualMode=true,RowHeadersVisible=false,BackgroundColor=Color.White};
            foreach(var title in new[]{"Nhóm","X","Y","Z nguồn","ΔZ nhóm","Handle","Layer","Loại","Đỉnh mô hình","Đoạn","Nguồn phát sinh"})
                _grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=title,Width=title=="Nguồn phát sinh"?240:100});
            _grid.CellValueNeeded+=(s,e)=>{
                if(e.RowIndex<0||e.RowIndex>=_rows.Count)return;
                var row=_rows[e.RowIndex];var a=row.Source;var g=row.Group;
                object[] values={g.Id,g.Position.X,g.Position.Y,a.Z,g.DeltaZ,a.Handle,a.Layer,a.Type,
                    a.VertexIndex<0?"—":a.VertexIndex.ToString(),a.SegmentIndex<0?"—":a.SegmentIndex.ToString(),a.Origin};
                e.Value=values[e.ColumnIndex];
            };
            Controls.Add(_grid);Controls.Add(bar);Controls.Add(_status);
            _documentWatch=new System.Windows.Forms.Timer{Interval=300};
            _documentWatch.Tick+=(s,e)=>{if(CadApplication.DocumentManager.MdiActiveDocument!=_document)Close();};
            Shown+=async(s,e)=>{_documentWatch.Start();if(_report==null)await ScanAsync();else BindRows();};
            FormClosed+=(s,e)=>{_scanCts?.Cancel();_documentWatch.Stop();_documentWatch.Dispose();};
        }
        private Row Selected()=>_grid.CurrentCell==null||_grid.CurrentCell.RowIndex>=_rows.Count?null:_rows[_grid.CurrentCell.RowIndex];
        private bool Current()=>CadApplication.DocumentManager.MdiActiveDocument==_document&&
            ReferenceEquals(_source,ProjectState.Current.Get(_role).Source)&&_source.Revision==_revision;
        private void Run(Action action){try{if(!Current())throw new InvalidOperationException("Nguồn đã thay đổi. Hãy quét lại trước khi thao tác.");action();}
            catch(Exception ex){_status.Text=ex.Message;}}
        private void BindRows()
        {
            _rows.Clear();var filter=_filter.Text.Trim();
            if(_report!=null)foreach(var g in _report.Groups)foreach(var s in g.Sources)
                if(filter.Length==0||(g.Id+" "+s.Layer+" "+s.Type+" "+s.Handle).IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0)
                    _rows.Add(new Row{Group=g,Source=s});
            _grid.RowCount=_rows.Count;_grid.Invalidate();
            _status.Text=_report==null?"Chưa quét.":$"{_report.Groups.Count:n0} nhóm xung đột • {_rows.Count:n0} bản ghi hiển thị • dung sai XY={_report.XyTolerance:G}, Z={_report.ZTolerance:G}.\r\n"+
                "TIN 2.5D cần một Z tại mỗi XY. Không tự chọn cao độ; loại đối tượng chỉ tác động mô hình, không xóa CAD.";
        }
        private async Task ScanAsync()
        {
            if(_scanCts!=null)return;
            _source=ProjectState.Current.Get(_role).Source;_revision=_source.Revision;
            var polygon=ProjectState.Current.TinRegionPolygon.ToArray();
            _scanCts=new CancellationTokenSource();var token=_scanCts.Token;
            _scan.Enabled=false;_exclude.Enabled=false;_status.Text="Đang quét nguồn tham gia mô hình...";
            try{
                var source=_source;
                var result=await Task.Run(()=>{
                    var input=polygon.Length>=3?SurfaceRegionClipper.Clip(source,polygon,1e-7,token):source;
                    return new ConflictScanner().Scan(input,new SurfaceBuildOptions(),null,token);
                },token);
                if(IsDisposed)return;
                if(!Current())throw new InvalidOperationException("Nguồn thay đổi trong lúc quét; kết quả bị hủy, hãy quét lại.");
                _report=result;BindRows();
            }catch(OperationCanceledException){if(!IsDisposed)_status.Text="Đã hủy quét. Không thay đổi dữ liệu nguồn.";}
            catch(Exception ex){if(!IsDisposed)_status.Text=ex.Message;}
            finally{_scanCts.Dispose();_scanCts=null;if(!IsDisposed){_scan.Enabled=true;_exclude.Enabled=true;}}
        }
        private async Task ExcludeAsync()
        {
            var row=Selected();if(row==null)return;
            if(!Current()){_status.Text="Nguồn đã thay đổi; hãy quét lại.";return;}
            var entity=_source.Entities.FirstOrDefault(x=>x.Id==row.Source.EntityId);
            if(entity==null){_status.Text="Không tìm thấy đối tượng gốc. Hãy nạp lại dữ liệu và quét.";return;}
            if(MessageBox.Show(this,$"Loại TOÀN BỘ {entity.Type}, handle {entity.Handle}, khỏi mô hình {ProjectState.Current.Get(_role).Name}?\r\n"+
                "Các đỉnh và đoạn của đối tượng sẽ không tham gia tính. CAD gốc được giữ nguyên. TIN, mặt cắt và khối lượng cũ sẽ mất hiệu lực.",
                "Xác nhận loại đối tượng",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            try{
                if(!Current())throw new InvalidOperationException("Nguồn đã thay đổi; hãy quét lại.");
                _source.RemoveEntityFromModel(entity.Id);
                SurfaceWorkflowService.InvalidateTin(_role,clearCadLayer:false);
                await ScanAsync();
            }catch(Exception ex){_status.Text=ex.Message;}
        }
    }
}
