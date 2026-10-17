# Báo cáo chuyển cấu trúc IMSAT — 17/10/2026

Đây là **nhánh chuẩn bị chuyển đổi**, không phải bản phát hành đã nghiệm thu. Không tự động hợp nhất trước khi chạy lại CI và kiểm tra thay đổi trên máy Windows.

| Dự án | Thư mục mới | Mã nguồn ban đầu | Trạng thái |
| --- | --- | --- | --- |
| IMSAT CAD core | `IMSAT-CAD/foundation/` | `main:tools/IMSAT_CAD/` | Đã chuyển |
| IMSAT CAD V1 | `IMSAT-CAD/v1/` | `main:tools/IMSAT_CAD_V1/` | Đã chuyển |
| IMSAT VOLUME | `IMSAT-VOLUME/source/` | `miningvolume-v2-chatgpt-p0-continuation:tools/MiningVolume_HSNext/` | Đã chuyển mã nguồn GitHub V2 |
| IMSAT ROAD | `IMSAT-ROAD/source/` | `imsat-road-v1.0:tools/IMSAT_ROAD/` | Đã chuyển |
| IMSAT SLOP | `IMSAT-SLOP/source/` | `imsat-slop-v1.0-work:products/imsat-slop/` | Đã chuyển |
| IMSAT HRM | `IMSAT-HRM/` | Chưa tìm được nguồn | Chưa chuyển mã |

Đã điều chỉnh các đường dẫn GitHub Actions hiện có và bổ sung CI của IMSAT ROAD. Đã sửa tham chiếu từ IMSAT ROAD tới `MiningVolume.Core` sau khi chuyển mã.

Các nhánh gốc vẫn giữ nguyên, có thể dùng để so sánh hoặc khôi phục. Nội dung ngoài phạm vi IMSAT (ứng dụng TKV_ATVSLD: `main.py`, `database/`, `modules/`, `ui/`) không bị di chuyển. Thay đổi chưa đồng bộ từ máy tính của người dùng không có trong snapshot này.

**Chưa đạt điều kiện merge hoặc phát hành:** cần kiểm tra workflow, build Windows, runtime AutoCAD, các đường dẫn tương đối khác, dữ liệu phần mềm và quyền sử dụng mã nguồn. Repository đang ở chế độ Public; không thêm dữ liệu cá nhân hoặc mã thương mại nhạy cảm khi chưa chuyển sang Private.
