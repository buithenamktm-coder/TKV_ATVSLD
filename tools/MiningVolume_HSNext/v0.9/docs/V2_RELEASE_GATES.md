# V2.0 Release Gates — BLOCKED

Ngày 11/10/2026. Chỉ phát hành khi TẤT CẢ gate bắt buộc đạt. CI xanh không thay nghiệm thu AutoCAD.

| Gate | Trạng thái | Bằng chứng / điều kiện thiếu |
|---|---|---|
| 1 — CI/build đúng HEAD | Cần xác minh run của HEAD bàn giao | Hai mốc 1b4d6f8 và 4a83b44 đã PASS. Artifact V2_BUILD_VERIFICATION.txt phải khớp HEAD cuối; compile mọi handler, core tests, issuer self-test, Setup đều PASS. |
| 2 — strict/test, >3m, provenance, hủy | PASS kỹ thuật lõi; UI NOT TESTED | 63 C# + 218 reference; clipping oracle 4/4; Cancel/Enter/Esc/dialog trong host còn thiếu. |
| 3 — hai DXF/SQ, hai TIN thực | BLOCKED | Quét thật hoàn thành nhưng dữ liệu còn xung đột; không chọn Z thay khảo sát. Chưa dựng/kiểm tra/lưu mở hai TIN trong AutoCAD. |
| 4 — toàn bộ V2 vận hành | NOT TESTED đầy đủ | P1 kế thừa code/build/core; thiếu chứng cứ giao diện, MC, grid, vỉa, Excel/CAD, multi-DWG. TT34 chưa nghiệm thu nghiệp vụ. |
| 5 — key/Setup thực | BLOCKED | Public trust anchor rỗng. Chưa nghiệm thu cài/gỡ/nâng cấp và issuer restore trên máy Windows quản trị độc lập. |
| 6 — hồ sơ + checksum + nghiệm thu | PARTIAL | Có báo cáo, logs định lượng, checklist, CI artifact chứa SHA256 V2; còn thiếu hồ sơ host và phê duyệt nghiệm thu. |

P0 chưa qua nghiệm thu; P1/P2/P3 chưa hoàn thành toàn bộ. Không công bố DONE hoặc phát hành chính thức. Không đổi trạng thái NOT TESTED thành PASS dựa trên static tests, code review, build hoặc file mẫu chỉ quét offline.

CI mốc: https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38158783762
PR: https://github.com/buithenamktm-coder/TKV_ATVSLD/pull/26
