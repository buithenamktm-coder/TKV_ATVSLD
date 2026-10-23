# PLANCODE — WORK / IMSAT VOLUME V2.0
## Hoàn thiện thực chất AutoCAD 2012–2026 và rà soát tổng thể 5 vòng

**Mã công việc:** IMSAT-VOLUME-V2-MULTICAD-FINAL-20261013  
**Chế độ:** WORK — sửa mã nguồn, biên dịch, kiểm thử, đóng gói, cung cấp bằng chứng; KHÔNG dừng ở đề xuất/README/demo.  
**Dự án duy nhất:** `buithenamktm-coder/TKV_ATVSLD`  
**Nhánh kế thừa:** `miningvolume-v2-chatgpt-p0-continuation`  
**Mốc tham chiếu khi lập nhiệm vụ:** `25e89317c99e1e9a6f09aa0828a518db2322e399` (luôn kiểm tra HEAD thực tế trước khi làm; **không reset** các commit mới).  
**Thư mục:** `tools/MiningVolume_HSNext/v0.9/`  
**Tên hiển thị:** **IMSAT VOLUME**; tác giả cố định **Bùi Thế Nam**, ĐT/Zalo **0967280686**.  
**Phạm vi hỗ trợ mong muốn:** **AutoCAD bản đầy đủ 64-bit từ 2012 đến 2026**; AutoCAD LT không nằm trong cam kết nếu thiếu API chạy add-in.  
**Nguyên tắc nghiệm thu:** **BUILD PASS ≠ RUNTIME PASS ≠ RELEASE PASS.** Không tạo hoặc gửi Setup được quảng bá “2012–2026” khi vẫn thiếu kiểm thử thực tế trên các đời máy được công bố hỗ trợ.

---

## A. LỆNH THỰC HIỆN CHO WORK

Bạn là kỹ sư trưởng chịu trách nhiệm hoàn thiện IMSAT VOLUME V2.0 theo mã nguồn hiện có. **Tự thực hiện liên tục, có kiểm soát, cho đến khi các đầu ra đạt gate nghiệm thu; không chỉ lập kế hoạch.** Làm trực tiếp trên nhánh trên (có thể lập nhánh kỹ thuật từ HEAD, nhưng phải giữ lịch sử và đưa kết quả hợp nhất về nhánh dự án đúng cách). Trước khi sửa phải đọc project, issue/log CI, các ảnh lỗi và file DWG/DXF người dùng đã cung cấp trong cuộc trao đổi nếu còn truy cập được. Không suy đoán rằng ảnh chụp cung cấp đủ dữ liệu hình học.

**Thứ tự bắt buộc:** kiểm kê nguồn → khóa đường cơ sở AutoCAD 2023 → tách kiến trúc tương thích → xử lý 2012–2026 → bộ cài đa đời CAD → nghiệm thu nghiệp vụ AutoCAD thực → **5 vòng rà soát độc lập sau khi sửa xong** → phát hành/báo cáo tình trạng chính xác.

Không xóa chức năng đã hoạt động; không thiết kế lại UI đã chốt; không tạo sản phẩm phần mềm thứ hai; không biến giải pháp thành bộ LISP thao tác thủ công; không tự thêm chức năng BIM/ROAD ngoài phạm vi. Giữ cách vận hành add-in bên trong AutoCAD, menu bar và Ribbon IMSAT, Palette tiếng Việt; dùng chung lõi toán học và dữ liệu dự án. Không thay đổi `FILEDIA`, `CMDDIA`, phím `Ctrl+Shift+S` của AutoCAD.

## B. ĐƯỜNG CƠ SỞ VÀ CÁC LỖI CẦN RÀ SOÁT

