using System;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Surface;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Xem TIN hiện trạng/thiết kế ngay trong khung nhìn AutoCAD 2023.
    /// Tạm ẩn Palette, mở AutoCAD 3DORBIT, khôi phục khi kết thúc bằng Esc.
    /// Không dựng một cửa sổ 3D mới; chỉ dùng Face TIN trên layer đã kiểm chứng.
    /// </summary>
    internal static class Tin3DViewService
    {
        private static Document _document;
        private static ViewTableRecord _previousView;
        private static object _previousVisualStyle;
        private static bool _existingWasVisible;
        private static bool _designWasVisible;
        // Large TINs must not trigger automatic 3DORBIT or Conceptual shading.
        private const int LargeTinFaceThreshold = 200000;

        public static bool IsActive => _document != null;

        public static void Start(ModelRole role)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument ??
                throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            if (IsActive && !ReferenceEquals(_document, doc)) Restore();
            if (IsActive)
                throw new InvalidOperationException("Đang xem TIN 3D. Bấm TRỞ VỀ 2D trước.");

            // VIEW must not rescan or rebuild millions of CAD 3DFACEs.
            SurfaceWorkflowService.EnsureTinReady(role, synchronizeCadLayer: false);
            var session = ProjectState.Current.Get(role);
            int faceCount = checked(session.Tin.Triangles.Count + session.Tin.VerticalFaces.Count);
            if (session.TinCadIsPreview || session.TinCadFaceCount != faceCount ||
                !TinCadRenderer.LayerExists(doc.Database, session.TinLayer))
                throw new InvalidOperationException(
                    "Layer TIN CAD chưa đủ số tam giác chính thức. Hãy tạo/cập nhật TIN trước khi xem 3D.");

            if (faceCount > LargeTinFaceThreshold)
            {
                var accepted = MessageBox.Show(
                    "TIN có " + faceCount.ToString("N0") + " mặt 3D. " +
                    "3DORBIT/Conceptual tự động có thể làm AutoCAD mất phản hồi.\r\n\r\n" +
                    "Mở góc nhìn 3D khung dây, không khởi động Orbit, " +
                    "không giảm số tam giác TIN? Bấm TRỞ VỀ 2D khi xem xong.",
                    "NAMVECTA VOLUME - VIEW TIN 3D",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (accepted != DialogResult.Yes) return;
            }

            var bounds = GetTinBounds(session);
            _document = doc;
            _existingWasVisible = ProjectState.Current.Existing.TinVisible;
            _designWasVisible = ProjectState.Current.Design.TinVisible;
            try
            {
                _previousView = doc.Editor.GetCurrentView();
                try { _previousVisualStyle = AcApp.GetSystemVariable("VSCURRENT"); }
                catch { _previousVisualStyle = null; }

                // Change two layer states without doing multiple full REGENTs.
                var existing = ProjectState.Current.Existing;
                var design = ProjectState.Current.Design;
                using (doc.LockDocument())
                {
                    if (existing.TinVisible != (role == ModelRole.Existing))
                        TinCadRenderer.SetVisible(doc.Database, existing.TinLayer,
                            role == ModelRole.Existing);
                    if (design.TinVisible != (role == ModelRole.Design))
                        TinCadRenderer.SetVisible(doc.Database, design.TinLayer,
                            role == ModelRole.Design);
                }
                existing.TinVisible = role == ModelRole.Existing;
                design.TinVisible = role == ModelRole.Design;
                try { AcApp.SetSystemVariable("VSCURRENT", "2D Wireframe"); }
                catch { /* Retain current style if not accepted. */ }

                using (var view = doc.Editor.GetCurrentView())
                {
                    view.ViewDirection = new Vector3d(1, -1, 0.85);
                    view.Target = new Point3d(bounds.cx, bounds.cy, bounds.cz);
                    view.CenterPoint = new Point2d(0, 0);
                    view.ViewTwist = 0;
                    view.Width = bounds.span * 1.8;
                    view.Height = bounds.span * 1.8;
                    doc.Editor.SetCurrentView(view);
                }
                ProjectState.Current.NotifyChanged();
                // No Editor.Regen(), palette hiding or automatic 3DORBIT.
            }
            catch
            {
                Restore();
                throw;
            }
        }

        public static void Stop()
        {
            if (IsActive) Restore();
        }

        private static (double cx, double cy, double cz, double span) GetTinBounds(ModelSession selected)
        {
            // XY lấy từ tile bounds khi TIN file-backed để không nạp toàn bộ hàng triệu tam giác.
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            var tiles = selected.Tin.Triangles as ITiledTriangleSource;
            if (tiles != null && tiles.Tiles.Count > 0)
            {
                foreach (var t in tiles.Tiles)
                {
                    minX = Math.Min(minX, t.MinX); minY = Math.Min(minY, t.MinY);
                    maxX = Math.Max(maxX, t.MaxX); maxY = Math.Max(maxY, t.MaxY);
                }
            }
            else
            {
                foreach (var t in selected.Tin.Triangles)
                {
                    minX = Math.Min(minX, Math.Min(t.A.X, Math.Min(t.B.X, t.C.X)));
                    minY = Math.Min(minY, Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y)));
                    maxX = Math.Max(maxX, Math.Max(t.A.X, Math.Max(t.B.X, t.C.X)));
                    maxY = Math.Max(maxY, Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y)));
                }
            }

            // Nội suy cao độ trong mỗi tam giác không vượt quá min/max Z các đỉnh đầu vào.
            // Dùng vertices nguồn để tránh tải đầy đủ file-backed triangles.
            double minZ = double.PositiveInfinity, maxZ = double.NegativeInfinity;
            if (selected.Source != null)
                foreach (var e in selected.Source.Entities)
                    if (e.IsEnabled)
                        foreach (var vertex in e.Vertices)
                            if (vertex.IsEnabled)
                            {
                                minZ = Math.Min(minZ, vertex.Position.Z);
                                maxZ = Math.Max(maxZ, vertex.Position.Z);
                            }

            if (double.IsInfinity(minX) || double.IsInfinity(maxX) ||
                double.IsInfinity(minY) || double.IsInfinity(maxY))
                throw new InvalidOperationException("Không xác định được đường bao TIN để xem 3D.");
            if (double.IsInfinity(minZ) || double.IsInfinity(maxZ))
            {
                minZ = 0; maxZ = 0;
            }
            double span = Math.Max(1.0, Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ)));
            return ((minX + maxX) * 0.5, (minY + maxY) * 0.5, (minZ + maxZ) * 0.5, span);
        }

        private static void Restore()
        {
            var doc = _document;
            _document = null;
            try
            {
                if (doc != null && ReferenceEquals(doc, AcApp.DocumentManager.MdiActiveDocument))
                {
                    if (_previousView != null)
                        doc.Editor.SetCurrentView(_previousView);
                    if (_previousVisualStyle != null)
                        try { AcApp.SetSystemVariable("VSCURRENT", _previousVisualStyle); } catch { }
                    using (doc.LockDocument())
                    {
                        var existing = ProjectState.Current.Existing;
                        var design = ProjectState.Current.Design;
                        if (existing.TinVisible != _existingWasVisible)
                            TinCadRenderer.SetVisible(doc.Database, existing.TinLayer, _existingWasVisible);
                        if (design.TinVisible != _designWasVisible)
                            TinCadRenderer.SetVisible(doc.Database, design.TinLayer, _designWasVisible);
                        existing.TinVisible = _existingWasVisible;
                        design.TinVisible = _designWasVisible;
                    }
                    ProjectState.Current.NotifyChanged();
                }
            }
            finally
            {
                if (_previousView != null) _previousView.Dispose();
                _previousView = null;
                _previousVisualStyle = null;
                if (EntryPoint.Palette != null) EntryPoint.Palette.Visible = true;
            }
        }
    }
}
