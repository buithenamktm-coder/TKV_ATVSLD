using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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
        private sealed class Row {
            public ConflictGroup Group;
            public ConflictSource Source;
            public bool IsGroupHeader => Source==null;
        }
        private readonly ModelRole _role;
        private readonly Autodesk.AutoCAD.ApplicationServices.Document _document;
        private readonly DataGridView _grid;
        private readonly Label _status;
        private readonly TextBox _filter;
        private readonly Button _scan, _exclude, _excludeVertex, _zoomPoint;
        private readonly CheckBox _highDeltaOnly;
        private const double HighDeltaZMetres = 3.0;
        private readonly System.Windows.Forms.Timer _documentWatch;
        private readonly List<Row> _rows = new List<Row>();
        private readonly Dictionary<string,MiningVolume.Core.Model.SourceEntity> _entityById =
            new Dictionary<string,MiningVolume.Core.Model.SourceEntity>(StringComparer.Ordinal);
        private readonly Font _groupFont;
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
            Font=new Font("Arial",9F); Size=new Size(1220,660); MinimumSize=new Size(800,460);
            _groupFont=new Font("Arial",9F,FontStyle.Bold);
            StartPosition=FormStartPosition.CenterScreen;
            _status=new Label{Dock=DockStyle.Bottom,Height=58,Padding=new Padding(8)};
            var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(6)};
            _scan=new Button{Text="Quét lại",AutoSize=true,Height=34};
            _scan.Click+=async(s,e)=>await ScanAsync();
            var zoom=new Button{Text="Zoom đối tượng",AutoSize=true,Height=34};
            zoom.Click+=(s,e)=>Run(()=>{var row=Selected();if(row!=null)CadViewService.ZoomToHandle(row.Source.Handle);});
            _zoomPoint=new Button{Text="Zoom đúng XY",AutoSize=true,Height=34};
            _zoomPoint.Click+=(s,e)=>Run(()=>{var row=Selected();if(row!=null)CadViewService.ZoomToXY(row.Group.Position.X,row.Group.Position.Y);});
            _excludeVertex=new Button{Text="LOẠI ĐIỂM KHỎI MÔ HÌNH",AutoSize=true,Height=34};
            _excludeVertex.Click+=async(s,e)=>await ExcludeVertexAsync();
            _exclude=new Button{Text="Loại cả đối tượng",AutoSize=true,Height=34};
            _exclude.Click+=async(s,e)=>await ExcludeAsync();
            var cancel=new Button{Text="Hủy quét",AutoSize=true,Height=34};
            cancel.Click+=(s,e)=>_scanCts?.Cancel();
            _highDeltaOnly=new CheckBox{Text="Chỉ hiện ΔZ > 3 m",Checked=true,AutoSize=true,
                Padding=new Padding(0,8,0,0)};
            _highDeltaOnly.CheckedChanged+=(s,e)=>BindRows();
            _filter=new TextBox{Width=175}; _filter.TextChanged+=(s,e)=>BindRows();
            bar.Controls.AddRange(new Control[]{_scan,zoom,_zoomPoint,_excludeVertex,_exclude,
                cancel,_highDeltaOnly,new Label{Text="Lọc nhóm/layer/handle",AutoSize=true},_filter});
            _grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
                AutoGenerateColumns=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,
                VirtualMode=true,RowHeadersVisible=false,BackgroundColor=Color.White};
            foreach(var title in new[]{"Nhóm XY","X","Y","Z nguồn (m)","ΔZ nhóm (m)","Handle","Layer","Loại","Đỉnh","Đoạn","Nguồn phát sinh"})
                _grid.Columns.Add(new DataGridViewTextBoxColumn{
                    Name=title,HeaderText=title,Width=title=="Nguồn phát sinh"?205:title=="Nhóm XY"?170:95,
                    SortMode=DataGridViewColumnSortMode.NotSortable
                });
            AddRowAction("row_zoom","Zoom",68);
            AddRowAction("row_remove","Loại điểm",94);
            AddRowAction("row_edit_z","Sửa Z",70);
            _grid.CellValueNeeded+=(s,e)=>{
                if(e.RowIndex<0||e.RowIndex>=_rows.Count||e.ColumnIndex<0)return;
                var row=_rows[e.RowIndex];var g=row.Group;
                if(row.IsGroupHeader)
                {
                    object[] values={
                        "▼ "+g.Id+" ("+g.Sources.Count+" nguồn)",
                        g.Position.X.ToString("0.###"),
                        g.Position.Y.ToString("0.###"),
                        "Z: "+g.Sources.Min(x=>x.Z).ToString("0.###")+" → "+
                            g.Sources.Max(x=>x.Z).ToString("0.###"),
                        g.DeltaZ.ToString("0.###"),
                        "","","","","",
                        "NHÓM XY • chọn điểm ở các dòng bên dưới",
                        "","",""
                    };
                    e.Value=values[e.ColumnIndex];
                    return;
                }
                var a=row.Source;
                object[] valuesDetail={
                    "   ↳ "+g.Id,
                    g.Position.X.ToString("0.###"),
                    g.Position.Y.ToString("0.###"),
                    a.Z.ToString("0.###"),
                    g.DeltaZ.ToString("0.###"),
                    a.Handle,a.Layer,a.Type.ToString(),
                    a.VertexIndex<0?"—":a.VertexIndex.ToString(),
                    a.SegmentIndex<0?"—":a.SegmentIndex.ToString(),a.Origin,
                    "Zoom", "Loại điểm", "Sửa Z"
                };
                e.Value=valuesDetail[e.ColumnIndex];
            };
            _grid.CellFormatting+=(s,e)=>{
                if(e.RowIndex<0||e.RowIndex>=_rows.Count)return;
                if(_rows[e.RowIndex].IsGroupHeader)
                {
                    e.CellStyle.BackColor=Color.FromArgb(231,239,249);
                    e.CellStyle.ForeColor=Color.FromArgb(25,48,78);
                    e.CellStyle.Font=_groupFont;
                }
            };
            _grid.CellContentClick+=async (s,e)=>{
                if(e.RowIndex<0||e.RowIndex>=_rows.Count||e.ColumnIndex<0)return;
                var row=_rows[e.RowIndex];
                if(row.IsGroupHeader)return;
                var action=_grid.Columns[e.ColumnIndex].Name;
                if(action=="row_zoom") ZoomRow(row);
                else if(action=="row_remove") await ExcludeVertexAsync(row);
                else if(action=="row_edit_z") await EditVertexZAsync(row);
            };
            Controls.Add(_grid);Controls.Add(bar);Controls.Add(_status);
            _grid.SelectionChanged+=(s,e)=>RefreshActions();
            _documentWatch=new System.Windows.Forms.Timer{Interval=300};
            _documentWatch.Tick+=(s,e)=>{if(CadApplication.DocumentManager.MdiActiveDocument!=_document)Close();};
            Shown+=async(s,e)=>{_documentWatch.Start();if(_report==null)await ScanAsync();else BindRows();};
            FormClosed+=(s,e)=>{
                _scanCts?.Cancel();_documentWatch.Stop();_documentWatch.Dispose();
                _groupFont.Dispose();
            };
        }
        private Row Selected()=>_grid.CurrentCell==null || _grid.CurrentCell.RowIndex<0 ||
            _grid.CurrentCell.RowIndex>=_rows.Count?null:_rows[_grid.CurrentCell.RowIndex];

        private void AddRowAction(string id,string title,int width)
        {
            _grid.Columns.Add(new DataGridViewButtonColumn {
                Name=id,HeaderText=title,Width=width,
                FlatStyle=FlatStyle.Flat,UseColumnTextForButtonValue=false,
                SortMode=DataGridViewColumnSortMode.NotSortable
            });
        }

        private void ZoomRow(Row row)
        {
            Run(()=>{
                if(row==null)return;
                if(row.Source==null)CadViewService.ZoomToXY(row.Group.Position.X,row.Group.Position.Y);
                else
                {
                    // Prefer exact conflict XY; a long contour's extent is
                    // not the point that needs investigation.
                    CadViewService.ZoomToXY(row.Group.Position.X,row.Group.Position.Y);
                }
            });
        }
        private bool Current()=>_source!=null &&
            CadApplication.DocumentManager.MdiActiveDocument==_document&&
            ReferenceEquals(_source,ProjectState.Current.Get(_role).Source)&&_source.Revision==_revision;

        /// <summary>
        /// Map a conflict row back to a REAL, currently enabled source vertex.
        /// A clipped/generated CAD intersection has no deletable original vertex.
        /// Coordinates and elevation are checked against the unmodified input
        /// before permitting any exclusion.
        /// </summary>
        private MiningVolume.Core.Model.ModelVertex ResolveOriginalVertex(Row row)
        {
            if(row==null || _source==null || !Current())return null;
            if(row.IsGroupHeader)return null;
            if(!_entityById.TryGetValue(row.Source.EntityId,out var candidate) || !candidate.IsEnabled)
                return null;
            if(candidate==null)return null;
            double xy=_report?.XyTolerance??1e-6,z=_report?.ZTolerance??1e-4;
            bool Matches(MiningVolume.Core.Model.ModelVertex v)
            {
                double dx=v.Position.X-row.Group.Position.X;
                double dy=v.Position.Y-row.Group.Position.Y;
                return v.IsEnabled && dx*dx+dy*dy<=xy*xy &&
                    Math.Abs(v.Position.Z-row.Source.Z)<=z;
            }
            if (!row.Source.Generated && row.Source.VertexIndex>=0)
            {
                var indexed=candidate.Vertices.FirstOrDefault(v=>v.Index==row.Source.VertexIndex);
                return indexed!=null && Matches(indexed)?indexed:null;
            }
            // Region-clipping may produce a generated copy of an actual
            // original endpoint; only accept a provable coordinate/Z match.
            var matches=candidate.Vertices.Where(Matches).Take(2).ToList();
            return matches.Count==1?matches[0]:null;
        }

        private void RefreshActions()
        {
            if(IsDisposed)return;
            bool valid=Current() && _scanCts==null;
            var row=Selected();
            _excludeVertex.Enabled=valid && ResolveOriginalVertex(row)!=null;
            _exclude.Enabled=valid && row!=null && !row.IsGroupHeader;
            _zoomPoint.Enabled=valid && row!=null;
        }
        private void Run(Action action){try{if(!Current())throw new InvalidOperationException("Nguồn đã thay đổi. Hãy quét lại trước khi thao tác.");action();}
            catch(Exception ex){_status.Text=ex.Message;}}
        private void BindRows()
        {
            _rows.Clear();var filter=_filter.Text.Trim();
            int groupsVisible=0,sourceRows=0;
            if(_report!=null)
            {
                foreach(var g in _report.Groups.OrderByDescending(x=>x.DeltaZ).ThenBy(x=>x.Id,StringComparer.Ordinal))
                {
                    if(_highDeltaOnly.Checked && !(g.DeltaZ>HighDeltaZMetres))continue;
                    bool matchGroup=filter.Length==0 || (g.Id+" "+g.Position.X.ToString("R")+
                        " "+g.Position.Y.ToString("R")).IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0;
                    var matching=g.Sources.Where(x=>matchGroup ||
                        (x.Layer+" "+x.Type+" "+x.Handle+" "+x.Z.ToString("R"))
                        .IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0).ToList();
                    if(matching.Count==0)continue;
                    _rows.Add(new Row{Group=g});
                    foreach(var source in matching.OrderBy(x=>x.Z))
                    {
                        _rows.Add(new Row{Group=g,Source=source});
                        sourceRows++;
                    }
                    groupsVisible++;
                }
            }
            _grid.RowCount=_rows.Count;_grid.Invalidate();
            _status.Text=_report==null?"Chưa quét.":
                $"{groupsVisible:n0}/{_report.Groups.Count:n0} nhóm XY • {sourceRows:n0} điểm nguồn đang hiển thị • ΔZ > 3 m: "+
                (_highDeltaOnly.Checked?"BẬT":"TẮT")+" • dung sai XY="+
                _report.XyTolerance.ToString("G",CultureInfo.InvariantCulture)+" m."+
                "\r\nNút LOẠI ĐIỂM chỉ tắt đúng đỉnh thật trong mô hình TIN (không xóa CAD). "+
                "Dòng nội suy/giao điểm không có đỉnh gốc hợp lệ phải xử lý ở đối tượng nguồn.";
            RefreshActions();
        }
        private async Task ScanAsync()
        {
            if(_scanCts!=null)return;
            _source=ProjectState.Current.Get(_role).Source;_revision=_source.Revision;
            _entityById.Clear();
            foreach(var e in _source.Entities)
                if(!_entityById.ContainsKey(e.Id))_entityById.Add(e.Id,e);
            var polygon=ProjectState.Current.TinRegionPolygon.ToArray();
            _scanCts=new CancellationTokenSource();var token=_scanCts.Token;
            _scan.Enabled=false;_exclude.Enabled=false;_excludeVertex.Enabled=false;
            _zoomPoint.Enabled=false;
            _status.Text="Đang quét nguồn tham gia mô hình...";
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
            finally{_scanCts.Dispose();_scanCts=null;if(!IsDisposed){_scan.Enabled=true;RefreshActions();}}
        }
        private async Task ExcludeVertexAsync()
        {
            var row=Selected();
            if(row==null)return;
            if(!Current())
            {
                _status.Text="Nguồn TIN đã thay đổi; phải quét lại trước khi loại điểm.";
                return;
            }
            var vertex=ResolveOriginalVertex(row);
            if(vertex==null)
            {
                _status.Text="Dòng này chỉ là điểm nội suy hoặc không ánh xạ duy nhất đến "+
                    "đỉnh nguồn. Không xóa nhầm: hãy Zoom XY và kiểm tra đường gốc.";
                return;
            }
            var entity=_source.Entities.First(e=>e.Id==row.Source.EntityId);
            int index=vertex.Index;
            if(MessageBox.Show(this,
                "Loại CHỈ MỘT ĐỈNH khỏi dữ liệu dựng TIN?"+
                "\r\nLayer: "+entity.Layer+" • Handle: "+entity.Handle+" • Đỉnh: "+index+
                "\r\nXY: "+row.Group.Position.X.ToString("0.###")+"; "+
                row.Group.Position.Y.ToString("0.###")+
                " • Z: "+vertex.Position.Z.ToString("0.###")+" m"+
                "\r\nΔZ nhóm: "+row.Group.DeltaZ.ToString("0.###")+" m."+
                "\r\n\r\nKhông xóa/sửa bản vẽ DWG gốc. TIN, mặt cắt và "+
                "kết quả tính đang lưu sẽ mất hiệu lực. Tiếp tục?",
                "Xác nhận LOẠI ĐIỂM khỏi mô hình",
                MessageBoxButtons.YesNo,MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            try
            {
                if(!Current())throw new InvalidOperationException("Nguồn đã thay đổi.");
                // Re-resolve after the modal dialog, to protect against
                // changes to the original model while the dialog was shown.
                var confirmed=ResolveOriginalVertex(row);
                if(confirmed==null || confirmed.Index!=index)
                    throw new InvalidOperationException("Đỉnh nguồn đã thay đổi, vui lòng quét lại.");
                _source.SetVertexEnabled(entity.Id,index,false);
                SurfaceWorkflowService.InvalidateTin(_role,clearCadLayer:true);
                await ScanAsync();
            }
            catch(Exception ex) { if(!IsDisposed)_status.Text=ex.Message; }
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
                SurfaceWorkflowService.InvalidateTin(_role,clearCadLayer:true);
                await ScanAsync();
            }catch(Exception ex){_status.Text=ex.Message;}
        }
    }
}
