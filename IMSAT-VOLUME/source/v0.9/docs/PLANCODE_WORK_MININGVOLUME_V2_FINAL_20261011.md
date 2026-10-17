# PLANCODE — WORK TIẾP QUẢN HOÀN THIỆN IMSAT MININGVOLUME V2.0

**Mức ưu tiên: P0 — Sửa TIN / xử lý trùng XY, khác Z trước mọi nhiệm vụ khác.**  
**Ngày lập: 11/10/2026.**  
**Sản phẩm cuối: add-in .NET chạy thực tế trên AutoCAD 2023, bộ Setup/Uninstall, QA report + bản phát hành V2.0 khi đã đủ bằng chứng.**

## 0. Nguồn sự thật và nguyên tắc làm việc

- Repository: `buithenamktm-coder/TKV_ATVSLD`.
- Đường dẫn sản phẩm cũ cần kế thừa: `tools/MiningVolume_HSNext/v0.9/` — **KHÔNG lập project khác** chỉ vì đường dẫn còn chữ `v0.9`.
- **V1.0 Work baseline — KHÔNG sửa/ghi đè/force-push:** `miningvolume-v1.0`.
- **V2.0 QA baseline:** `miningvolume-v2-qa-candidate`, PR #25, tại commit `74628b9d0d220097613694a731ed1dda7a14b3fb`.
- **TIN đang phát triển:** `miningvolume-v2-hs-all-sources-tin`, PR #26, tại commit lúc bàn giao `249be04a31cd0b4c0a99cacb983641dcdcfc5fdb`. **Không mặc định nhánh này PASS.**
- Work tiếp tục trên chính nhánh PR #26 hoặc tách nhánh `miningvolume-v2-work-final` từ HEAD của PR #26; tránh commit song song vào nhánh đang do người khác sửa. Tạo PR tích hợp, không merge vào V1 trước khi nghiệm thu.
- Giữ toàn bộ tính năng đang hoạt động ở PR #25; không viết lại menu, Ribbon, Palette, mô hình, hoặc workbook từ đầu. Mỗi sửa lỗi cần test chống hồi quy.
- Tập trung hoàn toàn **IMSAT MiningVolume**, không chuyển dự án.
- Không được nhận định thuật toán HSMo là “lấy Z trung bình” chỉ dựa vào việc HSMo dựng được TIN. Trung bình Z là giả thuyết đối chứng được người dùng đồng ý thử.
- Không tự phát hành/đánh dấu PASS AutoCAD host nếu chỉ có GitHub CI xanh.

## 1. Tình trạng thực tế / lỗi biết trước phải xử lý đầu tiên

1. PR #25 chứa TIN HT/TK, mặt cắt, grid, khoáng sản theo vỉa, xuất Excel/CAD, View 3D, license/trial và Setup QA; trước đó có CI PASS nhưng **chưa nghiệm thu AutoCAD 2023 runtime**.
2. PR #26 đã tạo lõi `MeanZPreviewPreparer.cs`, `MeanZPreviewWorkflowService.cs`, nút TIN Z TB thử trên `ModelPage.cs`, `DuplicateXYConflictUi.cs` và cửa sổ mới `MeanZConflictReviewForm.cs`. Hai cửa sổ xử lý trùng XY cùng tồn tại; **phải thống nhất tích hợp cho đúng luồng UI**, không để nút không có tác dụng.
3. CI PR #26 FAIL ở bước `Run independent QA`, kết quả quan sát: **215 passed, 1 failed**, lỗi `tests/reference/test_tin_zmean_preview_static.py:19` đòi chuỗi `SurfaceRegionClipper.Clip(` trong `MeanZPreviewWorkflowService.cs`; phải kiểm tra API vùng TIN mới đã thực sự thực hiện tương đương rồi cập nhật test theo hành vi, **không xóa test hoặc đặt PASS giả**. Kiểm tra thêm compile lỗi do hàm `EditVertexZAsync(Row)` và `ExcludeVertexAsync(Row)` nếu `DuplicateXYConflictUi.cs` đang gọi mà chưa định nghĩa. Chạy lại các pipeline tới HEAD cuối cùng PASS.
4. Dữ liệu HSMo tham chiếu do người dùng cung cấp trong cuộc trò chuyện: `Tinh kl SQ 2026.09.091.dxf`, `Tinh kl SQ 2026.09.09.dwg`, `Test HS.dxf`, các ảnh xung đột XY/Z. Tìm dữ liệu đính kèm từ chat/Project; nếu bytes không sẵn trong Work, báo đúng danh mục còn thiếu và yêu cầu nạp lại, không dựng số liệu kiểm chứng.
5. HSMo lưu TIN qua đối tượng độc quyền `HDAI_TIN_MODEL_OBJ`, không đồng nghĩa có mesh 3DFACE giải mã được. HSMo đã xuất hệ mặt cắt 20 m; có thể kiểm tra **hình dạng mặt cắt** và Z đường mặt cắt, nhưng không được giả định biết quy tắc tam giác nội bộ.

