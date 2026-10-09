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
        private static bool _startupUiOpened;
        internal static string StartupUiError { get; private set; }
        private static string _loadedDrawingFingerprint;

        public void Initialize()
        {
            StartupLog("Initialize: MiningVolume v1.0");
            TryRibbon();
            HookIdle();
        }

        public void Terminate()
        {
            if (_idleHooked) AcApp.Idle -= OnIdle;
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
                        StartupLog("Startup palette auto-open is OFF. Use Ribbon/menu to open MiningVolume.");
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
            if (Palette == null)
            {
                Palette = new PaletteSet("IMSAT VOLUME", new Guid("8C96DC13-BC65-4A2C-8B34-2D7644FD1C62"))
                {
                    Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu,
                    MinimumSize = new System.Drawing.Size(620, 540),
                    Size = new System.Drawing.Size(800, 650)
                };
                MainControl = new MainPaletteControl();
                Palette.Add("IMSAT VOLUME", MainControl);
            }
            Palette.Visible = true;
            MainControl.ShowPage(page);
        }

        [CommandMethod("MVOPEN", CommandFlags.Session)] public static void OpenPalette() => Open(AppPage.Project);

        internal static void TogglePalette()
        {
            if (Palette == null)
            {
                Open(AppPage.Project);
                return;
            }

            Palette.Visible = !Palette.Visible;
            StartupLog("Palette visibility toggled: " + (Palette.Visible ? "ON" : "OFF"));
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
        [CommandMethod("MV_SECTION", CommandFlags.Session)] public static void OpenSection() => Open(AppPage.Section);
        [CommandMethod("MV_VOLUME", CommandFlags.Session)] public static void OpenVolume() => Open(AppPage.Volume);
        [CommandMethod("MV_EXPORT", CommandFlags.Session)] public static void OpenExport() => Open(AppPage.Export);

        [CommandMethod("MV_PROJECT_SAVE", CommandFlags.Session)]
        public static void SaveProject()
        {
            var saved = Services.ProjectPersistenceService.SaveCurrentProject();
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nIMSAT VOLUME: đã lưu Project vào DWG lúc " + saved.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + ".");
        }

        [CommandMethod("MV_PROJECT_LOAD", CommandFlags.Session)]
        public static void LoadProject()
        {
            var r = Services.ProjectPersistenceService.LoadCurrentProject(true);
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nIMSAT VOLUME: " + r.Message);
            _loadedDrawingFingerprint = CurrentFingerprint();
            Open(AppPage.Project);
        }

        private static void EnsureCurrentDrawingProjectLoaded()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            string fingerprint = CurrentFingerprint();
            if (string.Equals(_loadedDrawingFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase)) return;

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
                ed?.WriteMessage("\nMiningVolume v1.0 SELFTEST: PASS (" + r.Checks.Count + " checks). Log: " + r.OutputFile);
            else
                ed?.WriteMessage("\nMiningVolume v1.0 SELFTEST: FAIL (" + r.Errors.Count + " errors). Log: " + r.OutputFile);
        }

        [CommandMethod("MVABOUT")]
        public void About()
        {
            AcApp.ShowAlertDialog("IMSAT VOLUME\nMine Survey & Earthwork for AutoCAD\nPhát triển: Bùi Thế Nam\nĐiện thoại: 0967280686\nPhiên bản v1.0");
        }
    }
}