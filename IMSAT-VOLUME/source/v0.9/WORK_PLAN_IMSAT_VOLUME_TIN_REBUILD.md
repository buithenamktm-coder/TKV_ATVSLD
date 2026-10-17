# WORK PLAN – IMSAT VOLUME V1.0
## Tái cấu trúc lõi TIN, dừng vòng lặp vá lỗi

### 1. Mục tiêu
Hoàn thiện **IMSAT VOLUME V1.0 cho AutoCAD 2023** thành bản dùng được thực tế trên dữ liệu mỏ lớn.

Ưu tiên số 1 là thay cách xử lý TIN hiện tại theo hướng ổn định và có thể kiểm chứng, thay vì tiếp tục vá từng lỗi edge-flip/breakline.

Các lỗi thực tế đã gặp cần coi là lỗi bắt buộc phải xử lý tận gốc:
- TIN chạy rất lâu/treo ở bước “đang chuẩn hóa”.
- Một ô TIN từng phình lên khoảng **164.111 đỉnh + 163.954 breakline**.
- Lỗi kiểu: **“Không tìm thấy dãy cạnh tam giác cắt breakline 302-491… dữ liệu có thể suy biến cục bộ.”**
- Lỗi đa luồng phân ô.
- Người dùng phải thử quá nhiều bản cài mới nhưng lỗi chỉ chuyển từ chỗ này sang chỗ khác.

### 2. Nguyên tắc bắt buộc
1. Làm trên repo hiện tại:
   - Repo: `buithenamktm-coder/TKV_ATVSLD`
   - Branch: `miningvolume-v1.0`
   - Root: `tools/MiningVolume_HSNext/v0.9`
2. **Không tạo project mới.**
3. **Không thay đổi workflow người dùng đã chốt** nếu không thực sự cần thiết.
4. Không xóa chức năng đang hoạt động.
5. Không làm giảm/simplify điểm đo hoặc vertex ngầm để tăng tốc.
6. Tính toán phải dùng dữ liệu đầy đủ; preview AutoCAD có thể giới hạn để giữ nhẹ giao diện.
7. Không quảng cáo AutoCAD 2012+ ở V1.0. V1.0 hiện chỉ nghiệm thu AutoCAD 2023.
8. Không yêu cầu người dùng cài Visual Studio/Build Tools.
9. Không yêu cầu anh Nam kiểm thử lại cho đến khi:
   - build sạch;
   - test core sạch;
   - test bằng đúng hai file DXF thực tế;
   - runtime AutoCAD 2023 PASS;
   - bộ cài mới đã được tạo.
10. Mỗi thay đổi phải có test hồi quy tương ứng.

---

# PHẦN A – ĐÓNG BĂNG VÀ CHẨN ĐOÁN

### A1. Tạo checkpoint
Trước khi sửa kiến trúc:
- ghi lại HEAD hiện tại;
- tạo tag/checkpoint nội bộ hoặc commit marker;
- không merge vào main trước khi hoàn tất nghiệm thu.

### A2. Dùng đúng dữ liệu thực tế
Tìm và dùng **hai file DXF mẫu người dùng đã gửi trong Project/conversation**.

Với file “vỉa 4”:
- layer hiện trạng: `HT`;
- layer thiết kế: `-NAM4` hoặc đúng tên layer thiết kế đã chốt trong dữ liệu;
- phạm vi tính phải hỗ trợ chọn polygon kín trên CAD.

Không chỉ dùng synthetic data để chứng minh.

### A3. Viết benchmark trước khi sửa
Tạo một test harness/headless benchmark cho core, tối thiểu log:
- số entity;
- số vertex;
- số breakline;
- thời gian load;
- thời gian clip vùng;
- thời gian dedupe;
- thời gian topology normalization;
- thời gian triangulation;
- thời gian constraint insertion;
- số tile;
- min/avg/max vertex mỗi tile;
- min/avg/max breakline mỗi tile;
- số triangle;
- peak working set nếu đo được.

Output benchmark phải lưu thành file text/JSON để so sánh trước/sau.

---

# PHẦN B – QUYẾT ĐỊNH KIẾN TRÚC TIN

### B1. Không tiếp tục phụ thuộc vào edge-flip tự viết nếu không chứng minh được độ bền
Hiện tại `ConformingTinBuilder` dùng:
- Bowyer-Watson tự viết;
- khôi phục breakline bằng edge-flip;
- nhiều fallback cho site suy biến/collinear.

Đây đang là nguồn lỗi chính.

Work phải làm một spike kỹ thuật và chọn **một trong hai phương án**:

#### Phương án ưu tiên: dùng constrained Delaunay engine đã được kiểm chứng
Đánh giá một thư viện CDT production-grade tương thích:
- .NET Framework 4.8;
- AutoCAD 2023;
- redistribution hợp pháp;
- không cần cài thêm runtime ngoài bộ cài;
- hỗ trợ PSLG / constrained segments;
- đủ nhanh với dữ liệu lớn.

