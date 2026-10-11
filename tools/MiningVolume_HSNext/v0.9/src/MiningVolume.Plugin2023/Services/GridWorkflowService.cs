using System;
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
