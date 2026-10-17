# IMSAT MiningVolume V1.0 - Release Baseline

## Mục tiêu
V1.0 là bản hoàn thiện đầu tiên để sử dụng thực tế trong AutoCAD cho công tác khảo sát mỏ, lập mặt cắt và tính khối lượng đào đắp. Không phát hành V1.0 chính thức khi chưa đạt đầy đủ checklist nghiệm thu và runtime gate.

## Checklist bắt buộc
- [ ] Nhận diện IMSAT đồng bộ: logo, icon Setup, icon ứng dụng, Ribbon/Menu, Excel.
- [ ] Giao diện cân đối, không mất chữ, không lộ công cụ phát triển.
- [ ] Menu/Ribbon MINING VOLUME và lệnh TKL bật/tắt rõ ràng; không tự bật palette mặc định.
- [ ] Chọn trực tiếp POINT/LINE/LWPOLYLINE/POLYLINE 2D/3D trên CAD; chọn theo layer chỉ là phương án bổ sung.
- [ ] Quản lý dữ liệu đã nạp: X/Y/Z, loại đối tượng, layer nguồn, loại/bật lại đối tượng và cập nhật mô hình.
- [ ] TIN hiện trạng + thiết kế: xử lý dữ liệu lớn, có tiến độ, hủy được, không silent-decimate điểm đo.
- [ ] Phạm vi TIN tùy chọn: người dùng được chọn LWPOLYLINE khép kín trên CAD để chỉ dựng cặp TIN trong vùng cần tính; vẫn có lựa chọn dùng toàn bộ dữ liệu.
- [ ] Xử lý mềm dẻo các xung đột Z: trùng XY, point trên breakline, breakline cắt/chồng khác Z; người dùng được chọn dừng / đỉnh trên / đỉnh dưới.
- [ ] Tính khối lượng theo quy tắc hình học thích ứng giữa hai mặt cắt liền kề, không dùng một công thức cố định cho mọi trường hợp.
- [ ] Excel dùng công thức thực và liên kết ô/sheet; thay đầu vào trong Excel phải tự tính lại.
- [ ] Excel Arial, không tô nền, bảng đóng khung; có logo IMSAT và thông tin người phát triển.
- [ ] Bộ cài hoàn chỉnh, không tự mở AutoCAD sau cài; file Setup runtime-pass phải mang icon IMSAT chính thức.
- [ ] Thông báo cài đặt chỉ nêu phần mềm đang tiếp tục cải tiến/hoàn thiện và thông tin liên hệ.
- [ ] Runtime gate AutoCAD 2023 PASS trước khi phát hành installer.
- [ ] Kiểm thử file nhỏ, file mỏ thực tế lớn, dữ liệu bẩn, xuất Excel, đóng/mở lại project.

## Quy tắc tính khối lượng V1.0
Áp dụng độc lập cho đào và đắp giữa hai mặt cắt liền kề, với F1/F2 là diện tích ở hai đầu và L là khoảng cách:

1. Một đầu bằng 0, đầu kia > 0: hình chóp
   V = L/3 × (F1 + F2)

2. Hai đầu > 0 và mức chênh tương đối |F1-F2| / max(F1,F2) <= 40%:
   trung bình diện tích hai đầu
   V = L/2 × (F1 + F2)

3. Hai đầu > 0 và mức chênh tương đối > 40%:
   hình chóp cụt
   V = L/3 × (F1 + F2 + sqrt(F1×F2))

Ngưỡng 40% là tham số nghiệp vụ của V1.0, không phải số liệu cứng trong từng dòng. Excel phải xuất công thức theo tham chiếu ô để khi sửa F1/F2/L thì khối lượng tự tính lại.

## Tương thích AutoCAD
V1.0 chỉ được tuyên bố hỗ trợ phiên bản AutoCAD nào đã có build + runtime test thực tế. Mục tiêu kiến trúc là dùng shared core + adapter theo phiên bản để mở rộng xuống AutoCAD 2012+, nhưng không ghi quảng cáo hỗ trợ 2012+ trước khi từng adapter/build được kiểm thử.

## Thông tin cố định
Phát triển: Bùi Thế Nam
Điện thoại: 0967280686
