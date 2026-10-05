using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace MiningVolume2023.Services
{
    internal static class CadViewService
    {
        public static void ZoomToHandle(string handleText)
        {
            if (string.IsNullOrWhiteSpace(handleText)) return;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                long h = Convert.ToInt64(handleText, 16);
                var id = doc.Database.GetObjectId(false, new Handle(h), 0);
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) return;
                var ext = ent.GeometricExtents;
                ZoomWindow(doc.Editor, ext.MinPoint, ext.MaxPoint, 1.35);
                tr.Commit();
            }
        }

        private static void ZoomWindow(Editor ed, Point3d min, Point3d max, double margin)
        {
            var view = ed.GetCurrentView();
            double w = Math.Max(1e-6, max.X - min.X) * margin;
            double h = Math.Max(1e-6, max.Y - min.Y) * margin;
            var center = new Point2d((min.X + max.X) * 0.5, (min.Y + max.Y) * 0.5);
            double viewRatio = view.Width / Math.Max(view.Height, 1e-9);
            double boxRatio = w / Math.Max(h, 1e-9);
            if (boxRatio > viewRatio) h = w / viewRatio; else w = h * viewRatio;
            view.CenterPoint = center;
            view.Width = w;
            view.Height = h;
            ed.SetCurrentView(view);
        }
    }
}