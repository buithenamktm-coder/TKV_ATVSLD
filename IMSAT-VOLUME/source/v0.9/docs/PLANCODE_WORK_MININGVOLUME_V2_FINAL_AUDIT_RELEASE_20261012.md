# PLANCODE WORK — IMSAT MININGVOLUME V2.0 — RÀ SOÁT CUỐI, SỬA DỨT ĐIỂM VÀ CHỐT BÀN GIAO
**Ngày lập:** 12/10/2026 (Asia/Bangkok).  
**Vai trò:** Work tiếp quản kỹ thuật, kiểm tra độc lập, sửa mã nguồn và hoàn thiện bản phát hành.  
**Trạng thái:** Tài liệu giao việc cuối. KHÔNG phải xác nhận phần mềm đã nghiệm thu AutoCAD.  
**Tính ưu tiên:** P0 → P1 → P2 → P3; không đảo P0 vì mục tiêu chính là TIN chính xác, dữ liệu lớn và nguồn CAD nguyên vẹn.

## 0. NHIỆM VỤ DUY NHẤT GIAO WORK

Tiếp tục trực tiếp IMSAT MiningVolume V2.0 đang có; rà soát TOÀN BỘ chức năng, sửa các lỗi thật, kiểm thử tới commit cuối, đóng gói Setup/Uninstall có chứng cứ. KHÔNG lập project mới, KHÔNG viết lại phần đã đạt, KHÔNG chuyển sang HRM/MineBIM, KHÔNG sửa/force-push nhánh miningvolume-v1.0.

Repository: buithenamktm-coder/TKV_ATVSLD  
Source root: tools/MiningVolume_HSNext/v0.9/ (tên v0.9 là đường dẫn lịch sử; sản phẩm phát triển là V2.0).  
Nhánh tiếp quản: miningvolume-v2-chatgpt-p0-continuation.  
HEAD đã xác minh trước khi tạo tài liệu này: 6f4cb90a9911c2c939befcffbb5fc9dc13c9e22d. HEAD sau đó có thể thay đổi: LUÔN fetch và ghi SHA thật trước khi sửa.  
Nhánh gốc cũ: miningvolume-v2-hs-all-sources-tin, PR #26; PR #26 vẫn open tại HEAD khác nhánh tiếp quản, không giả định code đã có trong PR #26.  
PlanCode nền không được bỏ qua: docs/PLANCODE_WORK_MININGVOLUME_V2_FINAL_20261011.md.  
Tham khảo thêm docs/PLANCODE_WORK_IMSAT_MININGVOLUME_V2_0_FINAL.md, docs/V2_REQUIREMENTS_AUDIT_20261012.md, docs/V2_FINAL_QA_REPORT.md, docs/V2_RELEASE_GATES.md, docs/V2_AUTOCAD_ACCEPTANCE.md, docs/V2_QA_RELEASE_HANDOFF.md.

**Dữ kiện được xác minh:** Hai workflow Windows cùng HEAD 6f4cb90 đã PASS (run 38173694576 và 38173697827); log run 38173694576 ghi 228 pytest PASS, 66 C# regression PASS, build AutoCAD 2023 và QA Setup PASS. Artifact IMSAT-MiningVolume-V2.0-QA-CANDIDATE-NOT-RELEASE đã được tạo; chỉ là ứng viên QA, không phải Release chính thức. Các số test này là của HEAD NÊU TRÊN, không mặc định là kết quả của HEAD về sau.

**BẮT BUỘC khi bắt đầu Work:**
1. Fetch HEAD nhánh tiếp quản, origin/PR #26/PR #25, trạng thái commit, so sánh diff và xem các workflow HEAD; không ghi đè thay đổi mới hơn.
2. Dựng bảng truy vết Req-ID → source file → test ID → actual evidence → tình trạng PASS/FAIL/NOT TESTED/BLOCKED. Đọc đủ tài liệu người dùng đã chốt, không chỉ tài liệu cũ.
3. Phân định test compile/static/reference, core regression, Windows installer, AutoCAD runtime và nghiệp vụ thực. Không được cộng dồn những bằng chứng khác cấp thành một PASS.
4. Ưu tiên sửa lỗi thực theo severity: P0 data loss/sai số/treo/crash; P1 thiếu chức năng/luồng dữ liệu; P2 UI/xuất báo cáo/bản quyền/Setup; P3 nghiệm thu tích hợp.
5. Mỗi thay đổi có commit và test chống hồi quy. Không xóa bài test thất bại hoặc đổi kiểm thử để che lỗi. Nếu test cũ chỉ kiểm tra chuỗi triển khai đã đổi hợp lệ, vẫn phải có kiểm thử hành vi tương đương.
6. Nếu Work không có máy AutoCAD 2023, ghi chính xác NOT TESTED và chuẩn bị trình tự kiểm thử đầu cuối cho người dùng, không báo 100% nghiệm thu.

