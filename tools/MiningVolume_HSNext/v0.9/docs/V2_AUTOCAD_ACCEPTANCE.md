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


18. **Chống mất sửa tay khi xuất lại MC:** sau khi đã Grip chỉnh đường HT hoặc TK nhưng CHƯA bấm CẬP NHẬT MẶT CẮT, bấm thành lập/xuất mặt cắt lần nữa: phải hiện hộp xác nhận thay thế; mặc định **Không**, Esc/Không giữ nguyên hình CAD, diện tích, khối lượng; chọn Đồng ý chỉ trên bản sao DWG rồi kiểm tra bản mới được vẽ đúng.
19. **Hủy chọn điểm chèn:** sau khi đã có bộ mặt cắt/chỉ tiêu đã tính, bấm thành lập mặt cắt mới rồi Cancel khi AutoCAD hỏi điểm chèn. Hệ MC có thẩm quyền và kết quả volume trong Palette/Excel phải còn nguyên. Thử vừa thay DPI/dock trái.
20. **DWG save/reopen bảo toàn sửa tay:** trên bản sao, chỉnh Grip để cả đoạn 10 m tăng Z 1 m → F đào tăng 10 m²; thử chỉ một đầu +1m → F tăng 5 m². Bấm CẬP NHẬT MẶT CẮT, kiểm V chi tiết/vùng tầng và Excel. Lưu IMSAT Project rồi SAVE/SAVEAS/đóng/mở và so cùng giá trị. Trên bản sao khác, xóa TẤT CẢ polyline mặt cắt CAD đã lưu rồi thử nạp Project: phải cảnh báo **không tìm thấy CAD đã chỉnh**, không khôi phục âm thầm volume từ TIN và không sửa source.
21. **Đổi DWG/TIN khi đang tính:** với ít nhất hai bản vẽ riêng, khởi tạo chạy tính mặt cắt lâu ở A, chuyển B hoặc đổi dữ liệu TIN: phép tính của A không được vẽ vào B, không ghi đè dữ liệu B; phải có thông báo yêu cầu tính lại. Lặp lại khi đang chọn điểm chèn. Kiểm thử hiệu năng ở bộ dữ liệu lớn, không để UI vẽ sai tài liệu. **Bổ sung:** mở Palette trên A rồi chuyển sang B khi đang mở: Palette A phải tự ẩn, không còn các nút sử dụng mô hình A; bấm Ribbon/TKL trong B phải nạp đúng Project B. Thử lệnh MV_VIEW3D_HT/TK và MV_PROJECT_SAVE trên B để chứng minh không lấy TIN hoặc dự án từ A. Lưu Project trước khi rời bản vẽ A để không mất các chỉnh sửa chưa lưu; không được tự khẳng định chức năng chuyển qua lại nhiều DWG đã PASS chỉ dựa vào CI.
22. **Trọn gói Excel và cài đặt:** xuất workbook mặt cắt, lưới, khoáng sản từ đúng một lần tính thành công; Excel Desktop kiểm metadata đơn vị khai thác THAN/QUẶNG, Tháng/Quý/Năm, thời điểm tính, thời gian tính, 2 ô ký, logo IMSAT, font Arial12 không tô nền và công thức thực; đổi tham số cho phép và xác nhận recalc. Test V1→V2 QA Setup và Uninstall: chỉ xóa tệp Setup/bundle/shortcut/registry do IMSAT cài, vẫn giữ DWG/Project/Excel/license khách hàng. Khởi động CAD sau gỡ, không tự nạp. Chỉ dùng bộ QA trên máy thử.

**Ca bắt buộc bổ sung sau vòng audit 12/10:**

- Sau khi tính lưới, sửa Grip **một đỉnh** đường bao khép kín nhưng vẫn giữ nguyên handle. Thử **Vẽ lưới CAD** và **Xuất Excel**: cả hai phải từ chối dùng kết quả đã tính, báo phải tính lại. Tính lại thì chấp nhận và thể tích phải theo polygon mới.
- Vẽ MC, bấm **CẬP NHẬT MẶT CẮT** thành công, sau đó sửa Grip tăng một đầu đường HT thêm **0,001 m**. Chưa cập nhật lần hai, thử xuất Excel và bảng CAD: phải chặn. Cập nhật lại rồi xuất: đúng F/V đã sửa. Không được chặn sai chỉ vì sai số làm tròn khi CAD đọc lại đường không thay đổi.
- Sao chép DWG A thành B bằng **SAVEAS** (có thể giữ cùng `FingerprintGuid`), mở **cả hai DWG** trong cùng AutoCAD. Trên A và B đặt Project khác nhau, kiểm tra chuyển tab, Palette tự ẩn, mở lại đúng đơn vị/TIN/kỳ báo cáo của mỗi DWG. Lưu, đóng và mở lại từng bản vẽ để bảo đảm không có dữ liệu chéo. Không dùng mỗi `FingerprintGuid` làm kết luận phân biệt hai bản vẽ.
- Trong lúc cửa sổ chọn tên Excel còn mở, hoặc lúc phần mềm đang kết xuất workbook, đổi DWG hoặc chỉnh đường HT/TK. Báo cáo phải bị từ chối nếu dữ liệu đã thay đổi, không được ghi đè Excel chốt từ lần trước.

Mỗi ca ghi Expected, Actual, PASS/FAIL/NOT TESTED, ảnh/log, commit, máy chạy và người xác nhận. Mọi mục trên hiện **NOT TESTED trong AutoCAD**; yêu cầu người nghiệm thu ký duyệt trước Release.

Verifier tự động có sẵn: `release/verify_autocad2023_runtime.ps1` (đọc trước khi chạy; cần Administrator, phiên Windows tương tác và AutoCAD 2023). Verifier chỉ là một phần bằng chứng, không tự PASS các thao tác UI/mỏ/bản quyền ở trên.
