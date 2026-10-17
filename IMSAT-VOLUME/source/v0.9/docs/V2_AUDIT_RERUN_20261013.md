# IMSAT MiningVolume V2.0 — rà soát tổng thể bổ sung 13/10/2026

**Nhánh:** `miningvolume-v2-chatgpt-p0-continuation` (không sửa nhánh V1.0).  
**Phạm vi:** thuật toán TIN, nguồn/đỉnh trùng XY-Z, breakline, trình vẽ CAD, mặt cắt chỉnh tay, khối lượng, Excel, giao diện và đóng gói.  
**Trạng thái:** QA CANDIDATE / NOT RELEASE. Không thay thế nghiệm thu AutoCAD 2023 trực tiếp.

## Phát hiện mới và sửa trong lượt rà soát

| Mã | Phát hiện / rủi ro | Thay đổi | Kiểm chứng |
|---|---|---|---|
| AUD-23 | Xuất hệ mặt cắt theo 20 mặt/lượt với transaction riêng: lỗi giữa chừng có thể xóa bản cũ và chỉ ghi một phần bản mới | Bọc toàn bộ các transaction lô vào một giao dịch AutoCAD ngoài cùng; commit chỉ sau khi vẽ đủ mọi lô | Static regression, Windows build; **thử rollback thực trên AutoCAD: NOT TESTED** |
| AUD-24 | `Application.DoEvents()` trong lúc còn CAD transaction/DocumentLock cho phép event UI chen vào, rủi ro thay đổi DWG/dữ liệu | Bỏ DoEvents ở callback vẽ mặt cắt, vẫn cập nhật chữ trạng thái bằng Refresh | Static guard; **phản hồi UI host: NOT TESTED** |
| AUD-25 | Fallback chuẩn hóa breakline xóa các `ValidationSeverity.Error` dù không xác định được cạnh xung đột (unsafeEdges=0) | Giữ lỗi chặn khi không có đoạn vi phạm được chứng minh; không tự gỡ lỗi chỉ để xuất TIN | Thêm C# regression cho xung đột không xác định, Python static guard |

## Những phần đã kiểm tra từ vòng trước

- Xây TIN HT/TK từng lượt, giữ nguyên nguồn AutoCAD; không bắt loại đỉnh thủ công cho nhóm nhiều Z.
- Không dùng lưới CAD rút gọn 200.000 mặt. Trên nhánh hiện tại, TIN chính thức ghi toàn bộ tam giác support hợp lệ và mặt đứng, vẫn **chưa đo tốc độ/bộ nhớ AutoCAD thực tế**.
- Chế độ fallback nhiều Z giữ ràng buộc không xung đột, có cảnh báo số ràng buộc bị nới. Nếu topology chưa được chứng minh an toàn phải fail closed.
- Đồng bộ lại layer TIN cũ không hủy hệ mặt cắt/khối lượng đã tính.
- Core các phương pháp thể tích, theo tầng, grid, khoáng sản và công thức Excel có kiểm thử tự động.
- UI nút bấm Windows đã có kiểm tra ảnh thực không trắng; không đồng nghĩa đã kiểm tất cả Palette/DPI.

## Hạn chế còn BLOCKED / NOT TESTED

1. Dựng trực tiếp HT 1.681.755 mặt và TK 1.672.875 mặt trên AutoCAD 2023, đo RAM, thời gian, Esc/rollback, đối chiếu HSMo; cần nguồn và host thật.
2. Kiểm tra giao dịch lồng nhau và rollback hệ mặt cắt khi lỗi ở lô thứ hai; xóa/sửa/copy polyline, lưu và mở lại DWG.
3. Thực nghiệm Excel Desktop: sửa tham số đầu vào, công thức cập nhật, in biểu và logo.
4. Windows interactive: cài, nâng cấp, gỡ và tự nạp; giấy phép RSA sản xuất chưa provision, bộ cài chưa ký số.
5. AutoCAD 2012 trở lên chưa có binary hỗ trợ; QA hiện chỉ AutoCAD 2023 x64.
6. Vùng biên nhiều vòng/có lỗ chưa hỗ trợ đầy đủ; chưa được cho qua nghiệm thu chức năng này.
7. Toàn bộ ca nghiệp vụ T01–T22 và V-01–V-05 cần ảnh/log và ký xác nhận trên AutoCAD host.

**Quy tắc bàn giao:** Chỉ chia sẻ QA Setup khi CI đúng commit cuối PASS và hash/scan hoàn chỉnh. Không gọi đây là bản phát hành chính thức, không suy diễn CI xanh thành nghiệm thu AutoCAD.