## 2. P0 — Nghiệm thu việc tạo TIN đúng và có thể vận hành

### 2.1 Dữ liệu và hai mô hình độc lập

- Đầu vào Point, LINE, đường đồng mức, LWPOLYLINE/2D POLYLINE, 3D POLYLINE; đọc X/Y/Z chính xác theo loại; phân biệt 2D elevation và Z từng vertex 3D; kiểm tra layer và trạng thái ẩn/locked.
- Nạp toàn bộ các loại đối tượng được người dùng đánh dấu (như thao tác HS/HSMo), không tự bỏ qua file lớn; mỗi mô hình HT/TK chọn layer và vùng TIN riêng hoặc vùng chung được chốt. Ví dụ file mỏ Vỉa 4: `ht` = HT, `- nam4` = TK, `LO_TINHKL` = đường bao; file SQ dùng `!HT`/`!TK` theo dữ liệu thực tế.
- Giữ tọa độ lớn của bản đồ mỏ, dung sai rõ ràng; không nhầm m với mm hoặc đảo X/Y. Không chỉnh sửa đối tượng DWG gốc khi xử lý TIN.
- TIN HT và TIN TK có mô hình, layer, dữ liệu, màu và lifecycle độc lập; tắt/bật, lưu/khôi phục DWG; chống dùng TIN đã hết hạn khi nguồn sửa.
- Vùng TIN (boundary) có polygon kín, không tự chia mất lỗ; thông báo rõ điểm/đường bị clipping; không vẽ tam giác ngoài biên được phép.

### 2.2 Hai chế độ — không trộn kết quả

**A. TIN TIÊU CHUẨN / CHÍNH THỨC:** giữ nguyên cơ chế bảo toàn breakline có kiểm soát, từ chối dữ liệu xung đột cao độ chưa xử lý; không tự dùng Z TB khi có mâu thuẫn lớn. Nguồn được sửa bởi người dùng qua UI hoặc các quy tắc nguồn được xác nhận.

**B. TIN Z TRUNG BÌNH — CHỈ THỬ NGHIỆM/ĐỐI CHỨNG HSMo:**
- Từ **tất cả đỉnh đang tham gia mô hình**, nhóm cùng XY theo tolerance có căn cứ; khi nhóm gồm n quan sát: `Z_tb = Σ Z_i / n` (n ≥ 2); không nhầm trung bình cực trị với trung bình nhiều quan sát.
- Không áp dụng tự động cho mọi mặt địa hình chính thức; chỉ dùng tạo mesh đối chứng. Nếu loại bỏ breakline mâu thuẫn, phải cảnh báo rõ điều đó ảnh hưởng mép/chân tầng và hình học 3D; không được ghi nhãn “giống HSMo” khi chưa chứng minh.
- Bề mặt này chỉ nằm trên layer `MV_TIN_ZMEAN_HT_TEST_ONLY`/`MV_TIN_ZMEAN_TK_TEST_ONLY`, không cập nhật `ModelSession.Tin`, `SectionProfiles`, `VolumeResult` hay nguồn dữ liệu TIN chính thức. Không để lệnh mặt cắt/khối lượng tự chọn nhầm preview.
- Cho Cancel, thông báo tiến độ, bảo vệ đổi tab DWG trong lúc worker chạy, không làm treo AutoCAD; quản lý bộ nhớ/temporary file của TIN lớn.
- Không bỏ âm thầm hàng triệu điểm hay báo 100.000 mặt CAD xem trước là mesh đầy đủ. Nếu chỉ sampling preview, thông báo chính xác bao nhiêu tam giác thực và bao nhiêu mặt minh họa.
- Báo cáo nhóm gộp XY, Z_min/Z_max/Z_tb, ΔZ, số điểm, loại/handle/layer và status; CSV/Excel có thể mở được.

