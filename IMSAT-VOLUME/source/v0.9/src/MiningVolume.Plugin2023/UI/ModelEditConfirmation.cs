using System.Drawing;
using System.Windows.Forms;
namespace MiningVolume2023.UI {
    internal static class ModelEditConfirmation {
        internal static bool Show(IWin32Window owner,string title,string message)
        {
            using(var dialog=new Form {Text=title,Font=new Font("Arial",9F),ClientSize=new Size(620,320),
                StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,
                MaximizeBox=false,MinimizeBox=false}) {
                var body=new Label {Text=message,Dock=DockStyle.Fill,Padding=new Padding(15),AutoEllipsis=false};
                var buttons=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=55,
                    FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(10)};
                var no=new Button {Text="Không",DialogResult=DialogResult.No,Width=110,Height=32};
                var yes=new Button {Text="Đồng ý",DialogResult=DialogResult.Yes,Width=110,Height=32};
                buttons.Controls.Add(no);buttons.Controls.Add(yes);
                dialog.Controls.Add(body);dialog.Controls.Add(buttons);
                dialog.AcceptButton=no;dialog.CancelButton=no;
                dialog.Shown+=(s,e)=>no.Focus();
                return dialog.ShowDialog(owner)==DialogResult.Yes;
            }
        }

    }
}
