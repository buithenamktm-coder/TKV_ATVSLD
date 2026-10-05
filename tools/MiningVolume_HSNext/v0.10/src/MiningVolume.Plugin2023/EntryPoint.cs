using System;
using System.Windows.Forms;
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
        private static bool _healthCheckDone;
        private static string _loadedDrawingFingerprint;

        public void Initialize()
        {
            TryRibbon();
            HookIdle();
        }

        public void Terminate()
        {
            if (_idleHooked)
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

        private static void OnIdle(object sender, EventArgs e)
        {
            TryRibbon();
            TryInternalHealthCheck();

            if (Autodesk.Windows.ComponentManager.Ribbon != null && _healthCheckDone && _idleHooked)
            {
                AcApp.Idle -= OnIdle;
                _idleHooked = false;
            }
        }

        private static void TryRibbon()
        {
            try { RibbonBuilder.EnsureRibbon(); } catch { }
        }

        private static void TryInternalHealthCheck()
        {
            if (_healthCheckDone || Autodesk.Windows.ComponentManager.Ribbon == null) return;
            _healthCheckDone = true;
            try
            {
                var r = SelfTestService.Run();
                if (!r.Passed)
                {
                    MessageBox.Show(
                        "MiningVolume đã nạp nhưng kiểm tra nội bộ không đạt.\r\n\r\n" +
                        string.Join("\r\n", r.Errors) +
                        "\r\n\r\nLog: " + r.OutputFile,
                        "MiningVolume 2023",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    "MiningVolume không hoàn thành được kiểm tra nội bộ sau khi nạp:\r\n" + ex.Message,
                    "MiningVolume 2023",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
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

        internal static void SaveProjectFromRibbon()
        {
            try
            {
                var saved = ProjectPersistenceService.SaveCurrentProject();
                Open(AppPage.Project);
                MessageBox.Show(
                    "Đã lưu Project vào DWG lúc " + saved.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") + ".",
                    "MiningVolume 2023",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "Lưu Project", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal static void ShowAbout()
        {
            MessageBox.Show(
                "MiningVolume 2023\r\nHS-Next • Mine Survey & Earthwork\r\nv0.10 GUI Preview\r\n\r\nGiao diện Ribbon/Palette gọi trực tiếp C#, không điều khiển bằng chuỗi lệnh.",
                "MiningVolume 2023",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        internal static bool PaletteVisible
        {
            get { return Palette != null && Palette.Visible; }
        }

        internal static void SetPaletteVisible(bool visible)
        {
            if (Palette != null) Palette.Visible = visible;
        }

        [CommandMethod("MVOPEN", CommandFlags.Session)] public static void OpenPalette() => Open(AppPage.Project);
        [CommandMethod("MV_PROJECT", CommandFlags.Session)] public static void OpenProject() => Open(AppPage.Project);
        [CommandMethod("MV_DATA", CommandFlags.Session)] public static void OpenData() => Open(AppPage.Data);
        [CommandMethod("MV_MODEL", CommandFlags.Session)] public static void OpenModel() => Open(AppPage.Model);
        [CommandMethod("MV_SECTION", CommandFlags.Session)] public static void OpenSection() => Open(AppPage.Section);
        [CommandMethod("MV_VOLUME", CommandFlags.Session)] public static void OpenVolume() => Open(AppPage.Volume);
        [CommandMethod("MV_EXPORT", CommandFlags.Session)] public static void OpenExport() => Open(AppPage.Export);

        // Các lệnh dưới đây chỉ là shortcut/diagnostic tùy chọn, không được Ribbon sử dụng.
        [CommandMethod("MV_PROJECT_SAVE", CommandFlags.Session)]
        public static void SaveProject() => SaveProjectFromRibbon();

        [CommandMethod("MV_PROJECT_LOAD", CommandFlags.Session)]
        public static void LoadProject()
        {
            try
            {
                var r = ProjectPersistenceService.LoadCurrentProject(true);
                _loadedDrawingFingerprint = CurrentFingerprint();
                Open(AppPage.Project);
                MessageBox.Show(r.Message, "MiningVolume 2023", MessageBoxButtons.OK,
                    r.Warnings.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "Nạp Project", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                if (ProjectPersistenceService.HasSavedProject())
                    ProjectPersistenceService.LoadCurrentProject(true);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    "Không tự nạp được Project đã lưu trong DWG:\r\n" + ex.Message,
                    "MiningVolume 2023",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
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
            var r = SelfTestService.Run();
            MessageBox.Show(
                r.Passed
                    ? "SELFTEST PASS (" + r.Checks.Count + " kiểm tra).\r\n" + r.OutputFile
                    : "SELFTEST FAIL (" + r.Errors.Count + " lỗi).\r\n" + r.OutputFile,
                "MiningVolume 2023",
                MessageBoxButtons.OK,
                r.Passed ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        [CommandMethod("MVABOUT")]
        public void About() => ShowAbout();
    }
}
