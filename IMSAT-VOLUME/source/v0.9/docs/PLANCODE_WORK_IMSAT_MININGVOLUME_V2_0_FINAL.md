# PLANCODE — IMSAT MININGVOLUME V2.0: HOÀN THIỆN TOÀN BỘ, KIỂM THỬ VÀ NGHIỆM THU

**Trạng thái:** CHỐT NHIỆM VỤ GIAO CHATGPT WORK — triển khai ngay, không dừng ở báo cáo hoặc đề xuất.  
**Ngày chốt:** 11/10/2026.  
**Sản phẩm:** Add-in IMSAT MiningVolume V2.0 tích hợp AutoCAD; giao diện HS/HsMo-like bằng Ribbon/menu/Palette tiếng Việt, không vận hành chủ yếu kiểu LISP/command line.  
**Repository:** `buithenamktm-coder/TKV_ATVSLD`  
**Thư mục sản phẩm duy nhất:** `tools/MiningVolume_HSNext/v0.9/` (GIỮ NGUYÊN cấu trúc; tên thư mục v0.9 không cho phép làm mất mã hoặc khởi tạo project mới).  

## A. Điểm xuất phát — CÁC NHÁNH BẮT BUỘC

- `miningvolume-v1.0` — Work V1.0; SHA `4c81508631151e58707e49de75e3522f55a76af2`. **KHÔNG sửa, force-push, reset, merge trực tiếp hoặc xóa nhánh này khi chưa nghiệm thu.**
- `miningvolume-v2-qa-candidate` — V2 QA tích hợp, PR #25, SHA `74628b9d0d220097613694a731ed1dda7a14b3fb`. Windows CI từng PASS; nền chức năng V2 có Grid, 3D, mineral, Excel/CAD TABLE, Trial.
- `miningvolume-v2-hs-all-sources-tin` — PR #26 (base QA), mã TIN Z trung bình thử nghiệm và bảng điểm trùng đang phát triển. Tại thời điểm lập PlanCode, HEAD `249be04a31cd0b4c0a99cacb983641dcdcfc5fdb`; **chưa nghiệm thu**, CI có lỗi và mã UI mới cần sửa/kiểm thử. **Lấy nhánh này làm điểm khởi hành, rồi tạo nhánh Work tích hợp riêng**, ví dụ `miningvolume-v2-work-completion`; không phá nhánh QA gốc.
- PR lịch sử: #14–#26; kế thừa những chức năng đã chạy, tránh chép lại hoặc merge trùng code. PR #22 License đã được kết hợp vào QA #25, không merge máy móc làm mất giao diện.

**MỤC TIÊU CUỐI:** một bộ cài V2.0 chạy thật, tính đúng, giao diện hoàn thiện, có đầy đủ thử nghiệm/biên bản và hướng dẫn. Chỉ tuyên bố RELEASE khi mọi hard gate đạt và có bằng chứng.

## B. QUYẾT ĐỊNH ĐÃ CHỐT VỀ TIN — ƯU TIÊN SỐ 1 (KHÔNG HỎI LẠI)

### B1. Cơ chế chuẩn TIN / cơ chế thử

