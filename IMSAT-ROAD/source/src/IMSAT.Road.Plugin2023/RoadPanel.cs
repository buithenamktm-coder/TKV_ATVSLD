using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace IMSAT.Road.Plugin2023
{
    internal sealed class RoadPanel : UserControl
    {
        private readonly Color _ink=Color.FromArgb(30,45,61);
        private readonly Color _accent=Color.FromArgb(11,93,143);
        private readonly Color _surface=Color.FromArgb(244,247,250);

        public RoadPanel()
        {
            AutoScaleMode=AutoScaleMode.Font;
            AutoScroll=true;
            BackColor=Color.White;
            Padding=new Padding(12);
            var layout=new TableLayoutPanel {
                Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=1,
                Margin=new Padding(0),Padding=new Padding(0),BackColor=Color.White
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            Controls.Add(layout);

            var title=new Panel{Height=78,Dock=DockStyle.Top,BackColor=_surface,Margin=new Padding(0,0,0,12)};
            var name=new Label{Text="IMSAT ROAD",Font=new Font("Segoe UI",17,FontStyle.Bold),
                ForeColor=_accent,AutoSize=true,Location=new Point(14,9)};
            title.Controls.Add(name);
            title.Controls.Add(new Label {Text="Thiết kế đường giao thông và đường vận tải mỏ",
                ForeColor=_ink,Font=new Font("Segoe UI",9),AutoSize=true,Location=new Point(16,48)});
            string logo=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "Assets","imsat_logo_64.png");
            if(File.Exists(logo))
            {
                try
                {
                    var img=Image.FromFile(logo);
                    title.Controls.Add(new PictureBox{Image=img,SizeMode=PictureBoxSizeMode.Zoom,
                        Size=new Size(52,52),Anchor=AnchorStyles.Top|AnchorStyles.Right,
                        Location=new Point(310,12)});
                }
                catch (Exception) { }
            }
            AddRow(layout,title);
            AddHeading(layout,"1. Khởi tạo từ bình đồ");
            AddButton(layout,"Tạo tuyến từ Polyline  ·  IR_TT","IR_TT",true);
            AddButton(layout,"Chọn TIN hiện trạng  ·  IR_DH","IR_DH",false);
            AddButton(layout,"Bố trí cọc trên tuyến  ·  IR_COC","IR_COC",false);
            AddHeading(layout,"2. Trắc dọc và trắc ngang tự nhiên");
            AddButton(layout,"Vẽ trắc dọc tự nhiên  ·  IR_TD","IR_TD",true);
            AddButton(layout,"Vẽ trắc ngang tự nhiên  ·  IR_TN","IR_TN",false);
            AddHeading(layout,"3. Các phân hệ đang phát triển");
            AddDisabled(layout,"Đường cong nằm, siêu cao và mở rộng");
            AddDisabled(layout,"Đường đỏ, đường cong đứng");
            AddDisabled(layout,"Nền đường, taluy, khối lượng và Excel");
            AddHeading(layout,"4. Kiểm tra");
            AddButton(layout,"Kiểm tra nạp add-in  ·  IR_SELFTEST","IR_SELFTEST",false);
            AddButton(layout,"Thông tin phiên bản","IR_ABOUT",false);

            var notes=new Label {AutoSize=true,MaximumSize=new Size(385,0),
                Font=new Font("Segoe UI",9),ForeColor=Color.FromArgb(87,100,115),
                Margin=new Padding(2,8,2,10),
                Text="Bản Preview: chỉ một TIN hiện trạng, chọn 3DFACE thuộc layer TIN. " +
                  "Mặc định layer MV_TIN_HIENTRANG từ IMSAT VOLUME. " +
                  "Các lệnh có tiền tố IR_ để không xung đột với VNRoad. " +
                  "Cấu hình tiêu chuẩn: TCVN 4054:2005 / III / đồng bằng; " +
                  "kiểm tra chỉ tiêu chưa công nhận cho đến khi nạp bảng giới hạn đã xác minh."};
            AddRow(layout,notes);
        }

        private static void AddRow(TableLayoutPanel table,Control control)
        {
            control.Dock=DockStyle.Top;
            table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(control,0,table.RowCount-1);
        }

        private void AddHeading(TableLayoutPanel table,string heading)
        {
            AddRow(table,new Label{Text=heading,Font=new Font("Segoe UI",10,FontStyle.Bold),
                ForeColor=_ink,AutoSize=true,Margin=new Padding(3,10,3,6)});
        }

        private void AddButton(TableLayoutPanel table,string title,string command,bool primary)
        {
            var button=new Button {
                Text=title,Height=40,Dock=DockStyle.Top,
                Font=new Font("Segoe UI",9,FontStyle.Regular),
                FlatStyle=FlatStyle.Flat,TextAlign=ContentAlignment.MiddleLeft,
                Padding=new Padding(10,0,5,0),
                BackColor=primary?_accent:_surface,
                ForeColor=primary?Color.White:_ink,
                Margin=new Padding(1,2,1,3)
            };
            button.FlatAppearance.BorderColor=Color.FromArgb(211,221,231);
            button.Click+=(s,e)=>RoadEntry.Execute(command);
            AddRow(table,button);
        }

        private void AddDisabled(TableLayoutPanel table,string title)
        {
            var label=new Label{Text="  "+title+"  (chưa hỗ trợ)",
                AutoSize=true,Margin=new Padding(4,4,3,4),ForeColor=Color.Gray,
                Font=new Font("Segoe UI",9)};
            AddRow(table,label);
        }
    }
}
