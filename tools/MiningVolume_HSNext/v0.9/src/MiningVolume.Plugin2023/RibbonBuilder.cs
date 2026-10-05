using Autodesk.Windows;
using System.Windows.Input;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MiningVolume2023
{
    internal static class RibbonBuilder
    {
        private const string TabId = "MININGVOLUME_TAB";
        internal static void EnsureRibbon()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;
            foreach (var t in ribbon.Tabs) if (t.Id == TabId) return;
            var tab = new RibbonTab { Title = "MINING VOLUME", Id = TabId };
            ribbon.Tabs.Add(tab);
            AddPanel(tab, "Dự án", new[] { Btn("Project", "MV_PROJECT "), Btn("Lưu Project", "MV_PROJECT_SAVE ") });
            AddPanel(tab, "Dữ liệu", new[] { Btn("Nạp dữ liệu", "MV_DATA "), Btn("Mô hình X-Y-Z", "MV_MODEL ") });
            AddPanel(tab, "Mô hình", new[] { Btn("Quản lý / TIN", "MV_MODEL ") });
            AddPanel(tab, "Mặt cắt", new[] { Btn("Hệ mặt cắt", "MV_SECTION ") });
            AddPanel(tab, "Khối lượng", new[] { Btn("Tính khối lượng", "MV_VOLUME ") });
            AddPanel(tab, "Báo cáo", new[] { Btn("Xuất Excel", "MV_EXPORT "), Btn("Thông tin", "MVABOUT ") });
        }
        private static RibbonButton Btn(string text, string cmd) => new RibbonButton { Text = text, ShowText = true, Size = RibbonItemSize.Large, Orientation = System.Windows.Controls.Orientation.Vertical, CommandParameter = cmd, CommandHandler = new Handler() };
        private static void AddPanel(RibbonTab tab, string title, RibbonButton[] buttons)
        {
            var src = new RibbonPanelSource { Title = title };
            foreach (var b in buttons) src.Items.Add(b);
            tab.Panels.Add(new RibbonPanel { Source = src });
        }
        private sealed class Handler : ICommand
        {
            public bool CanExecute(object parameter) => true;
            public event System.EventHandler CanExecuteChanged { add { } remove { } }
            public void Execute(object parameter)
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc != null) doc.SendStringToExecute((parameter as string) ?? "MVOPEN ", true, false, false);
            }
        }
    }
}