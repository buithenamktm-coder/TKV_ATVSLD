using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MiningVolume.Core.Geometry;
using System.Collections.Generic;

namespace MiningVolume2023.Services
{
    public sealed class PickDirectionResult
    {
        public Vec2 Start { get; set; }
        public Vec2 End { get; set; }
        public Vec2 Vector => new Vec2(End.X - Start.X, End.Y - Start.Y);
    }

    public static class SelectionService
    {
        public static IReadOnlyList<ObjectId> PickSurfaceEntities(string modelName)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;

            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\nChọn POINT, LINE, POLYLINE cho mô hình " + modelName + ": ",
                MessageForRemoval = "\nBỏ đối tượng khỏi lựa chọn: ",
                AllowDuplicates = false
            };

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "POINT"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "POLYLINE"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            });

            var result = doc.Editor.GetSelection(options, filter);
            if (result.Status != PromptStatus.OK || result.Value == null)
                return null;

            return result.Value.GetObjectIds();
        }

        public static string PickClosedBoundary()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            var opt = new PromptEntityOptions("\nChọn đường bao khép kín: ");
            opt.SetRejectMessage("\nĐối tượng phải là Polyline khép kín.");
            opt.AddAllowedClass(typeof(Polyline), true);
            var res = doc.Editor.GetEntity(opt);
            if (res.Status != PromptStatus.OK) return null;
            using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var pl = tr.GetObject(res.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null || !pl.Closed)
                {
                    doc.Editor.WriteMessage("\nĐường bao chưa khép kín.");
                    return null;
                }
                return pl.Handle.ToString();
            }
        }

        public static PickDirectionResult PickDirection()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            var p1 = doc.Editor.GetPoint("\nChọn điểm thứ nhất xác định hướng mặt cắt: ");
            if (p1.Status != PromptStatus.OK) return null;
            var opt = new PromptPointOptions("\nChọn điểm thứ hai xác định hướng mặt cắt: ") { BasePoint = p1.Value, UseBasePoint = true, UseDashedLine = true };
            var p2 = doc.Editor.GetPoint(opt);
            if (p2.Status != PromptStatus.OK) return null;
            if (p1.Value.DistanceTo(p2.Value) < 1e-8)
            {
                doc.Editor.WriteMessage("\nHai điểm hướng không được trùng nhau.");
                return null;
            }
            return new PickDirectionResult
            {
                Start = new Vec2(p1.Value.X, p1.Value.Y),
                End = new Vec2(p2.Value.X, p2.Value.Y)
            };
        }

        public static Vec2? PickPlanPoint(string prompt)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            var p = doc.Editor.GetPoint("\n" + prompt);
            if (p.Status != PromptStatus.OK) return null;
            return new Vec2(p.Value.X, p.Value.Y);
        }

        public static Autodesk.AutoCAD.Geometry.Point3d? PickInsertionPoint(string prompt)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            var p = doc.Editor.GetPoint("\n" + prompt);
            return p.Status == PromptStatus.OK ? p.Value : (Autodesk.AutoCAD.Geometry.Point3d?)null;
        }
    }
}