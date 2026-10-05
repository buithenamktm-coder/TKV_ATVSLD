# MiningVolume HS-Next v0.10 — GUI Event-Driven Preview

Mốc v0.10 sửa trực tiếp điểm chưa đạt của v0.9: giao diện không còn điều khiển AutoCAD bằng chuỗi lệnh kiểu LISP.

## Nguyên tắc UX v0.10

- Ribbon gọi trực tiếp hàm C# và mở đúng trang Project / Dữ liệu / Mô hình / Mặt cắt / Khối lượng / Excel.
- Không dùng `SendStringToExecute` trong Ribbon.
- Người dùng không phải gõ `MV_DATA`, `MV_MODEL`, `MV_SECTION`... để làm việc.
- Khi cần chọn đường bao, hướng, điểm thêm/dịch tuyến hoặc điểm chèn mặt cắt:
  1. palette tự ẩn;
  2. người dùng pick trên canvas AutoCAD;
  3. palette tự hiện lại đúng trang đang làm.
- Các CommandMethod `MV...` vẫn giữ làm shortcut/diagnostic tùy chọn, không phải đường vận hành chính.
- Setup không tự mở AutoCAD bằng script `.scr`, không chạy `MVSELFTEST` qua Command Line.
- Add-in tự chạy health-check C# một lần sau khi được AutoCAD nạp; chỉ hiện hộp thoại nếu health-check thất bại.

## Chuỗi chức năng

`Project → Dữ liệu X-Y-Z → TIN → Hệ mặt cắt → Mặt cắt HT/TK → Diện tích đào/đắp → Khối lượng chi tiết → Theo tầng → Excel → Lưu/khôi phục Project trong DWG`.

## AutoCAD đích

- AutoCAD 2023 64-bit
- R24.2
- .NET Framework 4.8
- Windows 10/11

## Phát hành

v0.10 là **GUI Preview**, chưa gọi Release 1.0. Chỉ phát hành Setup sau khi:
1. toàn bộ pytest PASS;
2. solution net48 compile PASS trên Windows;
3. bundle policy PASS;
4. installer PE64 build PASS;
5. test tĩnh xác nhận Ribbon không còn `SendStringToExecute` và installer không còn `.scr`/auto-command.
