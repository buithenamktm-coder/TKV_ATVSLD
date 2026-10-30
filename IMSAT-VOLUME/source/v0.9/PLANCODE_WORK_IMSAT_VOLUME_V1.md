# PLANCODE WORK — IMSAT VOLUME V1.0

## 1. Mục tiêu

Hoàn thiện **IMSAT VOLUME V1.0 cho AutoCAD 2023** thành bản dùng thực tế, tập trung tuyệt đối vào hai việc:

1. **TIN phải dựng ổn định trên dữ liệu mỏ thực tế, không treo, không lặp vòng vá lỗi breakline.**
2. **Hiệu năng phải cải thiện rõ rệt trên hai file DXF mẫu thực tế của dự án**, đồng thời giữ đầy đủ điểm đo và breakline dùng cho tính toán.

Không tiếp tục cách sửa từng lỗi nhỏ rồi yêu cầu người dùng thử lại nhiều lần. Work phải tự tái hiện lỗi, đo, sửa theo nguyên nhân gốc, kiểm thử đầy đủ rồi mới bàn giao một bản để người dùng nghiệm thu.

---

## 2. Phạm vi bắt buộc

Repo:
- `buithenamktm-coder/TKV_ATVSLD`

Nhánh:
- `miningvolume-v1.0`

Thư mục:
- `tools/MiningVolume_HSNext/v0.9`

PR hiện tại:
- PR #13 — IMSAT MiningVolume V1.0

Môi trường mục tiêu:
- AutoCAD 2023
- .NET Framework 4.8
- Windows
- Add-in AutoCAD thật, không demo, không giả lập

Tên sản phẩm hiển thị thống nhất:
- **IMSAT VOLUME**

Không quảng cáo AutoCAD 2012+ trong V1.0 khi chưa có build/runtime thật cho các phiên bản đó.

---

## 3. Nguyên tắc không được vi phạm

1. **Không giảm mẫu / decimate / bỏ điểm đo âm thầm** để làm nhanh.
2. Kết quả tính khối lượng phải dùng **TIN đầy đủ**, không dùng preview.
3. Preview CAD có thể giới hạn số tam giác để hiển thị, nhưng phải tách hoàn toàn khỏi TIN tính toán.
4. Không sửa dữ liệu CAD gốc khi xử lý xung đột.
5. Xung đột XY cùng vị trí khác Z vẫn cho người dùng chọn:
   - dùng đỉnh trên;
   - dùng đỉnh dưới;
   - dừng.
6. Không bắt người dùng sửa thủ công hàng nghìn breakline nếu phần mềm có thể chuẩn hóa tự động an toàn.
7. Không thay giao diện đã chốt thành một sản phẩm khác.
8. Không xóa chức năng đang hoạt động.
9. Không đưa Runner, GitHub, terminal, Build Tools, VS Code vào trải nghiệm người dùng cuối.
10. Không yêu cầu anh Nam thử lại sau từng commit nhỏ.
11. Không coi `MVSELFTEST PASS` là đủ. Phải có **kiểm thử file mỏ thực tế**.

---

## 4. Vấn đề hiện tại phải giải quyết tận gốc

### 4.1. Hiệu năng

Ảnh nghiệm thu thực tế cho thấy có ô TIN từng lên tới xấp xỉ:
- 164.111 đỉnh;
- 163.954 breakline;
- dừng lâu ở bước `đang chuẩn hóa...`.

Bản hiện tại đã thử:
- tiled TIN;
- halo;
- chia nhỏ tile;
- đa luồng;
- fallback 1 luồng;
- spatial index;
- giảm allocation.

Nhưng người dùng vẫn đánh giá **treo/chậm**.

Work phải đo riêng thời gian của:
- gom dữ liệu tile;
- ResolveDuplicateXY;
- point-on-breakline;
- breakline crossing/overlap normalization;
- Delaunay;
- constraint/breakline recovery;
- ghi file-backed TIN;
- ghi preview CAD.

