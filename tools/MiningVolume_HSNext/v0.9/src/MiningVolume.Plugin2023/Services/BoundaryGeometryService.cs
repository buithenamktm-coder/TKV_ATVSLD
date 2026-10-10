using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Geometry;

namespace MiningVolume2023.Services
{
    internal static class BoundaryGeometryService
    {
        public static string FindUniqueBoundaryOnLayer(string layer)
        {
            var doc = Application.DocumentManager.MdiActiveDocument ??
                throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            string handle = null;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var table = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var space = (BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in space)
                {
                    var pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                    if (pl == null || !pl.Closed || pl.NumberOfVertices < 3 ||
                        !string.Equals(pl.Layer, layer, StringComparison.OrdinalIgnoreCase)) continue;
                    if (handle != null)
                        throw new InvalidOperationException($"Layer {layer} có nhiều đường bao khép kín. Hãy chọn vùng trên CAD.");
                    handle = pl.Handle.ToString();
                }
                tr.Commit();
            }
            return handle ?? throw new InvalidOperationException($"Không tìm thấy đường bao khép kín trên layer {layer}.");
        }

        public static IReadOnlyList<Vec2> ReadBoundary(string handleText, double arcChord = 0.5)
        {
            if (string.IsNullOrWhiteSpace(handleText)) throw new InvalidOperationException("Chưa chọn đường bao tính khối lượng.");
            var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                long raw = Convert.ToInt64(handleText, 16);
                var id = doc.Database.GetObjectId(false, new Handle(raw), 0);
                var pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                if (pl == null || !pl.Closed) throw new InvalidOperationException("Đường bao phải là LWPOLYLINE khép kín.");
                var pts = Sample(pl, arcChord);
                tr.Commit();
                return pts;
            }
        }

        private static List<Vec2> Sample(Polyline pl, double maxChord)
        {
            var pts = new List<Vec2>();
            int n = pl.NumberOfVertices;
            if (n < 3) return pts;
            for (int i = 0; i < n; i++)
            {
                if (i == 0) Add(pts, pl.GetPoint3dAt(0));
                int j = (i + 1) % n;
                double bulge = pl.GetBulgeAt(i);
                if (Math.Abs(bulge) < 1e-12)
                {
                    if (j != 0) Add(pts, pl.GetPoint3dAt(j));
                    continue;
                }
                CircularArc3d arc = pl.GetArcSegmentAt(i);
                double sweep = arc.EndAngle - arc.StartAngle;
                double len = Math.Abs(sweep) * arc.Radius;
                int pieces = Math.Max(2, (int)Math.Ceiling(len / Math.Max(0.05, maxChord)));
                for (int k = 1; k <= pieces; k++)
                {
                    if (j == 0 && k == pieces) continue;
                    double a = arc.StartAngle + sweep * k / pieces;
                    Point3d q = arc.Center + arc.ReferenceVector.RotateBy(a - arc.StartAngle, arc.Normal) * arc.Radius;
                    Add(pts, q);
                }
            }
            return pts;
        }

        private static void Add(List<Vec2> pts, Point3d p) => pts.Add(new Vec2(p.X, p.Y));
    }
}