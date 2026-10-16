# IMSAT CAD — quyết định kiến trúc và bản quyền (ADR-001)

**Trạng thái:** CHỐT cho V1.0 (2026-10-17).  
**Mục tiêu:** CAD độc lập Windows x64, lệnh tương ứng quy trình AutoCAD, song ngữ Việt/Anh, logo IMSAT, có thể thương mại hóa và giữ riêng tài sản trí tuệ IMSAT.

## Quyết định
1. **QCAD Community Edition** là ứng viên lõi 2D ưu tiên. Dùng mã nguồn QCAD đúng giấy phép GPLv3 + ngoại lệ plugin chính thức, không tự xóa giấy phép, copyright, tác giả và các thành phần bên thứ ba. Giữ upstream ở commit/tag pin cụ thể; tránh sửa trực tiếp QCAD; mọi bản sửa trực tiếp phải được cung cấp mã nguồn tương ứng cho người nhận theo GPLv3.
2. **IMSAT CAD Modules** là các plugin/script **độc lập, không phái sinh từ nguồn QCAD**, được thiết kế qua API extension công khai. Được phép lựa chọn giấy phép thương mại theo điều khoản ngoại lệ QCAD; tuyệt đối không chép nguyên mã QCAD vào phần plugin độc quyền và gọi đó là mã IMSAT.
3. **DWG**: chỉ phân phối nếu có quyền hợp pháp. Phương án ưu tiên khi phát hành V1.0 hỗ trợ DWG là giấy phép **resale/OEM có xác nhận bằng văn bản từ QCAD/RibbonSoft** cho thành phần QCAD Professional. Trong khi chưa có quyền phân phối, chạy thử với QCAD CE/DXF; hoặc để khách hàng tự mua QCAD Professional và cung cấp plugin IMSAT riêng. **Không tự đưa DLL QCAD Professional/ODA vào installer**.
4. **Phương án dự phòng**: đánh giá ODA Drawings SDK nếu giá mua lại plugin DWG không đáp ứng nhu cầu thương mại hoặc thiếu API cần thiết. Cần phê duyệt chi phí và điều khoản trước khi nhúng SDK. ODA là khoản phí định kỳ, không tự thanh toán.
5. **Mã nguồn thương mại**: repository `buithenamktm-coder/TKV_ATVSLD` hiện là **PUBLIC**. Chỉ để mã lõi open-source, bộ dựng, dữ liệu test phi mật, các plugin mẫu không độc quyền trong repo này. Không đẩy thuật toán/trình cấp phép/khóa bí mật IMSAT lên repo public. Tạo repo riêng **private** cho plugin độc quyền khi có quyền tạo repo; trước thời điểm đó giữ tính năng riêng ở mức mẫu thử công khai.
6. **Bản quyền theo máy** chỉ dùng cho plugin IMSAT proprietary; không giới hạn quyền sử dụng/phân phối sửa đổi lõi GPL của QCAD. Bộ cài phải thể hiện rõ phiên bản và giấy phép của từng thành phần.
7. **Nhận diện thương hiệu**: yêu cầu logo IMSAT chính thức do chủ thể thương hiệu cho phép; ảnh biểu tượng dựng từ file cũ chỉ là tạm thời, không được mặc nhiên coi là logo pháp nhân.
8. **Ngôn ngữ**: hai lựa chọn ngay trong ứng dụng: Tiếng Việt / English; câu lệnh AutoCAD chuẩn không dịch alias (LINE, L, PLINE, PL,...), nhưng dịch prompt, menu, dialog, help.

## Ranh giới mã nguồn

| Thành phần | Quyền công bố | Định hướng |
| --- | --- | --- |
| QCAD Community + sửa đổi trực tiếp (nếu có) | GPLv3 / ngoại lệ theo bản upstream; thực hiện nghĩa vụ cung cấp mã nguồn | Công khai mã nguồn có thay đổi |
| QCAD plugin/Script mẫu không độc quyền | MIT hoặc GPL tùy mã và quyền | Có thể lưu repository hiện tại |
| IMSAT CAD proprietary plugin phát triển độc lập | Giấy phép IMSAT (sau khi rà soát) | Private; phân phối binary riêng |
| QCAD Professional / DLL DWG | Quyền do QCAD/RibbonSoft cấp, có thể cần mua license resale | Không phân phối trước khi ký hợp đồng |
| VTK / thư viện bổ trợ | Theo giấy phép từng phần | Lưu notices đầy đủ |

## Điều kiện bắt buộc trước bộ cài thương mại

- [ ] QCAD C++/Qt build Windows thành công và chạy thử giao diện thực, không chỉ unit tests.
- [ ] Ma trận lệnh theo `V1_RELEASE_GATES.md` đạt PASS với dữ liệu thiết kế chuẩn.
- [ ] DXF read/write không mất dữ liệu; DWG read/write đạt nghiệm thu riêng.
- [ ] Có bằng chứng quyền phân phối plugin DWG thương mại trước khi ship tính năng DWG.
- [ ] Có mã nguồn QCAD tương ứng và notice GPL/exceptions đi kèm bộ cài.
- [ ] Có xác nhận quyền sử dụng logo IMSAT cho mục đích thương mại.
- [ ] Repo plugin sở hữu riêng được bảo vệ; không đưa bí mật/thuật toán vào repo công khai.
- [ ] Cài/gỡ sạch trên Windows 10/11 x64, giao diện VI/EN và tất cả lệnh CAD đúng ý nghĩa.
- [ ] Hiệu năng và so sánh đối chiếu tệp mẫu AutoCAD đạt ngưỡng nghiệm thu.

## Điều kiện không được diễn giải sai

- Quyền phát triển plugin đóng mã theo ngoại lệ QCAD **không có nghĩa** được đóng mã phần lõi QCAD đã sửa.
- GPLv3 + ngoại lệ là cơ sở pháp lý cho mô hình này, nhưng từng plugin/SDK phụ thuộc vẫn cần kiểm tra license.
- Có thể thương mại hóa phần mềm đóng gói QCAD CE + plugin độc lập; **không** có quyền phân phối QCAD Professional theo một giấy phép đơn lẻ.
- Không được gắn nhãn V1.0 ổn định khi native QCAD chưa build, chưa chạy và chưa kiểm thử đường đi phát hành.

## Nguồn chính thức

- QCAD license: https://www.qcad.org/en/license/
- QCAD exception: https://github.com/qcad/qcad/blob/master/gpl-3.0-exceptions.txt
- QCAD original LICENSE: https://github.com/qcad/qcad/blob/master/LICENSE.txt
- ODA membership: https://www.opendesign.com/oda-membership
- ODA pricing: https://www.opendesign.com/pricing

**Quyết định mua sắm:** chưa chi tiền hoặc yêu cầu thành viên trả tiền, sẽ chỉ trình phê duyệt nếu bắt buộc cho giấy phép DWG.
