# PLANCODE BÀN GIAO WORK — NAMVECTA VOLUME 2.0
Ngày lập: 24/10/2026 | Trạng thái: ĐANG PHÁT TRIỂN / CHƯA NGHIỆM THU

## 0. Nguồn và nguyên tắc
- Repository: `buithenamktm-coder/TKV_ATVSLD`
- Nhánh nguồn kế thừa: `miningvolume-v2-chatgpt-p0-continuation`
- Nhánh chỉnh sửa, giao Work rà soát: `namvecta-volume-v2-streamline-20261024`
- Mốc tách nhánh: `3bd03351f83a417567c48e6cc99f9de6906c4ba0`
- Thư mục duy nhất: `tools/MiningVolume_HSNext/v0.9/`. KHÔNG tạo lại dự án hay mất các chức năng đã xây dựng.
- Tuyệt đối KHÔNG sửa/xóa/force-push `miningvolume-v1.0`, không tự merge nhánh khác.
- Bộ nhận diện bắt buộc: tệp Word do chủ sản phẩm cung cấp `NAMVECTA_Bo_nhan_dien_va_Master_Prompt_Chuyen_thuong_hieu.docx`; logo PA3 nhúng trong tệp là nguồn mẫu chính thức. Không tự vẽ lại bằng suy đoán, không lấy hình tìm kiếm web và không sử dụng IMSAT logo cũ.
- Tài liệu này chỉ định nhiệm vụ triển khai + rà soát; các commit sửa sơ bộ KHÔNG đồng nghĩa build/kiểm thử trong AutoCAD đã đạt.

## 1. Mức độ đã sửa sơ bộ trên nhánh
1. `MainPaletteControl.cs`: điều hướng chính thu gọn còn **Dữ liệu → Tính toán → Kết quả**, giữ công cụ cũ trong **Tùy chọn**; tài nguyên logo IMSAT ở header đã tạm ngưng tải để tránh trộn thương hiệu. Cần hoàn thiện đúng logo PA3 và test UI host.
2. `ExportWorkflowService.cs`, `ExportPage.cs`: mặc định chỉ chọn tổng hợp + chi tiết mặt cắt, không tự thêm sheet XYZ, hình học, cảnh báo; đổi tiêu đề và ngừng chèn logo IMSAT cũ. **Khi có dải cao độ/tầng**, công thức chi tiết đang tham chiếu sheet `Chi tiết tầng` bắt buộc; có thể xuất 3 sheet cho tới khi hợp nhất được mà không làm sai công thức. Work phải chứng minh tính đúng và tối ưu để chỉ còn 2 sheet mặc định khi khả thi.
3. `LicenseService.cs`, `LicensePage.cs`: đã chuyển thí điểm từ RSA MachineId / file .mvlic sang 5 Pass offline đã được chốt. Mã nguồn chỉ lưu SHA-256 của Pass; không nhúng chuỗi Pass thuần văn bản trong repo. Thời hạn 1, 3, 6, 12 tháng và vĩnh viễn. Ngày hết hạn cộng theo tháng lịch từ lần kích hoạt đầu tiên từng Pass; nhập lại không tự gia hạn; dùng thử 15 ngày. Giữ tính năng không xóa DWG nếu hết hạn. **Cần review toàn bộ cài đặt cũ, các tests RSA cũ, trạng thái khi đổi mật khẩu, rollback thời gian, an toàn lưu cục bộ, điều kiện chuyển dữ liệu; không tự tuyên bố bảo mật tuyệt đối.**
4. `ContactInfoPage.cs`: đổi tên hiển thị; các tên IMSAT còn trong assembly, installer, resources và trang chức năng khác **CHƯA chuyển hết**.

## 2. Mục tiêu tổng thể bắt buộc
### A. Nghiệp vụ 3 bước, thao tác tối thiểu
- Bước 1: Chọn dữ liệu hiện trạng, thiết kế, đường bao; tự phát hiện Point/Polyline/Contour/Breakline; dựng hai TIN độc lập. Giữ kết quả TIN dùng lại, không dựng lại khi chuyển phương pháp.
- Bước 2: Chọn **mặt cắt** hoặc **lưới ô vuông**, chỉ hiện tham số thực sự cần. Mặt cắt theo hướng 1→2, xem trước tuyến bình đồ, sửa đường mặt cắt ngay trên CAD, bấm **Cập nhật mặt cắt** và tính lại phần bị ảnh hưởng. Lưới nhập bước ô, vị trí gốc/hướng; lấy dữ liệu từ TIN đã chấp nhận.
- Bước 3: Xem tổng đào/đắp; xuất CAD và Excel. Các tùy chọn hiếm dùng trong **Tùy chọn**, giữ lệnh CAD cũ để tương thích.
- Dock trái, giao diện chuẩn 320–350 px có thể chỉnh rộng; nút 30–34 px, font 12–14 px tương ứng DPI; nút bo nhẹ, không viết hoa tất cả, tương phản dễ đọc. Palette tự ẩn khi chọn đối tượng theo hành vi đã chốt.
- Không bắt người dùng chọn lại TIN/vùng/tuyến khi dữ liệu còn hiệu lực. Không bỏ chức năng vẽ mặt cắt nhanh, 3D, xóa TIN, multi-seam hiện có; đặt chúng trong menu thích hợp.

