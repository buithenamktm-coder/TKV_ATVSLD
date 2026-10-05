using Autodesk.Windows;
using System;
using System.Windows.Input;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using MiningVolume2023.UI;

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

            AddPanel(tab, "Dự án", new[]
            {
                Btn("Project", RibbonAction.Project),
                Btn("Lưu Project", RibbonAction.SaveProject)
            });
            AddPanel(tab, "Dữ liệu", new[]
            {
                Btn("Nạp dữ liệu", RibbonAction.Data),
                Btn("Mô hình X-Y-Z", RibbonAction.Model)
            });
            AddPanel(tab, "Mô hình", new[]
            {
                Btn("Quản lý / TIN", RibbonAction.Model)
            });
            AddPanel(tab, "Mặt cắt", new[]
            {
                Btn("Hệ mặt cắt", RibbonAction.Section)
            });
            AddPanel(tab, "Khối lượng", new[]
            {
                Btn("Tính khối lượng", RibbonAction.Volume)
            });
            AddPanel(tab, "Báo cáo", new[]
            {
                Btn("Xuất Excel", RibbonAction.Export),
                Btn("Thông tin", RibbonAction.About)
            });
        }

        private static RibbonButton Btn(string text, RibbonAction action)
        {
            return new RibbonButton
            {
                Text = text,
                ShowText = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                CommandParameter = action,
                CommandHandler = new DirectHandler()
            };
        }

        private static void AddPanel(RibbonTab tab, string title, RibbonButton[] buttons)
        {
            var src = new RibbonPanelSource { Title = title };
            foreach (var b in buttons) src.Items.Add(b);
            tab.Panels.Add(new RibbonPanel { Source = src });
        }

        private enum RibbonAction
        {
            Project,
            SaveProject,
            Data,
            Model,
            Section,
            Volume,
            Export,
            About
        }

        /// <summary>
        /// GUI-first: Ribbon calls C# directly with no command-string indirection
        /// in the normal user workflow.
        /// </summary>
        private sealed class DirectHandler : ICommand
        {
            public bool CanExecute(object parameter) => true;
            public event EventHandler CanExecuteChanged { add { } remove { } }

            public void Execute(object parameter)
            {
                if (!(parameter is RibbonAction action)) return;

                switch (action)
                {
                    case RibbonAction.Project:
                        EntryPoint.Open(AppPage.Project);
                        break;
                    case RibbonAction.SaveProject:
                        EntryPoint.SaveProject();
                        break;
                    case RibbonAction.Data:
                        EntryPoint.Open(AppPage.Data);
                        break;
                    case RibbonAction.Model:
                        EntryPoint.Open(AppPage.Model);
                        break;
                    case RibbonAction.Section:
                        EntryPoint.Open(AppPage.Section);
                        break;
                    case RibbonAction.Volume:
                        EntryPoint.Open(AppPage.Volume);
                        break;
                    case RibbonAction.Export:
                        EntryPoint.Open(AppPage.Export);
                        break;
                    case RibbonAction.About:
                        AcApp.ShowAlertDialog("MiningVolume 2023\nHS-Next • Mine Survey & Earthwork\nGUI-first v0.10");
                        break;
                }
            }
        }
    }
}
