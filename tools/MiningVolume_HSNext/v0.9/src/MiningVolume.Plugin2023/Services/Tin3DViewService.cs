using System;
using System.Linq;
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
        private static bool _restorePending;

        public static bool IsActive => _document != null;

        public static void Start(ModelRole role)
        {
            if (IsActive) throw new InvalidOperationException("Chế độ xem TIN 3D đang hoạt động. Nhấn Esc để trở về IMSAT VOLUME.");
            SurfaceWorkflowService.EnsureTinReady(role, synchronizeCadLayer: true);
            var doc = AcApp.DocumentManager.MdiActiveDocument ??
                throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            var selected = ProjectState.Current.Get(role);
            if (!TinCadRenderer.LayerExists(doc.Database, selected.TinLayer) ||
                TinCadRenderer.CountFaces(doc.Database, selected.TinLayer) == 0)
                throw new InvalidOperationException("Chưa có đối tượng 3DFACE hợp lệ để xem 3D.");

            _document = doc;
            _existingWasVisible = ProjectState.Current.Existing.TinVisible;
            _designWasVisible = ProjectState.Current.Design.TinVisible;
            _previousView = doc.Editor.GetCurrentView();
            try { _previousVisualStyle = AcApp.GetSystemVariable("VSCURRENT"); }
            catch { _previousVisualStyle = null; }

            try
            {
                // Hiện một lớp TIN để tránh hai bề mặt chồng lấp gây khó quan sát.
                SurfaceWorkflowService.SetTinVisible(ModelRole.Existing, role == ModelRole.Existing);
                SurfaceWorkflowService.SetTinVisible(ModelRole.Design, role == ModelRole.Design);

                var target = GetTinBounds(selected);
                using (var view = doc.Editor.GetCurrentView())
                {
                    view.ViewDirection = new Vector3d(1.0, -1.0, 0.85);
                    view.Target = new Point3d(target.cx, target.cy, target.cz);
                    view.CenterPoint = new Point2d(0, 0);
                    view.ViewTwist = 0;
                    view.Width = target.span * 1.8;
                    view.Height = target.span * 1.8;
                    doc.Editor.SetCurrentView(view);
                }
                // AutoCAD sẽ duy trì phong cách vẽ hiện hành nếu không hỗ trợ Conceptual.
                try { AcApp.SetSystemVariable("VSCURRENT", "Conceptual"); } catch { }

                doc.CommandCancelled += OnOrbitFinished;
                doc.CommandEnded += OnOrbitFinished;
                doc.CommandFailed += OnOrbitFinished;
                if (EntryPoint.Palette != null) EntryPoint.Palette.Visible = false;
                doc.Editor.Regen();
                doc.SendStringToExecute("_.3DORBIT ", true, false, false);
            }
            catch
            {
                Restore();
                throw;
            }
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

        private static void OnOrbitFinished(object sender, CommandEventArgs e)
        {
            string cmd = (e.GlobalCommandName ?? string.Empty).TrimStart('_', '.');
            if (!string.Equals(cmd, "3DORBIT", StringComparison.OrdinalIgnoreCase)) return;
            if (_restorePending) return;
            _restorePending = true;
            // Khôi phục sau khi AutoCAD đã thực sự rời command context.
            AcApp.Idle += RestoreOnIdle;
        }

        private static void RestoreOnIdle(object sender, EventArgs e)
        {
            AcApp.Idle -= RestoreOnIdle;
            Restore();
        }

        private static void Restore()
        {
            var doc = _document;
            _document = null;
            _restorePending = false;
            AcApp.Idle -= RestoreOnIdle;
            if (doc != null)
            {
                doc.CommandCancelled -= OnOrbitFinished;
                doc.CommandEnded -= OnOrbitFinished;
                doc.CommandFailed -= OnOrbitFinished;
            }

            try
            {
                if (doc != null && ReferenceEquals(doc, AcApp.DocumentManager.MdiActiveDocument))
                {
                    if (_previousView != null)
                        doc.Editor.SetCurrentView(_previousView);
                    if (_previousVisualStyle != null)
                        try { AcApp.SetSystemVariable("VSCURRENT", _previousVisualStyle); } catch { }
                    SurfaceWorkflowService.SetTinVisible(ModelRole.Existing, _existingWasVisible);
                    SurfaceWorkflowService.SetTinVisible(ModelRole.Design, _designWasVisible);
                    doc.Editor.Regen();
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
