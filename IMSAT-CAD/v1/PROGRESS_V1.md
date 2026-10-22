# IMSAT CAD V1.0 — Bảng theo dõi tiến độ

**Cập nhật:** 2026-10-17 · **Nhánh:** `imsat-cad-foundation` · **PR:** [#28](https://github.com/buithenamktm-coder/TKV_ATVSLD/pull/28)

> **Trạng thái phát hành: NOT READY.** Dấu tích xanh GitHub Actions chỉ chứng minh một quy trình CI thành công, không thay thế nghiệm thu chức năng Windows. Bảng này chỉ thay đổi khi có bằng chứng tương ứng. Không suy diễn % hoàn thành từ số commit.

## Tiến độ theo cổng nghiệm thu

| STT | Hạng mục | Trạng thái | Bằng chứng / Việc còn thiếu |
|---:|---|---|---|
| 1 | Kiến trúc và giấy phép QCAD CE | Đã xác lập | [ADR-001](ADR_001_LICENSING_ARCHITECTURE.md); phải tuân thủ khi phân phối |
| 2 | Biên dịch QCAD Native (Windows) | CI PASS | [Native QCAD CI](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38993617988); chưa nghiệm thu chạy ứng dụng |
| 3 | Biên dịch Qt JavaScript / QCAD JS API | CI PASS | Cùng workflow native; chưa chứng minh plugin IMSAT được nạp trong runtime |
| 4 | Kiểm tra phụ thuộc Qt | CI PASS | Workflow native tạo manifest phụ thuộc; chưa kiểm thử trên máy sạch |
| 5 | DXF backend tự động | CI PASS | [DXF regression](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38993617890); không thay thế kiểm thử 20 bản vẽ thật |
| 6 | Bộ cài Windows Preview (Tkinter POC) | CI PASS | [Preview installer](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38993617874); **không phải** bộ cài QCAD Native V1 |
| 7 | Đóng gói QCAD Native + Qt + plugin IMSAT | CHƯA ĐẠT | Cần gói chạy được trên Windows sạch, ghi lại phiên bản và log |
| 8 | Khởi động, nạp plugin và giao diện Việt/Anh | CHƯA KIỂM THỬ | Test Win10/Win11 x64, 1366×768 và Full HD |
| 9 | Lệnh vẽ, hiệu chỉnh, truy bắt và command bar | CHƯA NGHIỆM THU | Kiểm tra đầy đủ [ma trận lệnh](V1_RELEASE_GATES.md) bằng bàn phím/chuột |
| 10 | Mở → sửa → lưu → mở lại DXF thực tế | CHƯA NGHIỆM THU | Ít nhất 20 bản vẽ và đối chiếu hình học/thuộc tính |
| 11 | DWG hợp pháp | CHƯA TRIỂN KHAI | Phụ thuộc phương án cấp phép phù hợp; không tuyên bố hỗ trợ trước khi kiểm thử |
| 12 | In/PLOT, logo, bộ cài và gỡ cài V1 | CHƯA NGHIỆM THU | Cần kiểm thử end-to-end trên máy Windows sạch |
| 13 | Nghiệm thu người dùng / phát hành V1.0 | BLOCKED | Chỉ mở sau khi các release gates bắt buộc đạt |

## Công đoạn đang ưu tiên

1. Đóng gói native QCAD executable, Qt runtime, QCAD JS API và addon IMSAT.
2. Tạo phép thử khởi động ứng dụng Windows thực sự; ghi log, exit code và bằng chứng nạp plugin.
3. Chạy thử lệnh CAD, lưu/mở DXF và so sánh bản vẽ; sửa lỗi theo kết quả.
4. Chỉ khi đạt mới phát hành bộ cài thử nghiệm QCAD Native cho người dùng.

## Quy tắc cập nhật

- **CI PASS**: workflow chạy thành công, nhưng chưa đủ để kết luận tính năng đạt nghiệm thu.
- **ĐẠT NGHIỆM THU**: có ca kiểm thử thực tế, kết quả và liên kết log/artifact.
- **CHƯA KIỂM THỬ / CHƯA ĐẠT**: không tính là hoàn thành.
- **BLOCKED**: có điều kiện bắt buộc chưa đáp ứng; phải nêu rõ nguyên nhân.
- Mỗi thay đổi trạng thái phải cập nhật ngày, bằng chứng và công đoạn tiếp theo.
- Không công bố phần trăm tổng thể nếu chưa có trọng số và tiêu chí nghiệm thu thống nhất.