1. **AutoCAD 2023** đang có bản QA, chạy các kiểm thử CI Python/C# trước đây; **chưa thể suy ra đạt host-runtime trên DWG thực tế**. Không được làm hỏng build 2023, quá trình tự nạp, Ribbon, menu, tạo TIN, mặt cắt, khối lượng, Excel và vẽ lưới.
2. `compat/IMSAT.CadCompatibility.props`, `compat/build_compat.ps1`, `compat/target_matrix.json`, workflow compile-only hiện đã có cấu hình thử nghiệm 2015–2026; **2012–2014 chưa có adapter hoàn chỉnh**. Các bản 2015/2016 lỗi API `Array.Empty<T>()` trên Framework cũ; đối chiếu log CI trước khi sửa. Không dùng bản biên dịch thử làm bản cài chính thức.
3. Installer hiện tập trung vào `MiningVolume2023.bundle`, `R24.2` và nhận diện AutoCAD 2023; cần nâng cấp kiến trúc đa đời, không mở `SeriesMax` để nạp DLL sai thế hệ.
4. Từ vòng kiểm thử thực địa: TIN trùng XY khác Z/mặt đứng; chọn đường bao khác vùng phủ TIN; tính lưới >5.000 ô; các ô báo thiếu TIN/khối lượng 0 giả; vùng thực có đào và đắp; 34 tuyến MC và tên chồng lên nhau; trục mặt cắt sai do lý trình tọa độ mỏ lớn; dải mức cũ `0 → +100` không giao cao độ TIN trên +300; khoảng MC bị loại toàn bộ vì mặt đứng; bảng CAD/Excel thiếu tên cột hoặc công thức liên kết; nút bị chồng chữ, cắt chữ; logo IMSAT và nhận diện; xuất Excel theo mẫu đã duyệt.
5. File tham chiếu: `KL mb.dwg`/`KL mb.dxf` và bảng CSV đối chứng 19 ô nếu còn thực sự có trong các tệp đầu vào. **Xác minh lại số liệu bằng tính độc lập, không tự gán các con số được nhắc ở chat là kết quả chuẩn nếu thiếu file nguồn và phương pháp.** Handle `233B`/`2343` chỉ dùng trong kiểm thử đúng file; không hardcode handle vào phần mềm.

## C. PHÂN KỲ THỰC HIỆN (THỰC THI, KHÔNG DỪNG Ở ĐỀ CƯƠNG)

### P0 — Kiểm kê / bảo toàn trạng thái

- Checkout nhánh dự án; lấy HEAD, kiểm tra trạng thái Git, danh mục công việc đã hoàn tất, trạng thái các workflow, lỗi mới nhất, quyền truy cập tệp đính kèm. Tạo `BASELINE.md` ghi commit, bản build 2023, hashes file nguồn, danh sách module, bảng tồn tại/thiếu sót.
- Tạo bảng 15 năm AutoCAD × runtime × thư viện API × output DLL × cơ chế nạp × host test × bằng chứng × PASS/FAIL/BLOCKED. Phân biệt **compile**, **load runtime**, **chức năng**, **setup/gỡ cài**.
- Lưu benchmark đối chứng TIN/mặt cắt/lưới/Excel hiện tại; chạy đầy đủ test có sẵn **trước khi sửa**; đánh dấu lỗi cũ và lỗi mới. Cấm xóa test lỗi để chuyển CI sang xanh.

**Gate P0:** có baseline và CI baseline; mọi thay đổi truy vết được về commit; không làm mất mã và dữ liệu người dùng.

### P1 — Lõi toán học độc lập CAD và chuẩn dữ liệu chung