## 1. KẾT QUẢ RÀ SOÁT BỔ SUNG LẦN NÀY — BẮT BUỘC KIỂM CHỨNG

| ID | Phát hiện tại HEAD 6f4cb90 | File/liên kết cần rà | Rủi ro/đầu ra bắt buộc |
|---|---|---|---|
| RF-01 | SectionCadRenderer.ReplaceProfilesBatched ghi XData FrameRegApp/EditRegApp nhưng không thấy gọi EnsureFrameRegApp tại chính luồng xuất này; hàm đăng ký có trong ReplacePreview | Services/SectionCadRenderer.cs | Thử xuất trực tiếp sau reopen DWG/chưa xem preview, tránh eInvalidRegAppName và metadata mất; nếu cần, đăng ký trong cùng transaction trước StampSection, test có/không preview |
| RF-02 | Excel THAN/QUẶNG theo vỉa là workbook riêng 4 sheet; MineralVolumeExcelReportBuilder chưa lấy MiningUnitName, VolumePeriodLabel, thời điểm tính thực, chưa có hai ô ký | Services/MineralVolumeExcelReportBuilder.cs, UI/MineralPage.cs | Đồng bộ đúng yêu cầu cho TẤT CẢ loại XLSX (mặt cắt, lưới, quặng); giữ vị trí và tham chiếu công thức khi thêm metadata |
| RF-03 | Tính lại từ mặt cắt CAD có logic Rebuild, nhưng chưa có bằng chứng runtime về khôi phục và tổng theo tầng; nguồn TIN vẫn được dùng nội suy mặt cắt trung gian | Services/EditableSectionCadService.cs, Core/Sections/EditedSectionProfileBuilder.cs, Core/Volumes/SectionVolumeCalculator.cs, UI/SectionPage.cs | Kiểm chứng chính xác F đào/đắp, V khoảng, V tầng, tổng; không được âm thầm dùng mặt cắt TIN thay mặt cắt người dùng sửa |
| RF-04 | Reader quy X từ polyline CAD về station bằng x0 cũ và kiểm soát Z theo baseZ/vExag | Services/EditableSectionCadService.cs, SectionCadRenderer.cs | Kiểm thử sửa grip, kéo cả polyline, đổi tỉ lệ, copy/trùng, xóa một nửa cặp HT/TK, polyline có bulge, thay đổi layer, thay đổi DWG; lỗi phải dừng không ghi đè kết quả cũ |
| RF-05 | UpdateAreaLabels tìm nhãn theo chênh tọa độ cứng giữa tiêu đề và F; chưa có ID nhãn riêng ổn định | Services/EditableSectionCadService.cs, SectionCadRenderer.cs | Thử dịch nhãn CAD, nhiều mặt cắt chồng nhãn, thiếu nhãn; ưu tiên gắn ID/handle ổn định hoặc transaction update có kiểm tra trước khi commit |
| RF-06 | Project load dựng lại TIN/MC và đọc CAD polyline nếu có. Cần kiểm soát dữ liệu sửa tay sau nhiều lần lưu/mở và sau các thay đổi nguồn | Services/ProjectPersistenceService.cs, ProjectSnapshot.cs | Mở lại DWG vẫn giữ sửa tay, không đọc lẫn DWG khác, revision/fingerprint không giả; invalidation cũ đúng |
| RF-07 | QA docs V2_FINAL_QA_REPORT và V2_RELEASE_GATES còn thống kê 218/63 và CI cũ | docs/V2_FINAL_QA_REPORT.md, docs/V2_RELEASE_GATES.md | Đồng bộ kết quả thật tại HEAD cuối, phân biệt lịch sử cũ và kết quả mới; không đổi host NOT TESTED thành PASS |
| RF-08 | V2 QA Setup build bằng patch mã Go tạm từ V1; có thay thế chuỗi global tên backup, registry và runtime | installer/main.go, installer/build_v2_qa.ps1 | Thử nâng cấp V1→V2QA, gỡ bản QA, xóa đúng registry/backup/uninstaller, tự nạp; không để dư V1/V2 gây conflict và không xóa DWG/license/dữ liệu khác |
| RF-09 | LicenseTrustAnchor.RsaPublicKeyXml vẫn rỗng | Services/LicenseService.cs, License Manager và pipeline packaging | V2 phát hành đầy đủ phải có public trust anchor thật được provision an toàn và thử activate trên máy độc lập; KHÔNG đưa private key vào repo, DLL hay Setup |
| RF-10 | Layout responsive mới ở MainPaletteControl, SectionPage, DataPage, GridPage; UI chưa thử thực với docking/DPI | UI/MainPaletteControl.cs, SectionPage.cs, DataPage.cs, GridPage.cs, ProjectPage.cs | Đánh giá thực tại 360/480/650 px và 1366×768, FullHD, 100/125/150% DPI; kiểm scroll, nút bị cắt, mất focus và giá trị form |
| RF-11 | MiningUnitCatalog là danh sách khởi tạo thủ công | Services/ProjectState.cs, UI/ProjectPage.cs | Đối chiếu lại danh sách THAN/QUẶNG lộ thiên trực thuộc TKV/VVMI/Vimico từ tài liệu người dùng và dữ liệu tin cậy; đúng tên pháp lý, không lẫn đơn vị hầm lò/nhà máy tuyển; vẫn cho nhập đơn vị khác |
| RF-12 | Cập nhật mã liên hệ ĐT/Zalo, nhưng có nơi nội dung V1 trong selftest/setup/hồ sơ lịch sử | EntryPoint.cs, SelfTestService.cs, installer/main.go, Excel builders | Nhãn ngoài người dùng phải đúng Bùi Thế Nam — ĐT/Zalo: 0967280686; test/manifest V2 rõ ràng, không sửa phá luồng tự kiểm tra V1 cần dùng |
| RF-13 | Thời điểm và thời gian tính thực đã thêm cho mặt cắt/lưới; chưa đồng bộ tính khoáng sản và vòng đời sau hủy/đổi dữ liệu | ProjectState.cs, VolumePage.cs, GridPage.cs, MineralPage.cs, các report builders | Chỉ ghi timestamp và số giây từ lần tính THÀNH CÔNG; vô hiệu khi nguồn/TIN/MC/parameter đổi; kỳ sản lượng Tháng/Quý/Năm khác thời điểm tính |
| RF-14 | Excel tổng khối có hai người ký, font Arial và không tô nền nhưng chưa được kiểm tra Excel Desktop và bản in | Core/Reporting/SpreadsheetReport.cs, Services/ExportWorkflowService.cs, XlsxBrandingInjector.cs | Đọc XLSX thật, so công thức đã tính, kiểm số hàng, ký tên/trang in, logo IMSAT, không #REF! / dòng mất chữ |

