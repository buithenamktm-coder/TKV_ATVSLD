# IMSAT VOLUME V2 — hướng dẫn kiểm thử và khôi phục

Hồ sơ này dành cho bản kỹ thuật chưa nghiệm thu; chưa có bộ cài đa AutoCAD
được chấp thuận. Tác giả phần mềm: Bùi Thế Nam — ĐT/Zalo 0967280686.

## Chuẩn bị và kiểm tra đúng bản

Giữ bản sao DWG/DXF và project trước khi thử. Không nạp DLL 2023 vào CAD
2012–2022 hoặc 2024–2026 để thử tương thích. Chọn đúng flavor/runtime và SDK;
các flavor compile-only không phải bộ cài. Đối chiếu SHA nguồn với manifest.
Trong máy AutoCAD thử nghiệm, dùng `MVBUILDINFO` kiểm tra đường DLL đang nạp,
source SHA, MVID và hash. Nếu sai bản, thoát toàn bộ CAD rồi kiểm tra các thư
mục ApplicationPlugins/NETLOAD cũ; không chép đè DLL khi CAD đang sử dụng.

## Quy trình nghiệp vụ cần nghiệm thu

1. Tạo/chọn dự án, đơn vị và kỳ khối lượng; chọn độc lập dữ liệu HT/TK.
2. Tạo từng TIN. Giữ cảnh báo điểm cùng XY khác Z, kiểm tra bảng quản lý và
   mặt đứng. Zoom/sửa/loại điểm chỉ tác động mô hình, không sửa CAD nguồn.
3. Chọn ranh kín đúng vùng cần tính. Hướng tuyến, lý trình cục bộ, thang tỷ lệ
   và dải mức phải phù hợp cao độ TIN; không giữ 0–100 khi TIN nằm trên +300.
4. Vẽ hệ mặt cắt hoặc mặt cắt nhanh; sửa Grip rồi cập nhật mặt cắt trước khi
   tính lại khối lượng. Kiểm tra ba công thức và khoảng MC được loại/cảnh báo.
5. Tính lưới, kiểm tra riêng đào/đắp và diện tích thiếu dữ liệu. Ô “chưa tính”,
   “thiếu phủ”, “nhiều lớp cao độ” không được đọc thành 0 m³ đã được xác nhận.
6. Xuất CAD/Excel, kiểm tra tên cột/đơn vị/formula, logo theo đơn vị TKV.
   Lưu/đóng/mở DWG, kiểm tra TIN bật/tắt và dữ liệu dự án phục hồi đúng.

## Hủy, lỗi và bảo toàn dữ liệu

Khi hủy phải chờ tác vụ kết thúc trước khi chạy lại. Ghi log, ảnh lỗi,
`MVBUILDINFO`, loại TIN và số tam giác; không xóa điểm hàng loạt để ép PASS.
Nếu source SHA không đúng, giải quyết bản DLL nạp trước khi kết luận thuật toán.
Không đổi FILEDIA/CMDDIA, không tắt bảo vệ máy để chạy Setup.

## Cài/gỡ

Chưa có quy trình cài/gỡ đa phiên bản đã được kiểm chứng để hướng dẫn như sản
phẩm phát hành. Với QA cũ đã cài, đóng CAD và dùng mục gỡ của chính bản cài
trong Windows Apps; sao lưu DWG/DXF/project/license trước. Việc giữ dữ liệu,
nâng cấp, rollback và cài nhiều CAD vẫn phải kiểm thử có bằng chứng. Không xóa
thủ công thư mục dữ liệu hoặc license để thay cho Uninstall.