1. **TIN TIÊU CHUẨN:** bảo toàn hình học và các đường khống chế địa hình/breakline đúng; xử lý chính xác Point, LINE, LWPOLYLINE (đường đồng mức có Elevation), POLYLINE/3D POLYLINE, đường ranh, dữ liệu theo layer; không gộp cao độ xung đột lớn theo cách ngầm định. Giữ chức năng xây hai TIN độc lập: hiện trạng và thiết kế. Không cắt bỏ ngầm một phần đỉnh/cạnh.
2. **TIN Z TRUNG BÌNH — THỬ NGHIỆM:** chỉ hoạt động khi chọn rõ bằng UI; dùng toàn bộ các **đỉnh hợp lệ đang được bật trong nguồn của mô hình tương ứng**. Tại cùng XY (dung sai XY `1e-6 m`, chỉ đổi sau khi có kiểm chứng), nếu `ΔZ ≤ 3.000 m`, lấy **trung bình cộng Z của TOÀN BỘ đỉnh** trong nhóm (không chỉ trung bình của Zmin/Zmax khi có nhiều hơn hai đỉnh). Nếu `ΔZ > 3.000 m`, **DỪNG trước bước triangulation**, bắt người dùng kiểm tra chính nhóm này; không tự gộp hay tạo lưới lặng lẽ.
3. Ngưỡng `3.000 m` **chỉ là ngưỡng bắt kiểm tra trong chế độ thử**, KHÔNG phải dung sai địa hình/sai số được công nhận cho tính khối lượng chính thức. `ΔZ = 3.000` được phép trung bình; `ΔZ > 3.000` bắt buộc xử lý.
4. Lưới thử phải vào layer riêng `MV_TIN_ZMEAN_HT_TEST_ONLY` / `MV_TIN_ZMEAN_TK_TEST_ONLY`, màu phân biệt và nhãn **“TIN THỬ NGHIỆM — KHÔNG DÙNG TÍNH KHỐI LƯỢNG”**. Không được gán vào `ModelSession.Tin`, không ghi đè `MV_TIN_HIENTRANG`/`MV_TIN_THIETKE`, không cập nhật `VolumeResult`, các mặt cắt tính chính thức, và không thay đổi source DWG.
5. Nếu chế độ thử bỏ các constraint breakline để tạo mesh từ điểm, **phải ghi rõ ràng** lưới đó KHÔNG bảo toàn mép tầng/chân tầng/đứt gãy. Không quảng cáo nó là cách HSMo thực tế nội suy nếu chưa đối chứng được tam giác HSMo. Cần kiểm tra tính liên tục, diện tích miền phủ và các lỗ trước khi so sánh.
6. Phạm vi TIN tùy chọn: khi chọn polygon vùng tính, lọc đúng các đỉnh nguồn thực có danh tính editable và dải đệm có kiểm chứng; không chỉ tạo đỉnh clipping giả rồi liên kết sai `VertexIndex` với CAD gốc. Lưới đầu ra phải giới hạn theo biên yêu cầu; không có dữ liệu thì báo rõ.
7. Hai TIN nguồn phải độc lập, lưu/khôi phục và không lẫn layer. Không bỏ qua lỗi nguồn `!HT`, `!TK`, `ht`, `- nam4`; vùng ranh `LO_TINHKL` nếu dữ liệu tương ứng.

### B2. BẢNG TRÙNG XY, KHÁC Z VƯỢT 3 M — UI CHỐT CHÍNH THỨC

