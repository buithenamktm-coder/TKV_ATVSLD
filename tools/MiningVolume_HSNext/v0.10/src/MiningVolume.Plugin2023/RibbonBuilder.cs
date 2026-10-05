using System;
using System.Windows.Input;
using Autodesk.Windows;
using MiningVolume2023.UI;

namespace MiningVolume2023
{
    /// <summary>
    /// Ribbon chỉ gọi trực tiếp C# UI/service. Không gửi chuỗi lệnh xuống Command Line.
    /// Các CommandMethod trong EntryPoint chỉ còn là shortcut/diagnostic tùy chọn.
    /// </summary>
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

            AddPanel(tab, "Dự án", new[]
            {
                Btn("Project", () => EntryPoint.Open(AppPage.Project)),
                Btn("Lưu Project", EntryPoint.SaveProjectFromRibbon)
            });
            AddPanel(tab, "Dữ liệu", new[]
            {
                Btn("Nạp dữ liệu", () => EntryPoint.Open(AppPage.Data)),
                Btn("Mô hình X-Y-Z", () => EntryPoint.Open(AppPage.Model))
            });
            AddPanel(tab, "Mô hình", new[]
            {
                Btn("Quản lý / TIN", () => EntryPoint.Open(AppPage.Model))
            });
            AddPanel(tab, "Mặt cắt", new[]
            {
                Btn("Hệ mặt cắt", () => EntryPoint.Open(AppPage.Section))
            });
            AddPanel(tab, "Khối lượng", new[]
            {
                Btn("Tính khối lượng", () => EntryPoint.Open(AppPage.Volume))
            });
            AddPanel(tab, "Báo cáo", new[]
            {
                Btn("Xuất Excel", () => EntryPoint.Open(AppPage.Export)),
                Btn("Thông tin", EntryPoint.ShowAbout)
            });
        }

        private static RibbonButton Btn(string text, Action action)
        {
            return new RibbonButton
            {
                Text = text,
                ShowText = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                CommandHandler = new DirectHandler(action)
            };
        }

        private static void AddPanel(RibbonTab tab, string title, RibbonButton[] buttons)
        {
            var src = new RibbonPanelSource { Title = title };
            foreach (var b in buttons) src.Items.Add(b);
            tab.Panels.Add(new RibbonPanel { Source = src });
        }

        private sealed class DirectHandler : ICommand
        {
            private readonly Action _action;
            public DirectHandler(Action action) { _action = action; }
            public bool CanExecute(object parameter) => _action != null;
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public void Execute(object parameter) => _action?.Invoke();
        }
    }
}
