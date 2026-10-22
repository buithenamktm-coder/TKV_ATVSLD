# IMSAT ROAD – phát triển phần mềm thiết kế đường giao thông và đường vận tải mỏ

## Mục tiêu
- Add-in tích hợp trong AutoCAD, tên sản phẩm **IMSAT ROAD**.
- TDT 9.1 Pro là phần mềm đối chứng nghiệp vụ; hướng dẫn VNRoad 7.1 là tài liệu tham khảo chi tiết ban đầu, cần xác minh khác biệt phiên bản 9.1.
- Xây dựng độc lập về mã nguồn, không sao chép thư viện/mã độc quyền TDT; tái hiện chức năng, cách dùng, kết quả đối chứng trong phạm vi phù hợp.
- Kế thừa có kiểm thử các thành phần kỹ thuật từ IMSAT VOLUME nhưng phát triển riêng, không sửa nhánh MiningVolume.

## Nguyên tắc mô hình địa hình
- IMSAT ROAD chỉ yêu cầu **một TIN hiện trạng đầu vào**.
- Tuyến và bề mặt thiết kế được tính từ bình đồ tim tuyến, đường đỏ, trắc ngang, taluy, khuôn đường và các tham số thiết kế.
- TIN/bề mặt thiết kế (nếu cần) được sinh nội bộ, không phải đầu vào bắt buộc.
- Các thay đổi cần được quản lý liên kết và cập nhật các kết quả phụ thuộc.

## Tiêu chuẩn và kiểm tra
- Mặc định: **TCVN 4054:2005**, đường cấp **III**, địa hình **đồng bằng**.
- Bật kiểm tra chỉ tiêu kỹ thuật khi thiết kế; ghi rõ lý trình, chỉ tiêu, giá trị tính và giới hạn nguồn tiêu chuẩn.
- Đường vận tải mỏ dùng cấu hình và căn cứ chuyên ngành riêng, không mặc định áp toàn bộ TCVN 4054:2005.

## Phân kỳ
1. Đặc tả nghiệp vụ; ma trận menu/lệnh/hộp thoại VNRoad 7.1 và đối chiếu TDT 9.1 Pro.
2. Tách lõi CAD, địa hình TIN, hình học và hạ tầng xuất hồ sơ để dùng lại an toàn.
3. Quản lý tuyến, bình đồ, đường cong, cọc và nội suy trắc dọc/trắc ngang hiện trạng.
4. Đường đỏ, đường cong đứng, siêu cao, mở rộng, trắc ngang thiết kế, nền và mặt đường.
5. Địa chất, rãnh/cống, khối lượng đào đắp, biểu bảng, in ấn, mô hình 3D tuyến.
6. Phân hệ đường mỏ, báo cáo kiểm tra tiêu chuẩn, tối ưu hiệu năng, kiểm thử đối chứng và bộ cài.

## Kiểm soát nghiệm thu
- Mỗi chức năng cần có: ID, thao tác/menu/lệnh, tham số, kết quả, test case, tiêu chí pass/fail.
- Mọi kết quả nghiệm thu phải đến từ chạy thử thực tế; không coi viết mã hoặc build thành công là đã kiểm thử AutoCAD.
- Không đưa bộ cài TDT, dữ liệu có bản quyền, khóa kích hoạt hay tài liệu bên thứ ba lên repository công khai.

## Nhánh và nguồn khởi tạo
Nhánh `imsat-road-v1.0` được tách từ `miningvolume-v2-chatgpt-p0-continuation` như một snapshot kỹ thuật; việc tái sử dụng từng thành phần sẽ được đánh giá và kiểm thử trước khi chuyển vào kiến trúc ROAD.