Không tối ưu theo cảm giác.

### 4.2. Breakline recovery

Lỗi thực tế đã xuất hiện:
- “Không tìm thấy dãy cạnh tam giác cắt breakline …”
- “Không có cạnh cắt và cũng không tìm thấy chuỗi site trung gian nằm trên breakline để tách ràng buộc.”
- edge-flip không hội tụ / hình học suy biến cục bộ.

Không tiếp tục chỉ nới tolerance hoặc thêm fallback vô hạn.

Work phải xác định rõ một trong hai hướng và chốt:

**Hướng A — giữ lõi hiện tại**
- chứng minh được nguyên nhân;
- sửa topology/constraint recovery bằng thuật toán xác định, có test regression;
- không dùng corridor quá lớn làm sai breakline.

**Hướng B — thay lõi constrained triangulation**
- khảo sát thư viện CDT/TIN .NET có giấy phép phù hợp để đóng gói thương mại/nội bộ;
- nếu chọn thư viện ngoài, phải vendor/pin version rõ ràng, không phụ thuộc Internet lúc chạy;
- adapter phải giữ nguyên contract của `MiningVolume.Core` / `MiningVolume.Surface`;
- không để thay đổi thuật toán lan ra UI/Excel.

Nếu lõi edge-flip hiện tại tiếp tục phát sinh lỗi cùng họ lỗi trên dữ liệu thực, **ưu tiên thay lõi CDT thay vì vá tiếp**.

---

## 5. Kế hoạch thực hiện bắt buộc

### Gate 0 — đóng băng và tái hiện

- Ghi lại source SHA đầu vào.
- Không merge `main`.
- Dùng chính hai file DXF mẫu người dùng đã cung cấp trong Project/chat nếu Work truy cập được.
- File “vỉa 4”: theo quyết định đã chốt:
  - `HT` = hiện trạng;
  - `- nam4` = thiết kế;
  - tính trong `LO_TINHKL`.
- Nếu file thực tế không truy cập được trong Work:
  - không giả vờ đã test;
  - tạo fixture synthetic đủ để tái hiện lỗi breakline;
  - ghi rõ “real-file acceptance pending”.

Tạo log benchmark trước khi sửa:
- số entity;
- số vertex;
- số breakline;
- số tile;
- vertex/breakline lớn nhất trên một tile;
- thời gian từng stage;
- peak working set nếu đo được;
- tile lỗi và source breakline ID.

### Gate 1 — instrumentation trước tối ưu

Bổ sung telemetry nội bộ vào log, không làm rối UI:
- `tile x/y`;
- vertex count;
- breakline count;
- normalize ms;
- Delaunay ms;
- constraint recovery ms;
- output ms;
- tổng ms/tile;
- memory snapshot;
- exception gốc.

Progress UI:
- cập nhật tối thiểu mỗi 1–2 giây;
- AutoCAD UI không được “Not Responding” chỉ vì worker đang chạy;
- nút Hủy phải phản hồi nhanh.

### Gate 2 — sửa dữ liệu tile

Kiểm tra lại kiến trúc bucket/halo:
- không để một contour dài bị nhân bản toàn bộ vào mọi tile chỉ vì bbox đi qua tile;
- clip segment trước khi đưa vào tile;
- segment ID dùng để deduplicate đúng phạm vi;
- tránh tái tạo hàng trăm nghìn breakline cho halo không cần thiết;
- tile sizing phải dựa cả vertex density và breakline density, không chỉ vertex count.

Nếu một tile sau halo vượt ngưỡng lớn:
- tự chia tile tiếp (recursive/adaptive subdivision);
- không tăng halo đến mức biến tile thành bài toán toàn mỏ.

### Gate 3 — chuẩn hóa breakline hiệu năng tuyến tính/gần tuyến tính

