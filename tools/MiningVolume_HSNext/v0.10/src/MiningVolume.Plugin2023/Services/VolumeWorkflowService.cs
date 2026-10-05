using System;
using System.Collections.Generic;
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

            return new SectionVolumeCalculator().Calculate(system, profiles, existing, design,
                new VolumeCalculationOptions
                {
                    FromLevel = fromLevel,
                    ToLevel = toLevel,
                    LevelStep = levelStep,
                    PreferPrismoidal = true,
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