using System;
using System.Collections.Generic;
using System.Linq;
using MiningVolume.Core.Sections;
using MiningVolume.Core.Surface;
using MiningVolume.Core.Volumes;

namespace MiningVolume2023.Services
{
    public static class VolumeWorkflowService
    {
        public static VolumeResult CalculateCore(SectionSystem system, IReadOnlyList<SectionProfile> profiles,
            TinSurface existing, TinSurface design, double fromLevel, double toLevel, double levelStep)
        {
            if (system == null) throw new InvalidOperationException("Chưa có hệ mặt cắt.");
            if (profiles == null || profiles.Count == 0) throw new InvalidOperationException("Chưa thành lập mặt cắt. Hãy thực hiện Bước 7 trước khi tính khối lượng.");
            if (existing == null || design == null) throw new InvalidOperationException("Chưa có đủ TIN hiện trạng và TIN thiết kế.");
            if (levelStep <= 0) throw new ArgumentOutOfRangeException(nameof(levelStep), "Mức chia tầng phải lớn hơn 0.");
            if (Math.Abs(fromLevel - toLevel) < 1e-9) throw new InvalidOperationException("Mức tính từ và mức tính đến phải khác nhau.");
            // The support CDT is single-Z at each XY. Vertical 3D faces
            // preserve the other survey heights, but cannot be interpolated as
            // a unique elevation by a 2.5D section sampler. A single review of
            // the drawn section system is required, not point-by-point cleanup.
            if ((existing.RequiresSectionReview || design.RequiresSectionReview) &&
                profiles.Any(p => p != null && p.HasData && !p.IsUserEdited))
                throw new InvalidOperationException(
                    "TIN chứa các đỉnh trùng XY nhưng khác Z và đã dựng mặt đứng 3D. " +
                    "Hãy kiểm tra các đường mặt cắt, chỉnh Grip tại vị trí cần thiết, " +
                    "rồi bấm CẬP NHẬT MẶT CẮT trước khi chốt khối lượng. " +
                    "Không cần loại từng điểm trùng trong dữ liệu nguồn.");

            return new SectionVolumeCalculator().Calculate(system, profiles, existing, design,
                new VolumeCalculationOptions
                {
                    FromLevel = fromLevel,
                    ToLevel = toLevel,
                    LevelStep = levelStep,
                    AreaDifferenceThreshold = 0.40,
                    PreferPrismoidal = false,
                    Tolerance = 1e-7
                });
        }

        public static void Commit(VolumeResult result, double fromLevel, double toLevel, double levelStep)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var state = ProjectState.Current;
            state.FromLevel = fromLevel;
            state.ToLevel = toLevel;
            state.LevelStep = levelStep;
            state.VolumeResult = result;
            state.NotifyChanged();
        }
    }
}