Các phát hiện RF được nêu dưới dạng **điểm nghi vấn hoặc thiếu bằng chứng**; Work phải tái hiện/đối chứng trước khi gọi là lỗi đã xác nhận. Những phát hiện nghiêm trọng như RF-01, RF-02, RF-03, RF-08, RF-09 không được bỏ qua chỉ vì CI hiện xanh.

## 2. P0 — DỮ LIỆU NGUỒN/TIN (HARD GATE ƯU TIÊN SỐ 1)

### P0-A: Đọc CAD, quản lý nguồn và hai TIN
- Kiểm tra Point, LINE, LWPOLYLINE, POLYLINE2D, POLYLINE3D, contour; X/Y/Z WCS đúng, elevation 2D/3D đúng, handle/layer/chỉ số đỉnh, vertex sinh khác vertex gốc.
- TIN hiện trạng và thiết kế được chọn nguồn riêng, có vùng cắt, layer/tam giác/đồ họa riêng; build strict không bị thay bằng mesh preview; vẽ 3D, bật/tắt và hủy an toàn.
- Cập nhật nguồn, bỏ/khôi phục 1 vertex và lưu/cập nhật model: không được xóa entity/Polyline trên CAD gốc, không xóa những vertex khác; mọi kết quả dẫn xuất từ source revision cũ phải bị vô hiệu.
- Thử DWG tọa độ 2.500.000+, dữ liệu nhiều triệu vertex, cancel, chuyển DWG, nhiều session, vẽ vùng lõm, vùng có lỗ, breakline giao/chồng và giới hạn RAM; không bỏ im lặng đối tượng được chọn.
- Dữ liệu tham chiếu: Tinh kl SQ 2026.09.091.dxf, Tinh kl SQ 2026.09.09.dwg, Test HS.dxf; tra Project/Repo/file đã lưu trước; chỉ yêu cầu tải lại nếu không thể lấy bytes. Đánh dấu CIRCLE/SPLINE chưa hỗ trợ nếu bộ đọc hiện chưa xử lý.

