using MiningVolume.Core.Surface;
using MiningVolume2023.Services;

namespace MiningVolume2023.UI
{
    // Same grouped editor and model-only actions; only the scanner differs.
    // Official review includes breakline intersections; preview reviews original vertices.
    internal sealed class MeanZConflictReviewForm : ConflictReviewForm
    {
        internal MeanZConflictReviewForm(ModelRole role, MeanZPreviewPreparer.Result scan,
            System.Action requestBuildPreview=null)
            : base(role, scan?.ToConflictReport(ProjectState.Current.Get(role).Source), true,
                requestBuildPreview) { }
    }
}
