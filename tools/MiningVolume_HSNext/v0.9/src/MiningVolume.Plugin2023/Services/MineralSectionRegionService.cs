using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Sections;

namespace MiningVolume2023.Services
{
    public sealed class MineralSectionArea
    {
        public string SectionName { get; internal set; }
        public string BodyId { get; internal set; }
        public string BodyName { get; internal set; }
        public string PolylineHandle { get; internal set; }
        public double AreaSquareMetres { get; internal set; }
        public string State { get; internal set; }
    }

    /// <summary>
    /// Lưu liên kết Polygon CAD ⇄ vỉa ⇄ mặt cắt trong chính DWG qua XData.
    /// Người dùng có thể kéo đỉnh Polyline bằng AutoCAD rồi bấm CẬP NHẬT
    /// để kiểm tra diện tích thực (đã bỏ hệ số phóng đại đứng).
    /// Không tính m3/tấn từ hai đường lộ vỉa 2D khi thiếu cơ sở hình học.
    /// </summary>
    internal static class MineralSectionRegionService
    {
        public const string RegionLayer = "IMSAT_KHOANG_SAN_MC";
        private const string LabelLayer = "IMSAT_KHOANG_SAN_NHAN";
        private const string AreaRegApp = "IMSAT_MV2_MINERAL_MC";
        private const string LabelRegApp = "IMSAT_MV2_MC_LABEL";

        private sealed class SectionFrame
        {
            public double X0, Y0, BaseZ, VExag;
        }

        private sealed class RegionGeometry
        {
            public MineralSectionArea Area;
            public IReadOnlyList<Vec2> Physical;
            public Point3d CadCenter;
        }

        public static double AssignRegion(string handle, MineralBody body, SectionProfile profile)
        {
            if (body == null || body.IsDeleted || !body.HasPairedOutcrops)
                throw new InvalidOperationException("Cần chọn đủ hai đường lộ vỉa cho vỉa đang gán.");
            if (profile == null || !profile.HasData)
                throw new InvalidOperationException("Mặt cắt chưa được dựng từ hai TIN.");
            if (string.IsNullOrWhiteSpace(handle)) throw new ArgumentException("Chưa chọn Polyline khoáng sản.");
            var doc = Application.DocumentManager.MdiActiveDocument ??
                throw new InvalidOperationException("Không có bản vẽ AutoCAD.");

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var frame = FindFrame(doc.Database, tr, profile.Line.Name);
                var id = doc.Database.GetObjectId(false,
                    new Handle(Convert.ToInt64(handle, 16)), 0);
                var poly = tr.GetObject(id, OpenMode.ForWrite) as Polyline;
                if (poly == null) throw new InvalidOperationException("Chọn LWPOLYLINE khép kín trên mặt cắt.");
                var region = ReadPhysicalPolygon(poly, frame);
                double area = MineralSectionPolygonCalculator.ValidateAndMeasure(profile, region);
                ValidateNoOverlap(doc.Database, tr, poly.ObjectId, profile.Line.Name, frame, region);
                EnsureRegApp(doc.Database,tr,AreaRegApp);
                var layer=EnsureLayer(doc.Database,tr,RegionLayer,6);
                poly.LayerId=layer;
                poly.ColorIndex=256;
                poly.XData=new ResultBuffer(
                    new TypedValue((int)DxfCode.ExtendedDataRegAppName,AreaRegApp),
                    new TypedValue((int)DxfCode.ExtendedDataAsciiString,body.Id),
                    new TypedValue((int)DxfCode.ExtendedDataAsciiString,profile.Line.Name));
                tr.Commit();
                ProjectState.Current.NotifyChanged();
                return area;
            }
        }