- Bảo toàn một lõi tính TIN, hình học tiết diện, khối lượng theo 3 công thức đang chốt, lưới ô vuông, trữ lượng/khối lượng trong phạm vi hiện hành, bảng kết quả và export. Không viết lại thuật toán khác cho từng CAD.
- Dời những phần phụ thuộc `Autodesk.*`, WinForms/WPF, thao tác UI, đường dẫn vào adapter; lớp lõi có test thuần không cần AutoCAD. Chuẩn hóa hợp đồng dữ liệu qua phiên bản (loại đối tượng, X/Y/Z, polyline, line, point, contour, TIN/3DFACE, mặt cắt, ô lưới, layer, cảnh báo, thống kê).
- Hỗ trợ TIN có cùng XY khác Z đúng hình học: mặt đứng 3D chiếu bằng 0 không tự làm mất toàn bộ diện tích lưới; nhưng hai tấm tam giác 2,5D chồng phủ XY thực sự phải được phát hiện, không cộng hai lần. Không xóa điểm nguồn, không lấy trung bình Z tùy tiện.
- Đối chứng tích phân riêng đào/đắp trên mỗi ô, ô vượt mép ranh, lỗ thủng ranh, lưới xoay, sai số tọa độ mỏ lớn, vùng phủ khác nhau và kiểm tra số 0/ô chưa tính. Tối ưu xây TIN và vẽ lưới nhiều ô; phân đoạn tiến trình và hủy thực sự; không đóng băng UI.

**Gate P1:** test hình học độc lập pass trên mô hình tổng hợp lẫn dữ liệu mẫu thực, đúng đơn vị và sai số được quy định rõ; không bỏ qua diện tích/khối lượng chưa xác nhận.

### P2 — Tích hợp AutoCAD theo từng thế hệ

- **2012**: adapter riêng tương thích CLR/.NET 3.5 và API AutoCAD R18.2, không tham chiếu API hoặc assembly hiện đại không tồn tại; có thể dùng **worker tính toán ngoài tiến trình** dùng chung lõi hiện hành qua IPC định dạng versioned, nhưng Ribbon/Menu/Palette, chọn đối tượng và vẽ kết quả phải vận hành **trong AutoCAD**. Worker tự cài, tự khởi động, quản lý lỗi và hủy; không biến thành phần mềm vẽ độc lập.
- **2013–2014**: adapter API R19.0/R19.1, CLR .NET 4.0; có thể kế thừa hợp đồng IPC nếu bảo đảm an toàn và tốc độ. Kiểm tra khả năng autoload/Ribbon theo các đời này, không giả định `AcCoreMgd` hay API chỉ có từ đời sau.
- **2015–2016**: sửa tận gốc build .NET 4.5; xử lý API không tồn tại như `Array.Empty<T>()` bằng tương thích/conditional implementation mà không làm sai nghiệp vụ, tránh nâng ngầm lên .NET 4.8.
- **2017–2018**, **2019–2020**, **2021–2024**: compile riêng với API Autodesk đúng release + framework thích hợp; vá khác biệt chữ ký API, Ribbon, DB transaction, document lock, graphics, Selection/Polyline, bảng native, geometry. Giữ các lệnh nghiệp vụ như nhau.
- **2025–2026**: compile và kiểm tra load cho biến thể runtime .NET 8/.NET 10 theo bản cập nhật thực tế, **tra cứu lại tài liệu Autodesk chính thức trước khi lựa chọn bản vá/runtime**; không suy từ năm AutoCAD rằng chỉ có một CLR. Tránh xung đột Autodesk assemblies.
- Mỗi phiên bản có flavor, API reference, output path, unit test, host loader smoke test và log riêng. Không mở rộng chung `RuntimeRequirements` cho DLL 2023; không dùng một DLL không tương thích cho tất cả.

**Gate P2:** 15 đời từ 2012 đến 2026 có đúng đường biên dịch/nạp được chứng minh; nếu thiếu AutoCAD thật, phân loại riêng `COMPILE_PASS / HOST_TEST_BLOCKED` thay vì nhận là hỗ trợ hoàn chỉnh.

### P3 — Bộ cài Setup đa phiên bản và tự nạp

