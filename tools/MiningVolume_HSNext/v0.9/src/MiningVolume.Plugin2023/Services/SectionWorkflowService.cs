using System;
using System.Diagnostics;
using System.Threading;
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
            // Never invalidate accepted section/volume state before the CAD
            // preview transaction has successfully committed.
            DrawPreview(system);
            ReconcileManualStationLimit(state, system);
            state.SectionSpacing = spacing;
            state.SectionDirection = direction;
            state.SectionSystem = system;
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            state.VolumeCalculatedUtc = null;
            state.VolumeCalculationSeconds = null;
            state.NotifyChanged();
            return system;
        }

        public static SectionSystem DeleteLine(string name)
        {
            var state = ProjectState.Current;
            EnsureSystem();
            var offsets = state.SectionSystem.Lines.Where(x => !string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).Select(x => x.Offset).ToList();
            // Draw the candidate first. A CAD error must not invalidate
            // the previous accepted section geometry or calculated volume.
            var candidate = RebuildAtOffsets(offsets);
            DrawPreview(candidate);
            ReconcileManualStationLimit(state, candidate);
            state.SectionSystem = candidate;
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            state.VolumeCalculatedUtc = null;
            state.VolumeCalculationSeconds = null;
            state.NotifyChanged();
            return candidate;
        }

        public static SectionSystem AddLineAtPoint(Vec2 point)
        {
            var state = ProjectState.Current;
            EnsureSystem();
            double offset = Dot(point, state.SectionSystem.Normal);
            var offsets = state.SectionSystem.Lines.Select(x => x.Offset).ToList();
            if (!offsets.Any(x => Math.Abs(x - offset) < 1e-6)) offsets.Add(offset);
            // Draw the candidate first. A CAD error must not invalidate
            // the previous accepted section geometry or calculated volume.
            var candidate = RebuildAtOffsets(offsets);
            DrawPreview(candidate);
            ReconcileManualStationLimit(state, candidate);
            state.SectionSystem = candidate;
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            state.VolumeCalculatedUtc = null;
            state.VolumeCalculationSeconds = null;
            state.NotifyChanged();
            return candidate;
        }

        public static SectionSystem MoveLineToPoint(string name, Vec2 point)
        {
            var state = ProjectState.Current;
            EnsureSystem();
            double offset = Dot(point, state.SectionSystem.Normal);
            var offsets = state.SectionSystem.Lines.Select(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) ? offset : x.Offset).ToList();
            // Draw the candidate first. A CAD error must not invalidate
            // the previous accepted section geometry or calculated volume.
            var candidate = RebuildAtOffsets(offsets);
            DrawPreview(candidate);
            ReconcileManualStationLimit(state, candidate);
            state.SectionSystem = candidate;
            state.SectionProfiles.Clear();
            state.VolumeResult = null;
            state.VolumeCalculatedUtc = null;
            state.VolumeCalculationSeconds = null;
            state.NotifyChanged();
            return candidate;
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
            Action<int, int, string> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (system == null) throw new InvalidOperationException("Chưa có hệ mặt cắt.");
            if (existing == null || design == null)
                throw new InvalidOperationException("Chưa có đủ TIN hiện trạng và TIN thiết kế.");

            var sampler = new TinSectionSampler();
            return sampler.BuildProfiles(system.Lines, existing, design, progress, cancellationToken: cancellationToken);
        }

        public static void CommitProfiles(IReadOnlyList<SectionProfile> profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            var state = ProjectState.Current;
            state.SectionProfiles.Clear();
            state.SectionProfiles.AddRange(profiles);
            state.VolumeResult = null;
            state.VolumeCalculatedUtc = null;
            state.VolumeCalculationSeconds = null;
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
            Action<int, int> progress = null,
            double layoutGap = 90.0,
            IReadOnlyList<SectionProfile> profilesToDraw = null)
        {
            if (double.IsNaN(horizontalScale) || double.IsInfinity(horizontalScale) ||
                double.IsNaN(verticalScale) || double.IsInfinity(verticalScale) ||
                horizontalScale < 1 || verticalScale < 1)
                throw new ArgumentOutOfRangeException(
                    "Tỷ lệ ngang/đứng phải là số dương hợp lệ.");
            if (string.Equals(ProjectState.Current.SectionParameterMode,
                    "TT34/2018/TT-BCT", StringComparison.Ordinal) &&
                (Math.Abs(horizontalScale - 1000.0) > 1e-9 ||
                 Math.Abs(verticalScale - 1000.0) > 1e-9))
                throw new InvalidOperationException(
                    "Bộ thông số TT34 sử dụng tỷ lệ ngang và đứng 1:1.000. " +
                    "Muốn thay đổi tỷ lệ, hãy chọn Tùy chỉnh.");
            var state = ProjectState.Current;
            if (profilesToDraw == null && state.SectionProfiles.Count == 0) BuildProfiles();
            var drawingProfiles = profilesToDraw ?? state.SectionProfiles;
            if (drawingProfiles.Count == 0)
                throw new InvalidOperationException("Không có mặt cắt hợp lệ để xuất.");
            // Reject axes-only output before starting any CAD write transaction.
            // Missing HT/TK overlap must not overwrite a previous edited drawing.
            if (!drawingProfiles.Any(p => p != null && p.HasData))
                throw new InvalidOperationException(
                    "Không có đường địa hình chung giữa hai TIN để vẽ mặt cắt. " +
                    "Kiểm tra phạm vi TIN hiện trạng/thiết kế và vị trí tuyến trên bình đồ.");
            var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");

            const int batchSize = 20;
            int total = drawingProfiles.Count;
            int completed = 0;
            // These are true host-side timings, measured inside AutoCAD 2023.
            // They are intentionally not estimates from a core-only test.
            var clock = Stopwatch.StartNew();
            var batchMs = new List<double>((total + batchSize - 1) / batchSize);
            string outcome = "FAILED";
            try
            {
                using (var meter = new SectionCadProgress(total))
                using (doc.LockDocument())
                using (var allSections = doc.Database.TransactionManager.StartTransaction())
                {
                    // The CAD renderer uses nested transactions per batch. Commit
                    // the OUTER transaction only after the final batch succeeds:
                    // an exception/cancel must leave the previous section drawing
                    // intact rather than erasing it and drawing only 20 profiles.
                    double nextY = insertion.Y;
                    for (int start = 0; start < total; start += batchSize)
                    {
                        int count = Math.Min(batchSize, total - start);
                        long beginTicks = clock.ElapsedTicks;
                        nextY = SectionCadRenderer.ReplaceProfilesBatched(
                            doc.Database,
                            drawingProfiles,
                            insertion,
                            horizontalScale,
                            verticalScale,
                            levelStep,
                            start,
                            count,
                            start == 0,
                            layoutGap,
                            nextY,
                            meter.Poll);
                        batchMs.Add((clock.ElapsedTicks - beginTicks) *
                            1000.0 / Stopwatch.Frequency);
                        meter.Report(start + count);
                        completed = start + count;
                        progress?.Invoke(completed, total);
                    }
                    meter.Poll();
                    allSections.Commit();
                }
                outcome = "PASS";
            }
            catch (OperationCanceledException)
            {
                outcome = "CANCELED";
                throw;
            }
            finally
            {
                clock.Stop();
                // On failure/cancel, completed counts only fully written
                // batches, NOT accepted CAD content (outer rollback retains
                // the previous drawing). This status prevents false PASSes.
                try
                {
                    HostPerformanceLog.Record("SECTION_CAD_EXPORT", outcome,
                        doc.Database.FingerprintGuid, total, completed,
                        drawingProfiles.Sum(p => p == null ? 0 : p.Segments.Count),
                        clock.Elapsed.TotalMilliseconds, batchMs);
                }
                catch { /* A logging failure must not mask the CAD transaction result. */ }
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
            return new SectionSystem(old.Direction, old.Normal, boundary, lines, MiningVolume.Core.Compatibility.EmptyArray<string>.Value);
        }

        // A user may move/delete stations after selecting the statutory 20m
        // preset. The UI must not still label the result "Theo TT34" when
        // the actual offsets are farther apart. Do not prevent manual edits:
        // mark them Custom, persist the choice and report it in Excel.
        private static void ReconcileManualStationLimit(ProjectState state, SectionSystem system)
        {
            if (!string.Equals(state.SectionParameterMode, "TT34/2018/TT-BCT",
                    StringComparison.Ordinal) || system == null ||
                system.Lines == null || system.Lines.Count == 0)
                return;

            const double maxStationGap = 20.0;
            const double tolerance = 1e-6;
            var offsets = system.Lines.Select(x => x.Offset).OrderBy(x => x).ToArray();
            var boundaryOffsets = system.Boundary.Select(x => Dot(x, system.Normal)).ToArray();
            if (boundaryOffsets.Length == 0)
                return;
            bool tooWide = offsets[0] - boundaryOffsets.Min() > maxStationGap + tolerance ||
                boundaryOffsets.Max() - offsets[offsets.Length - 1] > maxStationGap + tolerance;
            for (int i = 1; i < offsets.Length && !tooWide; i++)
                tooWide = offsets[i] - offsets[i - 1] > maxStationGap + tolerance;
            if (tooWide)
                state.SectionParameterMode = "Tùy chỉnh";
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