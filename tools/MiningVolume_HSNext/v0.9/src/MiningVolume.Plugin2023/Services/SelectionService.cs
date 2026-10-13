using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using MiningVolume.Core.Geometry;
using System;
using System.Collections.Generic;

namespace MiningVolume2023.Services
{
    public sealed class PickDirectionResult
    {
        public Vec2 Start { get; set; }
        public Vec2 End { get; set; }
        public Vec2 Vector => new Vec2(End.X - Start.X, End.Y - Start.Y);
    }

    /// <summary>
    /// Hide the IMSAT palette only for an interactive AutoCAD prompt. The
    /// existing controls, input values and scroll position remain intact.
    /// Always restore after Esc, cancel, failure or success, but do not open
    /// a palette that was already closed or belongs to another active DWG.
    /// </summary>
    internal sealed class CadSelectionPaletteScope : IDisposable
    {
        private readonly Autodesk.AutoCAD.Windows.PaletteSet _palette;
        private readonly Document _document;
        private readonly bool _restore;
        private bool _disposed;

        private CadSelectionPaletteScope()
        {
            _document = Application.DocumentManager.MdiActiveDocument;
            _palette = EntryPoint.Palette;
            _restore = _document != null && _palette != null && _palette.Visible;
            if (_restore) _palette.Visible = false;
        }
        internal static CadSelectionPaletteScope Hide() => new CadSelectionPaletteScope();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!_restore || !ReferenceEquals(Application.DocumentManager.MdiActiveDocument, _document)
                || !ReferenceEquals(EntryPoint.Palette, _palette)) return;
            try { _palette.Visible = true; }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
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

            PromptSelectionResult result;
            using (CadSelectionPaletteScope.Hide())
                result = doc.Editor.GetSelection(options, filter);
            if (result.Status != PromptStatus.OK || result.Value == null)
                return null;

            return result.Value.GetObjectIds();
        }

        public static IReadOnlyList<string> PickMineralOutcropHandles(string mineralName, ModelRole role)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            string title = role == ModelRole.Existing ? "hiện trạng" : "thiết kế";
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\nChọn các đoạn đường lộ vỉa " + mineralName + " trên bình đồ " + title + ": ",
                MessageForRemoval = "\nBỏ đoạn lộ vỉa khỏi lựa chọn: ",
                AllowDuplicates = false
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "POLYLINE"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            });
            PromptSelectionResult picked;
            using (CadSelectionPaletteScope.Hide())
                picked = doc.Editor.GetSelection(options, filter);
            if (picked.Status != PromptStatus.OK || picked.Value == null) return null;
            var handles = new List<string>();
            using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
                foreach (var id in picked.Value.GetObjectIds())
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity is Line || entity is Polyline ||
                        entity is Polyline2d || entity is Polyline3d)
                        handles.Add(entity.Handle.ToString());
                }
            return handles;
        }

        public static string PickClosedBoundary()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            var opt = new PromptEntityOptions("\nChọn đường bao khép kín: ");
            opt.SetRejectMessage("\nĐối tượng phải là Polyline khép kín.");
            opt.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult res;
            using (CadSelectionPaletteScope.Hide())
                res = doc.Editor.GetEntity(opt);
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
            PromptPointResult p1, p2;
            using (CadSelectionPaletteScope.Hide())
            {
            p1 = doc.Editor.GetPoint("\nChọn điểm thứ nhất xác định phương chuẩn (mặt cắt vuông góc): ");
                if (p1.Status != PromptStatus.OK) return null;
            var opt = new PromptPointOptions("\nChọn điểm thứ hai trên phương chuẩn (mặt cắt vuông góc): ") { BasePoint = p1.Value, UseBasePoint = true, UseDashedLine = true };
            p2 = doc.Editor.GetPoint(opt);
            }
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
            PromptPointResult p;
            using (CadSelectionPaletteScope.Hide())
                p = doc.Editor.GetPoint("\n" + prompt);
            if (p.Status != PromptStatus.OK) return null;
            return new Vec2(p.Value.X, p.Value.Y);
        }

        public static Autodesk.AutoCAD.Geometry.Point3d? PickInsertionPoint(string prompt)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            PromptPointResult p;
            using (CadSelectionPaletteScope.Hide())
                p = doc.Editor.GetPoint("\n" + prompt);
            return p.Status == PromptStatus.OK ? p.Value : (Autodesk.AutoCAD.Geometry.Point3d?)null;
        }
    }
}