1. Khi phát hiện bất kỳ nhóm có `ΔZ > 3.000 m`, **mở bảng quản lý**, không tự thực hiện triangulation thử.
2. **NHÓM XY:** một dòng/nhóm, hiển thị ID nhóm, X, Y, số điểm, Zmin, Zmax, ΔZ; ưu tiên sắp giảm theo ΔZ. Bộ lọc nhóm, layer, handle, loại nguồn; xử lý được 1.293+ nhóm và bản vẽ lớn (virtualization, phân trang/tìm kiếm nếu cần; không khóa AutoCAD).
3. **CHI TIẾT ĐIỂM TRONG NHÓM:** khi chọn nhóm, liệt kê toàn bộ các điểm nguồn có cùng XY, gồm Z của mỗi điểm, loại đối tượng, layer, handle, vị trí/vertex index, trạng thái tham gia. Không tự suy diễn điểm nào sai; cho kỹ sư quyết định.
4. **Mỗi dòng điểm có 3 THAO TÁC:** `Zoom tới`, `Sửa Z`, `Loại điểm`. Zoom vào vị trí XY của nhóm với dấu chỉ vị trí nếu làm được, không zoom ra toàn bộ polyline dài.
5. Khi bấm **Loại điểm**, bắt buộc hộp thoại tiếng Việt: **“Bạn có chắc chắn muốn loại điểm này khỏi mô hình?”**; hiển thị XY, Z, layer, handle, chỉ số đỉnh; có đúng hai lựa chọn **“Đồng ý” / “Không”**, mặc định **“Không”**, Esc tương đương “Không”. KHÔNG làm gì nếu người dùng chọn Không hoặc đóng hộp thoại.
6. Chỉ sau khi **Đồng ý** mới vô hiệu hóa **đúng đỉnh đã chọn** trong `SurfaceModel` (không vô hiệu hóa cả Polyline; không `ERASE`, `EXPLODE`, `DELETE` CAD gốc). Dùng API mô hình `SetVertexEnabled(entityId,vertexIndex,false)`; với Sửa Z dùng `OverrideVertexZ`. Phải đảm bảo lưu/khôi phục hiệu chỉnh theo DWG và `Restore`/Undo trong phạm vi mô hình; không làm mất khả năng khôi phục.
7. Sau sửa hoặc loại, **invalidate TIN và các kết quả liên quan**, tăng revision, quét lại tự động và cập nhật nhóm; không dùng cache cũ. Không dùng chỉ số đỉnh nguồn clip giả; kiểm soát chuyển DWG/layer giữa thao tác, tránh sửa nhầm.
8. Nếu hết nhóm `ΔZ > 3 m`, mới cho phép `Dựng TIN thử`. Nếu vẫn còn nhóm, UI cho biết số nhóm chưa xử lý, nút dựng không vượt kiểm tra.
9. Bảng **Điểm trùng tọa độ** cũ phục vụ TIN chuẩn cần tiếp tục hoạt động; không sửa ngưỡng `1e-4` của TIN chuẩn một cách lặng lẽ. Hai cơ chế có nhãn rõ.

### B3. CÁC CA KIỂM THỬ BẮT BUỘC ĐỐI VỚI TIN

- Cùng XY, Z = 120/122 -> trung bình 121 (chế độ thử).
- Cùng XY, Z = 100/100/102 -> trung bình 100,6666667; chứng minh không phải `(Zmax+Zmin)/2`.
- Cùng XY, Z = 0/94,862 -> `ΔZ=94,862` -> PHẢI MỞ BẢNG, KHÔNG DỰNG.
- Cùng XY, `ΔZ=3.000` -> phép thử cho phép; `3.000001` -> review (có chính sách epsilon rõ).
- Bảng nhiều nhóm, một nhóm có 3+ điểm; chọn từng điểm khác nhau phải loại đúng nguồn.
- **Không** trong hộp thoại xác nhận -> số điểm và revision KHÔNG thay đổi; **Đồng ý** -> chỉ một `Vertex` bị loại, CAD giữ nguyên, nhóm được quét lại.
- Sửa Z -> dữ liệu mô hình đổi, đỉnh CAD gốc không đổi, có thể khôi phục. Trường hợp quét đang chạy/đổi DWG/đổi layer, không làm sai đối tượng.
- Vùng chọn, Polylines 2D/3D, hàng trăm nghìn đến 1 triệu đỉnh, nguồn có nút giao cắt, nguồn có Z âm, tọa độ hệ VN-2000 lớn; khi thiếu dữ liệu báo no-data.
- So sánh *hình dạng TIN, cao độ* với dữ liệu HSMo, không chuyển việc sang so sánh thể tích trước khi hoàn tất bài toán TIN.

## C. BỘ FILE KIỂM THỬ/ĐỐI CHỨNG HSMo

Dùng các file trong cuộc hội thoại/Project nếu Work có quyền truy cập; KHÔNG tự chế số liệu:
- `Tinh kl SQ 2026.09.091.dxf` — DXF nguồn + đối tượng TIN tùy biến HSMo;
- `Tinh kl SQ 2026.09.09.dwg` — bản DWG HSMo;
- `Test HS.dxf` — đã có hệ mặt cắt HSMo **20 m/tuyến**;
- các DXF đã kiểm thử V1.0, bao gồm trường hợp `ht` là hiện trạng, `- nam4` là thiết kế, trong ranh `LO_TINHKL`, và các DXF vỉa 4 nếu có.
- HSMo tạo được TIN từ toàn bộ Point/2D Polyline/3D Polyline, nhưng định dạng `HDAI_TIN_MODEL_OBJ` trong DXF **chưa chứng minh** quy tắc chọn Z cụ thể. Không suy đoán kết quả HSMo = trung bình Z khi chưa có số liệu.

