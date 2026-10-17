# IMSAT SLOP — trạng thái khôi phục nguồn trên GitHub

**Ngày:** 2026-10-13  
**Nhánh:** `imsat-slop-v1.0-work`  
**Nền mã nguồn:** IMSAT Slope Stability Professional V6.2 (CAD + Computation Fix)  
**Commit phục hồi đầu tiên:** `0efbae935afeedafb5a08b3ebc2d876cbc92d033`.

## Đã đưa lên GitHub
- 68 tệp: `app/` (core/algorithms/io/ui/reports), `main.py`, các scripts và requirements, `tests/`, các kết quả kiểm thử JSON, logo IMSAT (.ico/.png), hình kiểm thử (.png), `PLANCODE_WORK.md`, các tài liệu nguồn và manifest.
- Duy trì nguyên đường dẫn tương đối của mã nguồn V6.2, nằm dưới `products/imsat-slop/`.
- `PLANCODE_WORK.md` quy định rõ **phải kế thừa và hiệu chỉnh**; không được khởi tạo project mới.

## Dữ liệu giữ ngoài GitHub công khai
Các tệp `.gsz` ví dụ gốc trong `examples/` chưa được commit do chưa xác nhận quyền chia sẻ dữ liệu địa chất/mặt cắt. Bộ nguồn ZIP V6.2 đầy đủ đã được khôi phục riêng và bàn giao qua tệp đính kèm; Work phải tiếp nhận và dùng làm fixtures cục bộ trong quá trình kiểm thử. Không mở public nếu chưa xác minh quyền.
 
Các ZIP V6.0 và V5.4.1 chỉ dùng tham chiếu lịch sử, không ghi đè chức năng hiện tại; chúng được bàn giao trong gói `IMSAT_SLOP_WORK_HANDOFF_V6_2.zip`.

## Trạng thái kiểm thử kiểm chứng trong phiên này
- Python `compileall app main.py`: **PASS** (môi trường hiện tại).
- Chạy `PYTHONPATH=. python tests/test_geometry_core.py` với fixtures trong ZIP: **PASS**.
- Các tập kết quả JSON trong V6.2 là **báo cáo lịch sử**, không được coi là đã kiểm thử lại phiên hiện tại.
- Solver bốn phương pháp, UI Windows, tạo EXE, nghiệm thu runtime: **NOT_VERIFIED** trong phiên này.

## Mệnh lệnh cho Work
1. Làm trực tiếp trên commit/nhánh này và thư mục `products/imsat-slop/`.
2. Nếu thiếu `.gsz` khi chạy các test cần dữ liệu mẫu, lấy từ gói bàn giao đầy đủ; không viết code giả/fixtures tùy tiện để test PASS.
3. Đối chiếu `SOURCE_MANIFEST.json` và hai ZIP lịch sử, lập danh sách từng chức năng kế thừa trước khi sửa.
4. Thực hiện tuần tự `PLANCODE_WORK.md` từ P0 đến P8. Chỉ nghiệm thu khi có bằng chứng thực.
5. Không sửa hoặc ảnh hưởng các sản phẩm khác trong cùng repository.