### P0-B: XY trùng, Z mâu thuẫn và thao tác UI
- Gom nhóm cùng XY theo dung sai có căn cứ; nhóm hiển thị X, Y, Z_min, Z_max, ΔZ, số điểm; hàng con từng source có Z/layer/entity type/handle/vertex index.
- Lọc mặc định CHỈ các nhóm ΔZ > 3,000 m, không nhầm >=; ΔZ=3,000 KHÔNG nằm trong danh sách mặc định; ΔZ=3,001 CÓ.
- Mỗi điểm có Zoom đúng XY, Loại điểm, Sửa Z; hàng header nhóm không có 3 nút; generated/intersection không truy ngược chắc chắn về vertex gốc không được phép sửa/xóa nhầm.
- Khi Loại điểm: nguyên văn "Bạn có chắc chắn muốn loại điểm này khỏi mô hình không?" hai nút Đồng ý / Không; nút mặc định Không; Esc và đóng cửa sổ = Không, không sửa nguồn; Loại đúng vertex và quét lại.
- Sửa Z phải kiểm số hữu hạn, chỉ mô hình IMSAT; khôi phục 1/nhiều/tất cả, ghi nhật ký old/new/time/source; mở lại DWG khôi phục đúng.
- Với 1.293 nhóm hoặc nhiều hơn, bảng cần virtualized/paged, tránh tạo 1 triệu control/nút, có tiến độ/hủy và quản lý memory.

### P0-C: TIN thử mean-Z đối chứng HSMo
- Chỉ trung bình toàn bộ Z tại XY nhóm có ΔZ ≤ 3,000 m (trung bình n điểm, không phải trung bình min/max khi n>2).
- Nếu bất kỳ ΔZ > 3,000 m: DỪNG trước triangulation, mở bảng xung đột để người dùng tự xử lý; sau thay đổi phải quét lại; không tự sửa.
- Layer TIN thử riêng HT/TK, cảnh báo chính xác "TIN THỬ NGHIỆM – KHÔNG DÙNG ĐỂ TÍNH KHỐI LƯỢNG."; TUYỆT ĐỐI không đưa mesh thử làm nguồn chính thức.
- Strict TIN bảo toàn breakline hợp lệ và từ chối bất nhất chưa xử lý, không giả định thuật toán HSMo dùng Z trung bình.
- Test XY trùng Z, ΔZ 0/3/3,001/94,862 m, nhiều điểm nhóm, breakline, nguồn đã override/restore, hủy và xung đột khi hai DWG.

**PASS P0** chỉ khi core/unit data và luồng host AutoCAD liên quan có bằng chứng tương ứng; nếu dữ liệu SQ còn mâu thuẫn bắt buộc quyết định khảo sát, đánh dấu BLOCKED dữ liệu thực nhưng không bỏ qua test trên bộ tổng hợp sạch.

## 3. P1 — TRẢI NGHIỆM, MẶT CẮT, KHỐI LƯỢNG VÀ BÁO CÁO

### P1-A: Ribbon/menu/Palette
- Menu bar và Ribbon IMSAT VOLUME, nút bật/tắt Palette, tự nạp add-in nhưng không tự mở cửa sổ trừ khi người dùng bật tùy chọn; màn 1366×768 và FullHD, DPI 100/125/150%.
- Nền sáng dịu, chữ/nút tương phản; primary/secondary/danger rõ, không tối quá, không dùng chữ bị cắt, sai font hoặc tấm nền thừa.
- Ghim trái theo phong cách MO: bố trí DỌC, lựa chọn trang phía trên, scroll, không mất dữ liệu khi đổi giữa 360, 480 và >650 px; DataPage, ModelPage, SectionPage, GridPage, ProjectPage, VolumePage, MineralPage, ExportPage, LicensePage đều thử.
- Khi chọn đường bao địa hình, nguồn CAD, hướng/tuyến mặt cắt, điểm chèn xuất mặt cắt, đường bao tính khối lượng và mọi prompt Editor khác: Palette tự ẩn trước khi pick, tự hiện sau chọn/Cancel/Esc; nếu trước đó người dùng đã ẩn thì không tự hiện; không tạo modal dialog che vùng chọn.

