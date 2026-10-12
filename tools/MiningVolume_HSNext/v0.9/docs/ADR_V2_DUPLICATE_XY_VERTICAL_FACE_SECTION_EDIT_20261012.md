# ADR — Điểm trùng XY, khác Z: bề mặt 3D có mặt đứng và hiệu chỉnh mặt cắt

**Ngày chốt nghiệp vụ:** 12/10/2026. **Trạng thái triển khai:** CORE CODE IMPLEMENTED / NATIVE TEST PASSED — HOST AUTOCAD NOT TESTED. Chế độ cảnh báo–dựng TIN mặt đứng đã triển khai, nhưng chưa được kết luận tương thích 100% hình học HSMo và chưa nghiệm thu trên AutoCAD thật.  
**Đầu vào:** Người dùng đã kiểm tra thao tác của HSMo: nhóm điểm cùng XY, khác Z được coi là một vị trí mặt bằng; khi dựng mô hình có thể xuất hiện mảng/mặt gần thẳng đứng dạng hình chóp. Tuyến mặt cắt có thể đi qua khu vực đó và người dùng **tự hiệu chỉnh đường mặt cắt**, sau đó cập nhật khối lượng.

## Cập nhật thực hiện theo quyết định mới của người dùng (12/10)

**Quy tắc vận hành đã đổi:** không yêu cầu loại/sửa thủ công từng đỉnh trùng XY trước khi dựng TIN. Mặc định tại workflow AutoCAD, phần mềm tự xử lý, **chỉ thông báo số nhóm XY trùng và số đỉnh nguồn liên quan**, khuyến cáo rà soát Point/Polyline/Line/đường đồng mức trước khi chốt khối lượng. Vẫn có thể sử dụng bảng dữ liệu nguồn để kiểm tra khi muốn nhưng không phải điều kiện bắt buộc của trường hợp duplicate-XY được hỗ trợ.

**Đã code:** `SurfaceBuildOptions.PreserveDuplicateXYVerticalFaces` bật trong `SurfaceWorkflowService.BuildCoreDetailed`; `SurfaceInputPreparer` thu thập `VerticalConflictGroups` và chuẩn hóa một nút XY support cho thư viện native CDT, chọn Z thấp nhất **chỉ cho phần lưới support 2,5D**, không xóa hoặc thay đổi Z nguồn. `VerticalFaceBuilder` tạo các mặt tam giác 3D từ các tầng Z khác nhau đến nút tam giác kề, bảo toàn đa cao độ trong lớp mặt đứng riêng; `TinCadRenderer` ghi cả mặt đứng dạng `3DFACE`. TIN lớn dùng cùng logic file-backed. `TinSectionSampler` gắn cảnh báo vào những mặt cắt liên quan; `SectionPage` hiện CẦN KIỂM TRA MC; `VolumeWorkflowService` buộc người dùng xem/chỉnh Grip đường mặt cắt rồi bấm `CẬP NHẬT MẶT CẮT` **một lần cho hệ MC**, không phải sửa từng điểm. Không cho dùng ngay F/V chưa duyệt khi nguồn có vertical faces.

**Đã test (không thay nghiệm thu host):** `CoreRegression.cs` có ca hai Point cùng XY khác Z và hai contour có đỉnh trùng XY khác Z, kiểm `VerticalFaces`, giữ Z nguồn và cảnh báo mặt cắt. `tests/reference/test_v2_duplicate_xy_vertical_faces_static.py` khóa luồng UI/CAD/volume.

**Giới hạn còn lại:** bộ sampler 2,5D tính một cao độ từ support TIN và gắn cảnh báo; **chưa tự thể hiện đoạn thẳng đứng tại đúng station trên mặt cắt**, do đường mặt cắt 2D hiện yêu cầu station tăng nghiêm ngặt. Người dùng có thể sửa Grip profile và cập nhật, nhưng không khẳng định trùng tuyệt đối thuật toán HSMo nếu chưa có DWG/mặt cắt đối chứng. Breakline giao nhau với Z trái nhau nhưng không có đủ hai đỉnh nguồn cùng XY vẫn có thể bị chặn để tránh xuất sai; cần test tiếp ca dữ liệu thực. Không phát hành V2.0 chính thức dựa duy nhất vào CI.

