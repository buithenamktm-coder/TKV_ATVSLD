using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(IMSAT.Road.Plugin2023.RoadEntry))]
[assembly: CommandClass(typeof(IMSAT.Road.Plugin2023.RoadEntry))]

namespace IMSAT.Road.Plugin2023
{
    /// <summary>
    /// AutoCAD entry point for IMSAT ROAD. All preview commands use IR_ prefix
    /// to avoid conflicts with TDT/VNRoad aliases (TT, TD, TN, CN...).
    /// </summary>
    public sealed class RoadEntry : IExtensionApplication
    {
        private static PaletteSet _palette;
        private static RoadPanel _panel;
        private static bool _uiReady;
        private static bool _idleAttached;

        public void Initialize()
        {
            _uiReady = false;
            if (!_idleAttached)
            {
                AcApp.Idle += OnIdle;
                _idleAttached = true;
            }
        }

        public void Terminate()
        {
            if (_idleAttached) AcApp.Idle -= OnIdle;
            _idleAttached = false;
        }

        private static void OnIdle(object sender, EventArgs e)
        {
            if (_uiReady) return;
            if (AcApp.DocumentManager.MdiActiveDocument == null) return;
            try
            {
                RoadRibbon.Ensure();
                RoadMenu.Ensure();
                _uiReady = true;
                if (_idleAttached) AcApp.Idle -= OnIdle;
                _idleAttached = false;
                AcApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                    "\nIMSAT ROAD: san sang. Mo Ribbon IMSAT ROAD hoac go IRROAD.");
            }
            catch (System.Exception)
            {
                // Workspace/ribbon/menu may not be ready until next Idle.
                // IRROAD always opens the palette even without a Ribbon.
            }
        }

        internal static void Open()
        {
            if (_palette == null)
            {
                _palette = new PaletteSet("IMSAT ROAD", new Guid("90FFED4F-8D0E-4D96-9985-9FD44023C930"))
                {
                    Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton,
                    MinimumSize = new System.Drawing.Size(410, 430),
                    Size = new System.Drawing.Size(540, 630),
                    DockEnabled = DockSides.Left | DockSides.Right
                };
                _panel = new RoadPanel();
                _palette.Add("Thiết kế đường", _panel);
            }
            _palette.Visible = true;
        }

        internal static void Execute(string command)
        {
            var document = AcApp.DocumentManager.MdiActiveDocument;
            if (document == null) { AcApp.ShowAlertDialog("Hay mo mot ban ve DWG."); return; }
            // Keep the docked palette visible; CAD prompts run in command context.
            // Dispatch through AutoCAD command context; do not hold a document lock from the palette.
            document.SendStringToExecute("\x03\x03_" + command + " ", true, false, false);
        }

        private static void TryRun(Action action)
        {
            try { action(); }
            catch (System.Exception e)
            {
                try { AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    "\nIMSAT ROAD [LOI]: " + e.Message); } catch { }
                AcApp.ShowAlertDialog("IMSAT ROAD\n" + e.Message);
            }
        }

        [CommandMethod("IRROAD")]
        public void RoadOpen() => TryRun(Open);

        [CommandMethod("IR_TT")]
        public void Alignment() => TryRun(RoadDrawing.SelectAlignment);

        [CommandMethod("IR_DH")]
        public void ExistingTerrain() => TryRun(RoadDrawing.SelectTerrainLayer);

        [CommandMethod("IR_COC")]
        public void Stations() => TryRun(RoadDrawing.CreateStations);

        [CommandMethod("IR_TD")]
        public void Longitudinal() => TryRun(RoadDrawing.DrawLongitudinal);

        [CommandMethod("IR_TN")]
        public void Cross() => TryRun(RoadDrawing.DrawCrossSections);

        [CommandMethod("IR_SELFTEST")]
        public void SelfTest() => TryRun(RoadDrawing.SelfTest);

        [CommandMethod("IR_ABOUT")]
        public void About() => AcApp.ShowAlertDialog(
            "IMSAT ROAD - AutoCAD 2023 Preview\n" +
            "Thiet ke duong giao thong va duong van tai mo\n" +
            "Cong cu da co: tim tuyen, TIN hien trang, coc, TD/TN tu nhien.\n" +
            "Chua co: cong nam, duong do, nen/mat duong, khoi luong.\n" +
            "Nha phat trien: Bui The Nam");
    }
}
