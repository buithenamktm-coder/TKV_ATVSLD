using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MiningVolume.Core.Surface;

namespace MiningVolume2023.UI
{
    internal static class DuplicateXYConflictUi
    {
        public static DuplicateXYConflictPolicy? Ask(
            IWin32Window owner,
            DuplicateXYConflictException ex)
        {
            int count = ex?.Issues?.Count ?? 0;
            var first = ex?.Issues?.FirstOrDefault();

            string detail = first == null
                ? string.Empty
                : "\r\n\r\nVí dụ:\r\n" + first.Message;

            var continueResult = MessageBox.Show(
                owner,
                $"Phát hiện {count:n0} vị trí trùng XY nhưng khác cao độ Z trong mô hình {ex?.ModelName ?? "TIN"}.\r\n\r\n" +
                "Bạn có muốn TIẾP TỤC tạo TIN khi dữ liệu có lỗi này không?" +
                detail +
                "\r\n\r\nNếu chọn Có, phần mềm sẽ yêu cầu chọn dùng ĐỈNH TRÊN hoặc ĐỈNH DƯỚI. " +
                "Bản vẽ CAD gốc không bị sửa.",
                "MiningVolume - Xung đột cao độ tại cùng XY",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (continueResult != DialogResult.Yes)
                return null;

            using (var form = new DuplicateXYChoiceDialog(count))
            {
                return form.ShowDialog(owner) == DialogResult.OK
                    ? form.Policy
                    : (DuplicateXYConflictPolicy?)null;
            }
        }
    }

    internal sealed class DuplicateXYChoiceDialog : Form
    {
        public DuplicateXYConflictPolicy Policy { get; private set; } = DuplicateXYConflictPolicy.Stop;

        public DuplicateXYChoiceDialog(int conflictCount)
        {
            Text = "MiningVolume - Chọn cách xử lý cao độ";
            Font = new Font("Arial", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 540;
            Height = 250;

            var title = new Label
            {
                Left = 18,
                Top = 16,
                Width = 490,
                Height = 42,
                Text = $"Có {conflictCount:n0} vị trí lỗi trùng XY khác Z. Chọn quy tắc dùng cho các vị trí lỗi này:",
                Font = new Font("Arial", 10F, FontStyle.Bold)
            };
            Controls.Add(title);

            var note = new Label
            {
                Left = 18,
                Top = 62,
                Width = 490,
                Height = 48,
                Text = "Đỉnh trên = lấy Z lớn hơn.   Đỉnh dưới = lấy Z nhỏ hơn.\r\n" +
                       "Chỉ áp dụng cho mô hình TIN đang dựng; không thay đổi đối tượng CAD nguồn."
            };
            Controls.Add(note);

            var upper = new Button
            {
                Text = "DÙNG ĐỈNH TRÊN (Z LỚN HƠN)",
                Left = 18,
                Top = 122,
                Width = 238,
                Height = 42
            };
            upper.Click += (s, e) =>
            {
                Policy = DuplicateXYConflictPolicy.UseUpper;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(upper);

            var lower = new Button
            {
                Text = "DÙNG ĐỈNH DƯỚI (Z NHỎ HƠN)",
                Left = 270,
                Top = 122,
                Width = 238,
                Height = 42
            };
            lower.Click += (s, e) =>
            {
                Policy = DuplicateXYConflictPolicy.UseLower;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(lower);

            var cancel = new Button
            {
                Text = "Hủy",
                Left = 408,
                Top = 176,
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(cancel);
            CancelButton = cancel;
        }
    }
}
