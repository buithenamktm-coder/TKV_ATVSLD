# Trạng thái mới nhất — QA TIN/Excel 12/10/2026

**QA CANDIDATE — NOT OFFICIAL RELEASE.** Báo cáo hiện hành: [V2_FINAL_TRACEABILITY_20261012.md](V2_FINAL_TRACEABILITY_20261012.md); nguyên nhân và bằng chứng P0: [P0_TIN_RENDER_AUDIT_20261012.md](P0_TIN_RENDER_AUDIT_20261012.md); [hướng dẫn cài/gỡ/kiểm DLL](V2_QA_INSTALL_VERIFY_20261012.md).

Lỗi renderer từ chối tam giác dương nhỏ đã sửa ở `c13f321`; SQ HT đúng **1.681.755** và TK **1.672.875** support faces qua native/core/shared renderer validation. HT có **14** face dương dưới ngưỡng cũ, không bị loại. Windows CI c13 run38212838355 PASS (88 C#,249 Python,5 Go). Sau sửa Excel/danh mục: local **89 C# +249 Python PASS**, clip GEOS4/4 và3 workbook recalc độc lập PASS. CI/checksum final HEAD được đóng trong `FINAL_BUILD_EVIDENCE.json` của gói tải, không tái sử dụng hash lịch sử.

**G1:** bằng chứng build/test/Setup ở mốc c13 có thật; final HEAD phải CI lại. **G2/G3:** lõi và fixture SQ PASS, AutoCAD/DWG/HSMo NOT TESTED. **G4:** toàn luồng CAD/Excel Desktop/UI NOT TESTED. **G5:** Windows GUI install/uninstall NOT TESTED, khóa RSA sản xuất BLOCKED, Setup unsigned. **G6:** hồ sơ QA có, chưa biên bản nghiệm thu ký xác nhận. Không được công bố hoàn thành toàn bộ V2.0.

---
## Hồ sơ lịch sử dưới đây — số test, SHA, hash và trạng thái chỉ đúng tại mốc ghi rõ, không phải kết quả final HEAD

# IMSAT MiningVolume V2.0 — RELEASE GATES (BLOCKED — QA CANDIDATE)

**Cập nhật ngày 12/10/2026.** Tài liệu này thay trạng thái release gate cũ từ 11/10/2026. Chỉ được công bố FINAL khi mọi hard gate PASS có bằng chứng AutoCAD và giấy phép thực. **CI xanh không thay thế AutoCAD host nghiệm thu.**

## 1. Trạng thái mã nguồn đã kiểm chứng

- Repository: `buithenamktm-coder/TKV_ATVSLD`; nhánh chính V2 đang tiếp quản: `miningvolume-v2-chatgpt-p0-continuation`, PR #27. Không sửa nhánh `miningvolume-v1.0`.
- Source SHA **`b81011af0149565585d081a9ba3fb0a9a458c02b`** là mốc CODE đã kiểm tra dưới đây (commit tài liệu sau đó không thay thế số liệu test của CODE).
- CI **PASS trên cùng code SHA**: [Run 38192286646](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38192286646), [Run 38192289465](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38192289465).
- Python pytest/reference: **242/242 PASS**; Core Regression C# thực + native CDT: **81/81 PASS**; Windows installer Go tests: **4/4 PASS**. AutoCAD 2023 API plugin .NET Release build PASS; license issuer backup/restore self-test PASS; V2 QA Setup build PASS.
- QA artifact thuộc run 38192286646: **`IMSAT-MiningVolume-V2.0-QA-CANDIDATE-NOT-RELEASE`**, artifact ID **11722695028**. Tệp EXE trong artifact: `IMSAT_MiningVolume_V2_0_QA_CANDIDATE.exe`. **SHA256 EXE: `a928f542d4905c02f39e4fdc57bf9ec4847875fe7ed974fc23b3cbdd2ea057d0`**. KHÔNG lấy hash của V1 hoặc ZIP thay hash EXE V2.

**Cập nhật hard gate bổ sung:** nếu sửa Grip đường bao cùng handle, nút vẽ lưới và xuất Excel phải đọc lại tọa độ và chặn V cũ; mặt cắt CAD đã chỉnh lại nhưng chưa bấm CẬP NHẬT MẶT CẮT không được xuất Excel/CAD; SaveAs tạo DWG khác dù GUID trùng vẫn phải tách phiên bằng Document identity. Có kiểm thử lõi + static và build PASS; tương tác AutoCAD host chưa được xác nhận.

**Bổ sung đã kiểm chứng bằng mã nguồn và unit tests:** giữ nguyên mặt cắt đã chỉnh khi hủy chọn điểm chèn; cảnh báo mặc định Không trước khi vẽ đè; khi nạp DWG kiểm tra đường CAD và SHA-256 toàn bộ nguồn TIN hiện trạng/thiết kế; đổi đường bao/hướng mặt cắt hủy khối lượng cũ; kiểm tra DWG/TIN/revision/cặp mặt cắt/thông số mức trước khi commit kết quả async; lưới ô vuông kiểm tra cả đường bao và kích thước ô. Chuyển DWG ẩn Palette cũ, lệnh mở Palette/3D/lưu Project nạp đúng bản vẽ hiện hành. **Đã có test lõi và compile, nhưng chưa nghiệm thu trong AutoCAD thực tế**.

**Mốc code mới 12/10:** chế độ tự dựng TIN tại đỉnh trùng XY có Z khác; giữ các giá trị nguồn, dựng mặt đứng 3DFACE, cảnh báo số nhóm X,Y, số đỉnh khác Z và số điểm trùng nguyên XYZ. Dữ liệu tọa độ thô không có handle cũng phải giữ đủ Z. Bộ kiểm thử Python **242/242**, C# native **81/81**, Go installer **4/4** PASS trên source SHA `b81011af` (hai Windows CI đều success). TIN có mặt đứng phải qua xác nhận mặt cắt trước thể tích; không dùng lưới ô vuông 2,5D để cho ra số liệu giả. **Đối chứng HSMo/AutoCAD host còn NOT TESTED**.

## 2. Sáu chốt nghiệm thu

| Gate | Trạng thái | Bằng chứng và thiếu sót |
|---|---|---|
| G1 — Source, reference/core, Windows build, QA Setup | **PASS trên CODE SHA b81011af** | 242 pytest, 81 core, 4 Go, build .NET API, issuer self-test, SHA256 V2 và 2 Windows CI đều PASS. Nếu HEAD CODE mới thì bắt buộc chạy lại. |
| G2 — Strict/mean-Z TIN, X/Y/Z, provenance, Cancel và editor | **PASS phần lõi; HOST NOT TESTED** | Các bài test ngưỡng ΔZ, sửa/khôi phục, không đổi CAD gốc đã qua; chưa thao tác AutoCAD 2023 thực. Không được biến nút chưa thử thành PASS. |
| G3 — TIN mỏ thật SQ và hai nguồn DXF/DWG | **BLOCKED** | Đã có code tự dựng TIN với mặt đứng từ điểm cùng XY khác Z, không yêu cầu loại điểm nguồn. Vẫn chưa chạy/sánh với nguồn SQ + HSMo thật trên AutoCAD 2023, chưa xác minh topology và kết quả mặt cắt/khối lượng. Không tự thay Z bằng trung bình. |
| G4 — Toàn bộ V2 end-to-end | **HOST NOT TESTED** | Mặt cắt chỉnh CAD, khối lượng theo tầng, Excel công thức, grid, quặng, menu/Ribbon/Palette nhiều DPI, ESC, nhiều DWG và AutoCAD TABLE đều đã có code/test một phần, nhưng chưa có ảnh/log DWG nghiệm thu thật cho T01–T22. |
| G5 — Trial, activation RSA, cài/nâng cấp/gỡ | **BLOCKED** | Windows Go installer tests PASS, V1→V2 cleanup/rollback đã thêm; nhưng chưa thực hiện cài/gỡ trên Windows/AutoCAD và public trust anchor RSA vẫn rỗng. Private signing key không được lưu vào repo. |
| G6 — Hồ sơ nghiệm thu, checksum và bản phát hành | **PARTIAL** | Có QA report, PlanCode 14 RF + 22 test, artifact QA, checksum, run links; chưa có biên bản host, chứng cứ license và ký xác nhận release. |

**Thay đổi nghiệp vụ ngày 12/10 — HSMo vertical-face:** Người dùng xác nhận HSMo cho phép nhóm trùng XY, khác Z biểu diễn hình học gần mặt đứng dạng chóp và mặt cắt có thể chỉnh tay sau khi vẽ. V2 phải có cơ chế bảo toàn tất cả Z nguồn, TIN 3D/mặt đứng và chỉnh MC trước khi tính khối lượng; không tự gộp trung bình hay buộc xóa nguồn. **Đã triển khai nhánh tự động bảo toàn Z + mặt đứng 3D trong code, các test Point/Contour đã PASS; nghiệm thu hình học HSMo và AutoCAD host vẫn BLOCKED**, không được gọi là tương thích HSMo hoàn toàn. Chi tiết ADR: `ADR_V2_DUPLICATE_XY_VERTICAL_FACE_SECTION_EDIT_20261012.md`.


**Quy tắc đã chốt bổ sung:** Khi dựng TIN từ Point/Line/Polyline/Contour có đỉnh trùng XY khác Z, workflow AutoCAD không yêu cầu loại từng đỉnh. Chỉ thống kê nhóm XY và số đỉnh nguồn, cảnh báo kiểm tra hệ điểm/đồng mức và dựng TIN với 3DFACE mặt đứng. Trong core, chọn Z thấp nhất làm support cho thuật toán CDT đơn trị và lưu tất cả Z khác trong mặt đứng 3D + conflict provenance; không sửa dữ liệu nguồn. Mặt cắt đi qua nhóm xung đột hiện cảnh báo CẦN KIỂM TRA MC; trước khi chốt V phải dùng CẬP NHẬT MẶT CẮT. **Lưới ô vuông 2,5D** không được dùng để tính khi có mặt đứng; cần tính theo MC đã duyệt hoặc bổ sung thuật toán 3D thực. Chưa nghiệm thu DWG HSMo/Excel Desktop trên máy thực tế.

## 3. Những việc chưa được phép gắn PASS

- RF-06: Đã lưu dấu hệ mặt cắt CAD và SHA-256 toàn bộ hình học nguồn HT/TK để chặn khôi phục âm thầm khi nguồn hoặc đường mặt cắt đã thay đổi. Migrate metadata của hình mặt cắt đời trước vẫn cần chính sách an toàn; SAVE/SAVEAS/reopen phải nghiệm thu thực tế.
- RF-10/RF-11: Layout 360–480 px, DPI 125/150% và danh mục đơn vị TKV phải đối chiếu trong môi trường thật/danh sách chính thức.
- Nghiệm thu Excel Desktop mở/tính lại/in thật; snapshot XML và công thức đã có test nhưng không thay trải nghiệm Excel desktop.
- License: verifier `LicenseTrustAnchor.RsaPublicKeyXml` chưa provision khóa công khai thật, không thể xem chức năng activation được nghiệm thu.
- Đối chiếu DXF SQ với HSMo là đối chứng chuyên môn, không giải mã giả thiết `HDAI_TIN_MODEL_OBJ`.
- Hỗ trợ AutoCAD 2012+ chưa phải binary V2 hiện tại: mục tiêu QA hiện tại là **AutoCAD 2023 x64**.

**Quyết định:** chỉ cho phép cung cấp Setup **QA CANDIDATE — NOT RELEASE** phục vụ nghiệm thu. KHÔNG tag/publish V2.0 chính thức và không tuyên bố hoàn thiện 100% trước khi G2–G6 có bằng chứng PASS tương ứng.

PlanCode áp dụng: `PLANCODE_WORK_MININGVOLUME_V2_FINAL_AUDIT_RELEASE_20261012.md`.  
Báo cáo chi tiết lịch sử: `V2_FINAL_QA_REPORT.md`; worklog mới: `V2_FINAL_AUDIT_WORKLOG.md`.
