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
        private const string PreviewLabelLayer = "MV_MC_BINHDO_TEN";
        private const string ExistingLayer = "MV_MC_HIENTRANG";
        private const string DesignLayer = "MV_MC_THIETKE";
        private const string AxisLayer = "MV_MC_TRUC";
        private const string TextLayer = "MV_MC_TEXT";
        internal const string FrameRegApp = "IMSAT_MV2_MC_FRAME";
        internal const string EditRegApp = "IMSAT_MV2_MC_EDIT";
        internal const string AreaRegApp = "IMSAT_MV2_MC_AREA";
        private const string ArialStyle = "MV_ARIAL";

        public static void ReplacePreview(Database db, SectionSystem system)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                ObjectId layerId = EnsureLayer(db, tr, PreviewLayer, 2);
                ObjectId namesLayerId = EnsureLayer(db, tr, PreviewLabelLayer, 2);
                ObjectId styleId = EnsureArialStyle(db, tr);
                EnsureFrameRegApp(db, tr);
                var ms = ModelSpace(db, tr, true);
                EraseOnLayers(ms, tr, new[] { layerId, namesLayerId });

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
                            // Draw labels outside the dense TIN and route
                            // crossings; keep a separate toggleable label layer.
                            Position = new Point3d(p.X-18.0*line.Direction.X,
                                p.Y-18.0*line.Direction.Y,0),
                            TextString = line.Name,
                            Height = 3.0,
                            Rotation = Math.Atan2(line.Direction.Y, line.Direction.X),
                            LayerId = namesLayerId,
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
                if (!lt.Has(PreviewLayer) && !lt.Has(PreviewLabelLayer))
                { tr.Commit(); return; }
                var ms = ModelSpace(db, tr, true);
                var clear = new List<ObjectId>();
                if(lt.Has(PreviewLayer))clear.Add(lt[PreviewLayer]);
                if(lt.Has(PreviewLabelLayer))clear.Add(lt[PreviewLabelLayer]);
                EraseOnLayers(ms, tr, clear);
                tr.Commit();
            }
        }

        public static void ReplaceProfiles(Database db, IReadOnlyList<SectionProfile> profiles, Point3d insertion,
            double horizontalScale, double verticalScale, double levelStep, double layoutGap = 90.0)
        {
            ReplaceProfilesBatched(db, profiles, insertion, horizontalScale, verticalScale, levelStep, 0, profiles?.Count ?? 0, true, layoutGap);
        }

        public static double ReplaceProfilesBatched(
            Database db,
            IReadOnlyList<SectionProfile> profiles,
            Point3d insertion,
            double horizontalScale,
            double verticalScale,
            double levelStep,
            int startIndex,
            int count,
            bool clearFirst,
            double layoutGap = 90.0,
            double? batchStartY = null,
            Action pollCancellation = null)
        {
            if (profiles == null || profiles.Count == 0) throw new InvalidOperationException("Chưa có dữ liệu mặt cắt.");
            if (double.IsNaN(horizontalScale) || double.IsInfinity(horizontalScale) ||
                double.IsNaN(verticalScale) || double.IsInfinity(verticalScale) ||
                horizontalScale < 1 || verticalScale < 1)
                throw new ArgumentOutOfRangeException(
                    "Tỷ lệ ngang/đứng phải là số dương hợp lệ.");
            if (levelStep <= 0 || double.IsNaN(levelStep) || double.IsInfinity(levelStep))
                throw new ArgumentOutOfRangeException(nameof(levelStep),
                    "Bước chia trục cao độ trên mặt cắt phải lớn hơn 0.");
            if (double.IsNaN(layoutGap) || double.IsInfinity(layoutGap) || layoutGap < 30.0)
                throw new ArgumentOutOfRangeException(nameof(layoutGap), "Giãn cách bố trí mặt cắt trên CAD phải từ 30 đơn vị trở lên.");
            double vExag = horizontalScale / verticalScale;

            if (startIndex < 0 || startIndex > profiles.Count) throw new ArgumentOutOfRangeException(nameof(startIndex));
            count = Math.Max(0, Math.Min(count, profiles.Count - startIndex));

            using (var tr = db.TransactionManager.StartTransaction())
            {
                EnsureFrameRegApp(db, tr);
                ObjectId exLayer = EnsureLayer(db, tr, ExistingLayer, 1); // đỏ
                ObjectId deLayer = EnsureLayer(db, tr, DesignLayer, 5);   // xanh dương
                ObjectId axLayer = EnsureLayer(db, tr, AxisLayer, 7);
                ObjectId txLayer = EnsureLayer(db, tr, TextLayer, 7);
                ObjectId styleId = EnsureArialStyle(db, tr);
                var ms = ModelSpace(db, tr, true);
                if (clearFirst)
                    EraseOnLayers(ms, tr, new[] { exLayer, deLayer, axLayer, txLayer }, pollCancellation);

                double baseX = insertion.X;
                double currentY = batchStartY ?? insertion.Y;

                // Independent callers can reconstruct the origin. The normal
                // export carries the exact previous ending Y and skips this scan.
                for (int k = 0; !batchStartY.HasValue && k < startIndex; k++)
                {
                    var prev = profiles[k];
                    double prevBaseZ = Math.Floor(prev.MinZ / levelStep) * levelStep;
                    double prevTopZ = Math.Ceiling(prev.MaxZ / levelStep) * levelStep;
                    if (prevTopZ <= prevBaseZ) prevTopZ = prevBaseZ + levelStep;
                    currentY -= (prevTopZ - prevBaseZ) * vExag + layoutGap;
                }

                for (int pi = startIndex; pi < startIndex + count; pi++)
                {
                    pollCancellation?.Invoke();
                    var p = profiles[pi];
                    double width = Math.Max(1.0, p.Line.MaxT - p.Line.MinT);
                    double baseZ = Math.Floor(p.MinZ / levelStep) * levelStep;
                    double topZ = Math.Ceiling(p.MaxZ / levelStep) * levelStep;
                    if (topZ <= baseZ) topZ = baseZ + levelStep;
                    var elevationTicks = EnsureElevationTickBudget(baseZ, topZ, levelStep, p.Line.Name);
                    double height = (topZ - baseZ) * vExag;
                    double x0 = baseX, y0 = currentY;
                    double rightAxisX = x0 + width;

                    AddLine(ms, tr, new Point3d(x0, y0, 0), new Point3d(rightAxisX, y0, 0), axLayer);
                    AddLine(ms, tr, new Point3d(x0, y0, 0), new Point3d(x0, y0 + height, 0), axLayer);
                    // Opposite elevation axis: same origin, height and linear Z transform.
                    AddLine(ms, tr, new Point3d(rightAxisX, y0, 0),
                        new Point3d(rightAxisX, y0 + height, 0), axLayer);
                    var sectionTitle = AddText(ms, tr, styleId, txLayer,
                        new Point3d(x0, y0 + height + 8, 0), p.Line.Name, 3.0, 0);
                    // Saved with native CAD text to associate editable seam polygons
                    // with a stable section coordinate frame after DWG reopen.
                    sectionTitle.XData = new ResultBuffer(
                        new TypedValue((int)DxfCode.ExtendedDataRegAppName, FrameRegApp),
                        new TypedValue((int)DxfCode.ExtendedDataAsciiString, p.Line.Name),
                        new TypedValue((int)DxfCode.ExtendedDataReal, x0),
                        new TypedValue((int)DxfCode.ExtendedDataReal, y0),
                        new TypedValue((int)DxfCode.ExtendedDataReal, baseZ),
                        new TypedValue((int)DxfCode.ExtendedDataReal, vExag));
                    var areaLabel=AddText(ms, tr, styleId, txLayer, new Point3d(x0, y0 + height + 3.5, 0),
                        $"F đào = {p.CutArea:0.00} m²   F đắp = {p.FillArea:0.00} m²", 3.0, 0);
                    areaLabel.XData=new ResultBuffer(
                        new TypedValue((int)DxfCode.ExtendedDataRegAppName,AreaRegApp),
                        new TypedValue((int)DxfCode.ExtendedDataAsciiString,p.Line.Name),
                        new TypedValue((int)DxfCode.ExtendedDataAsciiString,db.FingerprintGuid.ToString()),
                        new TypedValue((int)DxfCode.ExtendedDataAsciiString,areaLabel.Handle.ToString()));

                    double xTick = NiceStep(width / 5.0);
                    for (double x = 0; x <= width + 1e-9; x += xTick)
                    {
                        AddLine(ms, tr, new Point3d(x0 + x, y0 - 1.5, 0), new Point3d(x0 + x, y0 + 1.5, 0), axLayer);
                        AddText(ms, tr, styleId, txLayer, new Point3d(x0 + x, y0 - 5.0, 0), x.ToString("0.##"), 3.0, 0);
                    }
                    foreach (double z in elevationTicks)
                    {
                        pollCancellation?.Invoke();
                        // Both sides share the same z and yy. Never independently
                        // round/interpolate the opposite scale.
                        double yy = y0 + (z - baseZ) * vExag;
                        string elevation = z.ToString("0.##");
                        AddLine(ms, tr, new Point3d(x0 - 1.5, yy, 0),
                            new Point3d(x0 + 1.5, yy, 0), axLayer);
                        AddLine(ms, tr, new Point3d(rightAxisX - 1.5, yy, 0),
                            new Point3d(rightAxisX + 1.5, yy, 0), axLayer);
                        AddText(ms, tr, styleId, txLayer, new Point3d(x0 - 12.0, yy - 1.5, 0),
                            elevation, 3.0, 0);
                        AddText(ms, tr, styleId, txLayer, new Point3d(rightAxisX + 4.0, yy - 1.5, 0),
                            elevation, 3.0, 0);
                    }
                    AddText(ms, tr, styleId, txLayer,
                        new Point3d(x0 + width / 2 - 4, y0 - 12, 0), "X (m)", 3.0, 0);
                    AddText(ms, tr, styleId, txLayer, new Point3d(x0 - 2, y0 + height + 3, 0),
                        "Z (m)", 3.0, Math.PI / 2);
                    AddText(ms, tr, styleId, txLayer,
                        new Point3d(rightAxisX + 2, y0 + height + 3, 0),
                        "Z (m)", 3.0, Math.PI / 2);
                    AddText(ms, tr, styleId, txLayer, new Point3d(x0 + width * 0.55, y0 + height + 8, 0),
                        $"TL ngang 1/{horizontalScale:0}   TL đứng 1/{verticalScale:0}", 3.0, 0);

                    // Do not create two AutoCAD LINE entities for every tiny TIN
                    // segment. On large TINs that can mean hundreds of thousands of
                    // DBObjects for 200+ sections. Group each contiguous profile run
                    // into one Polyline per surface instead.
                    // SectionProfileSegment.S0/S1 are already local stations:
                    // TinSectionSampler subtracts line.MinT when creating them.
                    // Do NOT subtract MinT again: mine survey coordinates would
                    // displace the sampled curves millions of units off-frame.
                    AddProfilePolylines(ms, tr, p, x0,
                        y0, baseZ, vExag, exLayer, deLayer, pollCancellation: pollCancellation);

                    currentY -= height + layoutGap;
                }
                pollCancellation?.Invoke();
                tr.Commit();
                return currentY;
            }
        }

        // Quick-section output is completely separate from the official
        // section station system and CAD layers. It never clears or changes
        // MV_MC_* geometry and is never used for quantity calculations.
        public static void DrawQuickProfiles(Database db,
            IReadOnlyList<SectionProfile> profiles, Point3d insertion,
            double horizontalScale, double verticalScale,
            double elevationStep, double layoutGap,
            Action pollCancellation = null, Action<int> reportProgress = null)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (profiles == null || profiles.Count == 0)
                throw new InvalidOperationException("Không có mặt cắt nhanh để vẽ.");
            if (horizontalScale < 1 || verticalScale < 1 ||
                double.IsInfinity(horizontalScale) || double.IsInfinity(verticalScale) ||
                double.IsNaN(horizontalScale) || double.IsNaN(verticalScale))
                throw new ArgumentOutOfRangeException(nameof(horizontalScale));
            if (elevationStep <= 0 || double.IsNaN(elevationStep) ||
                double.IsInfinity(elevationStep))
                throw new ArgumentOutOfRangeException(nameof(elevationStep));
            // A corrupt/reloaded layout gap must never propagate NaN/Infinity
            // into AutoCAD geometry. Match the official section drawing gate.
            if (double.IsNaN(layoutGap) || double.IsInfinity(layoutGap) || layoutGap < 30.0)
                throw new ArgumentOutOfRangeException(nameof(layoutGap),
                    "Giãn cách mặt cắt nhanh phải từ 30 đơn vị CAD trở lên.");
            double exaggeration = horizontalScale / verticalScale;
            pollCancellation?.Invoke();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                EnsureFrameRegApp(db, tr);
                var ht = EnsureLayer(db, tr, "MV_MC_NHANH_HT", 1);
                var tk = EnsureLayer(db, tr, "MV_MC_NHANH_TK", 5);
                var axis = EnsureLayer(db, tr, "MV_MC_NHANH_TRUC", 7);
                var label = EnsureLayer(db, tr, "MV_MC_NHANH_CHU", 7);
                var style = EnsureArialStyle(db, tr);
                var ms = ModelSpace(db, tr, true);
                double baseX = insertion.X, currentY = insertion.Y;
                int completed = 0;
                foreach (var profile in profiles)
                {
                    pollCancellation?.Invoke();
                    if (profile == null || !profile.HasData) continue;
                    double baseZ = Math.Floor(profile.MinZ / elevationStep) * elevationStep;
                    double topZ = Math.Ceiling(profile.MaxZ / elevationStep) * elevationStep;
                    if (topZ <= baseZ) topZ = baseZ + elevationStep;
                    var elevationTicks = EnsureElevationTickBudget(baseZ, topZ, elevationStep, profile.Line.Name);
                    double height = (topZ - baseZ) * exaggeration;
                    double width = Math.Max(1, profile.Line.MaxT - profile.Line.MinT);
                    double x0 = baseX, y0 = currentY, right = x0 + width;
                    // Axes are mirrored. Both ends display exactly identical
                    // elevation tick values, independent of the quick route bend.
                    AddLine(ms, tr, new Point3d(x0,y0,0),
                        new Point3d(right,y0,0),axis);
                    AddLine(ms, tr, new Point3d(x0,y0,0),
                        new Point3d(x0,y0+height,0),axis);
                    AddLine(ms, tr, new Point3d(right,y0,0),
                        new Point3d(right,y0+height,0),axis);
                    AddText(ms,tr,style,label,new Point3d(x0,y0+height+9,0),
                        "Mặt cắt nhanh " + profile.Line.Name,3.0,0);
                    foreach (double z in elevationTicks)
                    {
                        pollCancellation?.Invoke();
                        double yy = y0 + (z-baseZ)*exaggeration;
                        AddLine(ms,tr,new Point3d(x0-1.5,yy,0),
                            new Point3d(x0+1.5,yy,0),axis);
                        AddLine(ms,tr,new Point3d(right-1.5,yy,0),
                            new Point3d(right+1.5,yy,0),axis);
                        string elevation = z.ToString("0.##");
                        AddText(ms,tr,style,label,new Point3d(x0-12,yy-1.5,0),
                            elevation,3.0,0);
                        AddText(ms,tr,style,label,new Point3d(right+4,yy-1.5,0),
                            elevation,3.0,0);
                    }
                    // QuickSectionProfileBuilder also provides local stations.
                    AddProfilePolylines(ms,tr,profile,x0,y0,baseZ,
                        exaggeration,ht,tk, tagForEditing: false, pollCancellation: pollCancellation);
                    AddText(ms,tr,style,label,new Point3d(x0+width/2-4,y0-11,0),
                        "L (m)",3.0,0);
                    AddText(ms,tr,style,label,new Point3d(x0,y0-20,0),
                        "Chỉ tra TIN; không dùng tính khối lượng",2.5,0);
                    currentY -= height + Math.Max(40.0, layoutGap);
                    reportProgress?.Invoke(++completed);
                }
                pollCancellation?.Invoke();
                tr.Commit();
            }
        }

        // A user can set the CAD elevation-axis step as small as 0.1 m.
        // Without a bound, long sections produce tens of thousands of lines
        // and text labels per profile and can make AutoCAD appear frozen.
        // This protects only CAD annotation; it NEVER changes the separate
        // volume-band calculation parameters or silently coarsens a drawing.
        private const int MaxElevationTicksPerProfile = 250;

        private static IReadOnlyList<double> EnsureElevationTickBudget(
            double baseZ, double topZ, double step, string profileName)
        {
            if (double.IsNaN(baseZ) || double.IsInfinity(baseZ) ||
                double.IsNaN(topZ) || double.IsInfinity(topZ) ||
                double.IsNaN(step) || double.IsInfinity(step) || step <= 0)
                throw new InvalidOperationException(
                    "Cao độ hoặc bước chia trục Z của mặt cắt không hợp lệ.");

            double ticks = (topZ - baseZ) / step + 1.0;
            if (double.IsNaN(ticks) || double.IsInfinity(ticks) ||
                ticks > MaxElevationTicksPerProfile + 0.000001)
                throw new InvalidOperationException(
                    "Mặt cắt " + profileName +
                    ": bước chia trục Z quá nhỏ so với dải cao độ, vượt " +
                    MaxElevationTicksPerProfile.ToString("n0") +
                    " vạch/nhãn trên CAD. Hãy tăng 'Bước chia trục Z, m' " +
                    "rồi vẽ lại. Bước chia tầng tính khối lượng không thay đổi.");
            try { return SectionAxisTicks.Create(baseZ, topZ, step, MaxElevationTicksPerProfile); }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new InvalidOperationException("Mặt cắt " + profileName +
                    ": bước chia trục Z không biểu diễn được tại cao độ này. Hãy kiểm tra cao độ hoặc tăng bước chia trục Z.", ex);
            }
        }

        private static BlockTableRecord ModelSpace(Database db, Transaction tr, bool write)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], write ? OpenMode.ForWrite : OpenMode.ForRead);
        }

        private static void EraseOnLayers(BlockTableRecord ms, Transaction tr, IEnumerable<ObjectId> layers, Action pollCancellation = null)
        {
            var set = new HashSet<ObjectId>(layers);
            // ModelSpace may contain millions of TIN 3DFACEs. The IMSAT
            // section/preview renderer creates LINE, LWPOLYLINE and DBTEXT,
            // never 3DFACE. Classify ObjectIds BEFORE expensive DB opens;
            // retain unrelated TIN geometry even when exporting 300 sections.
            var faceClass = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(Face));
            int scanned = 0;
            foreach (ObjectId id in ms)
            {
                if ((scanned++ & 255) == 0) pollCancellation?.Invoke();
                if (id.ObjectClass.IsDerivedFrom(faceClass)) continue;
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
            ObjectId designLayer,
            bool tagForEditing = true, Action pollCancellation = null)
        {
            if (profile == null || profile.Segments == null || profile.Segments.Count == 0)
                return;

            var existing = new List<Point2d>();
            var design = new List<Point2d>();
            double previousS1 = double.NaN;
            int runIndex = 0;
            int processed = 0;

            foreach (var segment in profile.Segments)
            {
                if ((processed++ & 255) == 0) pollCancellation?.Invoke();
                bool contiguous =
                    existing.Count > 0 &&
                    !double.IsNaN(previousS1) &&
                    Math.Abs(segment.S0 - previousS1) <= 1e-7;

                if (!contiguous)
                {
                    if (existing.Count >= 2)
                    {
                        FlushProfilePair(ms, tr, profile.Line.Name, runIndex++,
                            existing, design, existingLayer, designLayer,
                            x0, y0, baseZ, vExag, tagForEditing, pollCancellation);
                    }
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

            if (existing.Count >= 2)
                FlushProfilePair(ms, tr, profile.Line.Name, runIndex,
                    existing, design, existingLayer, designLayer,
                    x0, y0, baseZ, vExag, tagForEditing, pollCancellation);
        }

        private static void FlushProfilePair(
            BlockTableRecord ms, Transaction tr, string section, int run,
            List<Point2d> existing, List<Point2d> design,
            ObjectId existingLayer, ObjectId designLayer,
            double x0, double y0, double baseZ, double vExag,
            bool tagForEditing, Action pollCancellation)
        {
            var a = FlushProfilePolyline(ms, tr, existing, existingLayer, pollCancellation);
            var c = FlushProfilePolyline(ms, tr, design, designLayer, pollCancellation);
            if (a == null || c == null)
                throw new InvalidOperationException("Không tạo được hai đường mặt cắt đối ứng.");
            // Only approved volume sections carry EditRegApp metadata.
            // Quick sections on MV_MC_NHANH_* must NEVER be picked up by
            // EditableSectionCadService when the user exports quantities.
            if (tagForEditing)
            {
                StampSection(a, section, "HT", run, x0, y0, baseZ, vExag);
                StampSection(c, section, "TK", run, x0, y0, baseZ, vExag);
            }
        }

        private static void StampSection(Polyline poly, string section, string role, int run,
            double x0, double y0, double baseZ, double vExag)
        {
            poly.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, EditRegApp),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, section),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, role),
                new TypedValue((int)DxfCode.ExtendedDataInteger16, run),
                new TypedValue((int)DxfCode.ExtendedDataReal, x0),
                new TypedValue((int)DxfCode.ExtendedDataReal, y0),
                new TypedValue((int)DxfCode.ExtendedDataReal, baseZ),
                new TypedValue((int)DxfCode.ExtendedDataReal, vExag),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString,poly.Database.FingerprintGuid.ToString()),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString,poly.Handle.ToString()));
        }

        private static Polyline FlushProfilePolyline(
            BlockTableRecord ms,
            Transaction tr,
            List<Point2d> points,
            ObjectId layer, Action pollCancellation)
        {
            if (points == null || points.Count < 2) return null;
            var poly = new Polyline(points.Count) { LayerId = layer };
            // Register before filling: cancellation leaves disposal/rollback
            // with the enclosing transaction instead of leaking a native object.
            ms.AppendEntity(poly);
            tr.AddNewlyCreatedDBObject(poly, true);
            for (int i = 0; i < points.Count; i++)
            {
                if ((i & 255) == 0) pollCancellation?.Invoke();
                poly.AddVertexAt(i, points[i], 0.0, 0.0, 0.0);
            }
            return poly;
        }

        private static DBText AddText(BlockTableRecord ms, Transaction tr, ObjectId style, ObjectId layer, Point3d p, string value, double height, double rotation)
        {
            var text = new DBText { Position = p, TextString = value, Height = height, Rotation = rotation, TextStyleId = style, LayerId = layer };
            ms.AppendEntity(text); tr.AddNewlyCreatedDBObject(text, true);
            return text;
        }

        private static void EnsureFrameRegApp(Database db, Transaction tr)
        {
            var reg = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            foreach (var name in new[] { FrameRegApp, EditRegApp, AreaRegApp })
            {
                if (reg.Has(name)) continue;
                if (!reg.IsWriteEnabled) reg.UpgradeOpen();
                var entry = new RegAppTableRecord { Name = name };
                reg.Add(entry);
                tr.AddNewlyCreatedDBObject(entry, true);
            }
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