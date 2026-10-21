# IMSAT ADM V1.0 — Kiểm toán kết cấu áo đường

## Mục tiêu
Phần mềm Windows độc lập. Lõi tính theo TCCS 38:2022/TCĐBVN, hỗ trợ đối chiếu 22 TCN 211-06. Giao diện gọn như ADM gốc. Xuất phụ lục tính toán **một sheet Excel**.

## Các điều kiện nghiệm thu bắt buộc
- Quy đổi tải trọng, lưu lượng xe, lớp vật liệu, kiểm tra đơn vị và phạm vi áp dụng.
- Tính được E_ch với phương pháp/toán đồ hợp lệ; kiểm toán độ võng đàn hồi.
- Tính được T_ax, T_av, kiểm toán cắt trượt.
- Tính được ứng suất kéo uốn và cường độ chịu kéo uốn; kiểm toán kéo uốn.
- Mỗi giá trị tra toán đồ phải có nguồn và được đối chiếu với bài toán mẫu TCCS 38:2022.
- Xuất một sheet Excel theo mẫu ADM, gồm đủ đầu vào, quá trình tính, kết quả và kết luận có truy vết.
- Kiểm thử tự động và kiểm thử chạy trên Windows; tạo IMSAT-ADM-V1.0-Setup.exe.
- **Không đánh dấu hoàn thành/đạt kiểm toán khi còn thiếu lõi tính hoặc kiểm chứng.**

## Trạng thái hiện có (chưa đồng bộ mã nguồn)
Mã nguồn phiên bản thử đang có tại cuộc trò chuyện Project, chưa được tải lên nhánh GitHub này. Bộ tính hiện có quy đổi trục, mô đun lớp, Ctt và bất đẳng thức từ giá trị toán đồ nhập tay. Bộ giải tự động toán đồ và kiểm toán đầy đủ chưa hoàn thành.

## Bảo mật / quyền sử dụng
Không commit ADMv2.exe, các tệp nhị phân .dat/.adm của bên thứ ba hoặc nội dung độc quyền chưa xác định quyền phân phối vào repository công khai. Chỉ đưa mã nguồn do dự án tự phát triển và dữ liệu kiểm thử được phép sử dụng lên GitHub.
