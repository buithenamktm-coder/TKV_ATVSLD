# IMSAT MiningVolume V2.0 — truy vết nghiệm thu cuối 12/10/2026

Nhánh `miningvolume-v2-chatgpt-p0-continuation`; không sửa V1. PlanCode: `PLANCODE_WORK_MININGVOLUME_V2_FINAL_AUDIT_RELEASE_20261012.md`, áp dụng cùng ADR trùng XY/mặt đứng đã chốt sau đó. **QA CANDIDATE, chưa phát hành chính thức.**

Bằng chứng mới: `P0_TIN_RENDER_AUDIT_20261012.md`, `qa/20261012-final/`. Commit sửa renderer `c13f3218ededfb2a311b42c2e6e51037a5bf183b`, Windows CI run 38212838355 PASS. Thay đổi sau mốc đó gồm Excel và danh mục đơn vị: local 89 C# + 249 Python PASS; kết quả CI, SOURCE_SHA và SHA-256 của gói bàn giao phải đọc trong `FINAL_BUILD_EVIDENCE.json` đi kèm Setup. Không dùng hash cũ cho Setup mới.

## 14 phát hiện RF

| ID | Sửa đổi / bằng chứng tự động | Phần chưa đạt nghiệm thu |
|---|---|---|
| RF-01 | `SectionCadRenderer`: đăng ký RegApp trong transaction xuất thật; static guard, Windows build | Xuất trực tiếp không preview, reopen DWG: NOT TESTED |
| RF-02 | `MineralVolumeExcelReportBuilder`: metadata đơn vị/kỳ/thời gian, hai người ký; 3 workbook production và công thức liên sheet PASS | Excel Desktop/AutoCAD workflow: NOT TESTED |
| RF-03 | `EditedSectionProfileBuilder`, `SectionVolumeCalculator`: bỏ midpoint TIN cũ cho MC đã sửa; tổng bằng đóng góp dải; +1 m cả đoạn F+10, V+200 và một đầu F+5 PASS | Grip/update nhãn CAD và số liệu khảo sát thật: NOT TESTED |
| RF-04 | `EditableSectionCadService`: kiểm cặp HT/TK, document, version, duplicate, bulge, station; snapshot geometry race; core/static PASS | Xóa/copy/layer/di chuyển tỷ lệ trên host: NOT TESTED |
| RF-05 | `SectionCadRenderer`, `EditableSectionCadService`: ID nhãn diện tích riêng; cập nhật transaction | Nhãn bị dịch/thiếu/chồng trong CAD: NOT TESTED |
| RF-06 | `ProjectSnapshot`, `ProjectPersistenceService`: source fingerprint, trạng thái CAD đã sửa; chặn khôi phục sai; snapshot regression PASS | SAVE/SAVEAS/reopen NOT TESTED; metadata đời cũ chặn an toàn, chưa tự migration |
| RF-07 | Báo cáo hiện tại tách rõ mốc lịch sử, core/CI/host và checksum final riêng | Nghiệm thu ký xác nhận còn thiếu |
| RF-08 | Go installer dùng linker version, rollback Bundle/registry, dọn đúng phiên bản; xác minh manifest DLL; 5 Go tests PASS ở P0 CI | Cài/nâng cấp/gỡ/cài lại Windows tương tác: NOT TESTED |
| RF-09 | RSA verifier/issuer và kiểm lỗi có test; không private key trong sản phẩm | BLOCKED: public trust anchor sản xuất chưa provision; activate máy độc lập chưa thử |
| RF-10 | Layout dọc/scroll/ẩn khi pick đã có mã, static/build PASS | 360/480/650 px, DPI 100/125/150%, 1366×768/FullHD: NOT TESTED |
| RF-11 | Sửa tên Nhôm Lâm Đồng, bỏ Tây Nam Đá Mài là đơn vị độc lập; regression và nguồn trong `V2_MINING_UNIT_CATALOG_AUDIT.md`; giữ nhập tên khác | Danh mục chưa chứng nhận đầy đủ pháp lý/đơn vị khai thác đang hoạt động |
| RF-12 | Giao diện/báo cáo/installer có Bùi Thế Nam — ĐT/Zalo: 0967280686; nhãn QA V2 riêng; workbook XML PASS | Toàn bộ màn hình Windows/CAD thực: NOT TESTED |
| RF-13 | Thời điểm tính thành công và thời gian thực riêng, invalidation khi đổi nguồn/tham số; test timing và grid stale PASS | Cancel/chuyển DWG nhiều thao tác host: NOT TESTED |
| RF-14 | Workbook production Arial12/viền/công thức/chữ ký/logo; sửa chồng logo/cắt chữ, render và engine độc lập PASS | Excel Desktop, sửa tham số trực tiếp, in thật: NOT TESTED |

