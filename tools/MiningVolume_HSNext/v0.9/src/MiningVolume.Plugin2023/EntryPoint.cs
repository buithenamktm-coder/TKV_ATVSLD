using System;
using System.IO;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using MiningVolume2023.UI;
using MiningVolume2023.Services;

[assembly: ExtensionApplication(typeof(MiningVolume2023.EntryPoint))]
[assembly: CommandClass(typeof(MiningVolume2023.EntryPoint))]

namespace MiningVolume2023
{
    public sealed class EntryPoint : IExtensionApplication
    {
        internal static PaletteSet Palette;
        internal static MainPaletteControl MainControl;
        private static bool _idleHooked;
        private static bool _documentHooked;
        private static bool _startupUiOpened;
        internal static string StartupUiError { get; private set; }
        private static string _loadedDrawingFingerprint;
        private static Autodesk.AutoCAD.ApplicationServices.Document _loadedDocument;

        public void Initialize()
        {
            StartupLog("Initialize: MiningVolume V2.0 QA");
            try { StartupLog(RuntimeBuildIdentity.Describe()); } catch(System.Exception ex) { StartupLog("Build identity: "+ex.Message); }
            TryRibbon();
            HookIdle();
            AcApp.DocumentManager.DocumentActivated += OnDocumentActivated;
            _documentHooked = true;
        }

        public void Terminate()
        {
            if (_idleHooked) AcApp.Idle -= OnIdle;
            if (_documentHooked)
            {
                AcApp.DocumentManager.DocumentActivated -= OnDocumentActivated;
                _documentHooked = false;
            }
        }

        private static void OnDocumentActivated(
            object sender,
            Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs e)
        {
            // Never leave the globally shared palette active with another
            // drawing's HT/TK sources or accepted volume result. Defer costly
            // DWG/TIN restoration until the user next opens NAMVECTA.
            if (_loadedDocument == null) return;
            var current = CurrentFingerprint();
            // SaveAs and copies can share a FingerprintGuid. The in-session
            // Document reference is the actual boundary between projects.
            if (ReferenceEquals(e.Document, _loadedDocument) &&
                string.Equals(current, _loadedDrawingFingerprint,
                    StringComparison.OrdinalIgnoreCase)) return;

            _loadedDocument = null;
            _loadedDrawingFingerprint = null;
            GridWorkflowService.Invalidate();
            if (Palette != null && Palette.Visible)
            {
                Palette.Visible = false;
                try
                {
                    e.Document?.Editor.WriteMessage(
                        "\\nNAMVECTA VOLUME: đã đổi bản vẽ. Mở lại Ribbon NAMVECTA để nạp Project của DWG hiện tại; không dùng kết quả của DWG trước.");
                }
                catch { }
            }
            StartupLog("Active DWG changed; old palette hidden and grid invalidated.");
        }

        private static void OnIdle(object sender, EventArgs e)
        {
            TryRibbon();

            if (!_startupUiOpened && AcApp.DocumentManager.MdiActiveDocument != null)
            {
                try
                {
                    // TIN layers are part of the calculation contract, not a cosmetic
                    // by-product. Create both dedicated layers as soon as a DWG is active.
                    SurfaceWorkflowService.EnsureOutputLayer(ModelRole.Existing);
                    SurfaceWorkflowService.EnsureOutputLayer(ModelRole.Design);

                    bool autoOpen = UserSettingsService.AutoOpenPalette;
                    if (autoOpen)
                    {
                        Open(AppPage.Project);
                        StartupLog("Startup palette opened automatically.");
                    }
                    else
                    {
                        StartupLog("Startup palette auto-open is OFF. Use Ribbon/menu to open NAMVECTA VOLUME.");
                    }

                    StartupUiError = null;
                    _startupUiOpened = true;
                    StartupLog("TIN output layers ready: " +
                        ProjectState.Current.Existing.TinLayer + " / " +
                        ProjectState.Current.Design.TinLayer);
                    StartupLog("Startup initialization successful. Ribbon=" +
                        (Autodesk.Windows.ComponentManager.Ribbon == null ? "OFF/Unavailable" : "Available"));
                }
                catch (System.Exception ex)
                {
                    StartupUiError = ex.ToString();
                    StartupLog("Startup palette FAILED: " + ex);
                    _startupUiOpened = true;

                    // During installer self-test, do not block AutoCAD with a modal dialog.
                    // MVSELFTEST will report this startup error as FAIL and the installer rolls back.
                    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MININGVOLUME_SELFTEST_FILE")))
                    {
                        try
                        {
                            AcApp.ShowAlertDialog("NAMVECTA VOLUME đã được nạp nhưng không mở được giao diện.\n\n" +
                                ex.Message + "\n\nXem log tại: " + StartupLogPath());
                        }
                        catch { }
                    }
                }
            }

            if (_startupUiOpened && _idleHooked)
            {
                AcApp.Idle -= OnIdle;
                _idleHooked = false;
            }
        }

        private static void HookIdle()
        {
            if (_idleHooked) return;
            AcApp.Idle += OnIdle;
            _idleHooked = true;
        }