- Xây **một Setup IMSAT VOLUME V2.0** có biểu tượng IMSAT, uninstall, nâng cấp/repair/rollback, tự phát hiện các phiên bản AutoCAD đã cài (nhiều bản cùng máy), kiến trúc 64-bit, patch/runtime. Hiển thị danh sách tìm thấy và trạng thái tương thích; không cài DLL vào phiên bản không xác định.
- Một bundle/ComponentEntry được khai báo đúng `SeriesMin/SeriesMax`, đường module, điều kiện CLR và version. Trường hợp runtime cập nhật không thể phân biệt chắc chắn thì **dừng nạp và báo không hỗ trợ**; không thử nạp nhầm để xem có crash không.
- Tự nạp khi mở CAD; có menu/Ribbon IMSAT VOLUME và mục bật/tắt Palette, không hiện Palette cưỡng bức mọi lần mở CAD. Một nút mở/đóng hoạt động; bảo toàn dự án và dữ liệu khi gỡ cài.
- Setup không đóng gói DLL Autodesk có bản quyền, không chép đè thư viện CAD; không xóa DWG/DXF/Excel/Project/License; có install manifest và SHA-256, log chính xác theo version. Cài nhiều đời cùng máy không gây đụng tên assembly hay menu.
- Không làm thay đổi `FILEDIA`, `CMDDIA`, AutoCAD shortcuts, các lệnh Save/SaveAs của người dùng.

**Gate P3:** cài/gỡ/nâng cấp/rollback/multi-CAD coexistence pass có log và ảnh đối chứng, không để AutoCAD treo khi mở; cài vào đời CAD chưa kiểm chứng phải báo chưa được hỗ trợ.

### P4 — Nghiệp vụ và giao diện end-to-end

**Quy trình chuẩn phải chạy được:**
1. Tạo/chọn dự án; chọn layer hiện trạng/thiết kế, Point/Line/Polyline/đồng mức, hiển thị bảng dữ liệu nguồn X/Y/Z/layer/loại đối tượng, lưu cập nhật dữ liệu.
2. Tạo TIN **từng lần** (không nút “Tạo 2 TIN”), có thông báo điểm trùng XY/Z nhưng không bắt loại từng điểm; hoạt động trên bộ lớn, không treo; TIN 3D/đường biên đúng.
3. Chọn ranh kín, hướng tuyến (2 điểm), khoảng cách, mức chia tầng, thang tỷ lệ tùy chỉnh; tạo hệ mặt cắt + **Vẽ mặt cắt nhanh** để chọn tuyến riêng trên TIN.
4. Vẽ MC bình đồ rõ ràng: nhãn Arial 3, không chồng với đường; hiện trạng/thiết kế đúng quy ước màu; tọa độ lý trình lớn chuyển về hệ trục MC cục bộ; mặt cắt tách đủ khoảng cách, không co cụm một cột.
5. Chỉnh Grip đường MC đã xuất → **Cập nhật mặt cắt** → tính lại F đào/F đắp, phân tầng, khối lượng; 3 công thức đã chốt đúng điều kiện áp dụng. Không loại 23 khoảng chỉ vì mặt đứng chiếu bằng 0; trường hợp thực thiếu cao độ phải cảnh báo đúng đoạn.
6. Dải mức `Từ mức / Đến mức` hợp lý với Z TIN; phát hiện cấu hình lệch hẳn dữ liệu (ví dụ 0–+100 khi TIN +330 đến +380) và sửa có thông báo/khả năng chỉnh; không cho kết quả 0 giả.
7. Lưới ô vuông: 10×10, 20×20, xoay, mép ranh, lưới trên 5.000 ô, kiểm thử 10.000/50.000/100.000 ô theo điều kiện máy và cơ chế render, có tiến trình/hủy. Tách lỗi thiếu TIN hiện trạng/thiết kế, vùng nhiều Z, no-data và khối lượng bằng 0 thật. Chỉ ghi 0 cho ô thực sự tính được bằng 0.
8. Xuất CAD TABLE có tên cột đầy đủ, đơn vị, số thứ tự, khối lượng đào/đắp/tổng theo mức; ô merge không chồng chữ; font Arial; bố trí theo mẫu người dùng đã gửi.
9. Xuất Excel font Arial, đóng khung, tiêu đề cột đầy đủ không cắt, công thức liên kết giữa sheet, số liệu có thể trace, logo IMSAT đúng điều kiện đơn vị thuộc TKV; không tự chèn logo với đơn vị ngoài TKV. Tên tác giả/điện thoại cố định.
10. UI Palette ở tối thiểu 1366×768 và Full HD: label, button không cắt chữ/chồng nhau, khoảng cách hài hòa; tên nút không viết hoa toàn bộ; phần công thức Equation Professional dễ đọc; nút phóng to/thu nhỏ và scroll đúng.