## 1. Nguyên tắc bắt buộc

1. **Không làm mất cao độ dữ liệu gốc.** Nhóm theo dung sai XY, giữ tất cả Z gốc cùng handle, layer, loại hình học, chỉ số đỉnh và lịch sử hiệu chỉnh; không loại Z, không lấy trung bình và không chọn upper/lower âm thầm.
2. **Tách mô hình kiểm tra nguồn và mô hình được chấp nhận.** TIN 2,5D truyền thống (`TinSurface` với tam giác có diện tích XY dương, CDT ràng buộc một nút/XY) không hỗ trợ trực tiếp hai Z tại một XY. Không được tắt `ConflictScanner` hay xóa kiểm tra `ConformingTinBuilder.siteIndex` nếu chưa có bộ dựng/mặt cắt xử lý hình học 3D tương ứng.
3. **Phát triển một chế độ tương thích có kiểm soát**, ví dụ `TIN 3D — giữ mặt đứng (HSMo)`, do người dùng chủ động lựa chọn. Chế độ TIN nghiêm ngặt hiện hành vẫn duy trì cho dữ liệu một Z/XY. Chế độ TIN trung bình Z tham khảo giữ nguyên nhãn KHÔNG DÙNG TÍNH KHỐI LƯỢNG, không đồng nhất với HSMo.
4. **Mô hình mặt đứng phải có hình học thực**, không dùng mẹo dịch XY giả (epsilon perturbation) để che xung đột hoặc tạo tam giác suy biến. Các tam giác dựng đứng có diện tích hình chiếu XY bằng 0, nhưng diện tích mặt 3D có thể dương; phải lưu riêng topology/faces và metadata nguồn.
5. **Mặt cắt là đầu vào kiểm soát kết quả.** Khi cắt tuyến qua/trùng vùng 3D có nhiều Z tại cùng XY, bộ sampler phải nhận biết sự gián đoạn, biểu diễn đoạn đứng hoặc phát cảnh báo mơ hồ, không chọn Z tuỳ tiện. Hiện rõ tuyến/MC nào chịu ảnh hưởng và cho phép sửa đường HT/TK bằng Grip rồi nhấn `CẬP NHẬT MẶT CẮT`.
6. **Chống xuất khi chưa xác nhận.** Với MC qua nơi nhiều Z mà chưa dựng được mặt cắt xác định một cách duy nhất, trạng thái phải `CẦN HIỆU CHỈNH / XÁC NHẬN`, chặn việc coi F/V là chính thức. Sau khi người dùng xác nhận/cập nhật thì tính lại diện tích đào–đắp, khối lượng khoảng MC, khối lượng theo tầng/mức và 3 workbook theo phép tính tương ứng. Thay đổi TIN, DWG, vùng biên, hay CAD grip sau tính phải làm mất hiệu lực kết quả.
7. **Không chỉnh sửa CAD gốc.** Tất cả lựa chọn hoặc hiệu chỉnh nằm trong model/project và trên các layer kết quả chuyên biệt. Lưu thông tin xung đột, các mặt đứng và chữ ký nguồn để DWG SAVE/SAVEAS/reopen kiểm tra lại; không tự nhận lại kết quả cũ nếu metadata thiếu hoặc hình học đã thay đổi.
8. **Không đánh đồng với dựng 3D tổng quát.** Không mở rộng ngoài TIN/MC/khối lượng MiningVolume V2.0 sang MineBIM, AutoCAD 2012+ hay trữ lượng tấn.

## 2. Đánh giá hiện trạng đã kiểm tra trong code

- `SurfaceInputPreparer.Prepare` và `ConflictScanner`: đang chặn nhóm trùng XY khác Z. `SurfaceBuildOptions.DuplicateXYConflictPolicy` ép `Stop`.
- `ConformingTinBuilder`: truyền đúng một XY vào thư viện native CDT, ánh xạ `Dictionary<XY,int>`, từ chối XY thứ hai và từ chối tam giác chiếu XY có diện tích không dương. Vì vậy **chỉ bỏ Stop là sai**.
- `MeanZPreviewPreparer`: bỏ qua breakline, dùng trung bình; chỉ là TIN thử nghiệm, không thay cho bảo toàn mặt đứng.
- `TinSectionSampler`: nội suy một Z theo tam giác chứa XY, chưa tính giao tuyến với các mặt đứng 3D/biên nhảy Z. `EditableSectionCadService`, `EditedSectionProfileBuilder`, `SectionWorkflowService` đã có luồng Grip → CẬP NHẬT MẶT CẮT → tính lại khối lượng, cần mở rộng để nhận topology mới chứ không viết lại.
- `ProjectSnapshot` và source fingerprint đang bảo vệ nguồn và mặt cắt chỉnh tay; phải bổ sung khả năng đọc/ghi các dữ liệu conflict topology và accepted-section status, giữ tương thích dữ liệu V2 hiện tại.

