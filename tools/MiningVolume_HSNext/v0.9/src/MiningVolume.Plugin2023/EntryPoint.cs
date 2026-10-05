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
        private static string _loadedDrawingFingerprint;

        public void Initialize()
        {
            StartupLog("Initialize: MiningVolume v0.10.1");
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
                    Open(AppPage.Project);
                    _startupUiOpened = true;
                    StartupLog("Startup palette opened successfully. Ribbon=" +
                        (Autodesk.Windows.ComponentManager.Ribbon == null ? "OFF/Unavailable" : "Available"));
                }
                catch (System.Exception ex)
                {
                    StartupLog("Startup palette FAILED: " + ex);
                    _startupUiOpened = true;
                    try
                    {
                        AcApp.ShowAlertDialog("MiningVolume đã được nạp nhưng không mở được giao diện.\n\n" +
                            ex.Message + "\n\nXem log tại: " + StartupLogPath());
                    }
                    catch { }
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
                Palette = new PaletteSet("MINING VOLUME", new Guid("8C96DC13-BC65-4A2C-8B34-2D7644FD1C62"))
                {
                    Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu,
                    MinimumSize = new System.Drawing.Size(580, 620),
                    Size = new System.Drawing.Size(820, 760)
                };
                MainControl = new MainPaletteControl();
                Palette.Add("Phần mềm", MainControl);
            }
            Palette.Visible = true;
            MainControl.ShowPage(page);
        }

        [CommandMethod("MVOPEN", CommandFlags.Session)] public static void OpenPalette() => Open(AppPage.Project);
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
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nMiningVolume: đã lưu Project vào DWG lúc " + saved.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + ".");
        }

        [CommandMethod("MV_PROJECT_LOAD", CommandFlags.Session)]
        public static void LoadProject()
        {
            var r = Services.ProjectPersistenceService.LoadCurrentProject(true);
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nMiningVolume: " + r.Message);
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
                        doc.Editor.WriteMessage("\nMiningVolume: Project đã nạp với " + r.Warnings.Count + " cảnh báo. Mở trang Dự án để xem/kiểm soát.");
                }
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\nMiningVolume: chưa tự nạp được Project - " + ex.Message);
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
                ed?.WriteMessage("\nMiningVolume v0.10.1 SELFTEST: PASS (" + r.Checks.Count + " checks). Log: " + r.OutputFile);
            else
                ed?.WriteMessage("\nMiningVolume v0.10.1 SELFTEST: FAIL (" + r.Errors.Count + " errors). Log: " + r.OutputFile);
        }

        [CommandMethod("MVABOUT")]
        public void About()
        {
            AcApp.ShowAlertDialog("MiningVolume 2023\nHS-Next • Mine Survey & Earthwork\nGUI-first v0.10.1");
        }
    }
}