# IMSAT VOLUME V2 — trạng thái năm vòng rà soát

Nhiệm vụ `IMSAT-VOLUME-V2-MULTICAD-FINAL-20261013`.
**Chưa hoàn tất chức năng đa phiên bản. Chưa đủ điều kiện bắt đầu/chốt năm vòng
nghiệm thu cuối sau hoàn thiện theo P5.** Bảng dưới là kết quả rà soát kỹ thuật
đã thực hiện và phần bắt buộc còn mở, không phải năm vòng AutoCAD đã PASS.

| Vòng | Kiểm tra đã thực hiện | Kết quả có bằng chứng | Phần còn mở | Gate |
|---|---|---|---|---|
| 1. Kiến trúc, dữ liệu, tương thích | Dependency Core; build 14 flavor cho 12 năm 2015–2026; nguồn và hash; không sửa V1 | MC-003, MC-004 đã sửa; CI 38380789988 PASS | Adapter 2012–2014; không có host load; MC-011 cần CI HEAD mới | BLOCKED |
| 2. TIN, mặt cắt, toán học | 117 C#; SQ đủ 1.681.755 tam giác HT; DXF KL mb với 19 ô × 2 ranh; oracle GEOS/NumPy | MC-001/002 tái hiện FAIL rồi PASS; 38 so sánh độc lập PASS | Grip edit, save/reopen, dựng 3DFACE và mặt cắt trong AutoCAD NOT TESTED | BLOCKED |
| 3. Lưới và hiệu năng | 10.000/50.000/100.000 ô; volume giải tích; progress; cancel trước và trong tác vụ | MC-005 sửa; 100.000 ô 6.397 ms ở lõi Linux trong lượt đo | Thời gian vẽ CAD, Undo/retry, leak qua nhiều phiên bản NOT TESTED | BLOCKED |
| 4. UI, CAD TABLE, Excel | CI Windows kiểm tra nút thực và contrast; regression report/formula/logo theo TKV | Test tự động PASS tại commit được CI ghi nhận | Palette thật 1366×768/Full HD, CAD TABLE, Excel desktop và ảnh nghiệm thu NOT TESTED | BLOCKED |
| 5. Setup, 15 đời CAD, phát hành | Rà soát bundle chỉ R24.2; phân biệt compile/host/release | Không nới series để nạp DLL sai; không bàn giao Setup chưa đạt | Setup đa CAD chưa triển khai đầy đủ; install/uninstall/upgrade/rollback/coexistence NOT TESTED | BLOCKED |

## Lỗi và bằng chứng

| ID | Mức | Nội dung | Trước / sau | Kiểm thử / commit |
|---|---|---|---|---|
| MC-001 | P1 | TIN chồng ngoài ranh/lỗ làm hỏng ô hợp lệ | FAIL / PASS | `multicad_grid_overlap_outside_selected_boundary_does_not_block`; d86f0cc |
| MC-002 | P1 | Ngưỡng XY áp lên dấu ΔZ làm mất đào/đắp rất nhỏ | FAIL / PASS | `multicad_grid_subnanometre_sign_change_keeps_cut_and_fill`; d86f0cc |
| MC-003 | P1 | .NET 4.5 gọi Array.Empty của .NET 4.6 | Không tương thích / COMPILE PASS 2015,2016 | Shared EmptyArray; d86f0cc; CI 38380789988 |
| MC-004 | P1 | Gói compile chỉ có DLL entry, thiếu dependency/hash | Thiếu / manifest đủ allowlist | `test_compile_candidate_preserves_dependencies_and_provenance`; d86f0cc |
| MC-005 | P1 | Cancel không kiểm tra khi lập index và overlay dày | Thiếu / regression PASS | `multicad_grid_cancel_before_index_and_between_cells`; 23d5c59 |
| MC-006 | P1 | Chưa có adapter 2012–2014 | OPEN | Cần thực hiện adapter legacy/worker, SDK đúng đời và host Windows |
| MC-007 | P1 | Setup chưa chọn module đúng từng đời/patch | OPEN | Hiện chỉ bản 2023; không quảng bá hỗ trợ nhiều đời |
| MC-008 | P1 | Chưa có bằng chứng AutoCAD host tại HEAD | OPEN | Cần máy Windows có AutoCAD bản quyền để chạy harness và workflow DWG |
| MC-009 | P1 | Chưa đạt điều kiện năm vòng nghiệm thu cuối | OPEN | Chờ MC-006/007/008, sau đó chạy lại đủ vòng và full regression 2023 |
| MC-010 | P2 | SQ HT 115,557 giây ở lõi, cần tối ưu thêm | OPEN | Không so trực tiếp 63 giây của code cũ vì chuỗi chuẩn hóa/breakline đã thay đổi |
| MC-011 | P2 | Core/Surface kế thừa WPF/WinForms qua props | Sửa điều kiện chỉ Plugin; chờ CI HEAD | `test_desktop_ui_framework_is_not_inherited_by_math_core` |
| MC-012 | P1 | Setup tin tên thư mục/registry mà chưa xác minh EXE x64/version | Đã sửa nguồn, chờ test Windows HEAD | `TestCADReleaseAndRuntimeClassification`, `TestRuntimeConfigFailsClosedOnUnknownOrConflictingMajor`, `TestWindowsExecutableVersionAndInvalidHostDetection` |

Setup hiện đã có kiểm kê các bản CAD từ registry 64-bit và thư mục chuẩn,
kiểm tra PE AMD64, file version thật và runtime config của host 2025/2026.
Thiếu hoặc mâu thuẫn runtime config thì không chọn flavor. Các phép thử Go
kiểm tra parser, phân loại và API đọc version Windows, không chứng minh bộ
cài đã hoạt động trên 15 bản AutoCAD. Payload hiện vẫn chỉ có R24.2; bước
cài đồng thời nhiều module, nâng cấp/rollback đa CAD của MC-007 còn mở.

Log tái hiện và sau sửa: `qa/20261013-multicad/`. Các test static được cập nhật
đúng API tương thích mới; không xóa ca lỗi để làm CI xanh. DXF, DWG và tọa độ
nguồn không được đưa vào Git. Kiểm thử số học không thay thế nghiệm thu dữ liệu
khảo sát, tự nạp, lựa chọn đối tượng, render, cài đặt hoặc phục hồi bản vẽ.