### B. Lõi tính toán
- Chỉ hỗ trợ **AutoCAD full 64-bit 2020–2024** trong đợt này; lớp adapter API 2020 và 2021–2024 theo kết quả khảo sát thực tế SDK/runtime, chung 1 lõi hình học và tính toán. KHÔNG công bố hỗ trợ 2025–2026 trong bản V2.0.
- Không làm lại lõi; sửa lỗi TIN suy biến, breakline giao nhau, trùng XY khác Z, mặt đứng 3DFACE; không tự gộp/loại điểm nguồn không được cho phép. Cảnh báo đủ số nhóm điểm nhưng vẫn dựng TIN khi hình học hợp lệ theo cơ chế đã chốt.
- Giữ đường bao TIN, không vẽ lan khỏi ranh. Đối với vùng nhiều giá trị Z và mặt đứng, không được áp dụng công thức 2,5D sai; yêu cầu người dùng rà soát hình học mặt cắt trước nghiệm thu thể tích.
- Khắc phục cảm giác treo khi tạo TIN triệu tam giác, dựng/xuất mặt cắt, xem 3D, tính lưới và Excel. Tiến độ, nút Hủy an toàn, không dùng API AutoCAD từ worker thread không có quyền.
- Mọi phép tính theo mặt cắt và lưới giữ phương pháp và công thức đã chốt, kiểm định bằng fixture độc lập. Thao tác Cancel/Undo không để lại CAD block, file Excel tạm hay tình trạng dữ liệu sai.
- Lưu phục hồi theo bản vẽ/DWG, revision và fingerprint; mở lại nhận TIN/mặt cắt/lưới/khối lượng. Khi dữ liệu thay đổi phải vô hiệu hóa đúng kết quả dẫn xuất.

### C. Excel / CAD kết quả
- Mặc định **01 Tổng hợp** và **02 Chi tiết**, không có sheet/cột thừa; ngoại lệ sheet kỹ thuật khi thật sự bắt buộc để duy trì công thức tầng/level phải có lý do và kiểm thử rõ.
- Bảng tổng hợp giữ đơn vị, công trình/kỳ tháng-quý-năm, người tính, người kiểm tra, tổng đào/đắp, thời điểm và mã phiên tính.
- Chi tiết có đủ L, F đầu-cuối và công thức/chênh kiểm tra để thẩm tra; không giảm cột đến mức không thể kiểm toán.
- Giữ công thức Excel thực, liên kết sheet không bị #REF!, kiểm tra calc/recalc bằng công cụ độc lập; không âm thầm thay công thức bằng giá trị cố định. Arial, không tô nền, bảng đóng khung, đơn vị m/m²/m³ và vùng in được thiết lập.
- Xuất bảng CAD/face/polyline/block nhẹ, không phình DWG; giữ block + native TABLE và tính sửa đổi/hủy vẽ.
- Đổi logo và tên trong template báo cáo; tuyệt đối không hiển thị hình IMSAT trên NAMVECTA report; không gỡ thông báo license bên thứ ba bắt buộc.

### D. Bản quyền: KHÔNG SỬA NGUYÊN TẮC
- Chính xác **5 Pass cố định**, 1 tháng / 3 tháng / 6 tháng / 1 năm / vĩnh viễn; 15 ngày dùng thử; không bắt Internet, tài khoản, máy chủ, MachineId, file license, RSA signing service, hay activation online.
- Chỉ nhập Pass trong ứng dụng; lưu kích hoạt sau một lần thành công; nhập lại cùng Pass trong lần cài hiện tại không gia hạn. Mật khẩu có thể chia sẻ giữa máy: rủi ro này được chủ sản phẩm chấp nhận.
- Kiểm thử đồng hồ lùi, máy mất mạng, bản vẽ sau hết hạn, khởi động lại, cài đè, nhập sai/sai chữ hoa, chuyển các hạng giấy phép. Nếu xóa sạch mọi dữ liệu máy sẽ không có bảo đảm chống gian lận tuyệt đối; không tuyên bố quá mức.
- Có trang trạng thái giấy phép, ngày kích hoạt, hết hạn. Tác giả hiển thị Bùi Thế Nam, liên hệ 0967280686. Kiểm toán quyền sở hữu mã nguồn/tác quyền trước khi ghi Copyright/Publisher theo cách khẳng định sở hữu pháp lý.
- Công cụ `license-manager` RSA cũ KHÔNG được tiếp tục là phụ thuộc phát hành mặc định. Cần quyết định loại khỏi Setup và cập nhật CI/tests; có thể lưu mã lịch sử để không phá nhánh khác.

