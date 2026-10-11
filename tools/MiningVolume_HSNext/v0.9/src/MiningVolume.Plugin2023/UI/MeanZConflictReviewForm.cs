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
using CadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MiningVolume2023.UI
{
    /// <summary>
    /// Two-level editor of source vertices with XY conflicts above 3 metres.
    /// A group is one XY; selecting it lists EVERY real source vertex at that
    /// XY. Each row has its own Zoom, Edit Z and Disable Vertex buttons.
    /// Only ProjectState.Source is changed; original CAD entities are never
    /// opened for write, erased, or exploded.
    /// </summary>
    internal sealed class MeanZConflictReviewForm : Form
    {
        private readonly ModelRole _role;
        private readonly Autodesk.AutoCAD.ApplicationServices.Document _document;
        private readonly SurfaceModel _source;
        private readonly DataGridView _groups;
        private readonly DataGridView _points;
        private readonly Label _status;
        private readonly TextBox _filter;
        private readonly Button _rescan;
        private readonly System.Windows.Forms.Timer _watch;
        private readonly List<MeanZPreviewPreparer.ConflictMean> _visibleGroups =
            new List<MeanZPreviewPreparer.ConflictMean>();
        private readonly List<MeanZPreviewPreparer.Member> _visibleMembers =
            new List<MeanZPreviewPreparer.Member>();
        private MeanZPreviewPreparer.Result _scan;
        private long _revision;
        private bool _scanning;
        private CancellationTokenSource _cts;

        internal MeanZConflictReviewForm(ModelRole role, MeanZPreviewPreparer.Result scan)
        {
            _role=role;
            _source=ProjectState.Current.Get(role).Source;
            _revision=_source?.Revision??-1;
            _scan=scan;
            _document=CadApplication.DocumentManager.MdiActiveDocument;
            Text="IMSAT VOLUME – KIỂM TRA TRÙNG XY, CHÊNH Z > 3,000 m – "+
                ProjectState.Current.Get(role).Name;
            Font=new Font("Arial",9F);
            Size=new Size(1250,700);
            MinimumSize=new Size(900,480);
            StartPosition=FormStartPosition.CenterScreen;

            _status=new Label {
                Dock=DockStyle.Bottom,Height=65,Padding=new Padding(10,7,8,4),
                TextAlign=ContentAlignment.MiddleLeft
            };
            Controls.Add(_status);

            var toolbar=new FlowLayoutPanel {
                Dock=DockStyle.Top,Height=54,WrapContents=false,
                Padding=new Padding(7,8,7,6),AutoScroll=true
            };
            _rescan=new Button { Text="Quét lại nhóm > 3 m",Width=160,Height=33 };
            _rescan.Click+=async(s,e)=>await RescanAsync();
            var zoomGroup=new Button { Text="Zoom tới nhóm XY",Width=148,Height=33 };
            zoomGroup.Click+=(s,e)=>ZoomGroup();
            _filter=new TextBox {Width=280,Height=32};
            _filter.TextChanged+=(s,e)=>BindGroups();
            toolbar.Controls.Add(_rescan);
            toolbar.Controls.Add(zoomGroup);
            toolbar.Controls.Add(new Label {
                Text="Lọc theo X, Y, handle hoặc layer:",AutoSize=true,
                Padding=new Padding(8,8,0,0)
            });
            toolbar.Controls.Add(_filter);
            Controls.Add(toolbar);

            var split=new SplitContainer {
                Dock=DockStyle.Fill,Orientation=Orientation.Vertical,
                SplitterWidth=7,Panel1MinSize=240,Panel2MinSize=450
            };
            split.SplitterDistance=535;

            var groupPanel=new Panel {Dock=DockStyle.Fill};
            var groupTitle=new Label {
                Text="NHÓM TRÙNG XY (chỉ hiện ΔZ > 3,000 m)",
                Dock=DockStyle.Top,Height=27,Font=new Font("Arial",9F,FontStyle.Bold)
            };
            _groups=new DataGridView {
                Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,
                AllowUserToDeleteRows=false,AutoGenerateColumns=false,
                SelectionMode=DataGridViewSelectionMode.FullRowSelect,
                MultiSelect=false,RowHeadersVisible=false,VirtualMode=true,
                BackgroundColor=Color.White
            };
            AddColumn(_groups,"Group","Nhóm",85);
            AddColumn(_groups,"X","X",110);
            AddColumn(_groups,"Y","Y",120);
            AddColumn(_groups,"Count","Số điểm",75);
            AddColumn(_groups,"Min","Z nhỏ",85);
            AddColumn(_groups,"Max","Z lớn",85);
            AddColumn(_groups,"Delta","ΔZ (m)",90);
            _groups.CellValueNeeded+=GroupCell;
            _groups.SelectionChanged+=(s,e)=>BindPoints();
            groupPanel.Controls.Add(_groups);
            groupPanel.Controls.Add(groupTitle);
            split.Panel1.Controls.Add(groupPanel);

            var pointPanel=new Panel {Dock=DockStyle.Fill};
            var pointTitle=new Label {
                Text="TỪNG ĐIỂM TRONG NHÓM – chọn đúng dòng để thao tác",
                Dock=DockStyle.Top,Height=27,Font=new Font("Arial",9F,FontStyle.Bold)
            };
            _points=new DataGridView {
                Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,
                AllowUserToDeleteRows=false,AutoGenerateColumns=false,
                SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,
                RowHeadersVisible=false,BackgroundColor=Color.White,VirtualMode=true
            };
            AddColumn(_points,"Z","Z (m)",85);
            AddColumn(_points,"Layer","Layer",88);
            AddColumn(_points,"Type","Loại",95);
            AddColumn(_points,"Handle","Handle",95);
            AddColumn(_points,"Index","Đỉnh #",75);
            AddButton(_points,"Zoom","Zoom tới",85);
            AddButton(_points,"Edit","Sửa Z",76);
            AddButton(_points,"Remove","Loại điểm",95);
            _points.CellValueNeeded+=PointCell;
            _points.CellContentClick+=PointClicked;
            pointPanel.Controls.Add(_points);
            pointPanel.Controls.Add(pointTitle);
            split.Panel2.Controls.Add(pointPanel);
            Controls.Add(split);
            split.BringToFront();

            _watch=new System.Windows.Forms.Timer{Interval=500};
            _watch.Tick+=(s,e)=>{
                if(CadApplication.DocumentManager.MdiActiveDocument!=_document)
                    Close();
            };
            Shown+=async(s,e)=>{
                _watch.Start();
                if(_scan==null)await RescanAsync();else BindGroups();
            };
            FormClosed+=(s,e)=>{
                _cts?.Cancel();
                _watch.Stop();
                _watch.Dispose();
            };
        }

        private static void AddColumn(DataGridView grid,string name,string title,int width)
            => grid.Columns.Add(new DataGridViewTextBoxColumn {
                Name=name,HeaderText=title,Width=width,SortMode=DataGridViewColumnSortMode.NotSortable
            });

        private static void AddButton(DataGridView grid,string name,string title,int width)
            => grid.Columns.Add(new DataGridViewButtonColumn {
                Name=name,HeaderText=title,Width=width,Text=title,UseColumnTextForButtonValue=true,
                SortMode=DataGridViewColumnSortMode.NotSortable
            });

        private bool IsCurrent()
        {
            return !_scanning && _document!=null &&
                CadApplication.DocumentManager.MdiActiveDocument==_document &&
                ReferenceEquals(_source,ProjectState.Current.Get(_role).Source) &&
                _source!=null && _source.Revision==_revision;
        }

        private MeanZPreviewPreparer.ConflictMean SelectedGroup()
        {
            if(_groups.CurrentCell==null)return null;
            int index=_groups.CurrentCell.RowIndex;
            return index>=0&&index<_visibleGroups.Count?_visibleGroups[index]:null;
        }

        private void BindGroups()
        {
            if(IsDisposed)return;
            _visibleGroups.Clear();
            string needle=(_filter.Text??"").Trim();
            if(_scan!=null)
                foreach(var group in _scan.ReviewGroups.OrderByDescending(x=>x.DeltaZ))
                {
                    bool matched=needle.Length==0 ||
                        (group.X.ToString("0.###")+" "+group.Y.ToString("0.###"))
                            .IndexOf(needle,StringComparison.OrdinalIgnoreCase)>=0 ||
                        group.Members.Any(m=>(m.Handle+" "+m.Layer+" "+m.Type)
                            .IndexOf(needle,StringComparison.OrdinalIgnoreCase)>=0);
                    if(matched)_visibleGroups.Add(group);
                }
            _groups.RowCount=0;
            _groups.RowCount=_visibleGroups.Count;
            _groups.Invalidate();
            if(_visibleGroups.Count>0)
            {
                _groups.CurrentCell=_groups.Rows[0].Cells[0];
            }
            BindPoints();
        }

        private void BindPoints()
        {
            _visibleMembers.Clear();
            var selected=SelectedGroup();
            if(selected?.Members!=null)_visibleMembers.AddRange(selected.Members
                .OrderBy(x=>x.Z).ThenBy(x=>x.Handle,StringComparer.OrdinalIgnoreCase)
                .ThenBy(x=>x.VertexIndex));
            _points.RowCount=0;
            _points.RowCount=_visibleMembers.Count;
            _points.Invalidate();
            _status.Text=_scan==null?"Chưa quét dữ liệu nguồn.":
                (_visibleGroups.Count.ToString("n0")+" nhóm hiển thị / "+
                _scan.ReviewGroups.Count.ToString("n0")+" nhóm ΔZ > 3 m chưa xử lý. "+
                "Chọn một nhóm XY để xem tất cả điểm khác cao độ.\r\n"+
                "Loại/Sửa chỉ áp dụng cho mô hình IMSAT, không xóa hoặc thay đổi đối tượng DWG. "+
                "Sau khi chỉnh sửa hãy dựng lại TIN; lưới thử nghiệm không dùng tính khối lượng.");
        }

        private void GroupCell(object sender,DataGridViewCellValueEventArgs e)
        {
            if(e.RowIndex<0||e.RowIndex>=_visibleGroups.Count)return;
            var g=_visibleGroups[e.RowIndex];
            switch(_groups.Columns[e.ColumnIndex].Name)
            {
                case "Group":e.Value="XY-"+(e.RowIndex+1).ToString();break;
                case "X":e.Value=g.X.ToString("0.###");break;
                case "Y":e.Value=g.Y.ToString("0.###");break;
                case "Count":e.Value=g.VertexCount;break;
                case "Min":e.Value=g.MinZ.ToString("0.###");break;
                case "Max":e.Value=g.MaxZ.ToString("0.###");break;
                case "Delta":e.Value=g.DeltaZ.ToString("0.###");break;
            }
        }
        private void PointCell(object sender,DataGridViewCellValueEventArgs e)
        {
            if(e.RowIndex<0||e.RowIndex>=_visibleMembers.Count)return;
            var m=_visibleMembers[e.RowIndex];
            switch(_points.Columns[e.ColumnIndex].Name)
            {
                case "Z":e.Value=m.Z.ToString("0.###");break;
                case "Layer":e.Value=m.Layer;break;
                case "Type":e.Value=m.Type.ToString();break;
                case "Handle":e.Value=m.Handle;break;
                case "Index":e.Value=m.VertexIndex;break;
            }
        }

        private void PointClicked(object sender,DataGridViewCellEventArgs e)
        {
            if(e.RowIndex<0||e.RowIndex>=_visibleMembers.Count||e.ColumnIndex<0)return;
            var name=_points.Columns[e.ColumnIndex].Name;
            var member=_visibleMembers[e.RowIndex];
            var group=SelectedGroup();
            if(group==null)return;
            if(name=="Zoom")ZoomXY(group);
            else if(name=="Edit")EditZ(member,group);
            else if(name=="Remove")ExcludeVertex(member,group);
        }

        private void ZoomGroup()
        {
            var group=SelectedGroup();
            if(group!=null)ZoomXY(group);
        }

        private void ZoomXY(MeanZPreviewPreparer.ConflictMean group)
        {
            try
            {
                if(!IsCurrent())throw new InvalidOperationException(
                    "Nguồn đã thay đổi hoặc chuyển bản vẽ. Hãy quét lại.");
                CadViewService.ZoomToXY(group.X,group.Y,30.0);
            }
            catch(Exception ex){_status.Text=ex.Message;}
        }

        private bool TryGetActiveVertex(MeanZPreviewPreparer.Member member,
            out SourceEntity entity)
        {
            entity=null;
            if(!IsCurrent()){_status.Text="Dữ liệu nguồn thay đổi. Hãy quét lại.";return false;}
            entity=_source.Entities.FirstOrDefault(x=>x.Id==member.EntityId);
            if(entity==null || !entity.IsEnabled ||
                member.VertexIndex<0 || member.VertexIndex>=entity.Vertices.Count ||
                !entity.Vertices[member.VertexIndex].IsEnabled)
            {
                _status.Text="Không xác định được đỉnh nguồn, hãy quét lại.";
                return false;
            }
            // Guard edits against stale snapshot even if a caller forgot Touch().
            if(Math.Abs(entity.Vertices[member.VertexIndex].Position.Z-member.Z)>1e-8)
            {
                _status.Text="Cao độ đỉnh đã thay đổi, hãy quét lại.";return false;
            }
            return true;
        }

        private void ExcludeVertex(MeanZPreviewPreparer.Member member,
            MeanZPreviewPreparer.ConflictMean group)
        {
            if(!TryGetActiveVertex(member,out var entity))return;
            // Explicit Vietnamese buttons. Default and Esc both mean NO.
            if(!ConfirmExclude(
                "Bạn có chắc chắn muốn loại ĐIỂM này khỏi mô hình IMSAT?\r\n\r\n"+
                "Nhóm XY: "+group.X.ToString("0.###")+"; "+group.Y.ToString("0.###")+
                "\r\nCao độ: "+member.Z.ToString("0.###")+" m"+
                "\r\nĐối tượng: "+member.Type+"; layer "+member.Layer+
                "; handle "+member.Handle+"; đỉnh "+member.VertexIndex+
                "\r\n\r\nChỉ vô hiệu hóa đúng đỉnh đang chọn trong mô hình, "+
                "không xóa hình học CAD gốc. TIN và kết quả tính trước đó sẽ hết hiệu lực."))
                return;
            try
            {
                if(!TryGetActiveVertex(member,out entity))return;
                _source.SetVertexEnabled(entity.Id,member.VertexIndex,false);
                SurfaceWorkflowService.InvalidateTin(_role,clearCadLayer:true);
                _revision=_source.Revision;
                _scan=null;
                _=RescanAsync();
            }
            catch(Exception ex){_status.Text=ex.Message;}
        }

        private void EditZ(MeanZPreviewPreparer.Member member,
            MeanZPreviewPreparer.ConflictMean group)
        {
            if(!TryGetActiveVertex(member,out var entity))return;
            using(var dialog=new NumericInputDialog("Sửa cao độ đỉnh trong mô hình IMSAT",
                "Z mới (m):",member.Z))
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                if(!TryGetActiveVertex(member,out entity))return;
                try
                {
                    _source.OverrideVertexZ(entity.Id,member.VertexIndex,dialog.Value);
                    SurfaceWorkflowService.InvalidateTin(_role,clearCadLayer:true);
                    _revision=_source.Revision;
                    _scan=null;
                    _=RescanAsync();
                }
                catch(Exception ex){_status.Text=ex.Message;}
            }
        }

        private bool ConfirmExclude(string message)
        {
            using(var dialog=new Form {
                Text="Xác nhận loại điểm khỏi mô hình",
                Font=new Font("Arial",9F),Width=590,Height=285,
                StartPosition=FormStartPosition.CenterParent,
                FormBorderStyle=FormBorderStyle.FixedDialog,
                MaximizeBox=false,MinimizeBox=false
            })
            {
                var description=new Label {
                    Text=message,Dock=DockStyle.Fill,Padding=new Padding(15,13,15,9)
                };
                var bar=new FlowLayoutPanel {
                    Dock=DockStyle.Bottom,Height=52,
                    FlowDirection=FlowDirection.RightToLeft,
                    Padding=new Padding(9)
                };
                var no=new Button {
                    Text="Không",Width=105,Height=29,DialogResult=DialogResult.No
                };
                var yes=new Button {
                    Text="Đồng ý",Width=105,Height=29,DialogResult=DialogResult.Yes
                };
                bar.Controls.Add(no);bar.Controls.Add(yes);
                dialog.AcceptButton=no;
                dialog.CancelButton=no;
                dialog.Controls.Add(description);
                dialog.Controls.Add(bar);
                return dialog.ShowDialog(this)==DialogResult.Yes;
            }
        }

        private async Task RescanAsync()
        {
            if(_scanning||IsDisposed)return;
            if(_source==null)return;
            if(CadApplication.DocumentManager.MdiActiveDocument!=_document ||
                !ReferenceEquals(_source,ProjectState.Current.Get(_role).Source))
            {
                _status.Text="Bản vẽ hoặc mô hình đã đổi. Đóng cửa sổ và mở lại.";
                return;
            }
            _scanning=true;
            _rescan.Enabled=false;
            _points.Enabled=false;
            _groups.Enabled=false;
            _status.Text="Đang quét lại các nhóm XY có chênh Z > 3 m...";
            _cts=new CancellationTokenSource();
            var token=_cts.Token;
            long sourceRevision=_source.Revision;
            var polygon=ProjectState.Current.HasTinRegion
                ? ProjectState.Current.TinRegionPolygon.ToArray():null;
            try
            {
                var scan=await Task.Run(()=>MeanZPreviewPreparer.Prepare(
                    _source,1e-6,1e-4,token,polygon),token);
                if(IsDisposed)return;
                if(CadApplication.DocumentManager.MdiActiveDocument!=_document ||
                   !ReferenceEquals(_source,ProjectState.Current.Get(_role).Source) ||
                   _source.Revision!=sourceRevision)
                    throw new InvalidOperationException("Dữ liệu đã thay đổi trong lúc quét. Hãy quét lại.");
                _revision=sourceRevision;
                _scan=scan;
                BindGroups();
            }
            catch(OperationCanceledException)
            {
                if(!IsDisposed)_status.Text="Đã hủy quét. Không sửa dữ liệu.";
            }
            catch(Exception ex)
            {
                if(!IsDisposed)_status.Text=ex.Message;
            }
            finally
            {
                _cts?.Dispose();_cts=null;
                _scanning=false;
                if(!IsDisposed)
                {
                    _rescan.Enabled=true;
                    _points.Enabled=true;
                    _groups.Enabled=true;
                }
            }
        }
    }
}
