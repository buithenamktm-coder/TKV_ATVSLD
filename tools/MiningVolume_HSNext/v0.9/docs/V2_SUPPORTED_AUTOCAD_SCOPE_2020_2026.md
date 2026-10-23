# IMSAT VOLUME V2.0 — Phạm vi AutoCAD 2020–2026

Quyết định phạm vi ngày 14/10/2026 theo yêu cầu chủ nhiệm sản phẩm.

- **Trong phạm vi:** AutoCAD full 64-bit các đời **2020, 2021, 2022, 2023, 2024, 2025, 2026**.
- **Ngoài phạm vi phát triển/nghiệm thu từ nay:** AutoCAD **2012–2019**. Không dành công sức cho adapter legacy 2012–2014 và không tiếp tục sửa riêng cho 2015–2019; mã cũ có thể tồn tại để bảo toàn lịch sử, nhưng không quảng bá hỗ trợ.
- **Ưu tiên:** hoàn thiện AutoCAD 2023 làm host kiểm thử đầu tiên, sau đó kiểm chứng độc lập từng đời còn lại.
- **Một trải nghiệm:** giữ giao diện 5 nhóm đã chốt, giữ nghiệp vụ TIN, tính khối lượng theo mặt cắt/lưới ô vuông, vẽ nhanh mặt cắt, CAD/Excel. Không tách ứng dụng riêng theo từng đời.
- **Runtime:** 2020 dùng .NET Framework tương ứng; 2021–2024 dùng .NET Framework tương ứng; 2025–2026 yêu cầu xác định đúng .NET runtime trên host trước khi chọn DLL. Không tự nạp DLL sai đời.
- **Setup:** phát hiện host 2020–2026, nhưng chỉ cho cài vào đời đã có DLL đúng API trong payload và được xác minh. Bản QA hiện tại đóng gói riêng AutoCAD 2023; không được ghi rằng đã cài được cho toàn bộ 2020–2026.
- **Các mức kiểm soát:** COMPILE PASS khác HOST RUNTIME PASS; HOST RUNTIME PASS khác FUNCTIONAL QA PASS. Chỉ xác nhận 100% khi đủ bằng chứng từng host và quy trình nghiệp vụ.

Nội dung này cập nhật phạm vi so với PLANCODE ban đầu. Các tiêu chí nghiệp vụ không đổi.