Mục tiêu:
- không O(B²) trên dữ liệu contour lớn;
- point-on-breakline dùng spatial query;
- crossing/overlap dùng spatial query;
- không tạo `HashSet`/string key khổng lồ trong inner loop nếu có thể thay bằng integer key/visited stamp;
- không validate toàn cặp lần thứ hai nếu stage trước đã tạo topology chuẩn và constraint builder có invariant kiểm soát.

Mọi tối ưu phải có test chứng minh không làm mất:
- crossing node;
- overlap node;
- duplicate XY conflict;
- upper/lower policy.

### Gate 4 — thay hoặc hoàn thiện constrained triangulation

Acceptance của breakline:
- mọi breakline đầu vào hợp lệ phải được biểu diễn bằng một cạnh hoặc chuỗi cạnh collinear của TIN;
- không được silently bỏ breakline;
- không được “coi như đạt” chỉ vì path nằm gần breakline vượt quá tolerance thiết kế.

Tạo regression fixture tối thiểu cho:
- điểm nằm đúng trên breakline;
- nhiều site collinear;
- contour dày;
- breakline đi qua tile boundary;
- breakline cắt nhau cùng Z;
- breakline cắt nhau khác Z;
- overlap;
- tọa độ mỏ lớn;
- near-collinear;
- hull boundary;
- clip boundary lõm.

### Gate 5 — hiệu năng TIN lớn

Mục tiêu thiết kế vẫn là hàng triệu điểm; stretch goal ~10 triệu vertex/model.

Không tuyên bố 10 triệu đã đạt nếu chưa benchmark thật.

Yêu cầu:
- full TIN file-backed/tiled;
- preview CAD bounded;
- không giữ toàn bộ tam giác lớn trong RAM nếu không cần;
- tile độc lập có thể chạy song song nếu topology nội bộ thread-safe;
- không song song hóa mutation dùng chung.

Tiêu chí thực tế:
- progress phải tiếp tục tăng, không đứng hàng phút tại một stage mà không có log;
- cùng một input, bản mới phải nhanh hơn rõ rệt so với baseline đo ở Gate 0;
- mục tiêu tối thiểu: **>= 3x nhanh hơn** ở stage đang là bottleneck, hoặc phải có giải trình định lượng nếu bottleneck đã chuyển sang stage khác.

### Gate 6 — chọn vùng tạo TIN

Chức năng “Chọn vùng trên CAD” phải:
- dùng chung cho Hiện trạng + Thiết kế;
- clip input trước triangulation;
- giữ support band đủ để nội suy mép;
- không kéo quá nhiều dữ liệu ngoài vùng;
- không tạo lỗ mép;
- persistence lưu/khôi phục đúng vùng;
- đổi vùng phải invalidate đúng 2 TIN cũ.

### Gate 7 — UI/branding

Giữ phong cách đã chốt:
- tên: **IMSAT VOLUME**;
- logo IMSAT trên cửa sổ thao tác;
- Ribbon: IMSAT VOLUME;
- classic Menu Bar: IMSAT VOLUME;
- Palette: IMSAT VOLUME;
- báo cáo/Excel: IMSAT VOLUME;
- Setup/shortcut: IMSAT VOLUME.

UI:
- gọn;
- ít khoảng trắng thừa;
- phong cách sáng, tối giản, gần iOS;
- không cắt chữ ở 1366×768;
- không ép người dùng mở palette khi AutoCAD khởi động;
- lệnh `TKL` vẫn bật/tắt palette.

### Gate 8 — mặt cắt, khối lượng, Excel

Sau khi TIN ổn:
- tạo hệ mặt cắt không treo;
- tính đào/đắp đúng;
- công thức khối lượng thích ứng:
  - 1 đầu = 0 → hình chóp;
  - chênh diện tích ≤ 40% → trung bình hai đầu;
  - chênh > 40% → hình chóp cụt;
- Excel phải chứa công thức thật tham chiếu ô;
- thay dữ liệu Excel phải recalc;
- Arial;
- đóng khung;
- không tô nền;
- logo IMSAT;
- cố định:
  - Bùi Thế Nam;
  - Điện thoại: 0967280686.