**Mục tiêu đối chứng đầu tiên:** hình học TIN thực, các cao độ xung đột 5–94,862 m và mặt cắt nội suy ở cùng vị trí. Dữ liệu mặt cắt có thể hỗ trợ xác nhận bề mặt ở tuyến lấy mẫu. Phần khối lượng HSMo là nhiệm vụ sau.

## D. KIỂM TOÁN V2 TOÀN BỘ SAU KHI ỔN ĐỊNH TIN

Giữ các chức năng và hoàn thiện đến mức sử dụng thực tế:
1. Đọc dữ liệu theo layer/chọn thủ công, quản lý X/Y/Z/loại/handle/layer, loại/khôi phục dữ liệu và cập nhật TIN; nạp nhiều trăm nghìn điểm không treo.
2. TIN HT/TK đủ phạm vi, xuất mặt TIN CAD `3DFACE`, View 3D/3DORBIT, Esc thoát/khôi phục Palette, layer, camera, không khóa AutoCAD; thử DWG reopen.
3. Hệ mặt cắt theo hướng/bước tuyến và giới hạn biên, bước điển hình 20 m; bố trí hình xuất CAD không chồng (khoảng hở layout độc lập với bước tuyến); nhãn Arial 3, trục X/Y, hiện trạng xanh, thiết kế đỏ theo baseline dự án, diện tích đào/đắp đúng.
4. Khối lượng theo mặt cắt và theo tầng, công thức diện tích đầu-cuối/thể tích, kiểm chứng ví dụ độc lập. KHÔNG dùng số liệu từ TIN thử nghiệm để tính khối lượng chính thức.
5. Tính khối lượng theo lưới ô vuông bất kỳ góc xoay, cắt chính xác giao TIN; cờ no-data/partial, xuất bình đồ lưới Ztn/Ztk và 6 sheet Excel, công thức liên kết; tổng phải bảo toàn khi đổi hướng lưới.
6. Quản lý nhiều vỉa than/quặng, lộ vỉa HT/TK, vùng Polygon khoáng sản trên MC, chỉnh Grip, kiểm tra vùng không chồng nhau và nằm trong phần đào, không tự lấy diện tích 0 nếu thiếu; tính m³/tấn theo thể trọng có nguồn và phương pháp nội suy được nêu.
7. Xuất Excel và AutoCAD TABLE editable, tổng hợp theo khoảng/tầng/vỉa, công thức Excel thật; Excel Arial 12, không tô nền, bảng đóng khung, logo IMSAT. Người phát triển cố định **Bùi Thế Nam; Điện thoại: 0967280686**.
8. UI Hài Hòa-like (không phải app độc lập), Ribbon + menu bar có lệnh thực sự chạy, bật/tắt Palette, không tự bật bảng mỗi lần mở AutoCAD, nút bo góc không mất chữ khi ở 1366×768, Full HD, DPI 100/125/150%.
9. Đảm bảo tất cả chức năng trên dùng chung DWG/project state, thao tác Save/Reload/Update thật, không giả lập dữ liệu hoặc các button không làm gì.

## E. BẢN QUYỀN, BỘ CÀI, TƯƠNG THÍCH