### E. Nhận diện PA3 và Setup
- BrandConfig độc lập; chuyển đổi logo, icon, Splash, Ribbon, Palette, About, Windows metadata, shortcut, Uninstall, tài liệu và Excel sang NAMVECTA. Logo phải đúng PA3 từ Word do chủ sản phẩm cung cấp. Asset vector sản xuất cần được đối chiếu/duyệt trước release.
- Trình cài `NAMVECTA-VOLUME-2.0-Setup.exe` có wizard tiếng Việt/Anh: Welcome; thông tin tác giả/phiên bản; điều kiện sử dụng; dò AutoCAD đã cài; chọn host/đường dẫn; bảng xác nhận; tiến độ; kết quả và hướng dẫn mở Ribbon/Palette. Cài đè/nâng cấp/gỡ rollback; không mất dữ liệu.
- AutoCAD .bundle / `PackageContents.xml` được xét theo adapter SDK thực tế, mỗi bản CAD chỉ load DLL tương thích; cài xong cần chứng minh lệnh/Ribbon/Palette chạy, không chỉ sao chép DLL.
- Tránh tự ghi đè Autodesk commands, phân phối font/logo vi phạm hoặc xóa thông báo pháp lý của các thành phần thư viện.

## 3. Work thực hiện tiếp theo (không dừng ở đề xuất)
1. Checkout nhánh `namvecta-volume-v2-streamline-20261024` và đọc diff từ mốc `3bd03351f83a417567c48e6cc99f9de6906c4ba0`. Chạy kiểm tra compile trên nguồn hiện tại **trước** khi thay đổi thêm.
2. Review tất cả 6 file đã sửa (tính đúng của lộ trình 3 bước, pass/trial, giữ công thức Excel); sửa lỗi compile/runtime và thêm regression tests. KHÔNG xóa những chức năng nền đã có.
3. Hoàn thiện toàn bộ A–E theo thứ tự chặn release: licensing và host compatibility → kết quả Excel/CAD → hiệu năng và persistence → brand/Setup → tích hợp.
4. Chạy reference/core/installer/unit tests và **MVSELFTEST PASS trên AutoCAD host**, thử T01–T22, benchmark dữ liệu lớn và đối chứng HSMo. Kiểm thử cài mới và nâng cấp trên **từng** AutoCAD 2020, 2021, 2022, 2023, 2024. Đánh dấu NOT TESTED / BLOCKED khi host chưa có.
5. Báo cáo số liệu hiệu năng trước/sau với dữ liệu và máy đo; đủ log screenshot, SHA256 của từng artifact. Chỉ tạo bản phát hành cuối sau khi đủ gate. Không gửi bản EXE cũ hoặc QA candidate làm bản mới.

## 4. Tiêu chuẩn nghiệm thu cứng
- **G1**: cùng HEAD mới, build sạch / tests pass / log rõ.
- **G2**: TIN HT/TK, ranh, breakline, multi-Z, lưu/mở DWG đúng.
- **G3**: mặt cắt (bao gồm chỉnh trên CAD), lưới, thể tích đào/đắp và Excel recalc đúng đối chứng.
- **G4**: UI 3 bước gọn và đúng thương hiệu PA3 (không còn hình IMSAT) sau host Windows.
- **G5**: Pass offline 5 mức + trial 15 ngày, Setup wizard, update/uninstall rollback thành công.
- **G6**: AutoCAD 2020–2024 độc lập; T01–T22; tài liệu/commit/Setup/hash/chứng cứ hoàn chỉnh.
- Mỗi gate phải là **PASS có bằng chứng / NOT TESTED / BLOCKED**; không gọi "100% xong" dựa vào compile CI.

## 5. Bàn giao
- Link commit/PR rõ ràng, danh mục thay đổi, test log, báo cáo đo, màn hình host, SHA256.
- Bộ cài cuối: `NAMVECTA-VOLUME-2.0-Setup.exe`; có mô tả rõ phiên bản AutoCAD đã chạy thực.
- Nếu không đủ môi trường AutoCAD trên Windows, chốt trạng thái "chưa nghiệm thu host" thay vì tạo bản phát hành giả.