### 2.3 Bảng kiểm tra điểm trùng XY/khác Z — yêu cầu đã CHỐT của người dùng

**UI phải rõ ràng và thao tác được ngay**, kể cả tại 1366×768:
- Bảng **nhóm theo từng XY**, mỗi nhóm có hàng đầu `XY / X / Y / số nguồn / Zmin / Zmax / ΔZ`; các hàng con liệt kê **tất cả điểm nguồn thuộc nhóm**, Z của từng điểm, layer, loại đối tượng, handle, chỉ số đỉnh, nguồn nội suy hay đỉnh gốc. Sắp xếp nhóm theo ΔZ giảm dần. Không trộn các điểm khác XY chỉ vì thuộc một layer.
- **Mặc định hiển thị nhóm ΔZ > 3,000 m** (điều kiện >, không phải ≥); có checkbox để hiển thị cả các nhóm nhỏ hơn nếu muốn. Trị số 3 m là ngưỡng yêu cầu **kiểm tra thủ công**, không phải dung sai cho phép hợp thức hóa sai lệch.
- Tùy chọn lọc layer/handle/tên nhóm; Zoom **đúng XY**, không chỉ zoom toàn bộ chiều dài Polyline. Tìm và nhảy tới đối tượng gốc.
- **MỖI DÒNG ĐIỂM:** có nút `Zoom`, `Loại điểm`, `Sửa Z`; các nút không hiển thị/sử dụng trên hàng tiêu đề nhóm. Nếu nguồn là giao điểm/nút sinh nội suy không ánh xạ chắc chắn về đúng vertex gốc, khóa nút sửa/xóa điểm, cho xem đối tượng nguồn và hướng dẫn cách giải quyết — không xóa nhầm.
- Khi chọn `Loại điểm`, mở dialog với **“Bạn có chắc chắn muốn loại điểm này khỏi mô hình không?”**; thể hiện rõ X, Y, Z, ΔZ, layer, handle, thứ tự đỉnh và hậu quả; hai nút **“Đồng ý”** và **“Không”**; **mặc định “Không”**. Chọn Không/đóng dialog → không thay đổi bất kỳ dữ liệu nào. Chọn Đồng ý → chỉ tắt đúng vertex nguồn trong mô hình, **không xóa entity/vertex khỏi CAD gốc**; không tự loại cả polyline vì một điểm lỗi.
- Nút `Sửa Z`: dialog nhập số Z mới, hiển thị Z cũ/nhóm/nguồn, kiểm tra hữu hạn; chỉ override vertex trong mô hình, giữ CAD gốc, có Confirm và không sửa khi Cancel.
- Sau bất kỳ sửa/loại nào, báo trạng thái và quét cập nhật nhóm; không dùng `ConflictReport`/model revision cũ; vô hiệu TIN/mặt cắt/khối lượng cũ đúng phần liên quan, lưu nhật ký sửa (old/new value, loại thao tác, thời gian, handle/index, người thực hiện nếu có).
- Nút phục hồi 1 điểm/khôi phục lựa chọn và khôi phục tất cả; sau khi mở lại DWG phải khôi phục trạng thái mô hình, không xóa mềm thành xóa cứng.
- Nếu 1.293 nhóm hoặc nhiều hơn: DataGridView VirtualMode, quét nền hủy được, giữ UI responsive, không chặn AutoCAD do binding 1 triệu hàng.
- Thống nhất vai trò hai cửa sổ `DuplicateXYConflictUi.cs` và `MeanZConflictReviewForm.cs` để có **một luồng xử lý dễ hiểu**, không bỏ dở các nút. Các handler mỗi dòng phải map tới lệnh hiện thực, không tạo giao diện giả.