## 3. Thứ tự triển khai và nghiệm thu

| Ưu tiên | Thay đổi | Gate / kiểm thử |
|---|---|---|
| P0 | Thêm cấu trúc `VerticalConflictGroup` (XY, danh sách Z có provenance và mối liên hệ breakline), giữ nguồn và báo cáo xung đột, không phá nhánh nghiêm ngặt | Z1=10 và Z2=20 cùng XY đều được bảo toàn; với Z1=Z2 không phát mặt đứng |
| P0 | Xây bộ topology 3D tách khỏi CDT 2,5D: nối cạnh đứng và các mặt 3D cần thiết theo hình học xác định được; CAD 3DFACE vẽ đúng, không tạo mặt giả; cảnh báo khi không đủ thông tin tạo hình chóp | Kiểm tra hình học bằng dataset nhỏ có đáp án xác định; duplicate XY sinh mặt 3D chứ không bị vứt |
| P0 | Bộ lấy mặt cắt xử lý crossing/collinear đối với cạnh/mặt đứng, đánh dấu MC mơ hồ và tạo đường có thể sửa trên CAD | Đi ngang XY xung đột: thể hiện đúng chênh Z, label xung đột và MC thao tác được |
| P0 | Grip sửa đường sau khi xuất; cập nhật F đào/đắp và V khoảng/tầng, chỉ ghi kết quả đã được duyệt | Trên đoạn 10 m tăng Z 1 m → ΔF=10 m²; giữa hai MC 20 m → ΔV=200 m³ trong ca hình học tương ứng; kiểm tình huống đoạn đứng |
| P1 | DWG SAVE/SAVEAS và reopen; kiểm metadata, invalidation/duplicate CAD handles; Excel Arial12 không nền công thức link thật | So khối lượng trước/sau reopen, không khôi phục âm thầm nếu nguồn/mặt cắt thay đổi |
| P2 | Runtime trên AutoCAD 2023, so TIN và mặt cắt với file HSMo thực; dữ liệu SQ có ΔZ >3 m | T01–T22 được cập nhật thêm ca vertical-face, host PASS có ảnh/log và người ký nghiệm thu |

**Nguyên tắc sai số:** Không kết luận thể tích của mặt đứng chỉ từ diện tích hình chiếu XY bằng 0. Mặt đứng có diện tích 3D nhưng thể tích đào/đắp phụ thuộc cách định nghĩa miền khép kín và hai bề mặt; với nhánh mặt cắt phải lấy theo hình học mặt cắt được duyệt. Không biến tự động bất định thành kết quả khối lượng chốt.

**Lưu ý cần đối chiếu:** Mô tả hình chóp/mặt đứng dựa trên quan sát của người dùng. Chưa có bản vẽ ví dụ + mặt cắt HSMo thực để xác định topology thuật toán HSMo; vì vậy không khẳng định bề mặt 3D do mình tạo sẽ trùng HSMo trước khi có kiểm thử đối chứng.

## 4. Ảnh hưởng tới release gate

- SQ trùng XY, khác Z không còn được mặc định diễn giải là `phải xóa/sửa Z gốc`; phải có đường **bảo toàn nguồn và hiệu chỉnh MC**, trước khi công bố tương thích HSMo.
- Release gate hiện tại giữ **BLOCKED** cho chế độ HSMo/vertical-face cho tới khi code được tích hợp và kiểm thử trong AutoCAD thật. Các bài test hiện hành PASS không kiểm chứng tính năng chưa viết.
- Không thay đổi nhánh V1.0; không tạo project mới; làm trong `tools/MiningVolume_HSNext/v0.9`.
