using Microsoft.Win32;

namespace MiningVolume2023.Services
{
    /// <summary>
    /// Tùy chọn cục bộ theo người dùng Windows.
    /// Không lưu vào DWG vì đây là hành vi giao diện của AutoCAD, không phải dữ liệu dự án.
    /// </summary>
    public static class UserSettingsService
    {
        private const string RegistryPath = @"Software\MiningVolume2023";
        private const string AutoOpenPaletteValue = "AutoOpenPalette";

        /// <summary>
        /// Mặc định false để MiningVolume không tự che vùng vẽ mỗi lần khởi động AutoCAD.
        /// Người dùng vẫn mở palette bất kỳ lúc nào từ Ribbon/menu MiningVolume.
        /// </summary>
        public static bool AutoOpenPalette
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false))
                    {
                        if (key == null) return false;
                        var value = key.GetValue(AutoOpenPaletteValue);
                        if (value is int n) return n != 0;
                        if (value is string s && bool.TryParse(s, out var b)) return b;
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    key.SetValue(AutoOpenPaletteValue, value ? 1 : 0, RegistryValueKind.DWord);
                }
            }
        }
    }
}