        public static IReadOnlyList<MineralSectionArea> UpdateAll()
        {
            var state = ProjectState.Current;
            if (!state.HasMineralInput)
                throw new InvalidOperationException("Chưa chọn đủ đường lộ vỉa hiện trạng và thiết kế.");
            if (state.SectionProfiles.Count==0)
                throw new InvalidOperationException("Chưa tạo hệ mặt cắt. Hãy xuất mặt cắt trước.");
            SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers:false);
            var doc=Application.DocumentManager.MdiActiveDocument ??
                throw new InvalidOperationException("Không có bản vẽ AutoCAD.");
            var output=new List<RegionGeometry>();
            using(doc.LockDocument())
            using(var tr=doc.Database.TransactionManager.StartTransaction())
            {
                var bt=(BlockTable)tr.GetObject(doc.Database.BlockTableId,OpenMode.ForRead);
                var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                var lookup=state.SectionProfiles.ToDictionary(p=>p.Line.Name,StringComparer.OrdinalIgnoreCase);
                var bodies=state.MineralBodies.Where(x=>!x.IsDeleted)
                    .ToDictionary(b=>b.Id,StringComparer.OrdinalIgnoreCase);
                var frames=new Dictionary<string,SectionFrame>(StringComparer.OrdinalIgnoreCase);
                // Native mine DWGs can contain more than a million TIN faces.
                // Reading each 3DFACE simply to find a mineral LWPOLYLINE
                // needlessly allocates AutoCAD wrappers and stalls the palette.
                var polyClass=Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Polyline));

                foreach(ObjectId id in ms)
                {
                    if(!id.ObjectClass.IsDerivedFrom(polyClass))continue;
                    var poly=tr.GetObject(id,OpenMode.ForRead) as Polyline;
                    if(poly==null)continue;
                    if(!TryReadTag(poly,AreaRegApp,out string bodyId,out string sectionName))
                        continue;
                    if(!string.Equals(poly.Layer,RegionLayer,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Đa giác khoáng sản "+poly.Handle+
                            " đã bị chuyển khỏi layer "+RegionLayer+"; hãy chuyển lại trước khi cập nhật.");
                    if(!bodies.TryGetValue(bodyId,out var body)||!body.HasPairedOutcrops)
                        throw new InvalidOperationException("Vùng "+poly.Handle+
                            " liên kết với vỉa đã xóa hoặc chưa chọn đủ hai đường lộ vỉa.");
                    if(!lookup.TryGetValue(sectionName,out var profile))
                        throw new InvalidOperationException("Không tìm thấy mặt cắt "+sectionName+
                            " đã gắn với vùng "+poly.Handle+". Cần kiểm tra lại hệ mặt cắt.");
                    if(!frames.TryGetValue(sectionName,out var frame))
                    {
                        frame=FindFrame(doc.Database,tr,sectionName);
                        frames.Add(sectionName,frame);
                    }
                    var physical=ReadPhysicalPolygon(poly,frame);
                    double area=MineralSectionPolygonCalculator.ValidateAndMeasure(profile,physical);
                    var ext=poly.GeometricExtents;
                    var result=new RegionGeometry
                    {
                        Area=new MineralSectionArea
                        {
                            SectionName=sectionName,BodyId=bodyId,BodyName=body.Name,
                            PolylineHandle=poly.Handle.ToString(),
                            AreaSquareMetres=area,State="Đã cập nhật hình học"
                        },
                        Physical=physical,
                        CadCenter=new Point3d((ext.MinPoint.X+ext.MaxPoint.X)/2,
                            (ext.MinPoint.Y+ext.MaxPoint.Y)/2,0)
                    };
                    output.Add(result);
                    if(output.Count>5000)throw new InvalidOperationException("Vượt 5.000 vùng khoáng sản.");
                }

                // Cross-body overlap in the same section must never silently double count.
                for(int i=0;i<output.Count;i++)
                    for(int j=i+1;j<output.Count;j++)
                    {
                        if(!string.Equals(output[i].Area.SectionName,output[j].Area.SectionName,
                            StringComparison.OrdinalIgnoreCase))continue;
                        double overlap=MineralSectionPolygonCalculator.OverlapArea(output[i].Physical,output[j].Physical);
                        if(overlap>1e-6)
                            throw new InvalidOperationException("Vùng "+output[i].Area.PolylineHandle+
                                " và "+output[j].Area.PolylineHandle+" chồng lấn "+overlap.ToString("0.###")+
                                " m² trên "+output[i].Area.SectionName+". Cần chỉnh trước khi cập nhật.");
                    }

                // Replace ONLY generated labels tagged by this plugin, not user CAD data.
                // Avoid opening any TIN faces or polylines while finding DBText.
                var textClass=Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(DBText));
                foreach(ObjectId id in ms)
                {
                    if(!id.ObjectClass.IsDerivedFrom(textClass))continue;
                    var text=tr.GetObject(id,OpenMode.ForRead) as DBText;
                    if(text!=null&&TryReadTag(text,LabelRegApp,out _,out _))
                    {
                        text.UpgradeOpen();text.Erase();
                    }
                }
                if(output.Count>0)
                {
                    EnsureRegApp(doc.Database,tr,LabelRegApp);
                    var labelLayer=EnsureLayer(doc.Database,tr,LabelLayer,7);
                    var styles=(TextStyleTable)tr.GetObject(doc.Database.TextStyleTableId,OpenMode.ForRead);
                    var styleId=styles.Has("MV_ARIAL")?styles["MV_ARIAL"]:doc.Database.Textstyle;
                    foreach(var region in output)
                    {
                        var label=new DBText
                        {
                            LayerId=labelLayer,TextStyleId=styleId,
                            Position=region.CadCenter,Height=3,
                            TextString=region.Area.BodyName+": "+region.Area.AreaSquareMetres.ToString("0.00")+" m2"
                        };
                        ms.AppendEntity(label);
                        tr.AddNewlyCreatedDBObject(label,true);
                        label.XData=new ResultBuffer(
                            new TypedValue((int)DxfCode.ExtendedDataRegAppName,LabelRegApp),
                            new TypedValue((int)DxfCode.ExtendedDataAsciiString,region.Area.PolylineHandle),
                            new TypedValue((int)DxfCode.ExtendedDataAsciiString,region.Area.SectionName));
                    }
                }
                tr.Commit();
            }
            state.NotifyChanged();
            try{doc.Editor.Regen();}catch{}
            return output.Select(x=>x.Area).ToList();
        }

        private static void ValidateNoOverlap(Database db,Transaction tr,ObjectId currentId,
            string sectionName,SectionFrame frame,IReadOnlyList<Vec2> candidate)
        {
            var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
            var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            var polyClass=Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Polyline));
            foreach(ObjectId id in ms)
            {
                if(id==currentId||!id.ObjectClass.IsDerivedFrom(polyClass))continue;
                var poly=tr.GetObject(id,OpenMode.ForRead) as Polyline;
                if(poly==null||!TryReadTag(poly,AreaRegApp,out _,out string oldSection)||
                   !string.Equals(sectionName,oldSection,StringComparison.OrdinalIgnoreCase))continue;
                var old=ReadPhysicalPolygon(poly,frame);
                double overlap=MineralSectionPolygonCalculator.OverlapArea(candidate,old);
                if(overlap>1e-6)
                    throw new InvalidOperationException("Vùng mới chồng lên vùng đã gán "+poly.Handle+
                        " với "+overlap.ToString("0.###")+" m². Không tính trùng.");
            }
        }

        private static List<Vec2> ReadPhysicalPolygon(Polyline poly,SectionFrame frame)
        {
            if(!poly.Closed||poly.NumberOfVertices<3)
                throw new InvalidOperationException("Polygon khoáng sản phải khép kín và ít nhất 3 đỉnh.");
            if(Math.Abs(poly.Normal.Z-1)>1e-6||Math.Abs(poly.Elevation)>1e-6)
                throw new InvalidOperationException("Vùng mặt cắt phải vẽ trong mặt phẳng WCS XY, Z=0.");
            var points=new List<Vec2>(poly.NumberOfVertices);
            for(int i=0;i<poly.NumberOfVertices;i++)
            {
                if(Math.Abs(poly.GetBulgeAt(i))>1e-9)
                    throw new InvalidOperationException("Chưa hỗ trợ cung tròn Bulge trong vùng khoáng sản; hãy chuyển thành đoạn thẳng.");
                var p=poly.GetPoint3dAt(i);
                points.Add(new Vec2(p.X-frame.X0,
                    frame.BaseZ+(p.Y-frame.Y0)/frame.VExag));
            }
            return points;
        }

        private static SectionFrame FindFrame(Database db,Transaction tr,string name)
        {
            var bt=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
            var ms=(BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            SectionFrame result=null;
            var textClass=Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(DBText));
            foreach(ObjectId id in ms)
            {
                if(!id.ObjectClass.IsDerivedFrom(textClass))continue;
                var text=tr.GetObject(id,OpenMode.ForRead) as DBText;
                if(text==null||!TryReadFrame(text,name,out var frame))continue;
                if(result!=null)throw new InvalidOperationException("Tên mặt cắt "+name+
                    " xuất hiện nhiều lần. Hãy xóa bản sao mặt cắt hoặc dùng mã riêng.");
                result=frame;
            }
            return result ?? throw new InvalidOperationException(
                "Không tìm thấy tọa độ khung mặt cắt "+name+
                ". Hãy vẽ lại hệ mặt cắt bằng IMSAT VOLUME V2.0 để tạo liên kết.");
        }

        private static bool TryReadFrame(Entity entity,string name,out SectionFrame frame)
        {
            frame=null;
            using(var data=entity.GetXDataForApplication(SectionCadRenderer.FrameRegApp))
            {
                if(data==null)return false;
                var a=data.AsArray();
                if(a.Length<6 || !string.Equals(a[1].Value as string,name,
                    StringComparison.OrdinalIgnoreCase))return false;
                var result=new SectionFrame
                {
                    X0=Convert.ToDouble(a[2].Value),Y0=Convert.ToDouble(a[3].Value),
                    BaseZ=Convert.ToDouble(a[4].Value),VExag=Convert.ToDouble(a[5].Value)
                };
                if(result.VExag<=0||double.IsNaN(result.VExag)||double.IsInfinity(result.VExag))
                    throw new InvalidOperationException("Khung tọa độ mặt cắt có tỷ lệ đứng không hợp lệ.");
                frame=result;return true;
            }
        }

        private static bool TryReadTag(Entity entity,string app,out string first,out string second)
        {
            first=second=null;
            using(var data=entity.GetXDataForApplication(app))
            {
                if(data==null)return false;
                var a=data.AsArray();
                if(a.Length<3)return false;
                first=a[1].Value as string;
                second=a[2].Value as string;
                return !string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(second);
            }
        }

        private static ObjectId EnsureLayer(Database db,Transaction tr,string name,short aci)
        {
            var table=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
            if(table.Has(name))
            {
                var layer=(LayerTableRecord)tr.GetObject(table[name],OpenMode.ForRead);
                if(layer.IsLocked||layer.IsFrozen||layer.IsOff)
                    throw new InvalidOperationException("Layer "+name+" đang khóa/đóng băng/tắt.");
                return table[name];
            }
            table.UpgradeOpen();
            var record=new LayerTableRecord
            {
                Name=name,Color=Color.FromColorIndex(ColorMethod.ByAci,aci)
            };
            var id=table.Add(record);tr.AddNewlyCreatedDBObject(record,true);
            return id;
        }

        private static void EnsureRegApp(Database db,Transaction tr,string name)
        {
            var table=(RegAppTable)tr.GetObject(db.RegAppTableId,OpenMode.ForRead);
            if(table.Has(name))return;
            table.UpgradeOpen();
            var record=new RegAppTableRecord{Name=name};
            table.Add(record);tr.AddNewlyCreatedDBObject(record,true);
        }
    }
}
