# IMSAT MiningVolume V2.0 — RELEASE GATES (BLOCKED — QA CANDIDATE)

**Cập nhật ngày 12/10/2026.** Tài liệu này thay trạng thái release gate cũ từ 11/10/2026. Chỉ được công bố FINAL khi mọi hard gate PASS có bằng chứng AutoCAD và giấy phép thực. **CI xanh không thay thế AutoCAD host nghiệm thu.**

## 1. Trạng thái mã nguồn đã kiểm chứng

- Repository: `buithenamktm-coder/TKV_ATVSLD`; nhánh chính V2 đang tiếp quản: `miningvolume-v2-chatgpt-p0-continuation`, PR #27. Không sửa nhánh `miningvolume-v1.0`.
- Source SHA **`983e3e27aeffac67c89a5a5328eff44f87f7411f`** là mốc CODE đã kiểm tra dưới đây (commit tài liệu sau đó không thay thế số liệu test của CODE).
- CI **PASS trên cùng code SHA**: [Run 38184927012](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38184927012), [Run 38184930156](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38184930156).
- Python pytest/reference: **228/228 PASS**; Core Regression C# thực + native CDT: **73/73 PASS**; Windows installer Go tests: **4/4 PASS**. AutoCAD 2023 API plugin .NET Release build PASS; license issuer backup/restore self-test PASS; V2 QA Setup build PASS.
- QA artifact thuộc run 38184927012: **`IMSAT-MiningVolume-V2.0-QA-CANDIDATE-NOT-RELEASE`**, artifact ID **11719058004**. Tệp EXE trong artifact: `IMSAT_MiningVolume_V2_0_QA_CANDIDATE.exe`. **SHA256 EXE: `5cec0e087215d4361c0aac9bd0a34fc69a1bb0dd71335ffd0a34b5d1e50ebb3e`**. KHÔNG lấy hash của V1 hoặc ZIP thay hash EXE V2.

## 2. Sáu chốt nghiệm thu

| Gate | Trạng thái | Bằng chứng và thiếu sót |
|---|---|---|
| G1 — Source, reference/core, Windows build, QA Setup | **PASS trên CODE SHA 983e3e27** | 228 pytest, 73 core, 4 Go, build .NET API, issuer self-test, SHA256 V2 và 2 Windows CI đều PASS. Nếu HEAD CODE mới thì bắt buộc chạy lại. |
| G2 — Strict/mean-Z TIN, X/Y/Z, provenance, Cancel và editor | **PASS phần lõi; HOST NOT TESTED** | Các bài test ngưỡng ΔZ, sửa/khôi phục, không đổi CAD gốc đã qua; chưa thao tác AutoCAD 2023 thực. Không được biến nút chưa thử thành PASS. |
| G3 — TIN mỏ thật SQ và hai nguồn DXF/DWG | **BLOCKED** | Nguồn SQ có xung đột Z >3 m; phải có quyết định chuyên môn về Z. Chưa dựng, so, lưu/mở hai TIN chính thức với AutoCAD 2023 thực tế. Không tự thay Z bằng trung bình. |
| G4 — Toàn bộ V2 end-to-end | **HOST NOT TESTED** | Mặt cắt chỉnh CAD, khối lượng theo tầng, Excel công thức, grid, quặng, menu/Ribbon/Palette nhiều DPI, ESC, nhiều DWG và AutoCAD TABLE đều đã có code/test một phần, nhưng chưa có ảnh/log DWG nghiệm thu thật cho T01–T22. |
| G5 — Trial, activation RSA, cài/nâng cấp/gỡ | **BLOCKED** | Windows Go installer tests PASS, V1→V2 cleanup/rollback đã thêm; nhưng chưa thực hiện cài/gỡ trên Windows/AutoCAD và public trust anchor RSA vẫn rỗng. Private signing key không được lưu vào repo. |
| G6 — Hồ sơ nghiệm thu, checksum và bản phát hành | **PARTIAL** | Có QA report, PlanCode 14 RF + 22 test, artifact QA, checksum, run links; chưa có biên bản host, chứng cứ license và ký xác nhận release. |

## 3. Những việc chưa được phép gắn PASS

- RF-06: Migrate metadata của những hình mặt cắt chỉnh tay đời trước cần chính sách an toàn, không cưỡng ép ghi đè hình học; identity/provenance cần nghiệm thu khi SAVE/SAVEAS/reopen.
- RF-10/RF-11: Layout 360–480 px, DPI 125/150% và danh mục đơn vị TKV phải đối chiếu trong môi trường thật/danh sách chính thức.
- Nghiệm thu Excel Desktop mở/tính lại/in thật; snapshot XML và công thức đã có test nhưng không thay trải nghiệm Excel desktop.
- License: verifier `LicenseTrustAnchor.RsaPublicKeyXml` chưa provision khóa công khai thật, không thể xem chức năng activation được nghiệm thu.
- Đối chiếu DXF SQ với HSMo là đối chứng chuyên môn, không giải mã giả thiết `HDAI_TIN_MODEL_OBJ`.
- Hỗ trợ AutoCAD 2012+ chưa phải binary V2 hiện tại: mục tiêu QA hiện tại là **AutoCAD 2023 x64**.

**Quyết định:** chỉ cho phép cung cấp Setup **QA CANDIDATE — NOT RELEASE** phục vụ nghiệm thu. KHÔNG tag/publish V2.0 chính thức và không tuyên bố hoàn thiện 100% trước khi G2–G6 có bằng chứng PASS tương ứng.

PlanCode áp dụng: `PLANCODE_WORK_MININGVOLUME_V2_FINAL_AUDIT_RELEASE_20261012.md`.  
Báo cáo chi tiết lịch sử: `V2_FINAL_QA_REPORT.md`; worklog mới: `V2_FINAL_AUDIT_WORKLOG.md`.
