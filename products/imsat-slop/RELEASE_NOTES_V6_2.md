# Release Notes V6.2 — CAD + Computation Fix

V6.2 sửa lỗi thực tế được xác nhận trên mô hình KTOD/KTDO: CAD đã hiển thị nhưng Explorer báo `Geometry (0 Region)`, khung nhìn dùng mặc định 0–100 và bộ giải không thể tạo lát/Fs.

## Sửa lỗi

- Nhận diện polyline mặt cắt kín là đồng thời **Region + địa hình**.
- Tự chọn vai trò `Mặt cắt kín (Region + địa hình)` cho layer có polyline kín.
- Khôi phục Region từ layer `Chưa gán` hoặc `Địa hình` khi CAD chỉ có một đường bao kín.
- Polygonize linework không khép theo một entity nhưng khép kín theo nhiều đoạn.
- Tự gán tạm `Vật liệu 1` cho Region CAD để chuỗi kiểm tra/tính toán có thể chạy ngay.
- Không vẽ lại đường bao Region bằng nét đứt tham chiếu.
- Khung nhìn lấy tọa độ thật của Region/terrain/reference, không còn biểu đồ nhỏ 0–100 nằm giữa màn hình.
- `Kiểm tra mô hình` báo lỗi rõ khi chưa có Region và chỉ cho chạy khi preflight tạo được lát hợp lệ.
- Giữ nguyên các sửa lỗi bộ giải V6.1: tìm kiếm thích nghi, chia lát, vật liệu đáy lát, Bishop và Morgenstern–Price.

## Kiểm thử mới

Bài kiểm thử một polyline kín dạng mặt cắt mỏ được chạy với 4 cách ánh xạ layer (`Chưa gán`, `Địa hình`, `Mặt cắt kín`, `Region kín`). Cả Bishop và Morgenstern–Price đều phải tạo Region, lát và Fs hữu hạn.
