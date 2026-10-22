# PLANCODE — IMSAT SLOP V1.0 | KẾ THỪA MÃ NGUỒN V6.2, HOÀN THIỆN PHẦN MỀM KIỂM TOÁN ỔN ĐỊNH BỜ MỎ

**Ngày chốt:** 13/10/2026  
**Tên thương mại theo Chủ sản phẩm:** IMSAT SLOP (giữ đúng tên, không tự đổi tên thành SLOPE)  
**Repository:** `buithenamktm-coder/TKV_ATVSLD`  
**Nhánh bắt buộc:** `imsat-slop-v1.0-work`  
**Thư mục sản phẩm:** `products/imsat-slop/`  
**Bộ nguồn kế thừa ưu tiên:** `IMSAT_Slope_Stability_V6_2_CAD_Computation_Fixed_CMD_Builder.zip`  
**Bản đối chiếu lịch sử:** `IMSAT_Slope_Stability_V6_0_Professional_Complete_Full.zip`, `IMSAT_Slope_Stability_V5_4_1_Startup_Fixed.zip`.

## MỆNH LỆNH CHUNG CHO WORK

**Tuyệt đối không tạo project mới, không viết lại từ đầu, không xóa chức năng đã có để thay bằng chức năng giả.** Khôi phục toàn bộ nguồn V6.2 vào `products/imsat-slop/` (loại bỏ một lớp thư mục chứa ZIP), giữ lịch sử và điều chỉnh tối thiểu cần thiết. Đối chiếu V6.0/V5.4.1 để lấy lại tài nguyên/icon, tài liệu, kiểm thử hoặc chức năng chưa được chuyển tiếp. Mọi thay đổi phải trên nhánh đã nêu, không thay đổi `main` hoặc các dự án IMSAT VOLUME/ROAD/HRM.

Rocscience Slide2 chỉ là **chuẩn tham chiếu chức năng, workflow và kết quả số**. Tự lập trình các thuật toán theo tài liệu học thuật/đặc tả công khai và kiểm thử độc lập; không sao chép mã, giao diện nguyên trạng, logo, tài sản hay dữ liệu bản quyền; không giải mã/reverse engineer ứng dụng thương mại. IMSAT SLOP phải có thiết kế riêng, nhãn tiếng Việt, đơn vị kỹ thuật và chất lượng nghiệm thu tương đương theo phạm vi đã chứng minh. Không mô tả '100% tương đương Slide2' nếu chưa có bằng chứng.

## P0 — PHỤC HỒI BẢN GỐC LÊN GITHUB (BLOCKING)

1. Trước khi phát triển tính năng, kiểm tra tất cả file ZIP gốc và đối chiếu `SHA256`, `README`, `KNOWN_LIMITATIONS`, `tests/`, `app/`, scripts builder. Gói V6.2 là **source of truth**, không được coi là chỉ có mô tả/chương trình mẫu.
2. Giải nén V6.2 vào `products/imsat-slop/` trên nhánh `imsat-slop-v1.0-work`. Không lồng thư mục `IMSAT_Slope_Stability_V6_2_CAD_Computation_Fix/` thừa. Giữ nguyên `app/core`, `app/algorithms`, `app/io`, `app/ui`, `app/reports`, `main.py`, tests, assets, builder, example và manifest cần thiết.
3. Lưu nguyên bản ZIP và thông tin phiên bản/checksum trong hồ sơ phục hồi; không commit vào repository public dữ liệu mỏ khách hàng, hồ sơ Xóm Đẩu chưa được phê duyệt công khai, secrets, mật khẩu, EXE, thư mục build/dist/venv, kết quả lớn có bản quyền. Chỉ đưa bộ ví dụ `.gsz` lên public nếu xác nhận quyền phân phối; nếu chưa, lưu ở kho kiểm thử riêng và lưu metadata SHA.
4. Rà soát khác biệt so với V6.0/V5.4.1 để hồi phục chức năng đã có nhưng bị lược bỏ. Ghi vào `LEGACY_FUNCTION_INVENTORY.md` (Implemented/Partial/Failed/Missing) và `SOURCE_PROVENANCE.md`; không thay đổi giao diện được chốt vô cớ.
5. Kiểm tra nhập `main.py`, `compileall`, unit tests, smoke tests nguồn và GUI, chạy ít nhất hai mô hình mẫu không bí mật; chụp ảnh giao diện thực. Thiếu thư viện hoặc môi trường Windows phải ghi BLOCKED, không đánh dấu PASS.
6. Commit P0 trên nhánh; tạo checkpoint có thể quay lại trước P1. **Không được bỏ P0 rồi bắt đầu code mới**.