### P1-B: Mặt cắt chỉnh tay thật
- Bố trí hệ mặt cắt 20 m đối chứng HSMo, Arial 3, màu HT/TK, trục X/Z, nhãn diện tích, giãn cách bố trí CAD độc lập với bước tuyến địa hình.
- Người dùng sửa polyline hiện trạng/thiết kế trực tiếp trong CAD, kể cả nhiều đoạn tách do vùng thiếu TIN; nút CẬP NHẬT MẶT CẮT đọc lại geometry chứ không lấy lại dữ liệu cũ.
- XData xác định mặt cắt, vai trò HT/TK, chỉ số đoạn, gốc XY của hình vẽ, baseZ và hệ số tỷ lệ; không phụ thuộc xem preview trước khi xuất. Xác định chắc chắn version/document/section để chống gộp khác bản vẽ và cặp bị copy/trùng.
- Test hình chữ nhật deltaZ +1 m trên TOÀN đoạn 10 m => F tăng 10 m²; chỉ thay một đầu +1m trên 10m => F tăng 5 m²; test giao HT/TK trong đoạn, giảm diện tích, khoảng có no-data, 2+ tuyến, nhiều level.
- Sau Update: diện tích đào/đắp toàn tuyến + theo dải/tầng, thể tích theo khoảng, tổng đào/đắp, CAD label và XLSX xuất lần sau đều mới; không giữ sai số hoặc công thức cũ.
- Không chỉnh TIN/source CAD gốc khi chỉnh mặt cắt; lưu DWG/mở lại khôi phục đúng sửa tay; không tự vẽ đè hay xóa sửa tay. Nếu validation fail (đường bị xóa, tự giao, cung, đảo station, missing cặp), rollback/thông báo và không ghi kết quả sai.
- Khi render re-export có xóa layer đầu ra, yêu cầu rõ xác nhận trước khi xóa bản chỉnh sửa CAD đang được bảo toàn.

### P1-C: Khối lượng
- Công thức trung bình diện tích hai đầu, hình chóp và hình chóp cụt đúng ngưỡng chọn, khoảng cách, đơn vị và mức; giải thích phương pháp trong Excel, đối chiếu độc lập phương án đã chốt, tính tổng = theo tầng = chi tiết trong dung sai.
- Grid ô vuông có kích thước X/Y, góc xoay, no-data, ranh lõm và ô giao cả đào/đắp; bảng CAD và XLSX có công thức thực.
- Than/quặng: nhiều vỉa/thân khoáng, outcrop HT/TK, polygons grip/confirm, F vỉa nằm trong miền đào, no-data khác zero, không đè vỉa, nhiều khoảng, m³/tấn theo density và hệ số bóc. Không tuyên bố mô hình địa chất 3D từ dữ liệu mặt cắt 2D nếu chưa có mặt nóc/đáy.

