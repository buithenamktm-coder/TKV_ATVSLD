# Checklist nghiệm thu tương tác AutoCAD 2023 — CHƯA CHẠY

Dùng Windows x64 + AutoCAD 2023 có bản quyền, phiên tương tác. Sao chép DWG/DXF mỏ sang thư mục thử riêng, giữ nguyên bản gốc. Ghi CPU/RAM, DPI, bản Windows/CAD, commit và SHA256 Setup từ artifact.

1. Cài Setup QA đúng HEAD; xác nhận logo, tên V2 QA, không báo là phát hành chính thức. Khởi động AutoCAD, kiểm tra autoload. Chạy `MVOPEN`, `MV_TOGGLE`, `MV_MODEL`, `MVSELFTEST`; lưu log command và kết quả self-test. Self-test không thay checklist này.
2. Vỉa 4: HT=`ht`, TK=`- nam4`, đường bao=`LO_TINHKL`. Chọn đủ các loại nguồn hỗ trợ, ghi số đối tượng/đỉnh; đối chiếu CAD gốc. SQ/Test HS dùng !HT/!TK; chọn đường bao rõ ràng. Không coi CIRCLE/SPLINE là đã hỗ trợ nếu chưa có adapter.
3. Mở bảng điểm trùng: mặc định chỉ ΔZ>3; tắt checkbox phải thấy nhóm nhỏ hơn. Lọc một layer vẫn thấy toàn bộ nguồn cùng XY; hàng nhóm không có nút sửa/xóa. Ở 1366×768 phải thấy ba nút Zoom/Loại/Sửa.
4. Zoom mỗi điểm tới đúng XY, có thể xem đối tượng gốc. Điểm giao/nội suy không có đỉnh nguồn chắc chắn phải khóa sửa/xóa. Không dùng hình ảnh Zoom toàn polyline thay bằng chứng đúng điểm.
5. Loại điểm: đúng câu hỏi đã chốt, Đồng ý/Không, Enter và Esc mặc định Không. Thử Không, đóng hộp thoại, Đồng ý; chỉ Đồng ý làm tắt đúng một đỉnh mô hình. Kiểm tra CAD nguồn không thay đổi, các đỉnh khác còn nguyên.
6. Sửa Z: kiểm tra Z cũ/mới/source; Cancel không sửa; xác nhận sửa thì quét lại, TIN/mặt cắt/khối lượng cũ hết hiệu lực. Thử khôi phục một điểm, nhiều điểm, tất cả; nhật ký phải có old/new/time/handle/index.
7. Nhóm Z=0 và 94.862 hoặc 0/20/94.862 phải chặn preview; đúng 3.000 không vượt gate thử, 3.001 phải chặn. TIN chính thức vẫn chặn sai khác >dung sai, không coi 3m là dung sai chính thức.
8. Xử lý nguồn theo quyết định khảo sát có ghi nhận, không tự sửa chỉ để hết lỗi. Dựng TIN HT/TK chính thức; đo diện tích/biên/Z/constraints bằng nguồn độc lập. Preview chỉ ở MV_TIN_ZMEAN_HT_TEST_ONLY/TK_TEST_ONLY và không thay TIN chính thức. Ghi số tam giác thực và số mặt preview.
9. Hủy quét/đổi DWG/đóng cửa sổ lúc quét; không chỉnh sửa được qua báo cáo cũ. Thử dữ liệu >1.293 nhóm và ~1 triệu đỉnh, ghi thời gian/peak RAM/UI responsiveness, không chỉ ghi managed memory.
10. `MV_PROJECT_SAVE`, lưu DWG, đóng/mở, `MV_PROJECT_LOAD`: giữ loại/sửa/audit; thử thay đổi hình học nguồn trước nạp: phải cảnh báo, không áp chỉ số cũ. Kiểm tra hai model độc lập.
11. `MV_VIEW3D_HT`, `MV_VIEW3D_TK`: orbit/pan, Esc, command failed/cancel, chuyển/đóng DWG; giao diện phải khôi phục và layer người dùng không bị xóa.
12. `MV_SECTION`: MC cách 20m; thay khoảng giãn bố trí không thay cự ly tính. Đối chiếu Z/hình dạng HS_MATCAT với nguồn có xác nhận, không suy đoán TIN HSMo từ custom object.
13. `MV_GRID`: ô xoay, biên, đào/đắp hỗn hợp, vùng thiếu TIN phải báo no-data. `MV_VOLUME`: đối chiếu độc lập công thức/đơn vị/tầng; nghiệm thu điều kiện TT34 bởi người phụ trách.
14. `MV_MINERAL`: ít nhất 3 vỉa, gán HT/TK, sửa Grip vùng trên mặt cắt, cập nhật; chặn chồng lấn/vượt phần đào, thiếu dữ liệu không bằng zero; kiểm tra m³/tấn/hệ số bóc.
15. `MV_EXPORT`: CAD TABLE native, Excel và cả hai; Excel desktop tính lại công thức khi sửa tham số; Arial12/viền/không tô/logo/tên liên hệ đúng, không tràn chữ.
16. `MV_LICENSE`: 15 ngày, hết hạn không mất DWG, rollback/corrupt ledger; provision public key thực, kích hoạt đúng/sai mã máy/tamper. Máy quản trị thứ hai khôi phục encrypted issuer backup; không đưa private key vào artifact.
17. Cài nâng cấp V1→V2, lỗi cài đặt/rollback, gỡ; kiểm tra DWG/license khách hàng còn nguyên. Test mở DWG sau gỡ. Không công bố hỗ trợ AutoCAD 2012+.

Mỗi ca ghi Expected, Actual, PASS/FAIL/NOT TESTED, ảnh/log, commit, máy chạy và người xác nhận. Mọi mục trên hiện **NOT TESTED trong AutoCAD**; yêu cầu người nghiệm thu ký duyệt trước Release.

Verifier tự động có sẵn: `release/verify_autocad2023_runtime.ps1` (đọc trước khi chạy; cần Administrator, phiên Windows tương tác và AutoCAD 2023). Verifier chỉ là một phần bằng chứng, không tự PASS các thao tác UI/mỏ/bản quyền ở trên.