1. Dùng thử **15 ngày liên tục**, khóa chức năng phần mềm khi hết hạn nhưng KHÔNG khóa bản vẽ/dữ liệu; cảnh báo và trạng thái bản quyền trong Palette; kiểm tra rollback clock và ledger tamper.
2. License gắn máy có chữ ký RSA hợp lệ; License Manager quản trị cấp key ngoại tuyến, backup/restore private key bằng tệp mã hóa có mật khẩu. **Đang thiếu provision verifier PUBLIC key cho bản phát hành**; phải giải quyết thật, test ký–kích hoạt trên MÁY KHÁC; TUYỆT ĐỐI không đưa private key vào repo, Setup hoặc báo cáo. Nếu có bước cần thao tác trên máy quản trị IMSAT, ghi rõ yêu cầu người phụ trách thực hiện; không tự tạo "SuperKey" vô điều kiện.
3. Installer V2.0 riêng, có icon IMSAT trên Setup, giao diện, shortcut và branding Excel; hỗ trợ cài, nâng cấp từ V1, sửa chữa (nếu đã giao), gỡ cài đặt, giữ nguyên dữ liệu CAD/license, không xóa file người dùng; ký số khi có chứng thư phù hợp.
4. **AutoCAD 2023 64-bit là target nghiệm thu trước**. Yêu cầu trước đây hướng tới AutoCAD 2012+: phải rà API, mô-đun adapter, runtime/.NET và lập ma trận phiên bản; chỉ tuyên bố hỗ trợ một phiên bản nếu có build + smoke test trên phiên bản ấy. Không đưa nhãn 2012+ vào Setup V2 nếu chưa kiểm thử.
5. Bản QA có thể chạy thử nhưng không giả vờ là release chính thức; bản phát hành phải ghi đúng V2.0 và có SHA-256, changelog, nguồn build reproducible.

## F. TRÌNH TỰ WORK PHẢI LÀM (KHÔNG BỎ GIAI ĐOẠN)

**GATE 0 — ĐỌC/KIỂM TOÁN:** checkout từ PR #26, ghi SHA đầu vào; đọc tài liệu `docs/V2_QA_RELEASE_HANDOFF.md` và PlanCode này, tạo nhánh Work riêng, rà toàn bộ CI/PR phụ thuộc. CI nhánh TIN đã từng FAIL do test `test_tin_zmean_preview_static.py` chờ chuỗi gọi `SurfaceRegionClipper.Clip(` trong khi mã đổi sang `MeanZPreviewPreparer.Prepare(source, ..., tinRegion)`. **Kiểm tra hành vi và sửa test đúng sự thật, KHÔNG chèn code chết để làm test xanh.** Fix lỗi compile/runtime nếu có; ghi log.

**GATE 1 — TIN:** hoàn thiện chức năng B1–B3, đặc biệt bảng nhóm XY/chi tiết điểm/3 nút mỗi hàng, hộp thoại xác nhận có **Đồng ý / Không mặc định Không**, cấp độ nguồn và vùng clip. Chạy test C# thật trên `CoreRegression`, kiểm tra dữ liệu lớn, giữ TIN chính thức độc lập. Không merge nếu còn lỗi.

**GATE 2 — HOST AUTOCAD:** chạy trực tiếp trên Windows AutoCAD 2023 (nếu môi trường Work thực sự có), nạp hai layer trong file mỏ, thử dữ liệu SQ/DXF. Test thao tác người dùng với 1.293 nhóm, bản vẽ lớn, kiểm tra GUI/ổn định/Cancel/Zoom/Save. **Nếu không truy cập được AutoCAD host thực, GHI `NOT TESTED`, tạo bộ cài QA và checklist thao tác cụ thể để người dùng kiểm thử; không ghi PASS suy đoán.**

**GATE 3 — CHỨC NĂNG CÒN LẠI:** test hồi quy V1+V2 gồm mặt cắt, grid, than/quặng, Excel, CAD TABLE, ribbon/menu, 3D, dữ liệu DWG. Sửa cho đến khi tất cả core/build/unit/integration tests đạt.

**GATE 4 — LICENSE/SETUP:** build release candidate có icon, dùng thử/activation thật, test cài/gỡ/upgrade/giữ dữ liệu, check antivirus/Windows; public verifier + quyền quản trị an toàn. Không bàn giao EXE như bản chính thức trước khi Gate 4 đạt.

