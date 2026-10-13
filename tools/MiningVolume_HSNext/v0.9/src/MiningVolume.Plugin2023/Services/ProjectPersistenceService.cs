using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using MiningVolume.Core.Geometry;
using MiningVolume.Core.Model;
using MiningVolume.Core.Sections;

namespace MiningVolume2023.Services
{
    public sealed class ProjectLoadResult
    {
        public bool Found { get; set; }
        public string Message { get; set; }
        public DateTime? SavedUtc { get; set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    public static class ProjectPersistenceService
    {
        private const string DictionaryKey = "MININGVOLUME_HSNEXT";
        private const string RecordKey = "PROJECT_V1";
        private const int ChunkSize = 1800;

        public static bool HasSavedProject()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;
            using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (!nod.Contains(DictionaryKey)) return false;
                var dict = tr.GetObject(nod.GetAt(DictionaryKey), OpenMode.ForRead) as DBDictionary;
                return dict != null && dict.Contains(RecordKey);
            }
        }

        public static DateTime SaveCurrentProject()
        {
            var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            var snapshot = Capture(ProjectState.Current);
            // Record the CAD evidence before persisting. If it later disappears,
            // never silently recalculate a manually approved section from TIN.
            snapshot.HadEditableSectionCad =
                ProjectState.Current.SectionProfiles.Any(x => x != null && x.IsUserEdited) ||
                EditableSectionCadService.HasEditableProfiles(doc.Database);
            snapshot.SavedUtc = DateTime.UtcNow;
            var encoded = ProjectSnapshotCodec.Encode(snapshot);
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForWrite);
                DBDictionary dict;
                if (nod.Contains(DictionaryKey))
                    dict = (DBDictionary)tr.GetObject(nod.GetAt(DictionaryKey), OpenMode.ForWrite);
                else
                {
                    dict = new DBDictionary();
                    nod.SetAt(DictionaryKey, dict);
                    tr.AddNewlyCreatedDBObject(dict, true);
                }