### 2.4 Hard gate tạo TIN thử

Khi người dùng ấn TIN Z TB: quét nguồn, nếu có bất kỳ nhóm **ΔZ > 3,000 m**, hiển thị bảng xem xét và hướng dẫn loại/sửa điểm, **không tự dựng lưới trung bình cao độ vượt ngưỡng**. Sau khi xử lý, quét lại; nếu không còn nhóm vượt ngưỡng mới cho người dùng xác nhận tạo TIN thử. Cho phép hủy/thoát mà không thay đổi nguồn. Hai trường hợp trung bình chưa vượt ngưỡng và có thể nội suy trên TIN thử vẫn **không được** dùng để tính khối lượng chính thức. Phải có tests P0 cho gate này.

### 2.5 Test định lượng tạo TIN

- XY trùng: 0 và 94,862 → mean = 47,431 m, **phải chặn preview tự động do ΔZ > 3**.
- XY ba đỉnh: 0; 20; 94,862 → mean = 38,287333... m nhưng **phải yêu cầu xử lý thủ công vì ΔZ > 3**, không chạy tới tính thể tích.
- XY trùng Z y hệt: khử trùng, không báo xung đột.
- ΔZ đúng 3,000 và 3,001 m; test chênh 0,001, nhiều điểm cùng nhóm, tolerances, coordinate 2.500.000+, Polyline3D giao nhau, đầu mút trùng 2D/3D, điểm nguồn bị loại và được khôi phục; không gán nhầm status generated → original.
- Chứng minh hình tam giác nguồn và cạnh constraint hợp lệ theo từng chế độ; không tam giác suy biến, không bỏ im lặng dữ liệu đã chọn.
- Tạo 2 TIN HT/TK trong vùng LO_TINHKL; lưu/đóng/mở DWG, sửa nguồn, dựng lại, so sánh số tam giác và Z. Dùng dữ liệu SQ so sánh trực quan với HSMo, không khẳng định thuật toán HS khi không giải mã được mesh.

## 3. P1 — Rà soát và hoàn thiện chức năng V2.0 kế thừa (SAU khi P0 đạt)

