using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Sections;

namespace MiningVolume2023.Services
{
    internal static class SectionCadRenderer
    {
        private const string PreviewLayer = "MV_MC_BINHDO";
        private const string ExistingLayer = "MV_MC_HIENTRANG";
        private const string DesignLayer = "MV_MC_THIETKE";
        private const string AxisLayer = "MV_MC_TRUC";
        private const string TextLayer = "MV_MC_TEXT";
        private const string ArialStyle = "MV_ARIAL";

        public static void ReplacePreview(Database db, SectionSystem system)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                ObjectId layerId = EnsureLayer(db, tr, PreviewLayer, 2);
                ObjectId styleId = EnsureArialStyle(db, tr);
                var ms = ModelSpace(db, tr, true);
                EraseOnLayers(ms, tr, new[] { layerId });

                foreach (var line in system.Lines)
                {
                    foreach (var span in line.Spans)
                    {
                        var a = line.PointAt(span.StartT);
                        var b = line.PointAt(span.EndT);
                        AddLine(ms, tr, new Point3d(a.X, a.Y, 0), new Point3d(b.X, b.Y, 0), layerId);
                    }
                    if (line.Spans.Count > 0)
                    {
                        var p = line.PointAt(line.Spans[0].StartT);
                        var txt = new DBText
                        {
                            Position = new Point3d(p.X, p.Y, 0),
                            TextString = line.Name,
                            Height = 3.0,
                            Rotation = Math.Atan2(line.Direction.Y, line.Direction.X),
                            LayerId = layerId,
                            TextStyleId = styleId
                        };
                        ms.AppendEntity(txt); tr.AddNewlyCreatedDBObject(txt, true);
                    }
                }
                tr.Commit();
            }
        }

        public static void ClearPreview(Database db)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(PreviewLayer)) { tr.Commit(); return; }
                var ms = ModelSpace(db, tr, true);
                EraseOnLayers(ms, tr, new[] { lt[PreviewLayer] });
                tr.Commit();
            }
        }

        public static void ReplaceProfiles(Database db, IReadOnlyList<SectionProfile> profiles, Point3d insertion,
            double horizontalScale, double verticalScale, double levelStep)
        {
            if (profiles == null || profiles.Count == 0) throw new InvalidOperationException("Chưa có dữ liệu mặt cắt.");
            if (horizontalScale <= 0 || verticalScale <= 0) throw new ArgumentOutOfRangeException("Tỷ lệ mặt cắt phải lớn hơn 0.");
            if (levelStep <= 0) levelStep = 5.0;
            double vExag = horizontalScale / verticalScale;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                ObjectId exLayer = EnsureLayer(db, tr, ExistingLayer, 1); // đỏ
                ObjectId deLayer = EnsureLayer(db, tr, DesignLayer, 5);   // xanh dương
                ObjectId axLayer = EnsureLayer(db, tr, AxisLayer, 7);
                ObjectId txLayer = EnsureLayer(db, tr, TextLayer, 7);
                ObjectId styleId = EnsureArialStyle(db, tr);
                var ms = ModelSpace(db, tr, true);
                EraseOnLayers(ms, tr, new[] { exLayer, deLayer, axLayer, txLayer });

                double baseX = insertion.X;
                double currentY = insertion.Y;
                foreach (var p in profiles)
                {
                    double width = Math.Max(1.0, p.Line.MaxT - p.Line.MinT);
                    double baseZ = Math.Floor(p.MinZ / levelStep) * levelStep;
                    double topZ = Math.Ceiling(p.MaxZ / levelStep) * levelStep;
                    if (topZ <= baseZ) topZ = baseZ + levelStep;
                    double height = (topZ - baseZ) * vExag;
                    double x0 = baseX, y0 = currentY;

                    AddLine(ms, tr, new Point3d(x0, y0, 0), new Point3d(x0 + width, y0, 0), axLayer);
                    AddLine(ms, tr, new Point3d(x0, y0, 0), new Point3d(x0, y0 + height, 0), axLayer);
                    AddText(ms, tr, styleId, txLayer, new Point3d(x0, y0 + height + 8, 0), p.Line.Name, 3.0, 0);
                    AddText(ms, tr, styleId, txLayer, new Point3d(x0, y0 + height + 3.5, 0),
                        $"F đào = {p.CutArea:0.00} m²   F đắp = {p.FillArea:0.00} m²", 3.0, 0);

                    double xTick = NiceStep(width / 5.0);
                    for (double x = 0; x <= width + 1e-9; x += xTick)
                    {
                        AddLine(ms, tr, new Point3d(x0 + x, y0 - 1.5, 0), new Point3d(x0 + x, y0 + 1.5, 0), axLayer);
                        AddText(ms, tr, styleId, txLayer, new Point3d(x0 + x, y0 - 5.0, 0), x.ToString("0.##"), 3.0, 0);
                    }
                    for (double z = baseZ; z <= topZ + 1e-9; z += levelStep)
                    {
                        double yy = y0 + (z - baseZ) * vExag;
                        AddLine(ms, tr, new Point3d(x0 - 1.5, yy, 0), new Point3d(x0 + 1.5, yy, 0), axLayer);
                        AddText(ms, tr, styleId, txLayer, new Point3d(x0 - 12.0, yy - 1.5, 0), z.ToString("0.##"), 3.0, 0);
                    }
                    AddText(ms, tr, styleId, txLayer, new Point3d(x0 + width + 4, y0 - 1.5, 0), "X (m)", 3.0, 0);
                    AddText(ms, tr, styleId, txLayer, new Point3d(x0 - 2, y0 + height + 3, 0), "Z (m)", 3.0, Math.PI / 2);
                    AddText(ms, tr, styleId, txLayer, new Point3d(x0 + width * 0.55, y0 + height + 8, 0),
                        $"TL ngang 1/{horizontalScale:0}   TL đứng 1/{verticalScale:0}", 3.0, 0);

                    // Do not create two AutoCAD LINE entities for every tiny TIN
                    // segment. On large TINs that can mean hundreds of thousands of
                    // DBObjects for 200+ sections. Group each contiguous profile run
                    // into one Polyline per surface instead.
                    AddProfilePolylines(ms, tr, p, x0, y0, baseZ, vExag, exLayer, deLayer);

                    currentY -= height + 45.0;
                }
                tr.Commit();
            }
        }

        private static BlockTableRecord ModelSpace(Database db, Transaction tr, bool write)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], write ? OpenMode.ForWrite : OpenMode.ForRead);
        }

        private static void EraseOnLayers(BlockTableRecord ms, Transaction tr, IEnumerable<ObjectId> layers)
        {
            var set = new HashSet<ObjectId>(layers);
            foreach (ObjectId id in ms)
            {
                var e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (e != null && set.Contains(e.LayerId)) { e.UpgradeOpen(); e.Erase(); }
            }
        }

        private static ObjectId EnsureLayer(Database db, Transaction tr, string name, short color)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return lt[name];
            lt.UpgradeOpen();
            var rec = new LayerTableRecord { Name = name, Color = Color.FromColorIndex(ColorMethod.ByAci, color) };
            ObjectId id = lt.Add(rec); tr.AddNewlyCreatedDBObject(rec, true); return id;
        }

        private static ObjectId EnsureArialStyle(Database db, Transaction tr)
        {
            var tt = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (tt.Has(ArialStyle)) return tt[ArialStyle];
            tt.UpgradeOpen();
            var rec = new TextStyleTableRecord { Name = ArialStyle, FileName = "arial.ttf", TextSize = 0.0 };
            ObjectId id = tt.Add(rec); tr.AddNewlyCreatedDBObject(rec, true); return id;
        }

        private static void AddLine(BlockTableRecord ms, Transaction tr, Point3d a, Point3d b, ObjectId layer)
        {
            var line = new Line(a, b) { LayerId = layer };
            ms.AppendEntity(line); tr.AddNewlyCreatedDBObject(line, true);
        }

        private static void AddProfilePolylines(
            BlockTableRecord ms,
            Transaction tr,
            SectionProfile profile,
            double x0,
            double y0,
            double baseZ,
            double vExag,
            ObjectId existingLayer,
            ObjectId designLayer)
        {
            if (profile == null || profile.Segments == null || profile.Segments.Count == 0)
                return;

            var existing = new List<Point2d>();
            var design = new List<Point2d>();
            double previousS1 = double.NaN;

            foreach (var segment in profile.Segments)
            {
                bool contiguous =
                    existing.Count > 0 &&
                    !double.IsNaN(previousS1) &&
                    Math.Abs(segment.S0 - previousS1) <= 1e-7;

                if (!contiguous)
                {
                    FlushProfilePolyline(ms, tr, existing, existingLayer);
                    FlushProfilePolyline(ms, tr, design, designLayer);
                    existing.Clear();
                    design.Clear();

                    existing.Add(new Point2d(
                        x0 + segment.S0,
                        y0 + (segment.ExistingZ0 - baseZ) * vExag));
                    design.Add(new Point2d(
                        x0 + segment.S0,
                        y0 + (segment.DesignZ0 - baseZ) * vExag));
                }

                existing.Add(new Point2d(
                    x0 + segment.S1,
                    y0 + (segment.ExistingZ1 - baseZ) * vExag));
                design.Add(new Point2d(
                    x0 + segment.S1,
                    y0 + (segment.DesignZ1 - baseZ) * vExag));
                previousS1 = segment.S1;
            }

            FlushProfilePolyline(ms, tr, existing, existingLayer);
            FlushProfilePolyline(ms, tr, design, designLayer);
        }

        private static void FlushProfilePolyline(
            BlockTableRecord ms,
            Transaction tr,
            List<Point2d> points,
            ObjectId layer)
        {
            if (points == null || points.Count < 2) return;
            var poly = new Polyline(points.Count) { LayerId = layer };
            for (int i = 0; i < points.Count; i++)
                poly.AddVertexAt(i, points[i], 0.0, 0.0, 0.0);
            ms.AppendEntity(poly);
            tr.AddNewlyCreatedDBObject(poly, true);
        }

        private static void AddText(BlockTableRecord ms, Transaction tr, ObjectId style, ObjectId layer, Point3d p, string value, double height, double rotation)
        {
            var text = new DBText { Position = p, TextString = value, Height = height, Rotation = rotation, TextStyleId = style, LayerId = layer };
            ms.AppendEntity(text); tr.AddNewlyCreatedDBObject(text, true);
        }

        private static double NiceStep(double raw)
        {
            if (raw <= 0) return 1;
            double p = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double n = raw / p;
            double q = n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10;
            return q * p;
        }
    }
}