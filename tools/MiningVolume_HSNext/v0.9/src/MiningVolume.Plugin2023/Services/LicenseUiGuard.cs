using System;
using System.Windows.Forms;

namespace MiningVolume2023.Services
{
    internal static class LicenseUiGuard
    {
        internal static bool Check()
        {
            var status = LicenseService.GetStatus();
            if (status.CanCalculate) return true;
            MessageBox.Show(
                status.Description + Environment.NewLine +
                "Mở mục Kết quả → Tùy chọn → Giới thiệu / bản quyền để nhập Pass." + Environment.NewLine +
                "Bản vẽ DWG và dữ liệu của người dùng không bị xóa.",
                "NAMVECTA VOLUME – Cần kích hoạt",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }
}