## P1 — KIỂM KÊ, SỬA LỖI V6.2 VÀ KHÓA KIẾN TRÚC

- Kiến trúc giữ Python/PySide6, mô hình dự án `.issv6` với migration không mất dữ liệu; hỗ trợ mở các dự án cũ nếu có cơ sở kiểm thử. UI Windows desktop IMSAT đã xây dựng: menu/sidebar, thanh công cụ, vùng mô hình 2D, bảng thuộc tính, thanh trạng thái, bảng kết quả; không đổi cấu trúc tổng thể nếu không có yêu cầu rõ ràng.
- Kiểm kê tình trạng thực tế: CAD closed polyline tạo Region+địa hình; gán vật liệu; 2D canvas; vật liệu nhiều vùng; Entry/Exit; Bishop/Janbu/Fellenius/Morgenstern–Price; nước/tải/động đất; quy trình giải tiến trình riêng; exporter; project save/load; diagnostics.
- Khắc phục triệt để: `Geometry (0 Region)`, polyline kín hở và đa đường, sai đơn vị, hình học tự giao/trùng biên, không đọc đúng địa tầng, màn hình chỉ nhìn 0–100, solver treo, FS NaN/0 giả, rò rỉ worker, xử lý ngoại lệ, lỗi lưu/mở, kiểm toán thông số.
- **Quy tắc:** gán tạm Vật liệu 1 là tiện ích khởi tạo, không được phép coi là cấu hình địa chất đã nghiệm thu. Chặn xuất kết luận kỹ thuật khi vật liệu chưa xác nhận, hoặc người dùng chưa duyệt giả thiết.
- Nhật ký đầy đủ: đầu vào, phiên bản thuật toán, phương pháp, tham số tìm kiếm, số lát, sai số hội tụ, miền tìm kiếm, mặt trượt trượt tới hạn, nguyên nhân loại từng ứng viên, hash đầu vào, dấu thời gian.
- Hệ thống trạng thái READY / IN_DEVELOPMENT / WAITING_FORMULA / WAITING_INPUT / NOT_APPLICABLE / DISABLED; cấm nút không làm gì hoặc tạo kết quả mặc định sai.

## P2 — HÌNH HỌC VÀ MÔ HÌNH ĐỊA CHẤT

- Import DXF trực tiếp, DWG qua bộ chuyển đổi hợp lệ hoặc adapter khi sẵn có; đọc tọa độ CAD thật và lớp; import mặt cắt ASCII/CSV X-Y và project cũ `.issv6`, `.issp` (khi có mẫu); GeoStudio GSZ chỉ trong phạm vi tương thích được chứng minh.
- Dựng/sửa terrain, Region địa tầng, tiếp xúc đá, đới phong hóa, đới dập vỡ, karst/hang hốc (nếu có dữ liệu), đới yếu và đứt gãy; khép vùng, cắt/gộp vùng, tô vật liệu, Undo/Redo, snap, pan/zoom, ruler, lưới tọa độ, đo góc/chiều dài.
- Thiết kế hiện trạng, thiết kế trung gian, bờ kết thúc là các **kịch bản độc lập**; không nội suy một mặt cắt hiện trạng thành một mặt cắt bờ kết thúc khác vị trí. Cho phép nhiều tuyến trong cùng một dự án và quản lý vị trí trên bình đồ.
- Mô hình vật liệu: Mohr–Coulomb (γ, γsat, c', φ'); Generalized Hoek–Brown (σci, mi, GSI, D và mb,s,a), tham số mỏ và xuất xứ; mô hình mặt yếu/anisotropy theo khả năng phương pháp. Quy đổi HB sang c–φ tương đương chỉ khi có dải ứng suất, thuật toán và cảnh báo sai số, không tráo mặc định c, φ của mẫu đá nguyên thành khối đá.
- Bộ dựng mặt cắt đọc từ IMSAT VOLUME/TIN/CAD qua trao đổi dữ liệu, nhưng IMSAT SLOP chạy độc lập nếu không có AutoCAD; không bắt buộc phải cài AutoCAD để tính ổn định.

