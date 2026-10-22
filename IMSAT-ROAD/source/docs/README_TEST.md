# IMSAT ROAD – HƯỚNG DẪN KIỂM THỬ PREVIEW AUTOCAD 2023

**Trạng thái:** Preview kỹ thuật, chưa phải V1.0 hoàn chỉnh và chưa nghiệm thu trên AutoCAD Windows thực tế.

## Cài đặt
1. Đóng AutoCAD 2023.
2. Chạy IMSAT_ROAD_Setup_AutoCAD2023_Preview.exe trên Windows 64 bit.
3. Chạy AutoCAD 2023, mở một DWG mới. Nếu xuất hiện thông báo tải add-in, chỉ cho phép nếu xác minh đúng gói do chính mình build.
4. Mở tab Ribbon IMSAT ROAD, menu-bar IMSAT ROAD hoặc gõ IRROAD. Gõ IR_SELFTEST để kiểm tra add-in nạp thành công.

## Quy trình kiểm thử (thao tác hiện thực)
1. Dùng Polyline (PLINE) hở, đoạn thẳng vẽ tim tuyến; lệnh IR_TT chọn tim, khai báo lý trình gốc và tên tuyến.
2. Chuẩn bị một TIN hiện trạng dưới dạng tam giác 3DFACE: có thể tận dụng layer MV_TIN_HIENTRANG của IMSAT VOLUME. Nếu đặt tại layer khác, gõ IR_DH rồi chọn một mặt 3DFACE.
3. IR_COC → nhập khoảng cách cọc, xem layer IR_COC và IR_NHAN_COC.
4. IR_TD → nhập bước trích, chỉ điểm đặt trắc dọc tự nhiên. Nếu một số vị trí ngoài TIN, chương trình bỏ đoạn không có cao độ và thông báo.
5. IR_TN → nhập khoảng cách cọc, nửa bề rộng hai bên, bước lấy mẫu, chỉ điểm đặt bộ trắc ngang. Thử cả tuyến có một phần ngoài phạm vi TIN.
6. Thử chọn nhầm Polyline có bulge, TIN thiếu mặt 3DFACE, đường bao thiếu cao độ, Esc giữa các bước; xác minh phần mềm không tự bịa cao độ.

## Phạm vi hạn chế có chủ đích
- Lấy một TIN hiện trạng, không yêu cầu TIN thiết kế. Hiện chỉ đọc trực tiếp 3DFACE có sẵn; chưa có công cụ dựng TIN độc lập từ Point/Polyline.
- Tim tuyến hỗ trợ các đoạn Polyline thẳng. Cung cong nằm và cong chuyển tiếp chưa có.
- Bản vẽ trắc dọc/trắc ngang mới là tuyến địa hình tự nhiên, chưa có đường đỏ, mặt cắt thiết kế, khuôn đường, khối lượng hoặc xuất Excel.
- Không lưu liên kết tuyến giữa các lần đóng/mở DWG; cần chọn lại tim tuyến bằng IR_TT khi mở phiên làm việc mới.
- Các layer đầu ra cọc và trắc dọc/trắc ngang là đối tượng CAD để xem nhanh, chưa có quản lý phiên bản tự động; xóa layer/đối tượng đầu ra cũ trước khi vẽ lại nếu cần.
- Bản preview dùng lệnh có tiền tố IR_ để tránh xung đột với TDT 9.1 Pro hoặc VNRoad (TT/TN/TD/CN).
- Các thông số tiêu chuẩn TCVN 4054:2005 / III / đồng bằng được ghi nhận trong lõi. Bảng giá trị kiểm tra chi tiết chưa được đối chiếu trực tiếp; tuyệt đối không coi là kiểm tra thiết kế đạt tiêu chuẩn.

## Gỡ cài
- Đóng AutoCAD.
- Mở Command Prompt hoặc PowerShell tại thư mục đã tải IMSAT_ROAD_Setup_AutoCAD2023_Preview.exe, chạy:
  IMSAT_ROAD_Setup_AutoCAD2023_Preview.exe --uninstall
- Hoặc xóa thư mục %APPDATA%\Autodesk\ApplicationPlugins\IMSATROAD2023.bundle sau khi đóng AutoCAD.

## Tiêu chí kiểm thử bắt buộc
- Add-in và Ribbon/menu hiển thị khi AutoCAD mở; lệnh IR_SELFTEST chạy thành công.
- IR_TT nhận tim không có cung cong và tính chiều dài đúng.
- IR_DH chọn đúng lớp TIN; IR_TD, IR_TN nội suy có cao độ và không treo.
- Lưu DWG, kiểm tra những đường đã vẽ còn sau khi mở lại (quản lý dự án liên kết qua session đang phát triển).
- Gửi ảnh/lỗi command line (nếu có) khi gặp sự cố.

## Bảo đảm
Các workflow GitHub Actions xác minh compile/smoke trên Windows build runner không phải runtime AutoCAD. Không ghi PASS runtime AutoCAD khi chưa kiểm thử bằng máy thực.
