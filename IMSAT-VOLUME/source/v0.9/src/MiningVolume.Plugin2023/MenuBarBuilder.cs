using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MiningVolume2023
{
    /// <summary>Classic AutoCAD menu, refreshed once per add-in load.</summary>
    internal static class MenuBarBuilder
    {
        private const string MenuName = "IMSAT VOLUME";
        private const string LegacyMenuName = "MINING VOLUME";
        private static bool _attempted;

        // ActiveX accepts control characters, as in Autodesk's AddMenuItem
        // example (Chr(3), Chr(3), command, space). Do not pass CUI caret text.
        private static string CommandMacro(string command)
            => "\u0003\u0003_" + command + " ";

        internal static void EnsureMenu()
        {
            if (_attempted) return;
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                dynamic acad = AcApp.AcadApplication;
                dynamic groups = acad.MenuGroups;
                if (groups == null || groups.Count == 0) return;

                dynamic group = groups.Item(0);
                dynamic menus = group.Menus;
                dynamic menu = null;
                // Inspect existing popups before rebuilding our menu.
                for (int i = menus.Count - 1; i >= 0; i--)
                {
                    dynamic candidate = menus.Item(i);
                    string name = Convert.ToString(candidate.Name);
                    if (string.Equals(name, MenuName, StringComparison.OrdinalIgnoreCase))
                        menu = candidate;
                    else if (string.Equals(name, LegacyMenuName, StringComparison.OrdinalIgnoreCase) &&
                             Convert.ToBoolean(candidate.OnMenuBar))
                        candidate.RemoveFromMenuBar();
                }

                if (menu == null)
                    menu = menus.Add(MenuName);

                // AutoCAD can retain this popup from an earlier installation.
                // Refresh its items too; otherwise updating the DLL leaves old
                // macros in place. Keep the popup itself and its menu-bar position.
                for (int i = menu.Count - 1; i >= 0; i--)
                    menu.Item(i).Delete();

                int p = 0;
                menu.AddMenuItem(p++, "Hiện / Ẩn IMSAT VOLUME", CommandMacro("TKL"));
                menu.AddSeparator(p++);
                menu.AddMenuItem(p++, "Dự án", CommandMacro("MV_PROJECT"));
                menu.AddMenuItem(p++, "Dữ liệu đầu vào", CommandMacro("MV_DATA"));
                menu.AddMenuItem(p++, "TIN / Mô hình", CommandMacro("MV_MODEL"));
                menu.AddMenuItem(p++, "Mặt cắt", CommandMacro("MV_SECTION"));
                menu.AddMenuItem(p++, "Tính khối lượng", CommandMacro("MV_VOLUME"));
                menu.AddMenuItem(p++, "Xuất Excel", CommandMacro("MV_EXPORT"));
                menu.AddSeparator(p++);
                menu.AddMenuItem(p++, "Thông tin IMSAT VOLUME", CommandMacro("MVABOUT"));

                if (!Convert.ToBoolean(menu.OnMenuBar))
                    menu.InsertInMenuBar(acad.MenuBar.Count + 1);

                // Only mark success after the menu is populated and visible.
                _attempted = true;
            }
            catch (Exception ex)
            {
                // Leave retry enabled for the next startup idle event.
                // Report failure instead of silently leaving an inert menu.
                doc.Editor.WriteMessage(
                    "\nIMSAT VOLUME: chưa cập nhật được Menu Bar: " + ex.Message);
            }
        }
    }
}
