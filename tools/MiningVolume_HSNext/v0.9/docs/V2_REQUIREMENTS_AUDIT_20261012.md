# IMSAT MiningVolume V2.0 — đối chiếu toàn bộ yêu cầu (12/10/2026)

Phạm vi kiểm soát: `buithenamktm-coder/TKV_ATVSLD`, nhánh `miningvolume-v2-chatgpt-p0-continuation`, kế thừa PlanCode `PLANCODE_WORK_MININGVOLUME_V2_FINAL_20261011.md`. Đây là nhật ký rà soát triển khai, **không phải tài liệu nghiệm thu AutoCAD** và không thay thế PlanCode.

## Quy ước bằng chứng

- **Đã triển khai mã nguồn**: đã commit cơ chế; có thể còn lỗi host chưa phát hiện.
- **CI PASS**: chỉ đánh dấu nếu workflow chạy tại HEAD liên quan đã kết thúc thành công; kiểm tra trực tiếp trên GitHub Actions.
- **AutoCAD thực NOT TESTED**: không nâng trạng thái từ compile, static test, unit test hoặc sản phẩm QA sang nghiệm thu host.
- **BLOCKED**: thiếu đầu vào phải quyết định bên ngoài phần mềm, tài nguyên chủ máy Windows, hoặc public trust anchor cấp phép.

## P0 — TIN và dữ liệu nguồn

| ID | Yêu cầu chốt | Phần triển khai / điểm kiểm | Host |
|---|---|---|---|
| P0-01 | TIN HT và TK độc lập, phân lớp riêng, Point/Line/LWPolyline/2D/3D/Contour; đúng XYZ/layer/handle/đỉnh | `SurfaceWorkflowService`, `SourceData`, `TinCadRenderer`, `DataPage` | NOT TESTED |
| P0-02 | Địa hình lớn, tránh quét trùng XY cấp phát danh sách ở từng điểm độc lập | `ConflictReport.cs`, `MeanZPreviewPreparer`, C# regression sparse site | NOT TESTED |
| P0-03 | Nhóm XY khác Z; mặc định lọc ΔZ > 3.000 m, từng điểm có Zoom/Loại/Sửa Z | `DuplicateXYConflictUi`, `MeanZConflictReviewForm`, `ModelPage` | NOT TESTED |
| P0-04 | Loại 1 đỉnh không xóa source CAD; xác nhận đúng câu chữ, mặc định Không, Esc không sửa | UI xung đột, lịch sử chỉnh sửa `SourceData` | NOT TESTED |
| P0-05 | Khôi phục đơn/multi/toàn bộ; cập nhật revision TIN và tính toán sau chỉnh | `ProjectState`, history, persistence và C# regression | NOT TESTED |
| P0-06 | TIN thử Z trung bình cho ΔZ ≤ 3, hard stop > 3; cảnh báo không dùng tính khối lượng; giữ breakline TIN chính thức | `MeanZPreviewWorkflowService`, `MeanZPreviewPreparer`, `SurfaceWorkflowService` | NOT TESTED |
| P0-07 | Ghi/lấy lại Project trong DWG, đối chiếu các bộ Test HS và SQ | `ProjectSnapshot`, `ProjectPersistenceService`, QA tài liệu gốc | NOT TESTED / dữ liệu SQ còn xung đột chờ xử lý có chuyên môn |

## P1 — giao diện, mặt cắt, khối lượng, Excel

| ID | Yêu cầu chốt | Phần triển khai / điểm kiểm | Host |
|---|---|---|---|
| P1-01 | Ribbon/menu/Palette tiếng Việt, bật tắt; không tự mở theo mặc định | `EntryPoint`, `MainPaletteControl`, `UserSettingsService` | NOT TESTED |
| P1-02 | Bảng màu sáng và dịu hơn, nút chính/phụ có tương phản; ghim trái bố cục dọc với cuộn | `UiTheme`, `MainPaletteControl`, `ProjectPage`; min width 360 px | NOT TESTED |
| P1-03 | Ẩn Palette khi chọn nguồn/đường bao/hướng/tuyến/điểm chèn trên CAD, tự mở lại kể cả Esc | `CadSelectionPaletteScope` trong `SelectionService` (tối thiểu 6 luồng) | NOT TESTED |
| P1-04 | Xem hai TIN 3D, tuyến mặt cắt bước 20 m, dịch/thêm/xóa tuyến | `Tin3DViewService`, `SectionPage`, `SectionWorkflowService` | NOT TESTED |
| P1-05 | Chỉnh đường mặt cắt ngay trong CAD, bấm **CẬP NHẬT MẶT CẮT**, đọc hình đã sửa, tính lại diện tích đào/đắp và khối lượng; giữ chỉnh sửa sau mở DWG | `EditedSectionProfileBuilder`, `EditableSectionCadService`, XData metadata `SectionCadRenderer`, `SectionPage`, restore; C# regression bảo vệ | NOT TESTED |
| P1-06 | Đào/đắp theo mặt cắt, theo tầng và lưới; công thức thích ứng và Excel liên kết thực | `SectionVolumeCalculator`, `GridVolumeCalculator`, `ExportWorkflowService`, `GridVolumeExcelReportBuilder` | NOT TESTED |
| P1-07 | Trữ lượng nhiều vỉa/thân quặng, quản lý phạm vi dữ liệu thiếu | `MineralPage`, `MineralBody`, tests lõi; trữ lượng khoáng sản theo tấn là hướng phát triển sau nếu không thuộc V2 chốt | NOT TESTED |
| P1-08 | Tên đơn vị khai thác TKV/VVMI/Vimico, phân than/quặng; cho tự nhập đơn vị khác | `MiningUnitCatalog`, `ProjectPage`, `ProjectState`, DWG snapshot | NOT TESTED |
| P1-09 | Chọn kỳ khối lượng Tháng/Quý/Năm và số kỳ/năm; giữ trong Project DWG, xuất Excel | `VolumePeriodCatalog`, `ProjectPage`, `ProjectPersistenceService`, báo cáo | NOT TESTED |
| P1-10 | Excel ghi tên đơn vị, kỳ, thời điểm **tính** khối lượng, thời gian xử lý thực, tách thời gian **xuất** | `VolumePage`/`GridPage` Stopwatch + UTC, `ExportWorkflowService`, `GridVolumeExcelReportBuilder` | NOT TESTED |
| P1-11 | Sheet Tổng khối/Tổng hợp lưới có hai cột ký: NGƯỜI TÍNH TOÁN, NGƯỜI KIỂM TRA; trống tên để ký | `ExportWorkflowService`, `GridVolumeExcelReportBuilder`, `SpreadsheetReport.HasSignatures` | NOT TESTED |
| P1-12 | Style Excel của chủ nhiệm: Arial 12, bảng đóng khung, không tô nền không cần thiết, công thức không bị dịch khi bổ sung dòng | `SimpleXlsxWriter` (`SpreadsheetReport.cs`), append metadata/signature sau dữ liệu | NOT TESTED |
| P1-13 | Tên phát triển cố định Bùi Thế Nam; **ĐT/Zalo: 0967280686** trong UI, Excel và Setup | `ProjectState`, `ExportWorkflowService`, `EntryPoint`, `RibbonBuilder`, `MainPaletteControl`, `installer/main.go` | NOT TESTED |

