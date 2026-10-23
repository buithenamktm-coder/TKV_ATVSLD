# ADR — Liquid Glass cho IMSAT MiningVolume V2.0 (13/10/2026)

**Quyết định:** Người dùng chốt Liquid Glass là phong cách chính thức của giao diện V2.0.

## Phạm vi áp dụng
- Palette AutoCAD: header IMSAT, sidebar, compact navigation, nhóm biểu mẫu, nút thao tác, thông báo và bảng quản lý.
- Không tạo phần mềm mới, không thay nghiệp vụ, tên nút, Ribbon hay lệnh TKL.
- Giữ nguyên logo, thông tin tác giả **Bùi Thế Nam – ĐT/Zalo: 0967280686**.
- Nền sáng tông xanh kính mờ; điều hướng có chiều sâu; nút chính xanh đậm, nút phụ có viền và tương phản; bảng số liệu vẫn nền trắng rõ nét.
- Chiều rộng palette nhỏ hơn 650 px phải chuyển sang điều hướng dạng compact, không làm mất nhãn hoặc chức năng.

## Quyết định kỹ thuật (WinForms / .NET Framework / AutoCAD 2023)
- **Liquid Glass-inspired**, không tuyên bố hiệu ứng Apple native hoặc acrylic blur của hệ điều hành.
- Dùng `GlassPanel` GDI+ vẽ gradient, highlight, separator bằng bộ đệm hai lớp. Không có timer, blur nền AutoCAD, animation khi dựng TIN hay hook cửa sổ hệ thống.
- Phân cấp thị giác chỉ áp dụng lớp chrome; bảng điểm, thông số X/Y/Z và khối lượng không dùng nền trong suốt gây khó đọc.
- Giao diện phải đảm bảo màu chữ/nền nút có tương phản từ 4.5:1 theo phép kiểm thử QA; các lớp glass không được che tương tác CAD.
- Tôn trọng cân bằng hiệu suất: tạo TIN, mặt cắt, khối lượng không phụ thuộc class `GlassPanel`.

## Tiêu chí kiểm chứng
1. CI phải build .NET Framework + AutoCAD 2023 plugin, static UI tests PASS.
2. Windows WinForms screenshot `UI_BUTTON_CONTRAST.png` và `UI_GLASS_CHROME.png` tồn tại; kiểm chứng ảnh không trắng và nền có thay đổi theo trục dọc.
3. Artifact QA lưu JSON, nguồn SHA, hash ảnh.
4. Chạy thực trên AutoCAD 2023 và đối chiếu Palette kích thước 360/480/650 px, DPI 100/125/150%, Windows theme tối/sáng: **NOT TESTED** cho đến khi có ảnh bằng chứng.
5. AutoCAD 2012+ chưa thuộc gói runtime này; không ghi đạt khi chưa build và kiểm thử.

**Gate:** Không gọi giao diện Liquid Glass hoàn chỉnh hoặc V2.0 RELEASE nếu chưa kiểm thử bằng cửa sổ Palette thực trong AutoCAD.
