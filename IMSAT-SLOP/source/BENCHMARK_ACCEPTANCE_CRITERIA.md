# Tiêu chí nghiệm thu kỹ thuật

1. 8 GSZ đọc đúng số Region/vật liệu, không có polygon lỗi hoặc chồng lấn diện tích.
2. Gán một Region không làm thay đổi Region khác.
3. Geometry không chứa đường chia lát; lát chỉ xuất hiện trong Results.
4. Fellenius, Bishop, Janbu và Morgenstern–Price trả ít nhất một Fs hữu hạn trên bài kiểm tra D1-D2.
5. Morgenstern–Price cùng dữ liệu lát/cột: sai lệch Fs so với GeoStudio không quá 0,02% cho từng mô hình.
6. Dự án `.issv6` lưu/mở lại đầy đủ kết quả và quan hệ vật liệu.
7. Bộ giải chạy bằng tiến trình riêng; Qt offscreen khởi tạo, mở GSZ, gán vật liệu và chuyển Results không lỗi.
8. Không đóng gói EXE khi bất kỳ kiểm thử bắt buộc nào không đạt.
