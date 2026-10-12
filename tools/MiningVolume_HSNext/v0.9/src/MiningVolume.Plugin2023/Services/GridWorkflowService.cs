using System;
using System.Collections.Generic;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Grid;
using MiningVolume.Core.Surface;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Phiên lưới đã tính gắn với đúng hai TIN. Không cho xuất kết quả cũ
    /// nếu người dùng đổi dữ liệu, TIN hoặc Project DWG.
    /// </summary>
    internal static class GridWorkflowService
    {
        private static GridVolumeReport _report;
        private static GridVolumeOptions _options;
        private static TinSurface _existing;
        private static TinSurface _design;
        private static long _revExisting, _revDesign;
        private static string _boundaryHandle;

        public static void Invalidate()
        {
            _report = null; _options = null; _existing = null; _design = null;
            _revExisting = 0; _revDesign = 0; _boundaryHandle = null;
            ProjectState.Current.GridCalculatedUtc=null;
            ProjectState.Current.GridCalculationSeconds=null;
        }

        public static bool IsCurrent
        {
            get
            {
                var state = ProjectState.Current;
                return _report != null && _options != null &&
                       state.Existing.IsTinCurrent && state.Design.IsTinCurrent &&
                       ReferenceEquals(_existing, state.Existing.Tin) &&
                       ReferenceEquals(_design, state.Design.Tin) &&
                       state.Existing.Source.Revision == _revExisting &&
                       state.Design.Source.Revision == _revDesign &&
                       string.Equals(_boundaryHandle, state.GridBoundaryHandle, StringComparison.OrdinalIgnoreCase) &&
                       _options.SizeX == state.GridSizeX &&
                       _options.SizeY == state.GridSizeY &&
                       _options.RotationDegrees == state.GridRotationDegrees;
            }
        }

        // The CAD boundary handle does not change after grip-editing its
        // vertices. Validate the actual polygon before drawing or exporting
        // previously computed results; a stale shape must never be published.
        public static void EnsureBoundaryCurrent(IReadOnlyList<Vec2> cadBoundary)
        {
            var accepted = _options?.Boundary;
            bool same = IsCurrent && accepted != null && cadBoundary != null &&
                        accepted.Count == cadBoundary.Count;
            if (same)
                for (int i = 0; i < accepted.Count; i++)
                    if (!Geometry2D.AlmostSame(accepted[i], cadBoundary[i], 1e-7))
                    {
                        same = false;
                        break;
                    }
            if (same) return;
            Invalidate();
            throw new InvalidOperationException(
                "Đường bao lưới trên CAD hoặc dữ liệu tính đã thay đổi. " +
                "Không vẽ/xuất khối lượng cũ; hãy tính lại lưới ô vuông.");
        }

        public static GridVolumeReport Report => IsCurrent ? _report : null;
        public static GridVolumeOptions Options => IsCurrent ? _options : null;

        public static void Commit(GridVolumeReport result, GridVolumeOptions options,
            TinSurface existing, TinSurface design, long existingRevision, long designRevision,
            string boundaryHandle = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var state = ProjectState.Current;
            if (!ReferenceEquals(state.Existing.Tin, existing) ||
                !ReferenceEquals(state.Design.Tin, design) ||
                state.Existing.Source.Revision != existingRevision ||
                state.Design.Source.Revision != designRevision ||
                !string.Equals(boundaryHandle, state.GridBoundaryHandle, StringComparison.OrdinalIgnoreCase) ||
                options == null ||
                options.SizeX != state.GridSizeX || options.SizeY != state.GridSizeY ||
                options.RotationDegrees != state.GridRotationDegrees)
                throw new InvalidOperationException("Dữ liệu/TIN đã thay đổi trong khi đang tính lưới. Hãy tính lại.");
            _report = result;
            _options = options;
            _existing = existing;
            _design = design;
            _revExisting = existingRevision;
            _revDesign = designRevision;
            _boundaryHandle = boundaryHandle;
            state.NotifyChanged();
        }
    }
}
