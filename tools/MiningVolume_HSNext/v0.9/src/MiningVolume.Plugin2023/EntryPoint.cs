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
        // Only the dedicated V2 AutoCAD-host QA session can opt into the
        // deterministic Idle self-test. Normal AutoCAD use never auto-tests.
        private static bool _hostQaSelfTestTriggered;
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
            // DWG/TIN restoration until the user next opens IMSAT.
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
                        "\\nIMSAT VOLUME: đã đổi bản vẽ. Mở lại Ribbon IMSAT để nạp Project của DWG hiện tại; không dùng kết quả của DWG trước.");
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
                        StartupLog("Startup palette auto-open is OFF. Use Ribbon/menu to open IMSAT VOLUME.");
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
                            AcApp.ShowAlertDialog("IMSAT VOLUME đã được nạp nhưng không mở được giao diện.\n\n" +
                                ex.Message + "\n\nXem log tại: " + StartupLogPath());
                        }
                        catch { }
                    }
                }
            }

            // AutoCAD's /b script may be held by a startup command or ribbon
            // event after NETLOAD; never infer a PASS from plugin startup.
            // An explicit host-only flag lets the *loaded plugin* execute the
            // same substantive self-test once, after the active document and
            // palette initialization are ready. The verifier still checks
            // the produced file, status, banner and actual DLL hashes.
            if (_startupUiOpened && !_hostQaSelfTestTriggered &&
                AcApp.DocumentManager.MdiActiveDocument != null &&
                string.Equals(Environment.GetEnvironmentVariable("MININGVOLUME_QA_HOST_AUTORUN"),
                    "1", StringComparison.Ordinal) &&
                string.Equals(Environment.GetEnvironmentVariable("MININGVOLUME_RUNTIME_PRODUCT_VERSION"),
                    "2.0", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("MININGVOLUME_SELFTEST_FILE")))
            {
                _hostQaSelfTestTriggered = true;
                if (_idleHooked)
                {
                    AcApp.Idle -= OnIdle;
                    _idleHooked = false;
                }
                StartupLog("V2 host QA: Idle self-test starting");
                try
                {
                    var result = SelfTestService.Run();
                    StartupLog("V2 host QA: Idle self-test ended; status=" +
                        (result.Passed ? "PASS" : "FAIL") +
                        "; file=" + result.OutputFile);
                }
                catch (System.Exception ex)
                {
                    StartupLog("V2 host QA: Idle self-test threw: " + ex);
                    try
                    {
                        // A host-side exception is a FAIL, never an absent
                        // proof that could later be misread as success.
                        File.WriteAllLines(
                            Environment.GetEnvironmentVariable("MININGVOLUME_SELFTEST_FILE"),
                            new[] { "NAMVECTA VOLUME 2.0 runtime self-test",
                                    "Status=FAIL", "Unhandled=" + ex });
                    }
                    catch (System.Exception writeEx)
                    {
                        StartupLog("V2 host QA: failed to write FAIL proof: " + writeEx.Message);
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
                Palette = new PaletteSet("IMSAT VOLUME", new Guid("8C96DC13-BC65-4A2C-8B34-2D7644FD1C62"))
                {
                    Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu,
                    MinimumSize = new System.Drawing.Size(360, 450),
                    Size = new System.Drawing.Size(800, 650)
                };
                MainControl = new MainPaletteControl();
                Palette.Add("IMSAT VOLUME", MainControl);
            }
            Palette.Visible = true;
            MainControl.ShowPage(page);
        }

        [CommandMethod("MVBUILDINFO", CommandFlags.Session)]
        public static void BuildInfo()
        {
            string report=RuntimeBuildIdentity.Describe();
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n"+report);
            System.Windows.Forms.MessageBox.Show(report,"IMSAT — Kiểm tra DLL đang nạp");
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
                "\nIMSAT VOLUME: tự động mở bảng khi khởi động AutoCAD = " + (next ? "BẬT" : "TẮT") + ".");
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
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nIMSAT VOLUME: đã lưu Project vào DWG lúc " + saved.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + ".");
        }

        [CommandMethod("MV_PROJECT_LOAD", CommandFlags.Session)]
        public static void LoadProject()
        {
            var r = Services.ProjectPersistenceService.LoadCurrentProject(true);
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nIMSAT VOLUME: " + r.Message);
            _loadedDocument = AcApp.DocumentManager.MdiActiveDocument;
            _loadedDrawingFingerprint = CurrentFingerprint();
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
                        doc.Editor.WriteMessage("\nIMSAT VOLUME: Project đã nạp với " + r.Warnings.Count + " cảnh báo. Mở trang Dự án để xem/kiểm soát.");
                }
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nIMSAT VOLUME: chưa tự nạp được Project - " + ex.Message);
            }
            _loadedDocument = doc;
            _loadedDrawingFingerprint = fingerprint;
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
                ed?.WriteMessage("\nIMSAT VOLUME v1.0 SELFTEST: PASS (" + r.Checks.Count + " checks). Log: " + r.OutputFile);
            else
                ed?.WriteMessage("\nIMSAT VOLUME v1.0 SELFTEST: FAIL (" + r.Errors.Count + " errors). Log: " + r.OutputFile);
        }

        // Host QA must never accidentally execute MVSELFTEST registered by an
        // older V1/NAMVECTA bundle that AutoCAD autoloaded before our NETLOAD.
        // The unique QA command exists only in this repaired V2 build.
        [CommandMethod("NAMVECTA_QA_SELFTEST_V2", CommandFlags.Session)]
        public static void QaSelfTestV2() => SelfTest();

        [CommandMethod("MVABOUT")]
        public void About()
        {
            AcApp.ShowAlertDialog("IMSAT VOLUME\nMine Survey & Earthwork for AutoCAD\nPhát triển: Bùi Thế Nam\nĐT/Zalo: 0967280686\nPhiên bản V2.0 (QA)");
        }
    }
}