- Ribbon và menu bar `IMSAT VOLUME`: các nút mở palette, menu bật/tắt không vô tác dụng, không tự pop-up bất tiện khi AutoCAD khởi động; tiếng Việt không mất chữ, Windows 1366×768/FullHD, scaling DPI.
- Quản lý điểm/line/polyline/contour: bảng X/Y/Z, loại, layer, remove/restore, Save/Update model, đảm bảo không tạo TIN từ trạng thái lỗi.
- TIN 3D và thao tác xoay/pan/ESC, đổi bản vẽ, tránh treo UI; không làm hỏng hoặc xóa layer khác.
- Vẽ hệ mặt cắt/nhãn thang trục/diện tích đào-đắp; khoảng giãn khi trình bày và cự ly mặt cắt tính toán độc lập; thử chính tuyến 20 m của HSMo.
- **MỚI CHỐT 12/10/2026 — HIỆU CHỈNH HÌNH HỌC MẶT CẮT:** Sau khi xuất hệ mặt cắt CAD, cho phép người dùng hiệu chỉnh trực tiếp đường biểu diễn hiện trạng và thiết kế tại từng mặt cắt để khắc phục những sai khác/lỗi hình học do TIN/3D. Mỗi đường có ID mặt cắt + vai trò HT/TK + hệ quy chiếu tọa độ mặt cắt riêng (không suy đoán chỉ từ layer). Nút **CẬP NHẬT MẶT CẮT** phải đọc lại đúng hình học CAD đã sửa, biến đổi từ tọa độ bố trí ra khoảng cách dọc tuyến và cao độ thực, kiểm tra trật tự điểm, giao nhau, vùng thiếu dữ liệu, không cho tính với đường bị xóa/không hợp lệ; cập nhật *dữ liệu mặt cắt có thẩm quyền*, tính lại diện tích đào/đắp (và phân theo tầng), tính lại toàn bộ khối lượng từ diện tích mới, vô hiệu bản tính cũ và cập nhật bảng/Excel khi xuất lại. **Không chỉ đổi đường hiển thị!** TIN gốc HT/TK không tự bị sửa; phải phân biệt rõ mặt cắt đã hiệu chỉnh thủ công với mặt cắt gốc tính từ TIN. Bảo toàn chỉnh sửa khi lưu/mở DWG; không tự vẽ lại và xóa mất sửa tay khi bấm Cập nhật. Test định lượng thay một cao độ +1 m trên chiều dài 10 m làm diện tích thay 10 m² và khối lượng cập nhật theo khoảng mặt cắt.
- Lưới ô vuông TIN HT/TK theo địa hình thật, grid xoay, ô hỗn hợp cut/fill/no-data, cạnh bao, nội suy Z tại góc/giao tam giác; không xóa ô thiếu dữ liệu khỏi báo cáo.
- Khối lượng từ các mặt cắt theo tầng, tổng và theo khoảng; kiểm tra công thức trung bình/hình chóp/hình chóp cụt có đơn vị và đúng điều kiện lựa chọn. **Sau khi P0 TIN đạt mới tập trung đối chứng khối lượng.**
- Nhiều vỉa than/thân quặng: gán đường lộ vỉa, vùng khoáng sản dạng polygon trên mặt cắt, nghiệm thu giới hạn trong phần đào, không đè vỉa, không coi thiếu diện tích là số 0; tính m³, tấn và hệ số bóc theo vỉa.
- Bảng CAD loại TABLE native, layer riêng; Excel có công thức liên kết giữa các sheet; giữ layout chuẩn **Arial 12, viền bảng, không tô nền**, logo IMSAT; tên nhà phát triển **Bùi Thế Nam; Điện thoại: 0967280686**.
- Thêm dữ liệu mẫu/đối chiếu số học tự động để chứng minh không mất công thức khi sửa tham số hoặc mở lại DWG. Sau mỗi sửa UI/công thức, chạy regression toàn bộ.

## 4. P2 — Bản quyền và Setup V2.0 thực

- License: dùng thử **15 ngày liên tục** tính từ lần dùng đầu tiên; không làm mất dữ liệu DWG nếu hết hạn. Không công bố tính năng activate vĩnh viễn khi `LicenseTrustAnchor.RsaPublicKeyXml` còn placeholder rỗng.
- Hoàn thiện cấp giấy phép mã máy bằng RSA-SHA256, public trust anchor gắn trong bản build chính thức, private signing key chỉ trên máy quản trị (không đi vào repo, DLL, Setup). Chạy backup/restore issuer key trên máy quản trị độc lập và self-test chữ ký/tamper/rollback; phân quyền, cơ chế cấp lại nếu máy thay đổi.
- Setup/Uninstall: **phân biệt rõ bản V2.0 chính thức với QA Candidate**; icon/logo IMSAT trên file .exe, palette và Excel; AutoCAD 2023 64-bit, khởi động tự nạp add-in, menu/Ribbon hiện đúng, bật/tắt giao diện, nâng cấp từ bản V1 an toàn, gỡ sạch phần mềm **không xóa dữ liệu DWG/license khách hàng**, rollback khi install thất bại, chữ ký số nếu có quy trình.
- Đánh giá hỗ trợ CAD 2012+: nêu ma trận API/.NET per version và adapter/build tương ứng; **không tuyên bố chạy 2012+ nếu chưa có binary + kiểm thử host từng đời**. AutoCAD 2023 là mục tiêu phát hành V2.0 hiện tại.

## 5. P3 — Kiểm thử Windows và AutoCAD host, bắt buộc bằng chứng