**Gate P4:** có bộ test sử dụng file thực (ưu tiên `KL mb.dxf`/DWG), số liệu độc lập và ảnh chụp CAD/Excel; tiêu chí đúng/sai công khai. Không báo PASS nếu chỉ kiểm thử unit mà không mở AutoCAD.

### P5 — Năm vòng rà soát tổng thể SAU KHI HOÀN THIỆN CHỨC NĂNG

Thực hiện **5 vòng độc lập**, mỗi vòng có checklist riêng, issue ID, severity P0/P1/P2/P3, bằng chứng trước/sau, commit sửa, test chống tái phát; hết vòng nào phải sửa hết lỗi P0/P1 rồi chạy lại vòng đó. Không đổi nhãn lỗi để hợp thức hóa PASS.

**Vòng 1 — Kiến trúc / an toàn dữ liệu / tính tương thích:** kiểm tra dependency graph, DLL theo release/runtime, API 2012–2026, một lõi nghiệp vụ, thread/transaction/cancellation, nguồn Z, cập nhật/lưu/gỡ cài không xóa dữ liệu.

**Vòng 2 — TIN, mặt cắt và toán học:** dữ liệu có đường đồng mức/point/line/polyline, điểm trùng XY khác Z, 3D vertical curtain, TIN chồng mặt phẳng, khối lượng đào–đắp, 3 công thức, dải cao độ, tầng, Grip editing, dữ liệu thực và đối chứng độc lập.

**Vòng 3 — Lưới ô vuông / hiệu năng / tính ổn định:** trên 5.000 ô và stress lớn, quay góc, biên không đều, hình học nhỏ/hẹp, đúng vùng phủ hai TIN, dữ liệu lớn, leak/treo/undo/retry/cancel, số liệu và độ nhạy số học.

**Vòng 4 — Giao diện / CAD TABLE / Excel / bản vẽ:** Palette 1366×768/Full HD, nhãn/tỷ lệ/nút, vẽ hệ MC và MC nhanh, font/màu, CAD TABLE mẫu, XLSX đầy đủ tên cột/formula/logo theo loại đơn vị, metadata tác giả, save/export không tác động FILEDIA.

**Vòng 5 — Cài đặt / tương thích 15 đời / nghiệm thu phát hành:** host load + workflow chính + install/uninstall/rollback/coexistence trên từng version/patch; antivirus/manifest/hash, sạch cảnh báo bắt buộc, reproducible build và đúng chứng cứ.

Sau khi sửa phát sinh ở vòng 2–5, bắt buộc **re-run bộ smoke test lõi + full regression AutoCAD 2023** để phát hiện lỗi hồi quy. Nếu một vòng còn lỗi P0/P1: `FAIL`, **không tự ý tuyên bố 100%**.

## D. GATE PHÁT HÀNH (CỨNG)

Tách **3 trạng thái** cho từng release:
- `COMPILE_PASS`: build được với SDK đúng release.
- `HOST_RUNTIME_PASS`: AutoCAD thực nạp được DLL, mở Palette/Ribbon/Menu, thao tác cơ bản không lỗi.
- `FUNCTIONAL_RELEASE_PASS`: đầy đủ nghiệp vụ end-to-end, bộ cài/gỡ và 5 vòng kiểm toán đạt.

