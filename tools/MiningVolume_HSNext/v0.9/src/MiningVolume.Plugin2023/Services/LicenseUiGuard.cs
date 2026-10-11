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
                "Mở mục Bản quyền của IMSAT VOLUME để nhập key." + Environment.NewLine +
                "Bản vẽ DWG và dữ liệu của người dùng không bị xóa.",
                "IMSAT VOLUME – Cần kích hoạt",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }
}