## P3 — HỆ PHƯƠNG PHÁP TÍNH/THUẬT TOÁN

- Giữ và kiểm chứng Fellenius (Ordinary Method of Slices), Bishop Simplified, Janbu Simplified, Morgenstern–Price; triển khai/kiểm chứng Spencer, Janbu Generalized và các phương pháp bổ sung khi có tài liệu và benchmark hợp lệ.
- Mỗi phương pháp phải ghi rõ giả thiết, loại mặt trượt, xử lý nước, tải, động đất, nội lực giữa lát, phương trình lực/mô men, công thức kiểm soát và tiêu chí hội tụ. MP/Spencer phải kiểm tra đồng thời cân bằng lực và mômen (không mặc định đồng nhất với Bishop nếu không hội tụ).
- Tìm mặt trượt tròn và không tròn: Entry–Exit, Grid–Radius, auto locate/optimization, mặt trượt xác định do kỹ sư nhập; mở rộng tìm kiếm mặt trượt không tròn bằng thuật toán đủ dữ liệu kiểm thử. Giới hạn vùng tìm kiếm minh bạch; không tuyên bố FS nhỏ nhất toàn cục nếu chỉ tìm cục bộ.
- Hệ số FS có sai số số học kiểm soát được; cấu hình số lát, số lặp, tolerance, hội tụ, báo lỗi khi không hội tụ thay vì hiện FS giả. Xử lý đúng vật liệu xuyên lát, đường mực nước, áp lực nước lỗ rỗng, tải phân bố/tập trung, lực neo nếu đã có solver hỗ trợ, động đất giả tĩnh.
- Thêm phân tích độ nhạy, chạy batch theo kịch bản: GSI, D, c, φ, γ, đường nước, tải, chiều cao tầng, góc dốc, bề rộng đai. Đầu ra ma trận FS, ΔFS, ΔFS%, biên so tiêu chí và cung trượt thay đổi.
- Các chức năng cao cấp của Slide2 như phân tích xác suất/Monte Carlo, ổn định theo mặt yếu, support reinforcement và groundwater phức tạp chỉ gắn READY sau khi thực sự có solver, benchmark và kiểm thử; trước đó trạng thái IN_DEVELOPMENT minh bạch.

## P4 — KIỂM TOÁN THỰC TẾ XÓM ĐẨU (BẮT BUỘC)

Sử dụng báo cáo đánh giá ổn định bờ mỏ Xóm Đẩu ngày 23/9/2026 làm **hồ sơ tham chiếu nghiệp vụ**, không tự động coi FS tham khảo là kết quả của IMSAT. Không đăng công khai hồ sơ mỏ/dữ liệu đầu vào có hạn chế quyền chia sẻ.

**Tám mốc kết quả Slide2 được lưu để đối chiếu sau khi nhập đúng từng mô hình gốc:**

| Trạng thái | Tuyến | TH1 cơ sở | TH2 bất lợi | Mức FS đối chiếu trong báo cáo |
|---|---|---:|---:|---:|
| Hiện trạng | EG | 2,375 | 1,589 | 1,20 (kiểm bổ sung 1,30) |
| Hiện trạng | FG | 2,119 | 1,402 | 1,20 (kiểm bổ sung 1,30) |
| Bờ kết thúc | XX | 2,498 | 1,234 | 1,30 |
| Bờ kết thúc | YY | 1,528 | 1,053 | 1,30 |

Bộ thông số Xóm Đẩu: γ=26,2 kN/m³, σci=72.400 kPa, mi=10; TH1 GSI=50, D=0,5; TH2 GSI=40, D=0,7. Những số này là thông số cụ thể của hồ sơ tham chiếu; GSI và D là **kịch bản**, không mô tả chắc chắn chất lượng khối đá thực địa. Mô hình nước trong benchmark phải giữ đúng file gốc, không tự bổ sung áp lực nước vào kết quả cũ.