Nếu dùng thư viện ngoài:
- kiểm tra license;
- ghi license vào repo;
- package DLL vào bundle;
- không phụ thuộc Internet khi chạy end-user;
- test chính xác với hai DXF thật.

#### Phương án 2: nếu không thể dùng thư viện ngoài
Thay hẳn thuật toán constraint recovery tự viết bằng một implementation CDT có cấu trúc rõ ràng:
- segment insertion theo triangle walk;
- split tại constraint intersection/site;
- local retriangulation;
- không dùng vòng lặp edge-flip tìm may rủi;
- không chấp nhận “fallback corridor” làm kết quả chính thức nếu nó làm lệch ràng buộc.

**Không được coi các patch tăng tolerance là giải pháp cuối.**

### B2. Tách core TIN thành interface
Tạo abstraction rõ ràng:
- `ITinEngine`
- `TinBuildRequest`
- `TinBuildResult`
- `TinConstraint`
- `TinDiagnostics`

UI/AutoCAD layer không biết engine cụ thể.

Mục tiêu:
- có thể đổi engine mà không sửa DataPage/ModelPage;
- core test chạy headless.

---

# PHẦN C – PIPELINE DỮ LIỆU LỚN

### C1. Tiền xử lý một lần
Không được chuẩn hóa toàn bộ breakline lặp lại cho từng halo.

Pipeline:
1. Đọc source entity.
2. Chuẩn hóa XY/Z conflict một lần.
3. Tạo spatial index toàn mô hình một lần.
4. Nếu có vùng tính:
   - clip/query dữ liệu theo spatial index;
   - chỉ lấy dữ liệu liên quan;
   - giữ support band hợp lý.
5. Phân tile sau khi đã có index.
6. Mỗi tile chỉ xử lý dữ liệu của tile + halo.

### C2. Không để tile phình bất thường
Thay cách chia tile chỉ theo số vertex bằng **adaptive tile splitting**.

Mỗi tile phải kiểm soát đồng thời:
- vertex count;
- breakline segment count.

Ví dụ logic:
- nếu vertex > giới hạn **hoặc**
- breakline > giới hạn
→ split tile tiếp theo trục dài hơn hoặc quadtree.

Không cố giữ một tile chứa >100.000 breakline như dữ liệu hiện tại.

Các ngưỡng phải là cấu hình nội bộ và được log, không hardcode rải rác.

### C3. Clip breakline theo tile chính xác
Constraint đi qua biên tile:
- tạo intersection vertex tại biên;
- Z nội suy theo segment gốc;
- cùng tọa độ biên giữa hai tile phải deterministic;
- không sinh khe/hở ở seam.

### C4. Stitch/accept kết quả
Phải xác định ownership rõ:
- triangle thuộc core tile nào chỉ ghi một lần;
- không trùng triangle;
- không hở seam;
- không chỉ dựa vào một heuristic mơ hồ.

Viết seam tests.

---

# PHẦN D – ĐỘ ỔN ĐỊNH DỮ LIỆU

### D1. Conflict Z
Giữ hành vi đã chốt:
- cùng XY khác Z → hỏi người dùng;
- chọn đỉnh trên hoặc đỉnh dưới;
- chỉ thay trong model tạm của lần dựng TIN;
- không sửa CAD nguồn.

### D2. Breakline
Phải hỗ trợ:
- contour polyline dày;
- line/polyline/2D polyline/3D polyline;
- crossing/overlap hợp lệ;
- crossing khác Z;
- endpoint gần trùng;
- segment rất ngắn;
- collinear chain;
- constraint đi qua site trung gian.

Không được chỉ “bỏ qua breakline gây lỗi” để TIN chạy.

### D3. Validation report
Nếu dữ liệu thực sự không hợp lệ:
- trả về lỗi cụ thể;
- có SourceId/entity/layer nếu truy được;
- có XY/Z;
- không hiện lỗi chung kiểu “TIN song song theo ô”.

---

# PHẦN E – HIỆU NĂNG

### E1. UI không được có cảm giác treo
Trong lúc build:
- progress cập nhật tối thiểu mỗi 0,5–1,0 s;
- AutoCAD/UI vẫn repaint được;
- nút Hủy phản hồi nhanh;
- hủy không làm hỏng Project state.

### E2. Không gọi AutoCAD API trong worker thread
Snapshot dữ liệu CAD trên UI/document thread trước.
Core computation chạy background thuần .NET.

### E3. Benchmark bắt buộc
Phải có 3 tầng:
1. unit/synthetic;
2. hai DXF thật;
3. stress synthetic lớn.