                Xrecord rec;
                if (dict.Contains(RecordKey))
                    rec = (Xrecord)tr.GetObject(dict.GetAt(RecordKey), OpenMode.ForWrite);
                else
                {
                    rec = new Xrecord();
                    dict.SetAt(RecordKey, rec);
                    tr.AddNewlyCreatedDBObject(rec, true);
                }
                rec.Data = ToBuffer(encoded);
                tr.Commit();
            }
            ProjectState.Current.LastProjectSavedUtc = snapshot.SavedUtc;
            ProjectState.Current.NotifyChanged();
            return snapshot.SavedUtc;
        }

        public static ProjectLoadResult LoadCurrentProject(bool rebuildDerived = true)
        {
            var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            string encoded = null;
            using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (!nod.Contains(DictionaryKey)) return new ProjectLoadResult { Found = false, Message = "Bản vẽ chưa có Project IMSAT VOLUME." };
                var dict = tr.GetObject(nod.GetAt(DictionaryKey), OpenMode.ForRead) as DBDictionary;
                if (dict == null || !dict.Contains(RecordKey)) return new ProjectLoadResult { Found = false, Message = "Bản vẽ chưa có Project IMSAT VOLUME." };
                var rec = tr.GetObject(dict.GetAt(RecordKey), OpenMode.ForRead) as Xrecord;
                encoded = FromBuffer(rec?.Data);
            }
            var snapshot = ProjectSnapshotCodec.Decode(encoded);
            if (snapshot == null) return new ProjectLoadResult { Found = false, Message = "Không đọc được dữ liệu Project trong DWG." };
            if (snapshot.FormatVersion > 2) throw new InvalidOperationException("Project được tạo bởi phiên bản mới hơn, bản hiện tại chưa hỗ trợ.");

            var result = new ProjectLoadResult { Found = true, SavedUtc = snapshot.SavedUtc };
            Restore(snapshot, rebuildDerived, result.Warnings);
            result.Message = result.Warnings.Count == 0 ? "Đã khôi phục Project từ DWG." : $"Đã khôi phục Project với {result.Warnings.Count:n0} cảnh báo.";
            return result;
        }

        public static void DeleteSavedProject()
        {
            var doc = Application.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("Không có bản vẽ AutoCAD đang hoạt động.");
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForWrite);
                if (nod.Contains(DictionaryKey))
                {
                    var dict = (DBDictionary)tr.GetObject(nod.GetAt(DictionaryKey), OpenMode.ForWrite);
                    if (dict.Contains(RecordKey))
                    {
                        var id = dict.GetAt(RecordKey);
                        dict.Remove(RecordKey);
                        var obj = tr.GetObject(id, OpenMode.ForWrite, false);
                        obj.Erase();
                    }
                }
                tr.Commit();
            }
            ProjectState.Current.LastProjectSavedUtc = null;
            ProjectState.Current.NotifyChanged();
        }

        private static ProjectSnapshot Capture(ProjectState state)
        {
            var s = new ProjectSnapshot
            {
                SavedUtc = DateTime.UtcNow,
                Existing = CaptureModel(state.Existing),
                Design = CaptureModel(state.Design),
                BoundaryHandle = state.BoundaryHandle,
                HasDirection = state.SectionDirection.HasValue,
                DirectionX = state.SectionDirection?.X ?? 0,
                DirectionY = state.SectionDirection?.Y ?? 0,
                SectionSpacing = state.SectionSpacing,
                SectionParameterMode = state.SectionParameterMode,
                SectionMapScale = state.SectionMapScale,
                SectionAxisElevationInterval = state.SectionAxisElevationInterval,
                SectionLayoutGap = state.SectionLayoutGap,
                GridBoundaryHandle = state.GridBoundaryHandle,
                GridSizeX = state.GridSizeX,
                GridSizeY = state.GridSizeY,
                GridRotationDegrees = state.GridRotationDegrees,
                LevelStep = state.LevelStep,
                FromLevel = state.FromLevel,
                ToLevel = state.ToLevel,
                HorizontalScale = state.HorizontalScale,
                VerticalScale = state.VerticalScale,
                DeveloperName = state.DeveloperName,
                DeveloperContact = state.DeveloperContact,
                MiningUnitGroup = state.MiningUnitGroup,
                MiningUnitName = state.MiningUnitName,
                VolumePeriodType = state.VolumePeriodType,
                VolumePeriodNumber = state.VolumePeriodNumber,
                VolumePeriodYear = state.VolumePeriodYear,
                VolumeCalculatedUtc = state.VolumeCalculatedUtc,
                VolumeCalculationSeconds = state.VolumeCalculationSeconds,
                GridCalculatedUtc = state.GridCalculatedUtc,
                GridCalculationSeconds = state.GridCalculationSeconds,
                HadProfiles = state.SectionProfiles.Count > 0,
                HadVolumeResult = state.VolumeResult != null,
                TinRegionHandle = state.TinRegionHandle
            };
            foreach (var body in state.MineralBodies)
            {
                var item = new MineralBodySnapshot
                {
                    Id = body.Id, Name = body.Name,
                    DensityTonPerCubicMetre = body.DensityTonPerCubicMetre,
                    DensitySource = body.DensitySource, IsDeleted = body.IsDeleted
                };
                item.ExistingOutcropHandles.AddRange(body.ExistingOutcropHandles);
                item.DesignOutcropHandles.AddRange(body.DesignOutcropHandles);
                item.ConfirmedAbsentSections.AddRange(body.ConfirmedAbsentSections);
                s.MineralBodies.Add(item);
            }
            foreach (var p in state.TinRegionPolygon)
                s.TinRegion.Add(new Point2Snapshot { X = p.X, Y = p.Y });
            if (state.SectionSystem != null)
            {
                foreach (var p in state.SectionSystem.Boundary) s.Boundary.Add(new Point2Snapshot { X = p.X, Y = p.Y });
                foreach (var line in state.SectionSystem.Lines)
                {
                    var l = new SectionLineSnapshot
                    {
                        Index = line.Index, Name = line.Name, Offset = line.Offset,
                        DirectionX = line.Direction.X, DirectionY = line.Direction.Y,
                        NormalX = line.Normal.X, NormalY = line.Normal.Y
                    };
                    foreach (var span in line.Spans) l.Spans.Add(new SectionSpanSnapshot { StartT = span.StartT, EndT = span.EndT });
                    s.SectionLines.Add(l);
                }
            }
            return s;
        }

        private static ModelSnapshot CaptureModel(ModelSession session)
        {
            var s = new ModelSnapshot
            {
                Layer = session.Layer,
                TinVisible = session.TinVisible,
                HadTin = session.IsTinCurrent,
                SourceMode = (int)session.SourceMode
            };
            foreach (var handle in session.SelectedHandles) s.SelectedHandles.Add(handle);
            foreach (var t in session.AllowedTypes) s.AllowedTypes.Add((int)t);
            if (session.Source == null) return s;
            // Validate all original HT/TK geometry after DWG reopen, not only
            // handles that have manual point overrides.
            s.FullSourceFingerprint = SourceFingerprint.ComputeModel(session.Source);
            s.EditHistory.AddRange(session.Source.EditHistory);
            foreach (var e in session.Source.Entities)
            {
                var edit = new EntityEditSnapshot { Handle = e.Handle, Enabled = e.IsEnabled };
                foreach (var v in e.Vertices)
                {
                    bool hasZ = Math.Abs(v.Position.Z - v.Original.Z) > 1e-9;
                    if (!v.IsEnabled || hasZ)
                        edit.Vertices.Add(new VertexEditSnapshot { Index = v.Index, Enabled = v.IsEnabled, HasZOverride = hasZ, Z = v.Position.Z });
                }
                if (!edit.Enabled || edit.Vertices.Count > 0)
                    edit.SourceFingerprint=SourceFingerprint.Compute(e);
                if (!edit.Enabled || edit.Vertices.Count > 0) s.Edits.Add(edit);
            }
            return s;
        }

        private static void Restore(ProjectSnapshot snap, bool rebuildDerived, List<string> warnings)
        {
            var state = ProjectState.Current;
            state.Reset();
            state.SectionSpacing = PositiveOr(snap.SectionSpacing, 20);
            // Legacy DWGs have no preset fields. Treat them as Custom, preserving
            // their original numeric values rather than silently applying TT34.
            state.SectionParameterMode = snap.SectionParameterMode == "TT34/2018/TT-BCT"
                ? snap.SectionParameterMode : "Tùy chỉnh";
            state.SectionMapScale = 1000;
            state.SectionAxisElevationInterval =
                PositiveOr(snap.SectionAxisElevationInterval, snap.SectionParameterMode == null ? PositiveOr(snap.LevelStep, 5) : 20);
            state.SectionLayoutGap = Math.Max(30.0, PositiveOr(snap.SectionLayoutGap, 90));
            state.GridBoundaryHandle = snap.GridBoundaryHandle;
            state.GridSizeX = PositiveOr(snap.GridSizeX, 20);
            state.GridSizeY = PositiveOr(snap.GridSizeY, 20);
            state.GridRotationDegrees = snap.GridRotationDegrees;
            state.LevelStep = PositiveOr(snap.LevelStep, 5);
            state.FromLevel = snap.FromLevel;
            state.ToLevel = snap.ToLevel;
            // Custom projects preserve the EXACT chosen scales across DWG reopen.
            // TT34 keeps the default 1:1000 on both axes. A legacy project
            // without a saved mode is treated as Custom (never silently discard).
            state.HorizontalScale = state.SectionParameterMode == "Tùy chỉnh"
                ? PositiveOr(snap.HorizontalScale, 1000.0) : 1000.0;
            state.VerticalScale = state.SectionParameterMode == "Tùy chỉnh"
                ? PositiveOr(snap.VerticalScale, 1000.0) : 1000.0;
            if (state.SectionParameterMode != "Tùy chỉnh" &&
                ((snap.HorizontalScale > 0 && Math.Abs(snap.HorizontalScale - 1000.0) > 1e-9) ||
                 (snap.VerticalScale > 0 && Math.Abs(snap.VerticalScale - 1000.0) > 1e-9)))
                warnings.Add("Bộ thông số TT34 sử dụng mặt cắt 1:1.000 trên hai trục. " +
                    "Project cũ chứa tỷ lệ khác; kiểm tra và vẽ lại mặt cắt CAD nếu cần.");
            state.DeveloperName = "Bùi Thế Nam";
            state.DeveloperContact = "ĐT/Zalo: 0967280686";
            state.MiningUnitGroup = MiningUnitCatalog.IsKnownGroup(snap.MiningUnitGroup)
                ? snap.MiningUnitGroup : MiningUnitCatalog.Coal;
            state.MiningUnitName = snap.MiningUnitName ?? string.Empty;
            state.VolumePeriodType = snap.VolumePeriodType == VolumePeriodCatalog.Year
                ? VolumePeriodCatalog.Year
                : snap.VolumePeriodType == VolumePeriodCatalog.Quarter
                    ? VolumePeriodCatalog.Quarter : VolumePeriodCatalog.Month;
            state.VolumePeriodYear = snap.VolumePeriodYear >= 1900 && snap.VolumePeriodYear <= 2200
                ? snap.VolumePeriodYear : DateTime.Now.Year;
            int maxPeriod = state.VolumePeriodType == VolumePeriodCatalog.Quarter ? 4 : 12;
            state.VolumePeriodNumber = snap.VolumePeriodNumber >= 1 && snap.VolumePeriodNumber <= maxPeriod
                ? snap.VolumePeriodNumber : 1;
            // A saved calculation timestamp is not proof that the reconstructed
            // source TIN and resulting geometry will match. Keep its provenance
            // only if rebuild is enabled and succeeds.
            state.VolumeCalculatedUtc = null;
            state.VolumeCalculationSeconds = null;
            state.GridCalculatedUtc = null;
            state.GridCalculationSeconds = null;
            if (snap.MineralBodies != null)
            {
                var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var body in snap.MineralBodies)
                {
                    if (body == null) continue;
                    string id = string.IsNullOrWhiteSpace(body.Id) ? Guid.NewGuid().ToString("N") : body.Id;
                    if (!seenIds.Add(id)) { warnings.Add("Trùng mã vỉa trong Project; bỏ qua bản ghi trùng."); continue; }
                    var restored = new MineralBody
                    {
                        Id = id, Name = body.Name,
                        DensityTonPerCubicMetre = body.DensityTonPerCubicMetre,
                        DensitySource = body.DensitySource,
                        IsDeleted = body.IsDeleted
                    };
                    restored.AddTraces(ModelRole.Existing, body.ExistingOutcropHandles);
                    restored.AddTraces(ModelRole.Design, body.DesignOutcropHandles);
                    if (body.ConfirmedAbsentSections != null)
                        foreach (var absent in body.ConfirmedAbsentSections)
                            restored.SetAbsentAtSection(absent,true);
                    state.MineralBodies.Add(restored);
                }
            }
            state.BoundaryHandle = snap.BoundaryHandle;
            state.TinRegionHandle = snap.TinRegionHandle;
            if (snap.TinRegion != null)
                foreach (var p in snap.TinRegion)
                    state.TinRegionPolygon.Add(new Vec2(p.X, p.Y));
            state.MarkTinRegionChanged();
            if (snap.HasDirection) state.SectionDirection = new Vec2(snap.DirectionX, snap.DirectionY);

            RestoreModel(ModelRole.Existing, snap.Existing, warnings);
            RestoreModel(ModelRole.Design, snap.Design, warnings);
            if(warnings.Count>0) {
                rebuildDerived=false;
                warnings.Add("Nguồn cần kiểm tra lại; không tự dựng TIN/mặt cắt/khối lượng từ dữ liệu chưa xác minh.");
            }

            if (snap.SectionLines != null && snap.SectionLines.Count > 0 && snap.Boundary != null && snap.Boundary.Count >= 3)
            {
                var boundary = snap.Boundary.Select(x => new Vec2(x.X, x.Y)).ToList();
                var lines = snap.SectionLines.Select(x => new SectionLine(x.Index, x.Name, x.Offset,
                    new Vec2(x.DirectionX, x.DirectionY), new Vec2(x.NormalX, x.NormalY),
                    x.Spans.Select(y => new SectionSpan(y.StartT, y.EndT)).ToList())).ToList();
                var direction = lines[0].Direction;
                var normal = lines[0].Normal;
                state.SectionSystem = new SectionSystem(direction, normal, boundary, lines, Array.Empty<string>());
            }

            if (rebuildDerived)
            {
                RebuildSavedTins(snap, warnings);
                if (snap.HadProfiles && state.SectionSystem != null && state.Existing.Tin != null && state.Design.Tin != null)
                {
                    try
                    {
                        SectionWorkflowService.BuildProfiles();
                        var doc = Application.DocumentManager.MdiActiveDocument;
                        bool cadSectionsAvailable = doc != null &&
                            EditableSectionCadService.HasEditableProfiles(doc.Database);
                        if (snap.HadEditableSectionCad && !cadSectionsAvailable)
                            throw new InvalidOperationException(
                                "Project đã lưu có hệ mặt cắt CAD hoặc đường đã hiệu chỉnh, nhưng hiện không tìm thấy. " +
                                "Không được tính lại từ TIN thay cho mặt cắt người dùng đã sửa. Hãy khôi phục bản vẽ hoặc hệ mặt cắt gốc.");
                        if (cadSectionsAvailable)
                        {
                            // Persisted CAD polylines are authoritative. Even when
                            // only one tagged segment survives, reconstruction
                            // validates completeness and fails closed.
                            var edited = EditableSectionCadService.ReadEditedProfiles(
                                doc.Database, state.SectionProfiles.ToArray());
                            SectionWorkflowService.CommitProfiles(edited);
                        }
                    }
                    catch (Exception ex)
                    {
                        state.SectionProfiles.Clear();
                        state.VolumeResult = null;
                        warnings.Add("Không khôi phục được mặt cắt đã sửa trên CAD. Chặn khối lượng cho đến khi xử lý: " + ex.Message);
                    }
                }
                if (snap.HadVolumeResult && state.SectionProfiles.Count > 0 && state.SectionSystem != null && state.Existing.Tin != null && state.Design.Tin != null && state.LevelStep > 0 && Math.Abs(state.FromLevel - state.ToLevel) > 1e-9)
                {
                    try
                    {
                        // Restored DWGs rebuild real geometry/volume: record the
                        // actual recomputation, never mislabel the old export date.
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        var v = VolumeWorkflowService.CalculateCore(state.SectionSystem,
                            state.SectionProfiles, state.Existing.Tin, state.Design.Tin,
                            state.FromLevel, state.ToLevel, state.LevelStep);
                        timer.Stop();
                        state.VolumeResult = v;
                        state.VolumeCalculatedUtc = DateTime.UtcNow;
                        state.VolumeCalculationSeconds = timer.Elapsed.TotalSeconds;
                    }
                    catch (Exception ex) { warnings.Add("Không khôi phục được kết quả khối lượng: " + ex.Message); }
                }
            }
            state.LastProjectSavedUtc = snap.SavedUtc;
            state.NotifyChanged();
        }

        private static void RestoreModel(ModelRole role, ModelSnapshot snap, List<string> warnings)
        {
            if (snap == null) return;
            var types = new HashSet<SourceEntityType>((snap.AllowedTypes ?? new List<int>()).Where(x => Enum.IsDefined(typeof(SourceEntityType), x)).Select(x => (SourceEntityType)x));
            if (types.Count == 0) foreach (SourceEntityType t in Enum.GetValues(typeof(SourceEntityType))) types.Add(t);
            try
            {
                var sourceMode = Enum.IsDefined(typeof(SourceSelectionMode), snap.SourceMode)
                    ? (SourceSelectionMode)snap.SourceMode
                    : SourceSelectionMode.Layer;

                if (sourceMode == SourceSelectionMode.ManualSelection && snap.SelectedHandles != null && snap.SelectedHandles.Count > 0)
                    SurfaceWorkflowService.LoadSelectedHandles(role, snap.SelectedHandles, types, clearPreviousCadTin: false);
                else if (!string.IsNullOrWhiteSpace(snap.Layer))
                    SurfaceWorkflowService.LoadLayer(role, snap.Layer, types, clearPreviousCadTin: false);
                else
                    return;

                var session = ProjectState.Current.Get(role);
                if (!string.IsNullOrWhiteSpace(snap.FullSourceFingerprint) &&
                    !string.Equals(SourceFingerprint.ComputeModel(session.Source),
                        snap.FullSourceFingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(session.Name +
                        ": nguồn CAD đã thay đổi so với lúc lưu Project; không áp sửa Z/loại điểm hoặc tính lại khối lượng từ mặt cắt đã chỉnh.");
                    return;
                }
                var byHandle = session.Source.Entities.GroupBy(e => e.Handle, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                foreach (var edit in snap.Edits ?? new List<EntityEditSnapshot>())
                {
                    if (!byHandle.TryGetValue(edit.Handle ?? string.Empty, out var entity)) { warnings.Add($"{session.Name}: không tìm thấy đối tượng handle {edit.Handle}."); continue; }
                    if(string.IsNullOrEmpty(edit.SourceFingerprint) || edit.SourceFingerprint!=SourceFingerprint.Compute(entity)) {
                        warnings.Add(session.Name+": nguồn thay đổi hoặc thiếu dấu kiểm chứng; không áp lại chỉnh sửa cũ cho handle "+edit.Handle);
                        continue;
                    }
                    entity.SetEnabled(edit.Enabled);
                    foreach (var ve in edit.Vertices ?? new List<VertexEditSnapshot>())
                    {
                        if (ve.Index < 0 || ve.Index >= entity.Vertices.Count) { warnings.Add($"{session.Name}: handle {edit.Handle} không còn đỉnh {ve.Index}."); continue; }
                        entity.Vertices[ve.Index].SetEnabled(ve.Enabled);
                        if (ve.HasZOverride) entity.Vertices[ve.Index].OverrideZ(ve.Z);
                    }
                }
                session.Source.EditHistory.AddRange(snap.EditHistory??new List<ModelEditRecord>());
                session.Source.Touch();
                session.TinVisible = snap.TinVisible;
            }
            catch (Exception ex) { warnings.Add($"Không nạp lại được {role}: {ex.Message}"); }
        }

        private static void RebuildSavedTins(ProjectSnapshot snap, List<string> warnings)
        {
            bool hadExisting = snap?.Existing?.HadTin == true;
            bool hadDesign = snap?.Design?.HadTin == true;

            if (hadExisting && hadDesign)
            {
                SurfaceBuildResult[] builds = null;
                try
                {
                    // A saved calculation-ready project must restore the two TINs as one pair.
                    // Build both cores first, then let DrawTinPair enforce the CAD atomicity gate.
                    builds = SurfaceWorkflowService.BuildPairCoreDetailed();
                    SurfaceWorkflowService.DrawTinPair(builds);
                    SurfaceWorkflowService.SetTinVisible(ModelRole.Existing, snap.Existing.TinVisible);
                    SurfaceWorkflowService.SetTinVisible(ModelRole.Design, snap.Design.TinVisible);
                }
                catch (Exception ex)
                {
                    // Even if CAD cleanup itself encountered a protected/foreign entity,
                    // never leave either in-memory TIN eligible for downstream calculation.
                    SurfaceWorkflowService.InvalidateTin(ModelRole.Existing, clearCadLayer: false, notify: false);
                    SurfaceWorkflowService.InvalidateTin(ModelRole.Design, clearCadLayer: false, notify: false);
                    ProjectState.Current.NotifyChanged();
                    warnings.Add("Không dựng lại được cặp TIN hiện trạng/thiết kế: " + ex.Message);
                }
                finally
                {
                    if (builds != null)
                        foreach (var build in builds)
                        {
                            // A file-backed mesh is owned by ProjectState only after
                            // successful adoption into the live AutoCAD session.
                            // Release all remaining temporary stores on failure.
                            if (build?.Tin != null &&
                                !ReferenceEquals(ProjectState.Current.Get(build.Role).Tin, build.Tin))
                                try { (build.Tin.Triangles as IDisposable)?.Dispose(); } catch { }
                        }
                }
                return;
            }

            if (hadExisting) TryRebuildTin(ModelRole.Existing, snap.Existing, warnings);
            if (hadDesign) TryRebuildTin(ModelRole.Design, snap.Design, warnings);
        }

        private static void TryRebuildTin(ModelRole role, ModelSnapshot snap, List<string> warnings)
        {
            if (snap == null || !snap.HadTin) return;
            var session = ProjectState.Current.Get(role);
            if (session.Source == null || session.Source.Entities.Count == 0) return;
            SurfaceBuildResult build = null;
            try
            {
                build = SurfaceWorkflowService.BuildCoreDetailed(role);
                SurfaceWorkflowService.DrawTin(role, build);
                SurfaceWorkflowService.SetTinVisible(role, snap.TinVisible);
            }
            catch (Exception ex) { warnings.Add($"Không dựng lại được TIN {session.Name}: {ex.Message}"); }
            finally
            {
                if (build?.Tin != null && !ReferenceEquals(session.Tin, build.Tin))
                    try { (build.Tin.Triangles as IDisposable)?.Dispose(); } catch { }
            }
        }

        private static double PositiveOr(double value, double fallback) => value > 0 ? value : fallback;

        private static ResultBuffer ToBuffer(string encoded)
        {
            var values = new List<TypedValue>();
            values.Add(new TypedValue((int)DxfCode.Int32, 1));
            for (int i = 0; i < encoded.Length; i += ChunkSize)
                values.Add(new TypedValue((int)DxfCode.Text, encoded.Substring(i, Math.Min(ChunkSize, encoded.Length - i))));
            return new ResultBuffer(values.ToArray());
        }

        private static string FromBuffer(ResultBuffer buffer)
        {
            if (buffer == null) return null;
            var parts = new List<string>();
            foreach (var tv in buffer)
                if (tv.TypeCode == (int)DxfCode.Text && tv.Value is string s) parts.Add(s);
            return string.Concat(parts);
        }
    }
}