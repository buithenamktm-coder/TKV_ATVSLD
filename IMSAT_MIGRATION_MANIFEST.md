# Báo cáo chuyển cấu trúc IMSAT — 17/10/2026

Đây là **nhánh chuẩn bị chuyển đổi**, không phải bản phát hành đã nghiệm thu. Không tự động hợp nhất trước khi chạy lại CI và kiểm tra thay đổi trên máy Windows.

| Dự án | Thư mục mới | Mã nguồn ban đầu | Trạng thái |
| --- | --- | --- | --- |
| IMSAT CAD core | `IMSAT-CAD/foundation/` | `main:tools/IMSAT_CAD/` | Đã đưa vào thư mục đích |
| IMSAT CAD V1 | `IMSAT-CAD/v1/` | `main:tools/IMSAT_CAD_V1/` | Đã đưa vào thư mục đích |
| IMSAT VOLUME | `IMSAT-VOLUME/source/` | `miningvolume-v2-chatgpt-p0-continuation:tools/MiningVolume_HSNext/` | Mã nguồn V2 nằm duy nhất tại thư mục mới trên nhánh chuyển đổi |
| IMSAT ROAD | `IMSAT-ROAD/source/` | `imsat-road-v1.0:tools/IMSAT_ROAD/` | Đã đưa vào thư mục đích |
| IMSAT SLOP | `IMSAT-SLOP/source/` | `imsat-slop-v1.0-work:products/imsat-slop/` | Đã đưa vào thư mục đích |
| IMSAT HRM | `IMSAT-HRM/` | Chưa tìm được nguồn | Chưa chuyển mã |

Đã điều chỉnh các đường dẫn GitHub Actions hiện có và bổ sung CI của IMSAT ROAD. Đã sửa tham chiếu từ IMSAT ROAD tới `MiningVolume.Core` sau khi chuyển mã.

Các nhánh gốc vẫn giữ nguyên, có thể dùng để so sánh hoặc khôi phục. Nội dung ngoài phạm vi IMSAT (ứng dụng TKV_ATVSLD: `main.py`, `database/`, `modules/`, `ui/`) không bị di chuyển. Thay đổi chưa đồng bộ từ máy tính của người dùng không có trong snapshot này.

**Chưa đạt điều kiện merge hoặc phát hành:** cần kiểm tra workflow, build Windows, runtime AutoCAD, các đường dẫn tương đối khác, dữ liệu phần mềm và quyền sử dụng mã nguồn. Repository đang ở chế độ Public; không thêm dữ liệu cá nhân hoặc mã thương mại nhạy cảm khi chưa chuyển sang Private.

## Cut (Move) / Không nhân đôi mã nguồn

Trên nhánh `reorg-imsat-project-folders-20261017`, các thư mục mã nguồn cũ `tools/IMSAT_CAD`, `tools/IMSAT_CAD_V1`, `tools/MiningVolume_HSNext` được **xóa khỏi cây thư mục** và thay bằng các đường dẫn IMSAT tương ứng. Đường dẫn dự án ROAD và SLOP từ những nhánh phát triển khác được nhập đúng **một vị trí** trên nhánh chuyển đổi; các nhánh lịch sử được giữ để tránh làm mất commit.

Phải merge PR #29 vào `main` để cách tổ chức này trở thành mặc định. Việc truy cập một commit hoặc nhánh lịch sử cũ không có nghĩa mã nguồn còn được lưu trùng trong cây `main` mới. Không xóa lịch sử Git hay nhánh đang phục vụ phát triển khi chưa di chuyển công việc dở dang.