### P1-D: Dự án / kỳ sản lượng / Excel chuẩn người dùng
- Trang Dự án có danh mục các đơn vị khai thác THAN/QUẶNG lộ thiên TKV/VVMI/Vimico đối chiếu với hồ sơ người dùng; có tùy chọn nhập đơn vị chưa có trong danh mục; không xóa tên đã chọn nếu chỉ đổi tab.
- Thứ tự chọn: đơn vị → loại kỳ Tháng/Quý/Năm → tháng 1–12 hoặc quý 1–4 hoặc năm → năm báo cáo. Kỳ năm không hiện/thực thi ô tháng. Lưu/mở DWG và Export đúng nhãn.
- TẤT CẢ workbook gồm thể tích mặt cắt, grid, khoáng sản/vỉa phải ghi đơn vị được chọn, nhóm, kỳ, THỜI ĐIỂM TÍNH THÀNH CÔNG, THỜI GIAN THỰC TÍNH bằng giây (nếu đã đo thực), thời điểm xuất file tách biệt. Không giả mạo timestamp nếu chưa tính.
- TẤT CẢ workbook có sheet tổng hợp phù hợp, đặt hai ô ký NGƯỜI TÍNH TOÁN và NGƯỜI KIỂM TRA, chỗ ký và ghi rõ họ tên. Không mặc định nhà phát triển là người tính toán hay kiểm tra.
- Excel theo phong cách chủ nhiệm: Arial 12, viền khung, không nền màu không cần thiết, không mất chữ, chiều rộng cột hợp lý, giá trị và đơn vị rõ ràng, in A4/A3 bố cục hài hòa, logo IMSAT và tên phát triển **Bùi Thế Nam — ĐT/Zalo: 0967280686**.
- Công thức liên kết giữa các sheet phải thực sự mở và tính được trong Excel Desktop, không phải ảnh/bảng số tĩnh. Kiểm tra mọi công thức và tham chiếu tuyệt đối sau thêm rows, không #REF!, không #DIV/0! bất thường; thử thay tham số được phép để Excel recalc. Không coi sửa bảng Excel là tự thay geometry AutoCAD.
- Dữ liệu khoáng sản 4 sheet có vị trí hàng cố định: nếu thêm notes hoặc row metadata, **bắt buộc tính lại các công thức dựa trên DataStart** thay vì chèn dòng làm lệch.

## 4. P2 — CẤP PHÉP, SETUP V2.0, NÂNG CẤP VÀ UNINSTALL

- Trial 15 ngày LIÊN TỤC từ lần đầu, không reset bằng đóng mở CAD, không mất DWG, có xử lý rollback clock và ledger lỗi. Hết hạn chỉ chặn tính năng cần giấy phép, không xóa dữ liệu khách hàng.
- RSA-SHA256 và machine binding: provision PUBLIC key thật vào bản release bằng quy trình an toàn, private key chỉ trên máy cấp phép, backup/restore issuer trên máy quản trị độc lập, sai key/tamper/wrong machine/expired đều từ chối. Không commit private key vào GitHub hoặc đính kèm Setup.
- Setup tên V2.0, icon IMSAT, Windows x64/AutoCAD 2023; tự nạp cùng AutoCAD, menu/Ribbon và Palette, chỉ AutoCAD 2023 được xác minh. Không claim hỗ trợ CAD 2012+ nếu không có build riêng.
- Kiểm tra cài mới, nâng cấp từ V1.0, cài chồng bản QA, rollback sau lỗi, repair nếu có; registry, Bundle PackageContents.xml, Start Menu/Windows Apps phải khớp version thực.
- Uninstall bằng Windows Installed Apps và shortcut Gỡ IMSAT VOLUME; xóa binary, Bundle, autoload registration, shortcuts và uninstaller/installer đã copy theo đúng version; không xóa DLL/app khác, không xóa bản vẽ DWG, Project, Excel đã xuất, tệp người dùng hay giấy phép hợp lệ. Kiểm tra dọn các backup .v100bak/.v200qabak đã phát sinh mà không xóa nhầm V1 vẫn đang được dùng.
- Sau uninstall thử khởi động AutoCAD: không còn Ribbon/autoload IMSAT do bundle đã gỡ; cài lại phải sạch, không reset thử việc/licensing khi chính sách không cho phép.
- Dùng chữ ký số nếu có quy trình ký; nếu chưa, ghi thiếu thực tế, không bịa chứng chỉ hoặc tuyên bố đã ký.

## 5. P3 — MA TRẬN KIỂM THỬ VÀ CÁCH LÀM VIỆC BẮT BUỘC