**Phân biệt hai loại kiểm chứng:** (1) đối chiếu mốc FS từ báo cáo để biết xu thế, không thể tuyên bố tái lập chính xác khi thiếu hình học chi tiết, miền tìm kiếm, tham số solver và tệp Slide2; (2) benchmark định lượng với cùng input/điều kiện tìm kiếm/mô hình gốc mới được so sai số thực. Khi chưa có nguồn nhập tương ứng, để `WAITING_INPUT`, không điều chỉnh tham số để ép FS trùng nhau.

- Kiểm tra tuyến khống chế: FG cho hiện trạng, YY cho bờ kết thúc; nếu kết quả khác phải giải thích trên cơ sở dữ liệu/mô hình.
- Kiểm toán đạt/không đạt theo tiêu chí do kỹ sư chọn và nguồn pháp lý/thiết kế xác minh hiện hành; không dùng một ngưỡng FS chung cho tất cả bờ, thời hạn tồn tại và điều kiện.
- Cảnh báo riêng cho đá rơi, trượt phẳng, trượt nêm, lật khối, khe nứt/đứt gãy, karst/hốc, hậu xung và mất đai: kết quả cung trượt tròn không được tự động kết luận an toàn cho những cơ chế này.
- Cho phép người dùng hiệu chỉnh thiết kế (góc bờ, đai, cao tầng) và **tính lại trên đúng bộ thông số bất lợi**, lưu lịch sử phương án và so sánh trước/sau. Tuyệt đối không báo 'đạt' chỉ bằng thay hình vẽ khi chưa chạy lại solver.

## P5 — GIAO DIỆN IMSAT SLOP V1.0 (KẾ THỪA, KHÔNG THAY VÔ CỚ)

Giao diện tiếng Việt, ứng dụng Windows desktop, bố cục rõ ràng theo quy trình: **Dự án → Hình học → Vật liệu/Địa tầng → Nước và Tải trọng → Phương pháp/Tìm mặt trượt → Phân tích → Kết quả → Báo cáo**. Giữ phong cách IMSAT và menu/canvas hiện có; hoàn thiện riêng đồ họa kỹ thuật với lưới tọa độ, vùng vật liệu màu có legend, mực nước, điểm vào/ra, mặt trượt và lát. Không bê nguyên giao diện thương mại Slide2/Rocscience, chỉ tham khảo bố cục chức năng tổng quát.

- Quản lý nhiều tuyến, nhiều phương án và nhiều kịch bản trong một project, tìm kiếm/lọc.
- Thao tác qua dialog/ribbon/menu ngữ nghĩa, không bắt kỹ sư nhập lệnh Python.
- Mọi nút phải có chức năng thật, trạng thái bận, hủy đúng, tiến độ, lỗi giải thích bằng tiếng Việt, UX 1366×768 và FullHD, scaling DPI, không mất chữ, icon IMSAT.
- Dữ liệu và kết quả liên kết hai chiều; thay đổi đầu vào bắt buộc đánh dấu kết quả cũ STALE, yêu cầu phân tích lại trước khi phát hành báo cáo.

## P6 — XUẤT BẢN VẼ, BÁO CÁO, LƯU VẾT

- Xuất CAD DXF hình học, mặt trượt, lát, tên tuyến và nhãn; PDF hình/báo cáo; DOCX báo cáo kỹ thuật tiếng Việt với bảng kết quả, mô hình, phương pháp, công thức, tiêu chí, hạn chế, kết luận; Excel dữ liệu đầu vào/đầu ra có bảng đóng khung, Arial và công thức kiểm chứng ở các bảng phù hợp; CSV khi cần.
- Word Times New Roman 14, bảng 13, mọi bảng có cột **Thứ tự** bên trái, công thức Equation Professional; kết luận không được vượt phạm vi đã tính; đánh dấu nguồn của tài liệu và tham số khảo sát/kịch bản.
- Phụ lục kiểm toán tái lập được: project version, input hash, solver version, search bounds, số lát, tolerance, iteration count, phương pháp, tổng hợp mặt trượt, chênh tiêu chí, cảnh báo không đủ dữ liệu.
- Tạo báo cáo tổng hợp cho nhiều mặt cắt và nhiều trường hợp như EG/FG/XX/YY, đồ thị FS, biên FS, độ nhạy. Xuất file không treo và có thông báo đường dẫn lưu.

