using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Sections;

namespace MiningVolume2023.Services
{
    public static class SectionWorkflowService
    {
        public static SectionSystem Preview(double spacing, Vec2 direction)
        {
            var state = ProjectState.Current;
            var boundary = BoundaryGeometryService.ReadBoundary(state.BoundaryHandle);
            var system = new SectionSystemBuilder().Build(boundary, direction, new SectionGenerationOptions
            {
                Spacing = spacing,
                NamePrefix = "MC-",
                FirstNumber = 1,
                AppendLastOffset = true
            });
            state.SectionSpacing = spacing;
            state.SectionDirection = direction;
            state.SectionSystem = system;
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            DrawPreview(system);
            state.NotifyChanged();
            return system;
        }

        public static SectionSystem DeleteLine(string name)
        {
            var state = ProjectState.Current;
            EnsureSystem();
            var offsets = state.SectionSystem.Lines.Where(x => !string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).Select(x => x.Offset).ToList();
            state.SectionSystem = RebuildAtOffsets(offsets);
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            DrawPreview(state.SectionSystem);
            state.NotifyChanged();
            return state.SectionSystem;
        }

        public static SectionSystem AddLineAtPoint(Vec2 point)
        {
            var state = ProjectState.Current;
            EnsureSystem();
            double offset = Dot(point, state.SectionSystem.Normal);
            var offsets = state.SectionSystem.Lines.Select(x => x.Offset).ToList();
            if (!offsets.Any(x => Math.Abs(x - offset) < 1e-6)) offsets.Add(offset);
            state.SectionSystem = RebuildAtOffsets(offsets);
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            DrawPreview(state.SectionSystem);
            state.NotifyChanged();
            return state.SectionSystem;
        }

        public static SectionSystem MoveLineToPoint(string name, Vec2 point)
        {
            var state = ProjectState.Current;
            EnsureSystem();
            double offset = Dot(point, state.SectionSystem.Normal);
            var offsets = state.SectionSystem.Lines.Select(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) ? offset : x.Offset).ToList();
            state.SectionSystem = RebuildAtOffsets(offsets);
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            DrawPreview(state.SectionSystem);
            state.NotifyChanged();
            return state.SectionSystem;
        }

        public static void EnsureProfileInputsReady()
        {
            EnsureSystem();
            // Profiles are calculation input. Never use a missing/stale/partially drawn TIN.
            SurfaceWorkflowService.EnsureBothTinsReady(synchronizeCadLayers: true);
        }

        public static IReadOnlyList<SectionProfile> BuildProfilesCore(
            SectionSystem system,
            MiningVolume.Core.Surface.TinSurface existing,
            MiningVolume.Core.Surface.TinSurface design,
            Action<int, int, string> progress = null)
        {
            if (system == null) throw new InvalidOperationException("Chưa có hệ mặt cắt.");
            if (existing == null || design == null)
                throw new InvalidOperationException("Chưa có đủ TIN hiện trạng và TIN thiết kế.");

            var sampler = new TinSectionSampler();
            return sampler.BuildProfiles(system.Lines, existing, design, progress);
        }

        public static void CommitProfiles(IReadOnlyList<SectionProfile> profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            var state = ProjectState.Current;
            state.SectionProfiles.Clear();
            state.SectionProfiles.AddRange(profiles);
            state.VolumeResult = null;
            state.NotifyChanged();
        }

        public static IReadOnlyList<SectionProfile> BuildProfiles()
        {
            EnsureProfileInputsReady();
            var state = ProjectState.Current;
            var profiles = BuildProfilesCore(
                state.SectionSystem,
                state.Existing.Tin,
                state.Design.Tin);
            CommitProfiles(profiles);
            return profiles;
        }

        public static void DrawProfiles(
            Point3d insertion,
            double horizontalScale,
            double verticalScale,
            double levelStep,
            Action<int, int> progress = null)
        {
            var state = ProjectState.Current;
            if (state.SectionProfiles.Count == 0) BuildProfiles();
            var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");

            const int batchSize = 20;
            int total = state.SectionProfiles.Count;
            using (doc.LockDocument())
            {
                for (int start = 0; start < total; start += batchSize)
                {
                    int count = Math.Min(batchSize, total - start);
                    SectionCadRenderer.ReplaceProfilesBatched(
                        doc.Database,
                        state.SectionProfiles,
                        insertion,
                        horizontalScale,
                        verticalScale,
                        levelStep,
                        start,
                        count,
                        start == 0);
                    progress?.Invoke(start + count, total);
                }
            }
        }

        public static void ClearPreview()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            using (doc.LockDocument()) SectionCadRenderer.ClearPreview(doc.Database);
        }

        private static SectionSystem RebuildAtOffsets(IEnumerable<double> sourceOffsets)
        {
            var state = ProjectState.Current;
            var old = state.SectionSystem;
            var boundary = old.Boundary;
            var builder = new SectionSystemBuilder();
            var offsets = sourceOffsets.OrderBy(x => x).ToList();
            var lines = new List<SectionLine>();
            int index = 1;
            foreach (var offset in offsets)
            {
                var line = builder.BuildLineAtOffset(boundary, old.Direction, offset, index, "MC-", 1e-7);
                if (line != null) { lines.Add(line); index++; }
            }
            if (lines.Count == 0) throw new InvalidOperationException("Sau hiệu chỉnh không còn tuyến mặt cắt hợp lệ trong đường bao.");
            return new SectionSystem(old.Direction, old.Normal, boundary, lines, Array.Empty<string>());
        }

        private static void DrawPreview(SectionSystem system)
        {
            var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            using (doc.LockDocument()) SectionCadRenderer.ReplacePreview(doc.Database, system);
        }

        private static void EnsureSystem()
        {
            if (ProjectState.Current.SectionSystem == null) throw new InvalidOperationException("Chưa tạo hệ mặt cắt. Hãy chọn đường bao, hướng và bấm Xem trước tuyến.");
        }

        private static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
    }
}