# IMSAT MiningVolume V2.0 — RELEASE GATES (BLOCKED — QA CANDIDATE)

**Cập nhật ngày 12/10/2026.** Tài liệu này thay trạng thái release gate cũ từ 11/10/2026. Chỉ được công bố FINAL khi mọi hard gate PASS có bằng chứng AutoCAD và giấy phép thực. **CI xanh không thay thế AutoCAD host nghiệm thu.**

## 1. Trạng thái mã nguồn đã kiểm chứng

- Repository: `buithenamktm-coder/TKV_ATVSLD`; nhánh chính V2 đang tiếp quản: `miningvolume-v2-chatgpt-p0-continuation`, PR #27. Không sửa nhánh `miningvolume-v1.0`.
- Source SHA **`32c0cfedbbd177eb0c97ccac91f71845dac27ff4`** là mốc CODE đã kiểm tra dưới đây (commit tài liệu sau đó không thay thế số liệu test của CODE).
- CI **PASS trên cùng code SHA**: [Run 38187451763](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38187451763), [Run 38187454588](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38187454588).
- Python pytest/reference: **238/238 PASS**; Core Regression C# thực + native CDT: **76/76 PASS**; Windows installer Go tests: **4/4 PASS**. AutoCAD 2023 API plugin .NET Release build PASS; license issuer backup/restore self-test PASS; V2 QA Setup build PASS.
- QA artifact thuộc run 38187451763: **`IMSAT-MiningVolume-V2.0-QA-CANDIDATE-NOT-RELEASE`**, artifact ID **11719703622**. Tệp EXE trong artifact: `IMSAT_MiningVolume_V2_0_QA_CANDIDATE.exe`. **SHA256 EXE: `dac67dda9e398ff42f379e3491fdd8f7e215323fa7316fe625e9ef8cb4766dac`**. KHÔNG lấy hash của V1 hoặc ZIP thay hash EXE V2.

**Cập nhật hard gate bổ sung:** nếu sửa Grip đường bao cùng handle, nút vẽ lưới và xuất Excel phải đọc lại tọa độ và chặn V cũ; mặt cắt CAD đã chỉnh lại nhưng chưa bấm CẬP NHẬT MẶT CẮT không được xuất Excel/CAD; SaveAs tạo DWG khác dù GUID trùng vẫn phải tách phiên bằng Document identity. Có kiểm thử lõi + static và build PASS; tương tác AutoCAD host chưa được xác nhận.

**Bổ sung đã kiểm chứng bằng mã nguồn và unit tests:** giữ nguyên mặt cắt đã chỉnh khi hủy chọn điểm chèn; cảnh báo mặc định Không trước khi vẽ đè; khi nạp DWG kiểm tra đường CAD và SHA-256 toàn bộ nguồn TIN hiện trạng/thiết kế; đổi đường bao/hướng mặt cắt hủy khối lượng cũ; kiểm tra DWG/TIN/revision/cặp mặt cắt/thông số mức trước khi commit kết quả async; lưới ô vuông kiểm tra cả đường bao và kích thước ô. Chuyển DWG ẩn Palette cũ, lệnh mở Palette/3D/lưu Project nạp đúng bản vẽ hiện hành. **Đã có test lõi và compile, nhưng chưa nghiệm thu trong AutoCAD thực tế**.

## 2. Sáu chốt nghiệm thu

| Gate | Trạng thái | Bằng chứng và thiếu sót |
|---|---|---|
| G1 — Source, reference/core, Windows build, QA Setup | **PASS trên CODE SHA 32c0cfed** | 238 pytest, 76 core, 4 Go, build .NET API, issuer self-test, SHA256 V2 và 2 Windows CI đều PASS. Nếu HEAD CODE mới thì bắt buộc chạy lại. |
| G2 — Strict/mean-Z TIN, X/Y/Z, provenance, Cancel và editor | **PASS phần lõi; HOST NOT TESTED** | Các bài test ngưỡng ΔZ, sửa/khôi phục, không đổi CAD gốc đã qua; chưa thao tác AutoCAD 2023 thực. Không được biến nút chưa thử thành PASS. |
| G3 — TIN mỏ thật SQ và hai nguồn DXF/DWG | **BLOCKED** | Nguồn SQ có xung đột Z >3 m; phải có quyết định chuyên môn về Z. Chưa dựng, so, lưu/mở hai TIN chính thức với AutoCAD 2023 thực tế. Không tự thay Z bằng trung bình. |
| G4 — Toàn bộ V2 end-to-end | **HOST NOT TESTED** | Mặt cắt chỉnh CAD, khối lượng theo tầng, Excel công thức, grid, quặng, menu/Ribbon/Palette nhiều DPI, ESC, nhiều DWG và AutoCAD TABLE đều đã có code/test một phần, nhưng chưa có ảnh/log DWG nghiệm thu thật cho T01–T22. |
| G5 — Trial, activation RSA, cài/nâng cấp/gỡ | **BLOCKED** | Windows Go installer tests PASS, V1→V2 cleanup/rollback đã thêm; nhưng chưa thực hiện cài/gỡ trên Windows/AutoCAD và public trust anchor RSA vẫn rỗng. Private signing key không được lưu vào repo. |
| G6 — Hồ sơ nghiệm thu, checksum và bản phát hành | **PARTIAL** | Có QA report, PlanCode 14 RF + 22 test, artifact QA, checksum, run links; chưa có biên bản host, chứng cứ license và ký xác nhận release. |

**Thay đổi nghiệp vụ ngày 12/10 — HSMo vertical-face:** Người dùng xác nhận HSMo cho phép nhóm trùng XY, khác Z biểu diễn hình học gần mặt đứng dạng chóp và mặt cắt có thể chỉnh tay sau khi vẽ. V2 phải có cơ chế bảo toàn tất cả Z nguồn, TIN 3D/mặt đứng và chỉnh MC trước khi tính khối lượng; không tự gộp trung bình hay buộc xóa nguồn. **Chưa triển khai mã nguồn tương thích HSMo; hiện vẫn BLOCKED cho trường hợp này**, dù CI của nhánh 2,5D đang PASS. Chi tiết ADR: `ADR_V2_DUPLICATE_XY_VERTICAL_FACE_SECTION_EDIT_20261012.md`.

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
