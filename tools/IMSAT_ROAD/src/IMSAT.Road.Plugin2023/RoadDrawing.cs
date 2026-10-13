using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using IMSAT.Road.Core.Geometry;
using IMSAT.Road.Core.Terrain;
using IMSAT.Road.TerrainBridge;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Surface;

namespace IMSAT.Road.Plugin2023
{
    /// <summary>
    /// Preview drawing operations. Never changes the source TIN or source centreline.
    /// Uses one existing 3DFACE terrain (can be produced by IMSAT VOLUME).
    /// </summary>
    internal static class RoadDrawing
    {
        internal sealed class RoadDocument
        {
            internal ObjectId AlignmentId = ObjectId.Null;
            internal PolylineAlignment Alignment;
            internal string RoadName = "Tuyen 1";
            internal string ExistingTinLayer = "MV_TIN_HIENTRANG";
        }

        private static readonly Dictionary<Database, RoadDocument> Documents =
            new Dictionary<Database, RoadDocument>();

        private static RoadDocument State(Database db)
        {
            RoadDocument state;
            if (!Documents.TryGetValue(db, out state))
                Documents[db] = state = new RoadDocument();
            return state;
        }

        private static Document Current()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("Hay mo mot ban ve DWG.");
            return doc;
        }

        private static PolylineAlignment GetAlignment(Document doc)
        {
            var state = State(doc.Database);
            if (state.AlignmentId.IsNull || !state.AlignmentId.IsValid || state.AlignmentId.IsErased)
                throw new InvalidOperationException("Chua khai bao tim tuyen. Hay su dung 'Tao tuyen' (IR_TT).");
            // Read the original CAD polyline on each command so grip edits are not stale.
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(state.AlignmentId, OpenMode.ForRead) as Polyline;
                if (ent == null) throw new InvalidOperationException("Tim tuyen khong con la LWPOLYLINE.");
                var points = new List<PlanPoint>();
                if (ent.Closed) throw new InvalidOperationException("Tim tuyen khong duoc la polyline khep kin.");
                for (int i = 0; i < ent.NumberOfVertices; i++)
                {
                    if (Math.Abs(ent.GetBulgeAt(i)) > 1e-10)
                        throw new InvalidOperationException("Polyline chua cung cong. Preview chua ho tro bulge; khong thay cung bang day cung.");
                    var p = ent.GetPoint3dAt(i);
                    points.Add(new PlanPoint(p.X, p.Y));
                }
                var built = new PolylineAlignment(points, state.Alignment == null ? 0 : state.Alignment.StartStation);
                tr.Commit();
                state.Alignment = built;
                return built;
            }
        }

        internal static void SelectAlignment()
        {
            var doc = Current();
            var opts = new PromptEntityOptions("\nChon LWPOLYLINE tim tuyen (chua co cung cong): ");
            opts.SetRejectMessage("\nChi chon LWPOLYLINE.");
            opts.AddAllowedClass(typeof(Polyline), true);
            var response = doc.Editor.GetEntity(opts);
            if (response.Status != PromptStatus.OK) return;
            var state = State(doc.Database);
            var oldId = state.AlignmentId;
            var oldAlignment = state.Alignment;
            state.AlignmentId = response.ObjectId;
            state.Alignment = null;
            try
            {
                // Start station first, and only save if the selected alignment is valid.
                var start = new PromptDoubleOptions("\nLy trinh goc, m <0>: ")
                {
                    AllowNegative = true, AllowZero = true, AllowNone = true, DefaultValue = 0
                };
                var pr = doc.Editor.GetDouble(start);
                if (pr.Status == PromptStatus.Cancel) { state.AlignmentId = oldId; state.Alignment = oldAlignment; return; }
                double station = pr.Status == PromptStatus.OK ? pr.Value : 0;
                state.Alignment = new PolylineAlignment(ReadVertices(doc.Database, response.ObjectId), station);
                var name = new PromptStringOptions("\nTen tuyen <Tuyen 1>: ") { AllowSpaces = true };
                var result = doc.Editor.GetString(name);
                if (result.Status == PromptStatus.Cancel) { state.AlignmentId = oldId; state.Alignment = oldAlignment; return; }
                state.RoadName = result.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(result.StringResult)
                    ? result.StringResult.Trim() : "Tuyen 1";
                doc.Editor.WriteMessage("\nIMSAT ROAD: tim " + state.RoadName +
                    " dai " + state.Alignment.Length.ToString("F3") + "m; goc " + station.ToString("F3") +
                    ". Da san sang phat sinh coc.");
            }
            catch
            {
                state.AlignmentId = oldId;
                state.Alignment = oldAlignment;
                throw;
            }
        }

        private static IReadOnlyList<PlanPoint> ReadVertices(Database db, ObjectId id)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                if (pl == null || pl.Closed) throw new InvalidOperationException("Tim tuyen phai la LWPOLYLINE mo.");
                var vertices = new List<PlanPoint>();
                for (int i = 0; i < pl.NumberOfVertices; i++)
                {
                    if (Math.Abs(pl.GetBulgeAt(i)) > 1e-10)
                        throw new InvalidOperationException("Preview chua ho tro duong cong bulge; can cong cu CN.");
                    var p = pl.GetPoint3dAt(i);
                    vertices.Add(new PlanPoint(p.X, p.Y));
                }
                tr.Commit();
                return vertices;
            }
        }

        internal static void SelectTerrainLayer()
        {
            var doc = Current();
            var opts = new PromptEntityOptions("\nChon 1 doi tuong 3DFACE thuoc TIN hien trang: ");
            opts.SetRejectMessage("\nCan chon 3DFACE.");
            opts.AddAllowedClass(typeof(Face), true);
            var pr = doc.Editor.GetEntity(opts);
            if (pr.Status != PromptStatus.OK) return;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var face = tr.GetObject(pr.ObjectId, OpenMode.ForRead) as Face;
                if (face == null) throw new InvalidOperationException("Doi tuong khong phai 3DFACE.");
                State(doc.Database).ExistingTinLayer = face.Layer;
                tr.Commit();
            }
            doc.Editor.WriteMessage("\nIMSAT ROAD: su dung TIN hien trang tren layer " +
                State(doc.Database).ExistingTinLayer + ". Khong can TIN thiet ke.");
        }

        private static IExistingTerrain ReadExistingTerrain(Document doc)
        {
            var state = State(doc.Database);
            var triangles = new List<Triangle3>();
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    if (!id.IsValid || id.IsErased) continue;
                    var face = tr.GetObject(id, OpenMode.ForRead) as Face;
                    if (face == null || !string.Equals(face.Layer, state.ExistingTinLayer, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var a = face.GetVertexAt(0);
                    var b = face.GetVertexAt(1);
                    var c = face.GetVertexAt(2);
                    var d = face.GetVertexAt(3);
                    var one = new Triangle3(new Vec3(a.X,a.Y,a.Z),new Vec3(b.X,b.Y,b.Z),new Vec3(c.X,c.Y,c.Z));
                    if (one.Area2D > 1e-10) triangles.Add(one);
                    if (c.DistanceTo(d) > 1e-8 && a.DistanceTo(d) > 1e-8)
                    {
                        var two = new Triangle3(new Vec3(a.X,a.Y,a.Z),new Vec3(c.X,c.Y,c.Z),new Vec3(d.X,d.Y,d.Z));
                        if (two.Area2D > 1e-10) triangles.Add(two);
                    }
                }
                tr.Commit();
            }
            if (triangles.Count == 0)
                throw new InvalidOperationException("Khong co 3DFACE hop le tren layer '" + state.ExistingTinLayer +
                    "'. Hay dung IR_DH de chon TIN hien trang da dung hoac tao TIN bang IMSAT VOLUME.");
            return new MiningVolumeExistingTerrainAdapter(
                new TinSurface(state.ExistingTinLayer, triangles, Array.Empty<ValidationIssue>()));
        }

        private static double AskPositive(Editor ed, string caption, double initial, double upper)
        {
            var pr = ed.GetDouble(new PromptDoubleOptions("\n" + caption + " <" + initial + ">: ")
            {
                AllowNone = true, AllowNegative = false, AllowZero = false, DefaultValue = initial
            });
            if (pr.Status == PromptStatus.Cancel) return double.NaN;
            var value = pr.Status == PromptStatus.OK ? pr.Value : initial;
            if (value > upper) throw new InvalidOperationException(caption + " khong duoc qua " + upper + ".");
            return value;
        }

        private static List<double> Stations(PolylineAlignment a, double step, int max)
        {
            var count = (int)Math.Ceiling(a.Length / step);
            if (count + 1 > max) throw new InvalidOperationException(
                "So mat cat/coc vuot " + max + ". Tang khoang cach de giu on dinh AutoCAD.");
            var result = new List<double>();
            for (int i = 0; i < count; i++) result.Add(a.StartStation + i * step);
            result.Add(a.StartStation + a.Length);
            return result;
        }

        private static ObjectId Layer(Transaction tr, Database db, string name, short color)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (table.Has(name)) return table[name];
            table.UpgradeOpen();
            var rec = new LayerTableRecord
            {
                Name = name,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByAci, color)
            };
            var id = table.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
            return id;
        }

        private static void Add(Transaction tr, BlockTableRecord space, Entity entity, ObjectId layer)
        {
            entity.LayerId = layer;
            space.AppendEntity(entity);
            tr.AddNewlyCreatedDBObject(entity, true);
        }

        private static void Label(Transaction tr, BlockTableRecord space,
            ObjectId layer, double x, double y, string value, double size)
        {
            var t = new DBText { Position = new Point3d(x, y, 0), TextString = value, Height = size };
            Add(tr, space, t, layer);
        }

        private static string Chainage(double station)
        {
            double positive = Math.Abs(station);
            int kilometers = (int)Math.Floor(positive / 1000.0);
            return (station < 0 ? "-" : "") + "Km" + kilometers + "+" +
                (positive - kilometers * 1000).ToString("000.000");
        }

        internal static void CreateStations()
        {
            var doc = Current();
            var a = GetAlignment(doc);
            var spacing = AskPositive(doc.Editor, "Khoang cach coc, m", 20, 10000);
            if (double.IsNaN(spacing)) return;
            var list = Stations(a, spacing, 2001);
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var layer = Layer(tr, doc.Database, "IR_COC", 2);
                var textLayer = Layer(tr, doc.Database, "IR_NHAN_COC", 7);
                var space = (BlockTableRecord)tr.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(doc.Database), OpenMode.ForWrite);
                foreach (double station in list)
                {
                    var p = a.PositionAt(station);
                    var n = p.LeftNormal;
                    var half = 0.75;
                    Add(tr, space, new Line(new Point3d(p.Point.X-n.X*half,p.Point.Y-n.Y*half,0),
                        new Point3d(p.Point.X+n.X*half,p.Point.Y+n.Y*half,0)), layer);
                    Label(tr, space, textLayer, p.Point.X + n.X, p.Point.Y + n.Y,
                        Chainage(station), 1.8);
                }
                tr.Commit();
            }
            doc.Editor.WriteMessage("\nIMSAT ROAD: da ve " + list.Count + " coc tren tim tuyen. " +
                "Co the xoa layer IR_COC va IR_NHAN_COC de ve lai.");
        }

        internal static void DrawLongitudinal()
        {
            var doc = Current();
            var alignment = GetAlignment(doc);
            var step = AskPositive(doc.Editor, "Buoc lay mau trac doc, m", 10, 1000);
            if (double.IsNaN(step)) return;
            var stations = Stations(alignment, step, 3001);
            var terrain = ReadExistingTerrain(doc);
            var origin = doc.Editor.GetPoint("\nChi diem goc ve trac doc: ");
            if (origin.Status != PromptStatus.OK) return;
            var points = new List<Tuple<double,double>>();
            var misses = 0;
            foreach (var s in stations)
            {
                var xy = alignment.PointAt(s);
                double z;
                if (terrain.TryElevation(xy.X, xy.Y, out z))
                    points.Add(Tuple.Create(s-alignment.StartStation,z));
                else
                {
                    points.Add(null);
                    misses++;
                }
            }
            if (misses == stations.Count) throw new InvalidOperationException(
                "TIN hien trang khong phu tim tuyen; khong ve trac doc ao.");
            var zs = points.Where(p => p != null).Select(p => p.Item2).ToList();
            var minZ = Math.Floor(zs.Min() - 1.0);
            var ox = origin.Value.X;
            var oy = origin.Value.Y;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var l = Layer(tr,doc.Database,"IR_TD_TUNHIEN",3);
                var grid = Layer(tr,doc.Database,"IR_TD_BANG",8);
                var name = Layer(tr,doc.Database,"IR_TD_CHU",7);
                var space = (BlockTableRecord)tr.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(doc.Database),OpenMode.ForWrite);
                Add(tr,space,new Line(new Point3d(ox,oy,0),
                    new Point3d(ox+alignment.Length,oy,0)),grid);
                for (int i=0;i<stations.Count;i++)
                {
                    if (points[i] == null) continue;
                    double px=ox+points[i].Item1;
                    double py=oy+(points[i].Item2-minZ)*10.0; // H:V = 1:10 preview
                    if (i>0 && points[i-1]!=null)
                    {
                        double ppx=ox+points[i-1].Item1;
                        double ppy=oy+(points[i-1].Item2-minZ)*10.0;
                        Add(tr,space,new Line(new Point3d(ppx,ppy,0),new Point3d(px,py,0)),l);
                    }
                    if (i==0 || i==stations.Count-1 || i%5==0)
                    {
                        Add(tr,space,new Line(new Point3d(px,oy,0),new Point3d(px,py,0)),grid);
                        Label(tr,space,name,px,oy-4.0,Chainage(stations[i]),1.5);
                    }
                }
                Label(tr,space,name,ox,oy-9.0,
                    "IMSAT ROAD - TRAC DOC TU NHIEN: "+State(doc.Database).RoadName+
                    " | MSS="+minZ.ToString("F2")+" | Phong dai dung 10x",2.0);
                tr.Commit();
            }
            doc.Editor.WriteMessage("\nIMSAT ROAD: trac doc hien trang: " +
                (stations.Count-misses)+" diem co cao do, "+misses+
                " diem ngoai TIN. Chua thiet ke duong do.");
        }

        internal static void DrawCrossSections()
        {
            var doc=Current();
            var alignment=GetAlignment(doc);
            var stationStep=AskPositive(doc.Editor,"Khoang cach trac ngang, m",20,10000);
            if(double.IsNaN(stationStep))return;
            var side=AskPositive(doc.Editor,"Nua be rong moi ben, m",20,1000);
            if(double.IsNaN(side))return;
            var sampleStep=AskPositive(doc.Editor,"Buoc lay mau ngang, m",2,100);
            if(double.IsNaN(sampleStep))return;
            var stations=Stations(alignment,stationStep,151);
            var terrain=ReadExistingTerrain(doc);
            var picked=doc.Editor.GetPoint("\nChi diem goc bo tri trac ngang: ");
            if(picked.Status!=PromptStatus.OK)return;
            var sections=new List<NaturalSection>(stations.Count);
            foreach(var station in stations)
                sections.Add(ExistingTerrainSections.Sample(alignment,terrain,station,side,side,sampleStep));
            var incomplete=sections.Count(x=>x.Warnings.Count>0);
            using(var tr=doc.Database.TransactionManager.StartTransaction())
            {
                var line=Layer(tr,doc.Database,"IR_TN_TUNHIEN",3);
                var grid=Layer(tr,doc.Database,"IR_TN_BANG",8);
                var label=Layer(tr,doc.Database,"IR_TN_CHU",7);
                var space=(BlockTableRecord)tr.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(doc.Database),OpenMode.ForWrite);
                for(int j=0;j<sections.Count;j++)
                {
                    var sect=sections[j];
                    var valid=sect.Points.Where(x=>x.ExistingElevation.HasValue).ToList();
                    if(valid.Count==0)continue;
                    double baseZ=Math.Floor(valid.Min(x=>x.ExistingElevation.Value)-1);
                    double ox=picked.Value.X+(j%2)*(2*side+25);
                    double oy=picked.Value.Y-(j/2)*65;
                    Add(tr,space,new Line(new Point3d(ox-side,oy,0),
                        new Point3d(ox+side,oy,0)),grid);
                    NaturalSectionPoint prior=null;
                    foreach(var pt in sect.Points)
                    {
                        if(!pt.ExistingElevation.HasValue) {prior=null;continue;}
                        double xx=ox+pt.OffsetMetres;
                        double yy=oy+(pt.ExistingElevation.Value-baseZ)*2.0;
                        if(prior!=null)
                        {
                            double prevX=ox+prior.OffsetMetres;
                            double prevY=oy+(prior.ExistingElevation.Value-baseZ)*2.0;
                            Add(tr,space,new Line(new Point3d(prevX,prevY,0),
                                new Point3d(xx,yy,0)),line);
                        }
                        prior=pt;
                    }
                    Label(tr,space,label,ox-side,oy-5.0,
                        "TRAC NGANG "+Chainage(sect.Station)+" | MSS="+baseZ.ToString("F2"),1.8);
                }
                tr.Commit();
            }
            doc.Editor.WriteMessage("\nIMSAT ROAD: ve "+sections.Count+
                " mat cat hien trang, "+incomplete+
                " mat cat thieu du lieu dia hinh. Chua ap trac ngang thiet ke.");
        }

        internal static void SelfTest()
        {
            var a=new PolylineAlignment(new[]{new PlanPoint(0,0),new PlanPoint(100,0)},0);
            if(Math.Abs(a.PointAt(50).X-50)>1e-8)
                throw new InvalidOperationException("IR_SELFTEST: interpolation failed.");
            Current().Editor.WriteMessage("\nIMSAT ROAD [CORE SELFTEST PASS]: Add-in da nap, tuyen 2D hoat dong. "+
                "De kiem thu CAD hay chay IR_TT, IR_DH, IR_COC, IR_TD, IR_TN.");
        }
    }
}