1. Run toàn bộ pytest/reference checks; C# CoreRegression với native CDT thật; build Release cả Core, Surface, Plugin2023, LicenseManager, installer; selftest/license backup; SHA256 của Setup; upload artifact đúng tên phiên bản V2.0.
2. Chạy trong AutoCAD 2023 thật nếu Work có quyền truy cập. Nếu không có máy/host, **ghi NOT TESTED**, kèm hướng dẫn cụ thể từng lệnh và checklist để người dùng chạy rồi Work nhận log, chẩn đoán và sửa.
3. Kiểm thử file mẫu cả đầu vào và đối chứng HSMo: `Tinh kl SQ 2026.09.091.dxf`, `Tinh kl SQ 2026.09.09.dwg`, `Test HS.dxf`, 2 file DXF từng báo lỗi trước đây. Test trường hợp 1.293 nhóm và chênh Z 5–94,862 m; chịu tải dữ liệu lớn, quản lý RAM, timeout/cancel, mở DWG lại.
4. E2E: cài → mở AutoCAD → Ribbon/menu/Pallette → đọc layer HT/TK → bảng ΔZ>3 nhóm XY → Zoom/Sửa Z/Loại Điểm (Không/Đồng ý) → quét lại → TIN strict và test layer → mặt cắt → lưới ô vuông → than/quặng → Excel/CAD TABLE → lưu mở lại → gỡ/bảo toàn DWG.
5. Mỗi ca có Expected/Actual/PASS|FAIL|NOT TESTED, ảnh/log nếu host test, lỗi có severity và commit fix; không dùng mock output thay kết quả CAD runtime.

## 6. Release Gates — điều kiện Work mới được báo DONE

- **Gate 1:** PR #26 CI và build Windows PASS trên commit HEAD cuối cùng; compile đầy đủ nút `Zoom`/`Loại điểm`/`Sửa Z`, không còn handler thiếu; warning không che mất crash.
- **Gate 2:** TIN tiêu chuẩn + thử nghiệm độc lập; ΔZ > 3m hiển thị nhóm và không tự gộp trước xử lý; Cancel thực sự không thay đổi source; có test định lượng đúng và data provenance.
- **Gate 3:** hai file DXF đầu vào và trường hợp SQ được tạo 2 TIN trong AutoCAD 2023, UI không treo, không xóa nhầm CAD, số liệu nguyên bản có nhật ký; Work thực hiện hoặc người dùng chạy theo test script.
- **Gate 4:** toàn bộ V2.0 kế thừa hoạt động: mặt cắt, grid, vỉa, export formula, Ribbon/menu/Palette, nhiều DWG và lưu/restore, không regression V1.
- **Gate 5:** bản quyền/key/Setup V2.0 thực hoạt động và nghiệm thu trên máy Windows có AutoCAD 2023; PUBLIC trust anchor thực được provision, không lộ private key.
- **Gate 6:** có báo cáo `V2_FINAL_QA_REPORT.md` + `V2_RELEASE_GATES.md`, link run CI, bảng kết quả kiểm thử và installer checksum. Không publish/tag Release V2.0 nếu còn gate FAIL hoặc NOT TESTED bắt buộc.

## 7. Bàn giao và phương thức hoạt động Work

**Work được yêu cầu tự triển khai trực tiếp**: đọc, sửa, commit, chạy test, sửa tiếp, tạo PR, cập nhật QA documents, đóng gói Setup. Không dừng ở phân tích hoặc đề cương. Tuy nhiên phải dừng đúng điểm thiếu host AutoCAD hoặc thiếu tài liệu nguồn, không giả lập PASS. Gửi báo cáo tiến độ theo mốc **P0 -> P1 -> P2 -> P3** và bản cuối đầy đủ:
- Nhánh/commit, PR, số test PASS/FAIL.
- Chức năng đã sửa, lỗi còn lại và đường dẫn artifact Setup V2.0.
- Trạng thái chứng cứ host/QA (PASS/FAIL/NOT TESTED).
- Cách kiểm thử trên máy người dùng, kể cả TIN ΔZ>3 và hai mô hình HT/TK.
- Chỉ yêu cầu người dùng cung cấp dữ liệu/môi trường thật khi thực sự cần. Không yêu cầu xác nhận lại những quyết định kỹ thuật đã chốt trong PlanCode.

**Ưu tiên tuyệt đối:** sửa cơ chế **TẠO TIN** và **BẢNG TRÙNG XY–CHÊNH Z** trước; không chuyển sâu vào tính khối lượng khi TIN chưa qua nghiệm thu.
