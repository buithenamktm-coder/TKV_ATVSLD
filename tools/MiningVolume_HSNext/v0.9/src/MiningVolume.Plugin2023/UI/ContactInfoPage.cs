using System;
using System.Drawing;
using System.Windows.Forms;

namespace MiningVolume2023.UI
{
    /// <summary>Single contact/about page; the existing licensing workflow remains available below.</summary>
    public sealed class ContactInfoPage : UserControl
    {
        public ContactInfoPage()
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.Canvas;
            AutoScroll = true;
            var version = typeof(MainPaletteControl).Assembly.GetName().Version;
            var intro = new Label
            {
                Dock = DockStyle.Top,
                Height = 180,
                AutoSize = false,
                Font = new Font("Arial", 10F),
                ForeColor = UiTheme.TextStrong,
                Padding = new Padding(12),
                Text = "NAMVECTA VOLUME 2.0" + Environment.NewLine +
                       "Phiên bản: " + (version == null ? "V2.0" : version.ToString()) + Environment.NewLine +
                       "Phát triển: Bùi Thế Nam" + Environment.NewLine +
                       "Điện thoại/Zalo: 0967280686" + Environment.NewLine + Environment.NewLine +
                       "Tạo mô hình TIN, tính khối lượng theo mặt cắt hoặc lưới ô vuông, " +
                       "vẽ mặt cắt và kết xuất bảng CAD/Excel."
            };
            var logoPanel = new Panel { Dock = DockStyle.Top, Height = 118,
                BackColor = Color.White };
            using (var stream = typeof(MainPaletteControl).Assembly.GetManifestResourceStream(
                "MiningVolume2023.Assets.namvecta_pa3_full.png"))
            {
                if (stream != null)
                {
                    Bitmap raster;
                    using (var original = Image.FromStream(stream))
                        raster = new Bitmap(original);
                    var brand = new PictureBox { Dock = DockStyle.Fill,
                        SizeMode = PictureBoxSizeMode.Zoom, Image = raster };
                    brand.Disposed += (s, e) => raster.Dispose();
                    logoPanel.Controls.Add(brand);
                }
            }
            var license = new LicensePage { Dock = DockStyle.Fill };
            var licenseHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            licenseHost.Controls.Add(license);
            Controls.Add(licenseHost);
            Controls.Add(intro);
            Controls.Add(logoPanel);
        }
    }
}