**Chỉ ghi “IMSAT VOLUME V2.0 hỗ trợ AutoCAD 2012–2026” khi TẤT CẢ các năm 2012…2026 có `FUNCTIONAL_RELEASE_PASS`.** Nếu thiếu bộ cài CAD bản quyền/Windows máy thử hoặc file DWG nguồn: yêu cầu đúng tài nguyên cần thiết, ghi `BLOCKED` và vẫn hoàn thành phần kỹ thuật có thể làm; không bịa ảnh, giờ chạy, kết quả thử, đường dẫn setup hoặc phần trăm nghiệm thu.

Bắt buộc kiểm tra định dạng dải năm / các gói cập nhật đặc biệt 2025–2026 theo thông tin Autodesk mới nhất trước khi gắn series; phải kiểm thử đúng minor update tương ứng.

## E. BẰNG CHỨNG VÀ BÀN GIAO

Trong repository và một thư mục giao nộp rõ ràng, Work phải tạo:

1. `IMSAT_V2_RELEASE_MATRIX.xlsx` (hoặc CSV + Excel): 15 đời CAD, bộ .NET/API, flavor, commit, compile, host test, tính TIN, MC, lưới, CAD, Excel, installer, screenshot, status, issue link.
2. `IMSAT_V2_5_ROUND_AUDIT.md` và `ISSUES_CLOSED_OPEN.xlsx`: 5 vòng, lỗi, gốc lỗi, thay đổi, kết quả trước/sau, mã test, log và blocker.
3. `IMSAT_V2_MATH_VALIDATION.xlsx`: dữ liệu nguồn đối chứng, kiểm tra diện tích đào/đắp và thể tích theo MC/lưới/tầng, sai số tuyệt đối/tương đối, công thức tính.
4. `IMSAT_V2_INSTALLER_MANIFEST.json` và SHA-256 của từng artifact; folder install/uninstall/rollback test; installer cuối nếu đủ gate, nếu chưa đủ thì chỉ có ứng viên QA theo từng host đã được kiểm chứng.
5. `IMSAT_V2_USER_GUIDE.pdf` hoặc Markdown: thao tác thực tế, chức năng nhanh, sử dụng với AutoCAD từng đời, giới hạn được chứng minh, phương án khôi phục khi lỗi.
6. `IMSAT_V2_FINAL_ACCEPTANCE.md`: kết luận **PASS/FAIL/BLOCKED** trung thực, mức hoàn thiện theo từng release, không tổng kết một con số 100% khi thiếu host runtime.

Không gửi “EXE” hoặc Setup nếu bản đó chưa chạy đủ cổng kiểm thử. Không tạo link tải không tồn tại. Cùng một commit phải là nguồn cho binary, manifest, bảng test và báo cáo cuối.

## F. CÁCH BÁO CÁO TIẾN ĐỘ CỦA WORK

Sau mỗi giai đoạn P0–P5 báo ngắn bằng bảng **đã làm – test – lỗi còn lại – commit – bước tiếp**. Báo ngay khi gặp P0/P1 hoặc thiếu quyền/tool/máy AutoCAD khiến không thể vượt gate. Mỗi báo cáo tiến độ phải nêu rõ `COMPILE`, `HOST`, `FUNCTIONAL` của nhóm CAD liên quan. Không dừng để hỏi người dùng khi có thể tự sửa/kiểm tra được; chỉ hỏi đúng tài nguyên bắt buộc chưa có (ví dụ quyền chạy một máy AutoCAD 2012 thật).

### KẾT QUẢ CUỐI CẦN GIAO

**Mục tiêu**: một IMSAT VOLUME V2.0 chất lượng sử dụng thực tế với các bản CAD 2012–2026, một bộ cài có nhận diện phiên bản chính xác, và báo cáo 5 vòng rà soát tổng thể. **Chưa có host acceptance thì công việc phần đó còn mở**; tiếp tục phần có thể kiểm chứng và không đưa ra tuyên bố sai.

**BẮT ĐẦU THỰC HIỆN NGAY: P0 → P1 → P2 → P3 → P4 → P5, với bằng chứng và gate như trên.**