## P2 — bản quyền, Setup và Uninstall

| ID | Yêu cầu chốt | Tình trạng chính xác |
|---|---|---|
| P2-01 | Trial liên tục 15 ngày, phát hiện lùi giờ | Đã có cơ chế C#; kiểm thử ngày/đồng hồ Windows thực NOT TESTED |
| P2-02 | Kích hoạt RSA xác thực, không có private key trong ứng dụng | Đã có trình xác thực; **public RSA trust anchor trong bản build hiện còn rỗng** → BLOCKED kích hoạt chính thức; không tự tạo private key giả trong repository |
| P2-03 | Setup AutoCAD 2023 x64, icon IMSAT, tự nạp và nâng cấp | Build QA candidate trong CI, **không phải Release**; cài/nâng cấp trên host thực NOT TESTED |
| P2-04 | Gỡ toàn bộ phần mềm với Uninstall trong Windows và Start Menu | `installer/main.go` có đường gỡ bundle, shortcut, registry, bộ cài đã lưu; giữ nguyên DWG/Project, báo cáo, trial/log dữ liệu người dùng. Host NOT TESTED |
| P2-05 | Không tự tuyên bố AutoCAD 2012+ | Giữ AutoCAD 2023 là nền tảng build/kiểm thử; các đời khác chưa được chứng nhận |

## P3 — điều kiện chốt

1. **HEAD chính thức**: chỉ lấy SHA và kết quả workflow cuối tại thời điểm bàn giao. Đối chiếu cả C# CoreRegression, pytest, .NET build, Setup, SHA256 và artifacts.
2. **Mặt cắt**: trong host sửa đỉnh đường HT/TK bằng grip, bấm cập nhật, so sánh F trước/sau và kết quả V; thử đảo hướng X, xóa 1 đường, hủy và mở lại DWG; không dùng dữ liệu cũ khi lỗi.
3. **TIN**: thử hai mô hình riêng, loại/sửa/khôi phục 1 hoặc nhiều đỉnh, trung bình Z thử nghiệm và hard gate >3 m; kiểm RAM/cancel/host trên bộ SQ và Test HS; không tự động giải quyết Z mâu thuẫn >3 m.
4. **UI**: ghim trái AutoCAD tại 1366×768/FullHD và DPI 100/125/150%; thử mọi nút chọn CAD, Esc, đóng, ẩn/hiện sau prompt; không mất form state.
5. **Excel**: tạo file mẫu từng phương pháp, mở trong Excel, đối chiếu công thức sau nhập ô F/L mới, metadata và hai chỗ ký, font/viền/khoảng trống ký.
6. **Setup**: cài mới/nâng cấp/gỡ ở Windows, xóa chính xác bundle IMSAT; giữ nguyên bản vẽ DWG và mọi dữ liệu người dùng; trial 15 ngày và cấp phép thật với public key từ máy quản trị.
7. Nếu thiếu bất cứ gate host, public key hoặc xử lý dữ liệu mỏ, **giữ nhãn QA/NOT TESTED**, không phát hành thành bản V2 chính thức.

## Tests và nguồn

- `tests/core/CoreRegression.cs` có test sửa mặt cắt (đào/đắp đổi dấu, nguồn giữ nguyên, từ chối hình học tự giao theo trục X và sửa đầu mút).
- `tests/reference/test_v2_user_requirements_20261012.py` bảo vệ mới về Palette, meta/period, CAD editing và uninstall.
- `docs/V2_FINAL_QA_REPORT.md` và `docs/V2_RELEASE_GATES.md` vẫn lưu lịch sử các gate nền. Nguồn SQ/HS đang trong báo cáo QA hiện có.
- CI và artifact chỉ được gọi PASS nếu chính lượt chạy SHA tương ứng kết thúc thành công. Không dùng số bài PASS của commit trước cho commit mới.