### Gate 9 — Project persistence

Kiểm:
- Save Project;
- đóng DWG;
- mở lại;
- nguồn dữ liệu;
- TIN region;
- trạng thái TIN;
- section system;
- volume result;
- không crash khi đọc snapshot format cũ.

### Gate 10 — build/runtime/release

Chỉ khi các gate trên đạt:
- toàn bộ test PASS;
- build .NET: 0 error;
- hosted build PASS;
- runtime AutoCAD 2023 PASS đúng source SHA;
- `MVSELFTEST PASS`;
- tạo Setup runtime-pass có icon IMSAT;
- checksum SHA256;
- PR #13 cập nhật đầy đủ.

Không merge main trước khi nghiệm thu file thực tế.

---

## 6. Bộ kiểm thử bắt buộc

### Unit/static
- giữ toàn bộ test hiện có;
- thêm regression test cho từng lỗi thực tế đã sửa;
- không sửa test chỉ để “cho xanh” nếu behavior thực đã bị thay sai.

### Synthetic benchmark
Tạo ít nhất:
- 10k vertex;
- 100k vertex;
- 500k vertex;
- 1M vertex;
- contour/breakline density cao.

Báo:
- thời gian;
- triangles;
- memory;
- throughput;
- lỗi.

### Real-file acceptance
Trên hai file thực tế:
- load dữ liệu;
- chọn vùng nếu cần;
- dựng Hiện trạng;
- dựng Thiết kế;
- tạo mặt cắt;
- tính khối lượng;
- Excel;
- save/open project.

Không được kết luận “hoàn tất V1.0” nếu chưa qua real-file acceptance.

---

## 7. Definition of Done

Work chỉ được báo DONE khi có đủ:

1. Source commit cuối.
2. Danh sách nguyên nhân gốc đã tìm thấy.
3. Danh sách thay đổi thuật toán.
4. Benchmark trước/sau.
5. Test regression PASS.
6. Hosted build PASS.
7. AutoCAD 2023 runtime PASS đúng source SHA.
8. Hai file thực tế hoàn thành workflow mà không treo/lỗi.
9. Setup V1.0 runtime-pass.
10. SHA256.
11. Không còn TODO/PATCH tạm trong đường chạy production.
12. Không quảng cáo 10M hoặc AutoCAD 2012+ khi chưa có chứng cứ.

---

## 8. Báo cáo cuối Work phải trả về

Báo cáo ngắn, có số liệu:

- Source SHA:
- Root cause:
- Thuật toán TIN cuối:
- Có/không dùng thư viện ngoài:
- License:
- Thời gian file thực tế A trước/sau:
- Thời gian file thực tế B trước/sau:
- Peak RAM:
- Số vertex:
- Số breakline:
- Số triangle:
- Tạo mặt cắt: PASS/FAIL:
- Khối lượng: PASS/FAIL:
- Excel formula: PASS/FAIL:
- Save/Open Project: PASS/FAIL:
- AutoCAD 2023 runtime: PASS/FAIL:
- Setup:
- SHA256:
- Hạn chế còn lại:

Nếu bất kỳ mục thực tế nào chưa test được, ghi rõ **CHƯA NGHIỆM THU**, không suy đoán.

---

## 9. Chỉ thị thực thi cho Work

Thực hiện toàn bộ kế hoạch này trên nhánh `miningvolume-v1.0`.

Ưu tiên:
1. tái hiện;
2. profile;
3. sửa nguyên nhân gốc;
4. regression;
5. benchmark;
6. runtime;
7. bộ cài.

Không dừng ở việc “đề xuất”. Hãy sửa code, chạy test, chạy build, cập nhật PR và tạo artifact cuối.

Không hỏi người dùng xác nhận giữa các bước nếu dữ liệu và quyền truy cập đã đủ. Chỉ hỏi khi thiếu file thực tế hoặc cần hành động vật lý trên máy AutoCAD mà Work không thể tự thực hiện.
