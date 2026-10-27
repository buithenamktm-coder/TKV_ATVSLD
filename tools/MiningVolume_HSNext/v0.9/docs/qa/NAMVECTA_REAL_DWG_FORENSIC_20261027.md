# NAMVECTA VOLUME 2.0 — audit tình trạng khôi phục TIN DWG 2026-10-27

Trạng thái: **chưa nghiệm thu trên bản vẽ gốc / không phát hành EXE**.

Nguồn: báo cáo chỉ đọc do AutoCAD sinh `NAMVECTA_DWG_AUDIT_20261027_18413300.txt`, người dùng cung cấp.
Không đưa tệp DWG, tọa độ chính xác hoặc dữ liệu khai thác vào repository công khai.

- 973.860 3DFACE; TIN hiện trạng 490.174; TIN thiết kế 483.686.
- 4 mặt co về điểm theo ngưỡng cạnh lớn nhất <= 1e-9 m (Handle: F5EDF, F6B48, F6978, 130113).
- 12.441 mặt khác có diện tích XY <= 1e-12 m²: có thể bao gồm mặt đứng, mặt rất mỏng và các dạng sai khác; không được xóa tự động.
- Mặt có diện tích XY > 1e-12 m²: 961.415.
- 43 đường mặt cắt HT và 43 đường mặt cắt TK còn trong DWG.
- Vùng BF9BD có các mặt đứng với các cao độ rất khác nhau tại cùng XY. Chưa xác minh được điều kiện tính duy nhất Z và vùng phủ.

Biện pháp trong mã nguồn:
1. Tách 3DFACE co thành điểm (đủ ba cạnh 3D <= 1e-9 m) ra khỏi TIN tính toán **chỉ nếu** tìm thấy một đỉnh thuộc tam giác XY dương được giữ lại và khớp cả ba đỉnh trong dung sai 3D <= 1e-9 m.
2. Nếu không chứng minh được đỉnh hỗ trợ, chặn phép tính và báo đúng Handle. Giữ nguyên mọi đối tượng trong DWG.
3. Tam giác có diện tích XY **dương nhưng <= 1e-12 m²** không được tự chuyển thành mặt đứng; giữ trong TIN và phát cảnh báo cần đánh giá vùng phủ/topology.
4. Mặt đứng XY bằng 0 vẫn không được đưa vào bộ lấy mẫu Z đơn trị.
5. Mọi phép tính khối lượng vẫn cần kiểm chứng vùng phủ, mặt cắt người dùng đã sửa và giá trị chuẩn trên DWG thật; các hồi quy mô phỏng PASS không đủ để đánh dấu nghiệm thu.

**Chưa chứng minh** ba mặt co điểm còn lại có đỉnh hỗ trợ, chưa kiểm chứng vùng phủ, Z đơn trị của các vùng khác, toàn bộ 43 cặp mặt cắt, và chưa có nghiệm thu trực tiếp trên DWG người dùng.
