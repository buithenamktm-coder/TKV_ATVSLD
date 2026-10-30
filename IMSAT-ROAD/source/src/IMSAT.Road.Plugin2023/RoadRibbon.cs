using System;
using System.Collections.Generic;
using System.Windows.Input;
using Autodesk.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IMSAT.Road.Plugin2023
{
    internal static class RoadRibbon
    {
        private static bool _ready;
        internal static void Ensure()
        {
            if(_ready)return;
            var ribbon=ComponentManager.Ribbon;
            if(ribbon==null)throw new InvalidOperationException("Ribbon chua san sang.");
            foreach(RibbonTab existing in ribbon.Tabs)
                if(existing.Id=="IMSAT_ROAD_TAB") { _ready=true; return; }
            var tab=new RibbonTab{Id="IMSAT_ROAD_TAB",Title="IMSAT ROAD"};
            var panel=new RibbonPanelSource{Title="Thiết kế tuyến"};
            foreach(var item in new[]{
                new Tuple<string,string>("Bảng điều khiển","IRROAD"),
                new Tuple<string,string>("Tạo tuyến","IR_TT"),
                new Tuple<string,string>("Chọn TIN hiện trạng","IR_DH"),
                new Tuple<string,string>("Bố trí cọc","IR_COC"),
                new Tuple<string,string>("Trắc dọc tự nhiên","IR_TD"),
                new Tuple<string,string>("Trắc ngang tự nhiên","IR_TN"),
                new Tuple<string,string>("Kiểm tra add-in","IR_SELFTEST")
            })
                panel.Items.Add(new RibbonButton {
                    Text=item.Item1,ShowText=true,Size=RibbonItemSize.Standard,
                    CommandParameter=item.Item2,CommandHandler=new RoadCommandHandler()
                });
            tab.Panels.Add(new RibbonPanel{Source=panel});
            ribbon.Tabs.Add(tab);
            _ready=true;
        }

        private sealed class RoadCommandHandler:ICommand
        {
            public bool CanExecute(object parameter)=>true;
            public event EventHandler CanExecuteChanged {add{}remove{}}
            public void Execute(object parameter)
            {
                if(parameter is string cmd)RoadEntry.Execute(cmd);
            }
        }
    }
}