        private static void TryRibbon()
        {
            try
            {
                RibbonBuilder.EnsureRibbon();
                MenuBarBuilder.EnsureMenu();
            }
            catch (System.Exception ex)
            {
                StartupLog("Ribbon unavailable/failed: " + ex.Message);
            }
        }

        private static string StartupLogPath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrWhiteSpace(root)) root = @"C:\ProgramData";
            return Path.Combine(root, "MiningVolume2023", "Logs", "startup.log");
        }

        private static void StartupLog(string message)
        {
            try
            {
                string file = StartupLogPath();
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.AppendAllText(file,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine);
            }
            catch { }
        }

        internal static void Open(AppPage page)
        {
            EnsureCurrentDrawingProjectLoaded();
            LicenseService.GetStatus();
            if (Palette == null)
            {
                Palette = new PaletteSet("NAMVECTA VOLUME", new Guid("8C96DC13-BC65-4A2C-8B34-2D7644FD1C62"))
                {
                    Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu,
                    MinimumSize = new System.Drawing.Size(360, 450),
                    Size = new System.Drawing.Size(800, 650)
                };
                MainControl = new MainPaletteControl();
                Palette.Add("NAMVECTA VOLUME", MainControl);
            }
            Palette.Visible = true;
            MainControl.ShowPage(page);
        }

        [CommandMethod("MV_DWG_CHECK", CommandFlags.Session)]
        public static void CheckOldDrawingCadEvidence()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            // Inspect existing native entities WITHOUT auto-importing, erasing,
            // regenerating, or approving a saved mine volume calculation.
            string report = Services.DwgRecognitionDiagnostic.Inspect(doc.Database,
                Services.ProjectPersistenceService.HasSavedProject());
            doc.Editor.WriteMessage("\n" + report);
            System.Windows.Forms.MessageBox.Show(report,
                "NAMVECTA — Nhận dạng TIN / mặt cắt DWG cũ",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);
        }

        [CommandMethod("MVBUILDINFO", CommandFlags.Session)]
        public static void BuildInfo()
        {
            string report=RuntimeBuildIdentity.Describe();
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n"+report);
            System.Windows.Forms.MessageBox.Show(report,"NAMVECTA — Kiểm tra DLL đang nạp");
        }

        [CommandMethod("MVOPEN", CommandFlags.Session)] public static void OpenPalette() => Open(AppPage.Project);

        internal static void TogglePalette()
        {
            if (Palette == null || !Palette.Visible)
            {
                // A new active DWG must restore its own project before Palette
                // becomes interactive again.
                Open(AppPage.Project);
                StartupLog("Palette visibility toggled: ON");
                return;
            }

            Palette.Visible = false;
            StartupLog("Palette visibility toggled: OFF");
        }

        [CommandMethod("TKL", CommandFlags.Session)]
        public static void TogglePaletteCommand() => TogglePalette();

        // Legacy alias kept for compatibility with previous builds/scripts.
        [CommandMethod("MV_TOGGLE", CommandFlags.Session)]
        public static void TogglePaletteLegacyCommand() => TogglePalette();

        [CommandMethod("MV_AUTOPEN", CommandFlags.Session)]
        public static void ToggleAutoOpen()
        {
            bool next = !UserSettingsService.AutoOpenPalette;
            UserSettingsService.AutoOpenPalette = next;
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\nNAMVECTA VOLUME: tự động mở bảng khi khởi động AutoCAD = " + (next ? "BẬT" : "TẮT") + ".");
            if (MainControl != null) MainControl.Refresh();
        }
        [CommandMethod("MV_PROJECT", CommandFlags.Session)] public static void OpenProject() => Open(AppPage.Project);
        [CommandMethod("MV_DATA", CommandFlags.Session)] public static void OpenData() => Open(AppPage.Data);
        [CommandMethod("MV_MODEL", CommandFlags.Session)] public static void OpenModel() => Open(AppPage.Model);
        [CommandMethod("MV_VIEW3D_HT", CommandFlags.Session)] public static void View3DExisting()
        {
            EnsureCurrentDrawingProjectLoaded();
            Tin3DViewService.Start(ModelRole.Existing);
        }
        [CommandMethod("MV_VIEW3D_TK", CommandFlags.Session)] public static void View3DDesign()
        {
            EnsureCurrentDrawingProjectLoaded();
            Tin3DViewService.Start(ModelRole.Design);
        }
        [CommandMethod("MV_SECTION", CommandFlags.Session)] public static void OpenSection() => Open(AppPage.Section);
        [CommandMethod("MV_QUICKSECTION", CommandFlags.Session)] public static void OpenQuickSection() => Open(AppPage.QuickSection);
        [CommandMethod("MV_VOLUME", CommandFlags.Session)] public static void OpenVolume() => Open(AppPage.Volume);
        [CommandMethod("MV_GRID", CommandFlags.Session)] public static void OpenGrid() => Open(AppPage.Grid);
        [CommandMethod("MV_MINERAL", CommandFlags.Session)] public static void OpenMineral() => Open(AppPage.Mineral);
        [CommandMethod("MV_EXPORT", CommandFlags.Session)] public static void OpenExport() => Open(AppPage.Export);
        [CommandMethod("MV_LICENSE", CommandFlags.Session)] public static void OpenLicense() => Open(AppPage.License);

        [CommandMethod("MV_PROJECT_SAVE", CommandFlags.Session)]
        public static void SaveProject()
        {
            EnsureCurrentDrawingProjectLoaded();
            var saved = Services.ProjectPersistenceService.SaveCurrentProject();
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nNAMVECTA VOLUME: đã lưu Project vào DWG lúc " + saved.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + ".");
        }

        [CommandMethod("MV_PROJECT_LOAD", CommandFlags.Session)]
        public static void LoadProject()
        {
            var r = Services.ProjectPersistenceService.LoadCurrentProject(true);
            if (!r.Found)
            {
                // No saved Xrecord means a BLANK project. A previous drawing's
                // singleton TIN/source state must never remain accepted here.
                ProjectState.Current.Reset();
            }
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nNAMVECTA VOLUME: " + r.Message);
            _loadedDocument = AcApp.DocumentManager.MdiActiveDocument;
            _loadedDrawingFingerprint = CurrentFingerprint();
            if (MainControl != null && !MainControl.IsDisposed)
                MainControl.RefreshForNewDrawing();
            Open(AppPage.Project);
        }

        private static void EnsureCurrentDrawingProjectLoaded()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            string fingerprint = CurrentFingerprint();
            if (ReferenceEquals(_loadedDocument, doc) &&
                string.Equals(_loadedDrawingFingerprint, fingerprint,
                    StringComparison.OrdinalIgnoreCase)) return;

            ProjectState.Current.Reset();
            try
            {
                if (Services.ProjectPersistenceService.HasSavedProject())
                {
                    var r = Services.ProjectPersistenceService.LoadCurrentProject(true);
                    if (r.Warnings.Count > 0)
                        doc.Editor.WriteMessage("\nNAMVECTA VOLUME: Project đã nạp với " + r.Warnings.Count + " cảnh báo. Mở trang Dự án để xem/kiểm soát.");
                }
                else
                {
                    // A legacy DWG can contain NAMVECTA-owned 3DFACE TIN layers
                    // but no Project Xrecord. Recover both meshes read-only;
                    // do not infer deleted routes, edited section geometry or
                    // historical earthwork volumes from missing metadata.
                    TryRecognizeLegacyCadTin(doc);
                }
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nNAMVECTA VOLUME: chưa tự nạp được Project - " + ex.Message);
            }
            _loadedDocument = doc;
            _loadedDrawingFingerprint = fingerprint;
            if (MainControl != null && !MainControl.IsDisposed)
                MainControl.RefreshForNewDrawing();
        }

        private static void TryRecognizeLegacyCadTin(
            Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            int recovered = 0;
            foreach (ModelRole role in new[] { ModelRole.Existing, ModelRole.Design })
            {
                var session = ProjectState.Current.Get(role);
                try
                {
                    if (SurfaceWorkflowService.TryRestoreTinFromCad(role))
                    {
                        recovered++;
                        doc.Editor.WriteMessage("\\n[NAMVECTA] Đã nhận diện TIN từ 3DFACE cũ: " +
                            session.TinLayer + " (" + session.Tin.Triangles.Count +
                            " tam giác hợp lệ). DWG không bị thay đổi.");
                    }
                }
                catch (System.Exception ex)
                {
                    doc.Editor.WriteMessage("\\n[NAMVECTA] Không thể xác nhận " +
                        session.TinLayer + ": " + ex.Message);
                }
            }
            try
            {
                if (EditableSectionCadService.HasOfficialSectionLayers(doc.Database))
                    doc.Editor.WriteMessage(
                        "\\n[NAMVECTA] Phát hiện layer mặt cắt cũ. Thiếu Project lưu kèm: " +
                        "không tự tái tạo tuyến, chỉnh sửa hoặc tính lại khối lượng.");
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage(
                    "\\n[NAMVECTA] Không kiểm tra được mặt cắt cũ: " + ex.Message);
            }
            if (recovered > 0)
                doc.Editor.WriteMessage(
                    "\\n[NAMVECTA] Đã đọc TIN cũ để kiểm tra; trước khi tính " +
                    "cần xác nhận ranh giới, hệ mặt cắt và Project.");
        }

        private static string CurrentFingerprint()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return string.Empty;
            try { return doc.Database.FingerprintGuid.ToString(); }
            catch { return doc.Name ?? string.Empty; }
        }

        [CommandMethod("MVSELFTEST", CommandFlags.Session)]
        public static void SelfTest()
        {
            var r = Services.SelfTestService.Run();
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            if (r.Passed)
                ed?.WriteMessage("\nNAMVECTA VOLUME v2.0 SELFTEST: PASS (" + r.Checks.Count + " checks). Log: " + r.OutputFile);
            else
                ed?.WriteMessage("\nNAMVECTA VOLUME v1.0 SELFTEST: FAIL (" + r.Errors.Count + " errors). Log: " + r.OutputFile);
        }

        [CommandMethod("MVABOUT")]
        public void About() => Open(AppPage.License);
    }
}