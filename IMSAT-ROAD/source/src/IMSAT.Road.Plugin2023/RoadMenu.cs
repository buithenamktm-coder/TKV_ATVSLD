using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IMSAT.Road.Plugin2023
{
    internal static class RoadMenu
    {
        private static bool _ready;
        private static string Macro(string cmd) => "\x03\x03_" + cmd + " ";
        internal static void Ensure()
        {
            if(_ready)return;
            var doc=AcApp.DocumentManager.MdiActiveDocument;
            if(doc==null)throw new InvalidOperationException("No active DWG.");
            dynamic acad=AcApp.AcadApplication;
            dynamic groups=acad.MenuGroups;
            if(groups==null||groups.Count==0)throw new InvalidOperationException("Menu group unavailable.");
            dynamic menus=groups.Item(0).Menus;
            dynamic menu=null;
            for(int i=0;i<menus.Count;i++)
            {
                dynamic candidate=menus.Item(i);
                if(string.Equals(Convert.ToString(candidate.Name),"IMSAT ROAD",StringComparison.OrdinalIgnoreCase))
                {menu=candidate;break;}
            }
            if(menu==null)menu=menus.Add("IMSAT ROAD");
            for(int i=menu.Count-1;i>=0;i--)menu.Item(i).Delete();
            int pos=0;
            menu.AddMenuItem(pos++,"Bảng điều khiển",Macro("IRROAD"));
            menu.AddSeparator(pos++);
            menu.AddMenuItem(pos++,"Dữ liệu bình đồ / TIN hiện trạng",Macro("IR_DH"));
            menu.AddMenuItem(pos++,"Tuyến / Tạo tuyến",Macro("IR_TT"));
            menu.AddMenuItem(pos++,"Tuyến / Bố trí cọc",Macro("IR_COC"));
            menu.AddSeparator(pos++);
            menu.AddMenuItem(pos++,"Trắc dọc / Vẽ tự nhiên",Macro("IR_TD"));
            menu.AddMenuItem(pos++,"Trắc ngang / Vẽ tự nhiên",Macro("IR_TN"));
            menu.AddSeparator(pos++);
            menu.AddMenuItem(pos++,"Tự kiểm tra",Macro("IR_SELFTEST"));
            menu.AddMenuItem(pos++,"Thông tin",Macro("IR_ABOUT"));
            if(!Convert.ToBoolean(menu.OnMenuBar))menu.InsertInMenuBar(acad.MenuBar.Count+1);
            _ready=true;
        }
    }
}