## P7 — CHẤT LƯỢNG, BENCHMARK VÀ KIỂM THỬ

- Mở rộng toàn bộ regression tests V6.2; bắt buộc kiểm tra ít nhất: geometry/Region, đọc GSZ/CAD, 4 solver hiện hữu, điển hình nhiều vật liệu, nước/không nước, không hội tụ, mặt trượt tròn và không tròn (nếu triển khai), lớn nhiều điểm, lỗi thiếu vật liệu/Region, serialization và report.
- Tối thiểu ba lớp benchmark: bài toán giải tích/độc lập theo từng phương pháp, dữ liệu mô hình chuẩn có phép đối chiếu phần mềm tham chiếu **cùng đầu vào**, và thực nghiệm Xóm Đẩu nếu có file mô hình Slide2 gốc. Ghi bảng sai số có điều kiện và giải thích; không tự nhận tương đương thuật toán nếu chỉ trùng vài FS.
- Rà soát mã nguồn 5 vòng: công thức & cân bằng; dữ liệu & hình học; UX; tính bền vững & hiệu năng; cài đặt & phát hành.
- Chạy tự động CI trên GitHub, phân biệt rõ trạng thái Linux core, Windows UI/EXE smoke test và nghiệm thu trực tiếp. Thử nghiệm bắt buộc trên Windows có giao diện, chạy frozen EXE và tính ra FS hữu hạn, không chỉ PyInstaller báo OK.
- Báo cáo `TEST_REPORT.md`, `BENCHMARK_MATRIX.xlsx` (nếu tạo), `COMPATIBILITY_MATRIX.md`, `KNOWN_LIMITATIONS.md`, `RELEASE_CHECKLIST.md`; không đưa ra release khi gate quan trọng FAIL hoặc NOT_RUN.

## P8 — BỘ CÀI VÀ NGHIỆM THU BÀN GIAO

- Windows desktop EXE, bộ cài Setup có icon/logo IMSAT, cài/gỡ chuẩn, không yêu cầu kỹ sư cài Python; hỗ trợ đường dẫn Unicode tiếng Việt, quyền người dùng thường, xử lý antivirus/SmartScreen trung thực.
- Mẫu project trong sạch, tài liệu thao tác, testcase tái hiện lỗi, ảnh chụp UI thật, bảng release gates.
- Chỉ phát hành khi P0–P7 hoàn tất và Windows runtime nghiệm thu PASS; nếu chưa, bàn giao source/QA candidate với nhãn **CHƯA PHÁT HÀNH**.
- Kết thúc bằng commit hash, release tag nếu đủ điều kiện, link nhánh, link artifacts, bảng tính năng ĐÃ CÓ/ĐÃ SỬA/CHỜ NGUỒN/CHƯA XONG, minh chứng các chức năng.

## THỨ TỰ THỰC HIỆN VÀ ĐIỀU KIỆN DỪNG

P0 (khôi phục nguồn) → P1 (audit+ổn định nền) → P2 (địa chất/hình học) → P3 (solver) → P4 (benchmark Xóm Đẩu) → P5 (UI) → P6 (báo cáo) → P7 (QA) → P8 (Setup).

**Sau P0 mới được triển khai bổ sung code nghiệp vụ.** Nếu thiếu gói ZIP V6.2 hoặc thiếu quyền ghi repo, báo BLOCKED rõ ràng và yêu cầu file/quyền; **không khởi tạo skeleton mới**. Tất cả code phải commit lên nhánh `imsat-slop-v1.0-work`, không ghi đè IMSAT VOLUME/ROAD/HRM. Không sửa ngầm, không thông báo hoàn thành khi chưa kiểm thử và bàn giao thực sự.
