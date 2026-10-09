using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MiningVolume2023
{
    /// <summary>
    /// Adds a classic AutoCAD menu-bar entry in addition to Ribbon and TKL.
    /// Uses late-bound AutoCAD ActiveX so the menu logic remains isolated from
    /// the calculation core and is easier to adapt across AutoCAD generations.
    /// </summary>
    internal static class MenuBarBuilder
    {
        private const string MenuName = "MINING VOLUME";
        private static bool _attempted;

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
                for (int i = 0; i < menus.Count; i++)
                {
                    dynamic candidate = menus.Item(i);
                    string name = Convert.ToString(candidate.Name);
                    if (string.Equals(name, MenuName, StringComparison.OrdinalIgnoreCase))
                    {
                        menu = candidate;
                        break;
                    }
                }

                if (menu == null)
                {
                    menu = menus.Add(MenuName);
                    int p = 0;
                    menu.AddMenuItem(p++, "Hiện / Ẩn MiningVolume", "^C^CTKL ");
                    menu.AddSeparator(p++);
                    menu.AddMenuItem(p++, "Dự án", "^C^CMV_PROJECT ");
                    menu.AddMenuItem(p++, "Dữ liệu đầu vào", "^C^CMV_DATA ");
                    menu.AddMenuItem(p++, "TIN / Mô hình", "^C^CMV_MODEL ");
                    menu.AddMenuItem(p++, "Mặt cắt", "^C^CMV_SECTION ");
                    menu.AddMenuItem(p++, "Tính khối lượng", "^C^CMV_VOLUME ");
                    menu.AddMenuItem(p++, "Xuất Excel", "^C^CMV_EXPORT ");
                    menu.AddSeparator(p++);
                    menu.AddMenuItem(p++, "Thông tin IMSAT MiningVolume", "^C^CMVABOUT ");

                    try
                    {
                        dynamic menuBar = acad.MenuBar;
                        menu.InsertInMenuBar(menuBar.Count + 1);
                    }
                    catch
                    {
                        try { menu.InsertInMenuBar(999); } catch { }
                    }
                }

                _attempted = true;
            }
            catch
            {
                // Ribbon/TKL remain available if classic Menu Bar is unavailable
                // in the current AutoCAD workspace.
            }
        }
    }
}
