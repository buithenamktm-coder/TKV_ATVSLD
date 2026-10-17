# Trạng thái mới nhất — QA TIN/Excel 12/10/2026

**QA CANDIDATE — NOT OFFICIAL RELEASE.** Báo cáo hiện hành: [V2_FINAL_TRACEABILITY_20261012.md](V2_FINAL_TRACEABILITY_20261012.md); nguyên nhân và bằng chứng P0: [P0_TIN_RENDER_AUDIT_20261012.md](P0_TIN_RENDER_AUDIT_20261012.md); [hướng dẫn cài/gỡ/kiểm DLL](V2_QA_INSTALL_VERIFY_20261012.md).

Lỗi renderer từ chối tam giác dương nhỏ đã sửa ở `c13f321`; SQ HT đúng **1.681.755** và TK **1.672.875** support faces qua native/core/shared renderer validation. HT có **14** face dương dưới ngưỡng cũ, không bị loại. Windows CI c13 run38212838355 PASS (88 C#,249 Python,5 Go). Sau sửa Excel/danh mục: local **89 C# +249 Python PASS**, clip GEOS4/4 và3 workbook recalc độc lập PASS. CI/checksum final HEAD được đóng trong `FINAL_BUILD_EVIDENCE.json` của gói tải, không tái sử dụng hash lịch sử.

**G1:** bằng chứng build/test/Setup ở mốc c13 có thật; final HEAD phải CI lại. **G2/G3:** lõi và fixture SQ PASS, AutoCAD/DWG/HSMo NOT TESTED. **G4:** toàn luồng CAD/Excel Desktop/UI NOT TESTED. **G5:** Windows GUI install/uninstall NOT TESTED, khóa RSA sản xuất BLOCKED, Setup unsigned. **G6:** hồ sơ QA có, chưa biên bản nghiệm thu ký xác nhận. Không được công bố hoàn thành toàn bộ V2.0.

---
## Hồ sơ lịch sử dưới đây — số test, SHA, hash và trạng thái chỉ đúng tại mốc ghi rõ, không phải kết quả final HEAD

# IMSAT MiningVolume V2.0 — QA candidate and release-gate handoff

**Cập nhật:** 11/10/2026. **Trạng thái:** Development / QA candidate. **Không phát hành rộng rãi.**

## Repository và nhánh
- Repository: `buithenamktm-coder/TKV_ATVSLD`.
- Thư mục sản phẩm: `tools/MiningVolume_HSNext/v0.9/`.
- **Giữ nguyên** nhánh Work V1.0: `miningvolume-v1.0`. KHÔNG ghi đè lên nhánh này.
- Cây phụ thuộc: PR #21 (tích hợp nền) → PR #23 (vùng khoáng sản mặt cắt) → PR #24 (thể tích m³/tấn từng vỉa) → PR #25 (ứng viên QA gồm cả License PR #22).
- Nhánh **để Work rà soát toàn diện**: `miningvolume-v2-qa-candidate` (PR #25).
- PR riêng #22: dùng thử 15 ngày và quản lý key; khi tích hợp đã giải quyết giao diện Mineral + License.

## Ma trận chức năng
| ID | Tính năng | Mã | Chứng cứ còn thiếu |
|---|---|---|---|
| MV2-01 | Đọc Point/Line/Polyline/Contour → TIN hai lớp | Kế thừa V1 | DWG mỏ nhiều điểm/đường breakline thực tế |
| MV2-02 | Hệ mặt cắt và tính thể tích đào/đắp | Kế thừa V1 | Đối chứng khối lượng, lỗi AutoCAD treo trước đây |
| MV2-03 | Khoảng giãn khi xếp các hình mặt cắt trên CAD, mặc định 90 đơn vị | Có | Xác nhận không ảnh hưởng cự ly tuyến tính toán |
| MV2-04 | Lưới ô vuông xoay/giao hai TIN, ô đào/đắp hỗn hợp/no-data | Có | Host TIN lớn, DWG, áp suất RAM, công thức thống kê |
| MV2-05 | Ztn/Ztk tại nút, bình đồ lưới, Excel 6 sheet | Có | Mở Excel thật, formula evaluator, CAD native layers |
| MV2-06 | Quản lý nhiều vỉa, gán tên, mật độ, 2 bộ lộ vỉa | Có | Tách layer, DWG save/load, outcrop handles khi sửa DWG |
| MV2-07 | Polyline khoáng sản chỉnh Grip trên mặt cắt | Có | Host XData, đổi tên vỉa, quay lại edit và cập nhật |
| MV2-08 | Diện tích khoáng sản chỉ nằm trong phần đào, không chồng lấn | Có | Bản vẽ dốc/âm cao độ, mặt cắt chia đoạn, tolerance |
| MV2-09 | Thể tích (m³) & tấn từng vỉa từ diện tích mặt cắt xác nhận | Có | Đối chứng địa chất, đúng công thức/đơn vị |
| MV2-10 | Quy tắc không tự lấy zero ở mặt cắt chưa gán khoáng sản | Có | Xác nhận vỉa vắng mặt, DWG save/load, lỗ thiếu dữ liệu |
| MV2-11 | Đất đá = V đào − V quặng; hệ số bóc m³/m³ và m³/t | Có | Kiểm tra tổng vùng và tổng interval không âm |
| MV2-12 | Xuất khoáng sản Excel 4 sheet có liên kết | Có | Excel desktop: công thức, in ấn, định dạng |
| MV2-13 | Xuất bảng khoáng sản trực tiếp CAD bằng TABLE native | Có | DWG host, số lượng lớn, sửa cell, không tràn bảng |
| MV2-14 | View 3D TIN HT/TK trực tiếp CAD, Esc khôi phục palette | Có | 3DORBIT / Esc trên AutoCAD thực |
| MV2-15 | Nút bo góc & tương phản | Có | 1366×768, FullHD, scaling 100/125/150% |
| MV2-16 | Trial 15 ngày từ lần đầu dùng, không khóa dữ liệu DWG | Có | Kiểm tra chạy offline/hết hạn/rollback/corrupt ledger |
| MV2-17 | RSA-SHA256 theo mã máy, License Manager CLI, backup/restore khóa ký được mã hóa | Có code + tự kiểm thử | **Chưa provision PUBLIC verifier; chưa kiểm thử khôi phục trên máy Windows quản trị thứ hai và admin UI** |
| MV2-18 | Setup V2.0 QA Candidate có icon IMSAT; kế thừa cài/gỡ | Đã build QA | Chưa kiểm thử cài/gỡ/upgrade thực trên AutoCAD; chưa ký số và chưa nghiệm thu phát hành |
| MV2-19 | Runtime nghiệm thu trên AutoCAD 2023 thật | CHƯA ĐẠT | Host QA log, screenshot, DWG test, thiết bị Windows |
| MV2-20 | Khả năng chạy AutoCAD 2012+ | CHƯA ĐẠT | Hiện add-in target NET API AutoCAD 2023/R24.2; cần build/adapter riêng nếu mở rộng |

**Cảnh báo khoa học:** khối lượng m³/tấn từ hình mặt cắt khoáng sản là phương pháp nội suy theo diện tích hai đầu đã xác nhận, KHÔNG phải mô hình địa chất khối 3D. Đường lộ vỉa hiện trạng và thiết kế 2D tự nó không cung cấp chiều dày. Nếu chủ đầu tư yêu cầu thể tích địa chất 3D phải đưa thêm mặt nóc/đáy, solid có kiểm chứng và dựng giao hình học 3D thực.

## Thứ tự nghiệm thu Work
1. Checkout **PR #25 HEAD** nguyên trạng, kiểm tra tất cả các PR phụ thuộc, so sánh với V1 baseline; tuyệt đối không tạo project mới hoặc xóa chức năng hoạt động.
2. Chạy Windows CI cho đúng HEAD: QA static, C# CoreRegression, restore/build .NET Framework AutoCAD 2023, setup installer, LicenseManager self-test.
3. Kiểm tra tài liệu có công thức và kết quả định lượng; nghiệm thu V1 Core/TIN riêng trước khi xác nhận kết quả V2.
4. Chạy với DWG mỏ thật: Point, Polyline, Line, contour, lỗ khoan và ranh giới, TIN lớn, TIN HT/TK, Mặt cắt, grid cells, bảng Excel/CAD. Đặc biệt rà treo máy/tốn RAM.
5. Tạo ít nhất ba vỉa. Mỗi vỉa nhiều đoạn lộ vỉa, gán đủ HT/TK, vẽ vùng khoáng sản trên MC, đổi Grip và bấm CẬP NHẬT MẶT CẮT. Đối chiếu diện tích từ AutoCAD. Ghi log nếu không thể map XData/section.
6. Xác nhận vỉa vắng trên một MC, kiểm tra phân biệt không có dữ liệu và F = 0. Tính m³/t theo công thức, density mỗi vỉa, m³ đất đá + HSB; cấm tổng quặng vượt tổng đào.
7. Kiểm thử tệp Excel 4+6 sheet, thay đổi thể trọng/diện tích ở Excel và xác nhận các công thức được Excel cập nhật. Giải thích rõ rằng sửa Excel không tái giao hình học trên AutoCAD.
8. Test native TABLE CAD tại điểm chèn, vùng nhỏ/lớn, cỡ chữ và lệnh Escape 3DORBIT. Tất cả vùng khoáng sản và bảng phải nằm trên layer riêng, không xóa hình học của người dùng.
9. Kiểm thử cài đặt v2.0 lên Windows/AutoCAD, uninstall/upgrade, icon IMSAT, licensing real machine và khóa cấp phép đã provision an toàn.
10. Không tạo tag V2.0 phát hành, không đưa Installer tới người dùng cuối khi còn bất kỳ hard gate nào chưa có chứng cứ.

## Những việc cụ thể còn phải viết/sửa
- **Setup V2.0:** đã có gói **QA Candidate** (AppVersion 2.0.0 và icon IMSAT) tách V1.0, chỉ phục vụ kiểm thử; vẫn phải làm tùy chọn thư mục hợp lệ, upgrade V1.0, repair, uninstall, ký số và kiểm chứng cài đặt host.
- **Bản quyền:** đã có backup/restore issuer key mã hóa AES+HMAC trên dòng lệnh và tự kiểm thử; vẫn phải nghiệm thu restore trên máy quản trị độc lập, xây dựng admin UI, audit thu hồi/cấp lại, provision PUBLIC key đúng release. Never bundle private key.
- **Runtime 3D:** xử lý command lifecycle Esc/cancel/failed, DWG đóng/chuyển tab, giữ/khôi phục palette/visible layers, không treo AutoCAD.
- **Hiệu năng:** grid/TIN nhiều tam giác; test mô hình có 100k/1M tam giác, memory/file-backed, vẽ nhiều ô và nhiều mặt cắt không chồng; không nâng giới hạn bằng cách bỏ qua phần dữ liệu thiếu.
- **Định dạng:** giao diện không tràn chữ, nút bo góc đồng đều, Excel Arial12/bảng đóng khung/IMSAT logo/developer name và điện thoại cố định.
- **Kết thúc:** tạo `QA_REPORT.md`, `RELEASE_GATES_V2.md`, log CI và host AutoCAD, DWG/bảng đối chiếu độc lập; đánh dấu riêng PASSED/FAILED/NOT TESTED.

**Chuẩn nghiệm thu:** không suy đoán hoàn thành từ việc GitHub Actions xanh. Tất cả các tình huống không test thực trên AutoCAD phải ghi rõ NOT TESTED, không được PASS thay.