**GATE 5 — NGHIỆM THU & PHÁT HÀNH:** lập báo cáo `docs/V2_0_QA_REPORT.md` và `docs/V2_0_RELEASE_GATES.md`, checklists đầy đủ, ghi SHA build, kết quả test, hình/log AutoCAD, đối chứng mỏ SQ và những giới hạn còn lại. Đính kèm EXE Setup, ZIP nguồn/README, SHA256, hướng dẫn sử dụng, hướng dẫn cài/gỡ, changelog. Chỉ sau khi PASS tất cả release gate mới tạo tag/release `v2.0.0`.

## G. TIÊU CHÍ NGHIỆM THU RÕ RÀNG

- **Không còn nút chết**, menu bar và Ribbon hoạt động, GUI không mất chữ.
- Dựng TIN từ **đối tượng thực trên layer nguồn**; không yêu cầu loại tay hàng nghìn trùng XY nhỏ hơn hoặc bằng 3 m trong chế độ thử. Các nhóm vượt 3 m được nhóm và kiểm tra có ý thức.
- Mỗi điểm được sửa/loại là **đúng đỉnh** thuộc mô hình, có xác nhận khi loại, có Zoom và khả năng khôi phục. Không xóa DWG gốc.
- Nhóm >3 m chưa xử lý => **bị chặn dựng TIN thử**; xử lý xong => tạo lưới trên layer `TEST_ONLY`, không đi vào khối lượng chính thức.
- TIN chính thức/cặp TIN phải phản ánh breakline và bảo toàn số đỉnh/cạnh hợp lệ, không rơi dữ liệu âm thầm.
- Thuật toán số liệu đúng với bài test độc lập; mọi no-data/partial/vi phạm địa chất báo rõ.
- PASS CI Windows, C# CoreRegression, QA Python, build DLL/native, đóng gói Setup, giấy phép admin self-test. **CI PASS không thay thế runtime AutoCAD.**
- Máy Windows AutoCAD thực tế cài/mở/hoạt động/xuất/tắt/gỡ không treo, không phá DWG.
- Mọi dòng nghiệm thu chỉ có **PASS / FAIL / NOT TESTED**, có bằng chứng hoặc lý do. Những item NOT TESTED do thiếu host phải được người dùng xác nhận trước khi release.

## H. ĐẦU RA BẮT BUỘC CỦA WORK

1. Code đã sửa/chạy và commit theo từng giai đoạn trên nhánh Work, PR riêng về nhánh QA; không merge/force-push V1.
2. Ứng dụng và installer V2.0 hoàn chỉnh (hoặc QA candidate rõ lý do chưa release).
3. Bộ test C# và CI Windows xanh tại HEAD, kèm link Actions; test không được chỉ quét chuỗi mà phải có regression logic.
4. Chứng cứ test DXF/DWG và HSMo, đặc biệt 1.293 nhóm và thao tác bảng trùng XY.
5. Báo cáo tổng hợp thực tế `V2_0_QA_REPORT.md`, ma trận `V2_0_RELEASE_GATES.md` và **danh sách lỗi + tình trạng khắc phục**.
6. Bộ hướng dẫn người dùng: cài Setup, mở IMSAT Volume, chọn layer HT/TK, dựng TIN thường và TIN thử, tìm/sửa/loại điểm >3 m, xuất CSV, tạo mặt cắt và Excel.
7. Trường hợp thật sự thiếu môi trường AutoCAD hoặc người cấp khóa license: giao chính xác file cài, bước thử, biểu mẫu kết quả và điều kiện cần người dùng bổ sung, rồi tiếp tục sửa ngay khi có chứng cứ.

**Lệnh triển khai:** Chủ động thực hiện toàn bộ, không chỉ xuất ý tưởng. Không hỏi đi hỏi lại các quyết định đã chốt. Không đánh dấu HOÀN THÀNH V2.0 khi còn hard gate chưa nghiệm thu; không tự khẳng định đã tái tạo được thuật toán nội suy HSMo chỉ từ các đối tượng proxy.
