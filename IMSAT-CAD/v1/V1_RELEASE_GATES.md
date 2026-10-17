# IMSAT CAD V1.0 — Tiêu chuẩn phát hành / Release gate

**Mức nghiệm thu yêu cầu:** ứng dụng CAD độc lập đủ dùng để vẽ, hiệu chỉnh, lưu và in hồ sơ thiết kế. Không chấp nhận kết quả chỉ là mockup, màn hình giả lập hoặc tập hợp các lệnh chưa xử lý hình học.

## I. Quyết định kiến trúc

- Nhánh phát triển: `imsat-cad-foundation`. Không ảnh hưởng nhánh IMSAT VOLUME.
- **Lõi CAD mục tiêu:** Qt/C++ với QCAD Community Edition (cùng `qtjsapi`, `qcadjsapi`) chỉ khi Windows build, khởi động và kiểm tra lệnh thực tế đạt.
- Ứng dụng Tkinter + ezdxf hiện tại là **Preview / POC**, không được dán nhãn "V1.0 đã đạt" hay dùng làm nền tảng lâu dài cho CAD chuyên nghiệp.
- **Kiến trúc & giấy phép ĐÃ CHỐT:** xem [ADR-001](ADR_001_LICENSING_ARCHITECTURE.md). Dùng QCAD CE GPLv3 + ngoại lệ cho plugin độc lập; giữ mã nghiệp vụ IMSAT độc quyền trong private repo; công bố bản sửa lõi QCAD khi phân phối; chỉ bán kèm chức năng DWG khi có quyền resale/OEM với nhà cung cấp. Không chọn SDK ODA ở giai đoạn đầu.
- **DWG:** không đồng nghĩa DXF; chỉ triển khai qua công nghệ có giấy phép hợp lệ. Chưa được tuyên bố DWG tương thích khi chưa có dữ liệu kiểm thử.
- Tất cả nội dung giới thiệu là tính năng đã kiểm thử, không dùng nhãn "đã xong" cho mục chưa nghiệm thu.

## II. Ma trận lệnh cần tương đương AutoCAD

| Nhóm | Alias → command | Điều kiện nghiệm thu |
|---|---|---|
| Vẽ hình | L→LINE, PL→PLINE, C→CIRCLE, A→ARC, REC→RECTANG, POL→POLYGON, EL→ELLIPSE | Vẽ bằng chuột và tọa độ, đúng hình học |
| Hiệu chỉnh | E→ERASE, CO/CP→COPY, M→MOVE, RO→ROTATE, SC→SCALE, MI→MIRROR, O→OFFSET | Có chọn đối tượng, nhập tham số, đúng số liệu |
| Biên tập | TR→TRIM, EX→EXTEND, F→FILLET, CHA→CHAMFER, BR→BREAK, X→EXPLODE, J→JOIN | Thao tác được trên tập đối tượng tương ứng |
| Trình bày | LA→LAYER, B→BLOCK, I→INSERT, H→HATCH, T/MT→MTEXT, DLI/DAL/DAN→DIM | Tương thích layer, block, hatch, ghi kích thước |
| Truy bắt | OS→OSNAP, F3, F8 ORTHO, F7 GRID, F9 SNAP, F10 POLAR | Có bật/tắt và hiệu lực trong vùng vẽ |
| Hiển thị | Z→ZOOM, PAN, REGEN, UCS, PLAN | Không gián đoạn thao tác vẽ |
| Đo đạc | DI→DIST, ID, AA→AREA, LIST | Kết quả đúng đơn vị/toạ độ, làm tròn nhất quán |
| Tệp tin | NEW, OPEN, QSAVE, SAVEAS, AUDIT, PURGE, PLOT | Mở/lưu/khôi phục, không mất đối tượng |
| Undo/redo | U / UNDO, REDO | Thao tác đúng nhóm, dữ liệu lưu nhất quán |

**Lưu ý:** bảng trên là **yêu cầu phát hành**, không đồng nghĩa tất cả đã triển khai. Hiện POC mới có một tập lệnh con (khai báo trong `imsatcad/autocad_commands.py`). TRIM/EXTEND tại POC mới hỗ trợ LINE; OFFSET mới hỗ trợ LINE/CIRCLE. Command bar hiện hỗ trợ lệnh một dòng và click tương tác cho một số hình; chưa đạt prompt-workflow kiểu AutoCAD.

## III. Kiểm thử bắt buộc

1. So sánh 1:1 tọa độ X/Y/Z, kích thước, hình học và thuộc tính đối tượng trên bộ DXF tham chiếu.
2. Test open→modify→save→close→reopen qua **ít nhất 20 tệp** có layer, block, hatch, dim, xref (nếu tính năng hỗ trợ), bản vẽ bố trí mỏ. Không mất dữ liệu thầm lặng.
3. Thử lỗi: tệp hỏng, tọa độ cực lớn, hình học suy biến, tên layer bất thường, quyền thư mục, đường dẫn tiếng Việt.
4. Test hiệu năng bằng dữ liệu thực tế; đo thời gian và RAM chứ không chỉ nhìn ảnh chụp. Không khẳng định 1 triệu tam giác chạy mượt trước khi benchmark.
5. AutoCAD command aliases phải được thử bằng bàn phím trong giao diện Windows, không chỉ unit test backend.
6. Ngôn ngữ Việt/Anh: toàn bộ menu, popup, hộp thoại, command line, trình cài đặt. Người dùng có thể chuyển trực tiếp.
7. Logo IMSAT đúng bản chính thức trên cửa sổ, EXE, bộ cài, Start Menu; kiểm tra quyền sử dụng thương hiệu.
8. Cài, mở, chạy, cập nhật và gỡ cài trên Win10/Win11 x64, độ phân giải 1366×768 và Full HD, môi trường sạch.
9. Phân loại FAIL/BLOCKED/PASS kèm ảnh, log và mã phiên bản. **Bất kỳ BLOCKER nào ⇒ không phát hành V1.0**.

## IV. Trạng thái lúc khởi tạo

- POC DXF đơn giản: đã tạo; kiểm thử cục bộ 12/12 trên Linux/Xvfb (chưa tương đương nghiệm thu Windows toàn bộ).
- Command aliases có unit tests; chưa phải full AutoCAD.
- Native QCAD C++: đã có workflow thử build; **chưa đạt chứng minh hoạt động runtime thực tế**.
- DWG write/read và in bản vẽ thương mại: chưa triển khai.
- IMSAT VOLUME/TIN: không nằm trong điều kiện phát hành CAD 2D nền tảng ban đầu; tích hợp sau khi lõi ổn định.
- Trạng thái phát hành **V1.0: NOT READY**.

## V. Yêu cầu hỗ trợ từ phía người dùng (chỉ khi cần)

- Cấp file DWG/DXF có thể dùng để kiểm thử không chứa tài liệu mật, bộ bản vẽ mẫu và kết quả đối chứng AutoCAD.
- Chốt hướng giấy phép phần mềm thương mại: giữ các thành phần GPL tuân thủ mở mã nguồn, hay dùng SDK được cấp phép để có lõi đóng.
- Xác nhận quyền sử dụng logo chính thức của IMSAT trên phần mềm thương mại.
