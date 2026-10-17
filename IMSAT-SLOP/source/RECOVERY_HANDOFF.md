# IMSAT SLOP — HỒ SƠ KHÔI PHỤC MÃ NGUỒN

Tên sản phẩm chốt: **IMSAT SLOP**.

`IMSAT_SLOP_recovered_V6_2/`: mã nguồn thực V6.2, giữ cấu trúc `app/`, `tests/`, `main.py`, scripts builder và assets.

`legacy_archives/`: ZIP nguyên bản V6.0 và V5.4.1 để đối chiếu, không dùng thay V6.2 nếu không có phân tích.

`PLANCODE_IMSAT_SLOP_V1.0_WORK.md`: yêu cầu bắt buộc cho Work; **P0 khôi phục code lên GitHub trước khi phát triển**.

`IMSAT_SLOP_RECOVERY_MANIFEST.json`: danh sách SHA256 từng file.

Nhánh đích: https://github.com/buithenamktm-coder/TKV_ATVSLD/tree/imsat-slop-v1.0-work
Thư mục đích: `products/imsat-slop/`.

Cảnh báo: GitHub repo hiện công khai. Work phải rà quét secrets, dữ liệu dự án khách hàng, tài sản bên thứ ba trước khi commit. Các ví dụ GSZ phải qua kiểm tra quyền chia sẻ. Không coi `pytest` không thu thập test do script `if __name__ == "__main__"` là kiểm thử đã đạt; chạy đúng script tests từng trường hợp.
