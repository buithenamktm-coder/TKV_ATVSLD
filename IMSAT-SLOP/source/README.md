# IMSAT Slope Stability Professional V6.2

Ứng dụng Windows/Python phục vụ phân tích ổn định bờ mỏ theo chuỗi kỹ thuật:

`Geometry → Materials → Assign → Entry/Exit → Computational Preflight → Solve → Results`

## Thay đổi quan trọng của V6.2

V6.2 tập trung sửa đúng hai lỗi đã xuất hiện khi thử mô hình KTOD/KTDO: CAD hiển thị nhưng `Geometry (0 Region)`, và do đó không thể sinh lát hoặc tính Fs.

- Polyline mặt cắt kín được nhận diện đồng thời là **Region + địa hình**.
- Layer có polyline kín được tự đề xuất vai trò `Mặt cắt kín (Region + địa hình)`.
- Region CAD được gán tạm Vật liệu 1 để chuỗi kiểm tra/tính toán chạy được ngay.
- Khung nhìn dùng tọa độ thật của mô hình, không còn trục 0–100 bé ở giữa màn hình.

Các sửa lỗi tính toán V6.1 tiếp tục được giữ và kiểm thử lại:

- Kiểm tra trước **toàn bộ khả năng tính toán**, không chỉ polygon và vật liệu.
- Mỗi mặt trượt bị loại có mã nguyên nhân cụ thể: ngoài miền, nằm trên địa hình, lát không có khối lượng, thiếu vật liệu đáy, v.v.
- Entry/Exit bắt dính vào địa hình, chấp nhận hai vùng được vẽ ngược thứ tự.
- Tìm kiếm thích nghi chỉ mở rộng vùng khi phạm vi người dùng không sinh được kết quả.
- Bộ chia lát xử lý khe hở và chồng lấn hình học nhỏ giữa các Region ổn định hơn.
- Bishop và Janbu có giảm chấn lặp; Morgenstern–Price dùng Bishop làm giá trị khởi tạo và thử lại có kiểm soát.
- Worker PyInstaller dạng cửa sổ không còn dừng trước khi tính khi `stdout` không tồn tại.
- Builder chỉ chấp nhận EXE khi chính **frozen EXE** giải được một bài Morgenstern–Price và ghi ra Fs hữu hạn.

## Chức năng

- Đọc mô hình GeoStudio `.gsz`, giữ polygon Region và quan hệ vật liệu.
- Nhập DXF trực tiếp; DWG thông qua ODA File Converter.
- Tạo, sửa, xóa Region bằng tọa độ.
- Vật liệu Mohr–Coulomb: γ, γsat, C, φ và màu.
- Gán vật liệu: chọn vật liệu rồi nhấp trực tiếp Region.
- Nước/phreatic line, tải phân bố và động đất giả tĩnh.
- Vẽ vùng Entry và Exit trực tiếp trên địa hình.
- Tìm kiếm Entry–Exit, Grid–Radius, Auto-Locate, Cuckoo sampling và mặt trượt xác định.
- Fellenius, Bishop Simplified, Janbu Simplified và Morgenstern–Price.
- Bộ giải chạy trong tiến trình riêng; lưu recovery và nhật ký chẩn đoán.
- Results hiển thị Region, vật liệu, mặt trượt, lát, Fs và danh sách mặt trượt.
- Lưu `.issv6`; xuất HTML và CSV.

## Tạo EXE trên Windows

Giải nén gói vào thư mục ngắn, ví dụ `C:\IMSAT\SlopeV6_2`, rồi chạy:

```bat
00_BUILD_EXE_BANG_CMD.bat
```

Builder tự tìm hoặc cài Python cục bộ, cài thư viện, chạy kiểm thử lõi/Qt, đóng gói và bắt buộc kiểm tra frozen solver. Chỉ khi tất cả đạt mới xuất:

```text
dist\IMSAT_Slope_Stability_V6_2_Professional.exe
release\IMSAT_Slope_Stability_V6_2_Professional.exe
```

## Kiểm chứng

Các báo cáo chính trong `tests/`:

- `V6_2_RELEASE_ACCEPTANCE.json`
- `V6_2_LARGE_OPEN_PIT_VALIDATION.json`
- `V6_2_FULL_AUDIT.json`
- `V6_2_CAD_CLOSED_SECTION_VALIDATION.json`
- `V6_MP_BENCHMARK.json`

Benchmark Morgenstern–Price trên cùng dữ liệu lát/cột tới hạn của 8 GSZ có sai lệch lớn nhất dưới 0,005%. Đây không phải tuyên bố tìm kiếm mặt trượt toàn cục giống hoàn toàn GeoStudio.

## Trách nhiệm sử dụng

Phần mềm là công cụ hỗ trợ kỹ thuật. Kỹ sư phải kiểm tra hình học, thông số, giả thiết, mặt trượt và kết quả trước khi dùng trong thiết kế hoặc quyết định an toàn.