## T01–T22: không đồng nhất test thành phần với nghiệm thu đầu cuối

Tất cả thao tác AutoCAD dưới đây **NOT TESTED** do môi trường hiện tại không có AutoCAD 2023. Cột tự động chỉ mô tả bằng chứng đã thực hiện; không biến thành PASS toàn ca.

| ID | Bằng chứng thành phần đã chạy | Kết quả đầu cuối còn thiếu |
|---|---|---|
| T01 | Menu/Ribbon/Palette build API PASS | Khởi động CAD, TKL/bật/tắt/settings NOT TESTED |
| T02 | Kiểm cấu trúc responsive trong Python PASS | Các kích thước/DPI mọi trang NOT TESTED |
| T03 | Scope ẩn/khôi phục Palette, cancellation guards build/static PASS | Pick/Esc/Close và khôi phục UI NOT TESTED |
| T04 | Adapter DXF thật tạo fixture HT/TK; core tọa độ mỏ lớn PASS | Autodesk reader + DWG X/Y/Z/layer/handle NOT TESTED; CIRCLE/SPLINE của adapter chưa hỗ trợ |
| T05 | ΔZ đúng 3/3.001/94.862, provenance và generated-node core PASS | Lọc và bảng CAD thực NOT TESTED |
| T06 | Sửa/loại model, invalidation, xác nhận mặc định Không static/core PASS | Zoom/Không/Esc/Đồng ý và so CAD gốc NOT TESTED |
| T07 | Restore/snapshot/fingerprint regression PASS | Khôi phục nhiều điểm, save/reopen DWG NOT TESTED |
| T08 | Strict/preview riêng, giữ Z/mặt đứng theo ADR, preview không tính V core PASS | HT/TK, màu/layer/toggle/view3D trong CAD NOT TESTED |
| T09 | SQ HT 1,681,755 + TK 1,672,875 support faces PASS; native/file-backed/shared render gate; số thời gian/RSS đã ghi; synthetic cancel PASS | CAD memory/UI/3DFACE/Esc/retry/Test HS host NOT TESTED |
| T10 | RegApp đăng ký ngay transaction, metadata version/ID static/build PASS | Xuất MC 20m trực tiếp không preview NOT TESTED |
| T11 | Full grip +1: F+10m², V+200m³; one grip F+5m²; tổng dải core PASS | Grip CAD thật, update nhãn/Excel cuối NOT TESTED |
| T12 | Invalid geometry/race/document guards core/static PASS | Copy/xóa/bulge/layer/nhãn host NOT TESTED |
| T13 | Snapshot source identity và bảo toàn MC chỉnh tay core/static PASS | Save/reopen/SAVEAS DWG thật NOT TESTED |
| T14 | Grid rotation/no-data/cut-fill, clip GEOS độc lập 4 case PASS; XLSX 1000m³ | CAD table/ranh sửa grip/Excel Desktop NOT TESTED; mặt đứng đa Z bị chặn lưới 2.5D |
| T15 | 3 vỉa: 120m³, 144t; no-data/overlap/domain core PASS | Chọn outcrop/polygon và confirm CAD thực NOT TESTED |
| T16 | 3 production XLSX mở/recalc engine độc lập, không lỗi công thức; PNG đã xem | Excel Desktop thay F/density, in NOT TESTED |
| T17 | Catalog/kỳ/snapshot/XLSX metadata tests PASS; tên khác vẫn được nhập | Chọn/lưu/mở đơn vị và kỳ trong CAD NOT TESTED |
| T18 | Timing/invalidation/stale grid và edited-profile guards PASS | Async cancel/chuyển tab/race host NOT TESTED |
| T19 | Trial/clock rollback/issuer backup-restore unit CI PASS | RSA production BLOCKED; activate máy độc lập NOT TESTED |
| T20 | Windows Setup build, 5 Go tests/manifest/tamper/rollback PASS ở c13; scan Defender PASS | Install/upgrade/uninstall/reinstall GUI/registry/autoload thực NOT TESTED; unsigned |
| T21 | Document/source/revision/fingerprint guard static/core PASS | Nhiều DWG, đóng/chuyển giữa tác vụ, deadlock NOT TESTED |
| T22 | Kế thừa regression suite 89 C#/249 Python; không sửa nhánh V1 | So chức năng V1 trên AutoCAD thực NOT TESTED |

## Điều kiện bàn giao

Có thể cung cấp QA Setup để người dùng kiểm thử, sau khi final HEAD CI/build/hash/scan đạt. Không tag Release V2.0 chính thức. G2–G6 chưa đủ: host runtime, Excel Desktop, HSMo/khối lượng khảo sát thật, Windows upgrade/uninstall, khóa sản xuất và nghiệm thu chuyên môn. Không tính phần trăm hoàn thành từ số lượng unit test.