### 5.1 Test matrix tối thiểu
| Test-ID | Dữ liệu / thao tác | Bằng chứng / Expected |
|---|---|---|
| T01 | Mở AutoCAD 2023, TKL/menu/Ribbon, bật/tắt Palette | Screenshot, log, giữ user setting |
| T02 | Ghim trái 360/480 px và rộng 650+, DPI 100/125/150%, tất cả trang | Mọi nút/ô hiện đủ, scroll, không mất chữ và input |
| T03 | Chọn layer, boundary, hướng, điểm chèn, lưới, Esc | Palette tự ẩn và trả trạng thái đúng |
| T04 | DXF/DWG Point/Line/LW/2D/3D/Contour, large-coordinate | Source count, X/Y/Z/layer/handle so CAD |
| T05 | Nhóm ΔZ=3,000; 3,001; 94,862; generated node | Bộ lọc chuẩn và không xóa nhầm |
| T06 | Zoom, Sửa Z, Loại điểm, chọn Không/Esc và Đồng ý | đúng điểm, log, CAD gốc không đổi |
| T07 | Khôi phục 1/nhiều/tất cả, save/reopen DWG | state đúng, invalidation và rebuild đúng |
| T08 | Strict TIN HT/TK và preview Z TB trên hai layer khác nhau | Triangle count, Z, breakline, không tính từ preview |
| T09 | Mỏ SQ/Test HS dữ liệu lớn, cancel/retry | Thời gian/RAM/host UI/không treo và không im lặng bỏ dữ liệu |
| T10 | Mặt cắt 20 m, xuất không xem trước, title/line XData | Đầy đủ polyline HT/TK, không error app registration |
| T11 | Grip +1m cho cả đoạn 10m, cập nhật MC | F tăng 10 m², V khoảng/tầng thay đúng |
| T12 | Xóa/copy/đổi layer/sửa bulge/di chuyển nhãn MC | Validation hoặc update chính xác, không kết quả cũ sai |
| T13 | Lưu DWG và reopen với MC chỉnh tay | F, V và CAD geometry giữ nguyên |
| T14 | Lưới ô xoay 0/15/45°, no-data, cut/fill | Cell and total, CAD table và XLSX khớp |
| T15 | Ít nhất ba vỉa/thân, nhiều outcrop, vùng thiếu/vắng | m³/tấn/HSB, no-data không bằng 0 |
| T16 | Excel mặt cắt + grid + khoáng sản, đổi diện tích/thể trọng | Formulas recalc, metadata, chữ ký, Arial/viền/logo |
| T17 | Đơn vị than/quặng, nhập tên khác, đổi tháng/quý/năm, lưu DWG | Tên và kỳ đúng ở tất cả workbook |
| T18 | Thời gian tính thực và thời gian xuất, huỷ/đổi dữ liệu | Timestamp được phân biệt, không dùng kết quả cũ |
| T19 | Trial 15 ngày, rollback, activate RSA thật | Log host/máy, không private key trong Setup |
| T20 | Setup V2 x64 cài mới/nâng cấp V1, uninstall/reinstall | Registry, Bundle, icon, autoload, dữ liệu an toàn |
| T21 | Nhiều DWG, đổi tab giữa thao tác, command Esc/Close | Không lẫn project, không deadlock, không mất tài liệu |
| T22 | Regression V1 chức năng từng chạy, không sửa branch V1 | Diff và workflow riêng không regress |

### 5.2 Quy chuẩn kiểm thử
- Mỗi Test-ID có môi trường cụ thể (Windows, AutoCAD build, CPU/RAM, màn, DPI), nguồn dữ liệu và SHA256, thao tác Steps, Expected, Actual, PASS/FAIL/NOT TESTED/BLOCKED, ảnh/log/DWG hoặc Excel thật, commit sửa lỗi và run CI sau sửa.
- Core tests số học: dữ liệu tổng hợp có đáp án độc lập; dùng oracle khi hợp lệ, không ghi "PASS" từ cùng một hàm tự so chính nó.
- Kiểm thử dữ liệu thật nếu còn ΔZ gây BLOCKED: không tự đoán Z khảo sát; test sạch ở dataset khác để xác nhận core, báo rõ tập dữ liệu SQ chưa được chấp thuận.
- Stress: số vertex 100k/1M+, mức RAM peak thực và thời gian (không chỉ managed memory cuối); timeout có hủy, không khóa UI.
- Nếu không có AutoCAD host, chỉ build+CLI+reference/core PASS, host NOT TESTED; yêu cầu người dùng chạy 1 đợt QA theo script tổng hợp, tránh từng thay đổi nhỏ lại yêu cầu người dùng thử lại.
- Mọi lỗi FAIL phải được sửa, thêm regression test, chạy lại chính xác đến HEAD sau cùng. Không coi pipeline cũ làm bằng chứng cho commit mới.

## 6. CÁCH THỰC HIỆN / GATE NGHIỆM THU