Mục tiêu release:
- hai DXF thật phải dựng được cả HT/TK, không exception;
- không có một tile nào phình vượt giới hạn cấu hình mà không tự split;
- tốc độ phải cải thiện rõ rệt so với baseline hiện tại;
- báo cáo thời gian trước/sau trong PR.

Mục tiêu dài hạn 10 triệu vertex là **stress target**, không được tuyên bố đạt nếu chưa benchmark thật.

---

# PHẦN F – UI/BRANDING SAU KHI LÕI TIN ỔN

Chỉ làm sau khi TIN qua test thật.

Giữ các yêu cầu đã chốt:
- Tên hiển thị: **IMSAT VOLUME**.
- Logo IMSAT trên:
  - cửa sổ chính;
  - Ribbon;
  - Setup;
  - Excel.
- Giao diện sáng, gọn, phong cách iOS:
  - nền xám rất nhạt;
  - card trắng;
  - accent xanh;
  - ít khoảng trống;
  - không chữ bị cắt.
- Palette không tự mở khi startup mặc định.
- Menu/Ribbon:
  - IMSAT VOLUME;
  - các chức năng chính phải hoạt động.
- Command `TKL` vẫn giữ.

Không ưu tiên thêm animation/decorative UI trong đợt này.

---

# PHẦN G – TEST BẮT BUỘC

### G1. Unit tests
Bổ sung test cho:
- constrained segment xuyên nhiều triangle;
- segment qua vertex;
- segment collinear với nhiều vertex;
- crossing constraints;
- near-collinear;
- duplicate XY;
- tile boundary;
- seam;
- region clip;
- cancel;
- persistence.

### G2. Golden tests
Với cùng input:
- triangle count hợp lý;
- bounding box đúng;
- không triangle đảo orientation;
- không triangle zero-area;
- mọi breakline bắt buộc được represented bởi edge chain của TIN;
- không có seam gap.

### G3. File thật
Hai DXF thực tế là release gate.

Tạo script/test có thể chạy lại tự động để:
- load;
- build HT;
- build TK;
- ghi summary;
- fail ngay nếu exception;
- fail nếu thời gian vượt ngưỡng cảnh báo đã xác định từ benchmark.

---

# PHẦN H – RELEASE GATE

Chỉ tạo bộ cài cho anh Nam khi đủ tất cả:

1. Python/static tests PASS.
2. .NET solution build:
   - 0 Error;
   - không warning nghiêm trọng.
3. Core benchmark hai DXF thật PASS.
4. HT + TK đều tạo TIN.
5. Không còn lỗi breakline recovery kiểu hiện tại.
6. Runtime AutoCAD 2023 PASS đúng source SHA.
7. MVSELFTEST PASS.
8. Setup có logo IMSAT.
9. Setup cài/gỡ sạch.
10. Không tự mở AutoCAD sau cài.
11. Không yêu cầu VS/Build Tools trên máy người dùng.
12. Xuất artifact kèm:
    - Setup EXE;
    - SHA256;
    - benchmark report;
    - test summary;
    - source commit.

---

# PHẦN I – CÁCH WORK THỰC HIỆN

Work phải thực hiện liên tục theo thứ tự:

1. Audit code hiện tại và hai DXF mẫu.
2. Chạy baseline benchmark.
3. Viết ADR ngắn chọn TIN engine/kiến trúc.
4. Implement engine mới phía sau interface.
5. Port validation/conflict policy.
6. Implement adaptive spatial tiling.
7. Viết unit + seam + file-real tests.
8. Benchmark.
9. Chỉ khi đạt mới nối lại vào UI.
10. Build hosted.
11. Runtime AutoCAD 2023.
12. Tạo Setup.
13. Gửi một báo cáo nghiệm thu duy nhất.

### Không làm
- Không gửi anh Nam từng bản vá nhỏ để thử.
- Không sửa tolerance liên tục rồi coi là hoàn thành.
- Không bỏ constraint lỗi để cho chạy.
- Không giảm vertex ngầm.
- Không đổi UI liên tục trong lúc core TIN chưa ổn.
- Không merge main trước khi hai DXF thật qua gate.

---

# DELIVERABLE CUỐI CÙNG CỦA WORK

Work phải trả về:

1. Danh sách file/code đã thay.
2. Kiến trúc TIN cuối cùng.
3. Vì sao lỗi breakline cũ không còn.
4. Benchmark trước/sau trên từng file thật.
5. Tổng vertex/breakline/tile/triangle.
6. Kết quả build/test/runtime.
7. Source commit.
8. Link artifact Setup.
9. SHA256.
10. Những giới hạn còn tồn tại, nếu có.

**Tiêu chí quan trọng nhất: không yêu cầu người dùng thử lại cho đến khi Work tự chứng minh được trên đúng dữ liệu thực tế.**
