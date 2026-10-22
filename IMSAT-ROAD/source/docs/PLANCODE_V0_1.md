# IMSAT ROAD – PlanCode V0.1 (khởi động triển khai)

## Căn cứ
- Bộ đối chứng: TDT 9.1 Pro AC2021–2024 (2026-09), cần kiểm thử trên Windows / AutoCAD có bản quyền.
- Tài liệu đã đọc: VNRoad 7.1 Useguide (192 trang), mục 1.3, 3.1.2, 3.1.3, 3.1.4, 3.1.6, 3.1.7.
- Đây là hành vi được mô tả trong 7.1; cần xác minh lại trên 9.1, không tuyên bố 9.1 giống hoàn toàn.

## Cổng V0.1
- [x] Tạo nhánh riêng để không ảnh hưởng IMSAT VOLUME.
- [x] Tách thư mục ROAD và dự án lõi riêng.
- [x] Khai báo mặc định TCVN 4054:2005 / cấp III / đồng bằng / bật kiểm tra.
- [x] Tuyến tim dạng đoạn thẳng từ các đỉnh Polyline có lý trình và nội suy XY.
- [x] Mặt cắt tự nhiên dùng một TIN hiện trạng qua adapter dùng MiningVolume.Core.
- [x] Không điền số 0 khi dữ liệu TIN không phủ.
- [x] Bộ đánh giá tiêu chí trả NotEvaluated nếu chưa có bảng tiêu chuẩn đã đối chiếu.
- [ ] Build + chạy smoke tests trên runner Windows (xem workflow; chưa ghi PASS khi chưa có log).
- [ ] Giao diện AutoCAD/Ribbon, lệnh TT, CN, HCT, TD, TN.
- [ ] Thuật toán cong nằm tròn / chuyển tiếp và lý trình theo cong.
- [ ] Hệ thống kiểm tra đầy đủ các bảng TCVN 4054:2005 đã xác minh và viện dẫn điều khoản.
- [ ] Đối chiếu tương tác / kết quả với bản TDT 9.1 Pro chạy thực.

## Các lệnh VNRoad khảo sát đầu tiên (đọc theo hướng dẫn 7.1)
| Lệnh | Menu tài liệu | Mục | Tình trạng ROAD |
|---|---|---|---|
| DLBD | Dữ liệu bình đồ | 3.1.1 | Adapter 1 TIN; chưa có hộp thoại |
| TT | Tuyến > Tạo tuyến | 3.1.2 | Lõi polyline bước đầu; chưa có AutoCAD command |
| CN | Hiệu chỉnh tuyến > Bố trí đường cong nằm | 3.1.3 | Chưa triển khai |
| HCT | Hiệu chỉnh tuyến > Hiệu chỉnh tuyến | 3.1.6 | Chưa triển khai |
| TD | Trắc dọc > Vẽ trắc dọc tự nhiên | 3.1.7 | Chưa triển khai bản vẽ |
| TN | Trắc ngang > Vẽ trắc ngang tự nhiên | 3.1.7 | Có bộ lấy cao độ, chưa triển khai bản vẽ |
| KBSC | Hiệu chỉnh tuyến > Khai báo siêu cao theo lý trình | 4.2.3 | Chưa triển khai |
| DD | Trắc dọc > Thiết kế trắc dọc | 4.2.6 | Chưa triển khai |
| CNDD | Trắc dọc > Cập nhật cao độ thiết kế | 4.2.7 | Chưa triển khai |

## Kiến trúc và an toàn bản quyền
- IMSAT.Road.Core không phụ thuộc AutoCAD UI; Road.TerrainBridge chỉ đọc TinSurface hiện trạng của MiningVolume.
- Không sao chép mã nguồn/binary/giao diện đồ họa có bản quyền từ TDT.
- Alias lệnh VNRoad trùng với TDT khi cùng cài AutoCAD: cần kiểm tra xung đột trước khi đăng ký trong add-in; giai đoạn đầu dùng tiền tố IR, về sau có thể cho người dùng cấu hình alias tương thích.
- Không đưa bộ cài TDT, thư viện đóng hoặc khóa cứng vào kho mã nguồn công khai.
- Nhánh Volume vẫn tiếp tục phát triển riêng; chỉ nhập lại phần dùng chung sau khi đã có test.

## Cổng V0.2
Thiết kế model hình học đoạn thẳng/cong nằm; lưu hồ sơ dự án tuyến; tạo lệnh CAD thực tế và palette; trích trắc dọc theo một TIN hiện trạng; kiểm thử trước khi gắn menu theo VNRoad.