**Bước P0:** Đọc code và lịch sử các bug TIN, xử lý các rủi ro dữ liệu/sai số lớn đầu tiên; đóng P0 bằng bằng chứng test và dữ liệu trước khi sửa sâu khối lượng khác.  
**Bước P1:** Sửa lần lượt RF-01 → RF-07, RF-10 → RF-14; đảm bảo 22 test cases có test tự động nhất có thể, đặc biệt Excel khoáng sản và CAD edited sections.  
**Bước P2:** Sửa installer/Uninstall/nâng cấp/license, tạo QA Setup có manifest/hash; chỉ nâng thành release candidate chính thức khi key và các gate bảo mật đã có bằng chứng.  
**Bước P3:** Chạy full automation CI cho HEAD sau cùng; host AutoCAD 2023 nếu được phép; cập nhật QA docs; tạo PR tích hợp từ đúng nhánh tiếp quản sang nhánh đích V2 chứ không tự merge vào V1.

**Hard release gates:**
- G1: GitHub Actions trên SHA cuối hoàn tất PASS ở Python, C# regression, .NET Release build, issuer tests, native installer, SHA256 artifact; không FAIL/skipped do lỗi.
- G2: TIN HT/TK, XY/Z, preview/strict, sửa/khôi phục, lưu/mở DWG không mất dữ liệu; xét trường hợp SQ còn vấn đề riêng.
- G3: Tất cả nút UI có handler thật, mọi thao tác CAD/palette và mặt cắt chỉnh sửa + cập nhật F/V hoạt động; test host thực.
- G4: Excel ba họ báo cáo có metadata TKV/kỳ/timing/chữ ký/logo/công thức hợp lệ và định dạng đúng chuẩn người dùng.
- G5: Trial/activation thực với public key, Setup + uninstall + nâng cấp test trên Windows/AutoCAD 2023; DWG và dữ liệu không bị xóa.
- G6: V2_FINAL_QA_REPORT.md và V2_RELEASE_GATES.md được cập nhật HEAD/CI/ảnh/log/đường dẫn checksum/Actual status, đánh dấu đầy đủ PASS/FAIL/NOT TESTED/BLOCKED.

**Không được ghi 100% hoàn tất hoặc DONE** trừ khi tất cả phần trong phạm vi Work và kiểm thử tự động có bằng chứng; nếu thiếu AutoCAD host thì chỉ được kết luận "Triển khai + CI PASS, AutoCAD runtime NOT TESTED". Không phát hành V2.0 chính thức khi có bất cứ hard gate phát hành nào NOT TESTED/BLOCKED/FAIL; vẫn có thể xuất QA Candidate gắn nhãn KHÔNG PHÁT HÀNH để người dùng nghiệm thu.

## 7. ĐẦU RA WORK PHẢI TỰ NỘP

1. Bảng đối chiếu Req-ID/RF-ID/T01-T22 với bằng chứng và trạng thái, không che lỗi chưa sửa.
2. Danh mục commit/PR, giải thích phần nào giữ nguyên phần nào sửa; không động nhánh V1.0.
3. Build/test/CI tại SHA cuối: số test PASS/FAIL, logs và hyperlink workflow.
4. Báo cáo đối chứng TIN dữ liệu lớn và sự chênh Z chưa được chuyên môn xử lý (nếu còn).
5. Bảng đối chứng tính F/V (mặt cắt gốc – đã chỉnh, từng tầng, tổng), Excel ba loại thực, screenshot giao diện 1366x768/FullHD + dock trái.
6. QA Setup V2.0 (hoặc release nếu mọi gate đạt) có đúng filename, link GitHub artifact/release đã xác minh, SHA-256 đối chiếu, hướng dẫn cài/nâng cấp/gỡ, test dữ liệu tồn tại.
7. Cập nhật docs/V2_FINAL_QA_REPORT.md, docs/V2_RELEASE_GATES.md, docs/V2_AUTOCAD_ACCEPTANCE.md và tài liệu này khi có kết quả mới.
8. TÓM TẮT BÀN GIAO: "Tự động PASS X/Y — AutoCAD host PASS/NOT TESTED — license PASS/BLOCKED — Setup QA/Release — SHA — checksum".

**LỆNH BẮT ĐẦU CHO WORK:** Mở repository/nhánh nêu trên, đọc toàn bộ tài liệu này và PlanCode ngày 11/10, kiểm tra HEAD và CI; THỰC HIỆN thật việc rà soát, sửa lỗi, commit, test, build và cập nhật hồ sơ theo thứ tự P0→P1→P2→P3. Không dừng lại ở việc viết một kế hoạch mới, không đòi người dùng nhắc lại yêu cầu đã chốt.
