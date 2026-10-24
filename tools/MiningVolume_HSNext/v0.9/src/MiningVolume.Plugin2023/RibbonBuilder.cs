using Autodesk.Windows;
using System;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using MiningVolume2023.UI;
using MiningVolume2023.Services;

namespace MiningVolume2023
{
    internal static class RibbonBuilder
    {
        private const string TabId = "MININGVOLUME_TAB";

        internal static void EnsureRibbon()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;
            foreach (var t in ribbon.Tabs)
            {
                if (t.Id != TabId) continue;
                t.Title = "NAMVECTA VOLUME";
                return;
            }

            var tab = new RibbonTab { Title = "NAMVECTA VOLUME", Id = TabId };
            ribbon.Tabs.Add(tab);

            AddPanel(tab, "1. Dữ liệu", new[]
            {
                Btn("Mở / Ẩn", RibbonAction.TogglePalette),
                Btn("TIN", RibbonAction.Model)
            });
            AddPanel(tab, "2. Tính toán", new[]
            {
                Btn("Mặt cắt", RibbonAction.Section),
                Btn("Lưới ô vuông", RibbonAction.Grid)
            });
            AddPanel(tab, "3. Kết quả", new[]
            {
                Btn("Xuất kết quả", RibbonAction.Export),
                Btn("Thông tin", RibbonAction.About)
            });
        }

        private static RibbonButton Btn(string text, RibbonAction action)
        {
            var button = new RibbonButton
            {
                Text = text,
                ShowText = true,
                Size = RibbonItemSize.Standard,
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                CommandParameter = action,
                CommandHandler = new DirectHandler()
            };
            var logo = LoadPa3Icon();
            if (logo != null)
            {
                button.Image = logo;
                button.LargeImage = logo;
            }
            return button;
        }

        private static ImageSource LoadPa3Icon()
        {
            try
            {
                using (var stream = typeof(RibbonBuilder).Assembly.GetManifestResourceStream(
                    "MiningVolume2023.Assets.namvecta_pa3_symbol.png"))
                {
                    if (stream == null) return null;
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch { return null; }
        }

        private static void AddPanel(RibbonTab tab, string title, RibbonButton[] buttons)
        {
            var src = new RibbonPanelSource { Title = title };
            foreach (var b in buttons) src.Items.Add(b);
            tab.Panels.Add(new RibbonPanel { Source = src });
        }

        private enum RibbonAction
        {
            TogglePalette,
            Project,
            SaveProject,
            Data,
            Model,
            Section,
            Grid,
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
                    case RibbonAction.TogglePalette:
                        EntryPoint.TogglePalette();
                        break;
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
                    case RibbonAction.Grid:
                        EntryPoint.Open(AppPage.Grid);
                        break;
                    case RibbonAction.Volume:
                        EntryPoint.Open(AppPage.Volume);
                        break;
                    case RibbonAction.Export:
                        EntryPoint.Open(AppPage.Export);
                        break;
                    case RibbonAction.About:
                        AcApp.ShowAlertDialog("NAMVECTA VOLUME 2.0 QA\n" +
                            RuntimeBuildIdentity.ShortTag() + "\n" +
                            "Mine Survey & Earthwork for AutoCAD\n" +
                            "Phát triển: Bùi Thế Nam\nĐT/Zalo: 0967280686\n" +
                            "Dùng lệnh MVBUILDINFO để xác minh DLL đang nạp.");
                        break;
                }
            }
        }
    }
}
