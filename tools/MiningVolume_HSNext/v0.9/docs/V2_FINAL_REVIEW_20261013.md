# Rà soát bổ sung 13/10/2026 — TIN từng lượt, dữ liệu nhiều Z và tương phản

Baseline `a7f879ae9b736c890bbbfb679d371f0ab675fbe8`, nhánh `miningvolume-v2-chatgpt-p0-continuation`. Không sửa V1. Đây là hồ sơ QA, **không phải nghiệm thu AutoCAD**.

## Quyết định mới của người dùng

- Bỏ nút tạo hai TIN ở cả trang Dữ liệu và Mô hình. Trang Dữ liệu có **TẠO TIN HIỆN TRẠNG / TẠO TIN THIẾT KẾ**; trang Mô hình chỉ dựng vai trò đang chọn. Hai lệnh không yêu cầu nguồn còn lại đã nạp. Mỗi lượt có progress/cancel; TIN còn lại không bị thay thế.
- Cùng XY khác Z vẫn tự tạo support TIN và mặt đứng, giữ dữ liệu/CAD gốc, chỉ cảnh báo số nhóm/đỉnh trùng và yêu cầu xem/chỉnh mặt cắt trước khối lượng. Không tự mở hộp yêu cầu loại điểm; không thay bằng Z trung bình. Strict core và mean-Z thử nghiệm vẫn riêng biệt.
- Tham khảo luồng HSMo do người dùng mô tả, không tuyên bố sao chép hay tương đương thuật toán nội bộ HSMo. Trang công bố chính thức https://harmonysoft.com.vn/san-pham/hsmo/ không công bố thuật toán giải xung đột XY/Z hoặc bộ thông số giao diện để kiểm chứng tương đương.

## Lỗi tìm thấy và sửa trong lượt này

1. ModelPage tạo cặp thiếu token hủy/guard đổi DWG: bỏ luồng nút cặp theo yêu cầu mới, giữ guard/hủy/giải phóng store ở luồng đơn.
2. DrawTinPair cũ có thể xóa cả hai TIN sau khi ghi TIN thứ hai lỗi. API nội bộ còn phục vụ mở lại project: thay bằng một transaction ghi cả hai, chỉ nhận store mới sau commit. Bổ sung ca MVSELFTEST cố tình lỗi mặt thứ hai và xác nhận hai layer cũ còn nguyên. **Ca runtime này chưa chạy trên AutoCAD.**
3. DataPage có thể giữ store tạm sau hủy/lỗi thiết kế: luồng đơn luôn giải phóng store chưa được nhận. Hai trang dùng chung lease để không khởi chạy đồng thời hai lượt dựng chính thức.
4. Chuẩn hóa điểm tạo chuỗi khóa XY cho từng ô lân cận qua nhiều triệu phép tra cứu. Chuyển sang khóa value-type gồm đúng hai số nguyên; giữ thứ tự lân cận, dung sai, thứ tự chọn canonical và chính sách Z. Thêm test tọa độ 0/+2.500.000/−2.500.000 và điểm gần nhau qua ô.
5. Progress cập nhật chuẩn hóa theo đợt; ghi CAD vẫn kiểm Esc theo chunk, chỉ repaint tối đa 4 lần/giây. Không bơm DoEvents trong transaction.
6. Nút phụ quá nhạt: nền xanh xám rõ, viền đậm, chữ đậm; nút chính xanh/chữ trắng, Loại/Hủy phân biệt đỏ. Tắt UseVisualStyleBackColor để Windows không thay nền đã chọn. Giữ bố cục dọc đã chốt, không đổi thành giao diện mới.

## Đối chứng đã chạy

| Ca | Trước | Sau | Kết quả |
|---|---:|---:|---|
| SQ HT toàn fixture, core/native/file-backed + kiểm mọi support face | 76,024 s | 63,246 s | 1.681.755 support faces, 7.818 vertical faces giữ nguyên |
| SQ HT diện tích chiếu tổng | 3.622.143,635797113 m² | 3.622.143,635797113 m² | Không đổi; 14 face nhỏ dương vẫn giữ |
| SQ TK core/native/shared validation | — | 62,687 s | 1.672.875 support, 7.681 vertical; diện tích 3.600.883,873284891 m² |
| C# production regression | — | 90 PASS / 0 FAIL | Bao gồm mới: normalizer tại tọa độ mỏ dương/âm |
| Python reference/static | — | 253 PASS / 0 FAIL | Guard legacy nút cặp cập nhật theo yêu cầu bỏ nút; không xóa test để che lỗi |

Các thời gian là một lượt đo trên máy Linux, không gồm Windows/AutoCAD rendering, không cam kết tốc độ mọi máy. HT giảm 16,81% ở lượt đối chiếu này; managedBytes cuối lượt có dao động và không phải peak RSS, **không tuyên bố RAM giảm**. Core vẫn giữ cảnh báo đồng mức mâu thuẫn và nới ràng buộc support; chưa chứng minh tương đương HSMo hoặc đúng khối lượng khảo sát thật. Dữ liệu đầu vào ở đây qua adapter DXF, không phải Autodesk DWG reader.

CI mới bổ sung probe Windows: nạp UiTheme thật từ DLL vừa build, dựng WinForms Buttons, đo foreground/background tối thiểu 4,5:1 và xuất PNG/JSON. Kết quả probe chỉ có giá trị khi CI chạy PASS; **không thay nghiệm thu docking/DPI/hover/focus trong AutoCAD**.

## Gate còn mở

AutoCAD 2023 đọc CAD thật → render 3DFACE → bật/tắt → save/reopen → mặt cắt grip/update → CAD/Excel/khối lượng: **NOT TESTED**. Khóa RSA sản xuất **BLOCKED**; Setup unsigned; Windows cài/gỡ tương tác và Excel Desktop **NOT TESTED**. Kiểm SHA DLL trong Setup không chứng minh máy người dùng đã nạp đúng DLL: cần MVBUILDINFO trên máy đó. TIN lớn vẫn dùng CAD preview giảm số mặt hiển thị, trong khi lõi tính toán dùng mesh đầy đủ; không lấy preview làm bằng chứng độ phủ toàn mesh.

Chỉ bàn giao QA Setup sau Windows CI cuối, kiểm payload/DLL/checksum và scan; thông tin SHA/run/hash cuối nằm trong FINAL_BUILD_EVIDENCE.json cùng gói. Không phát hành V2.0 chính thức.
