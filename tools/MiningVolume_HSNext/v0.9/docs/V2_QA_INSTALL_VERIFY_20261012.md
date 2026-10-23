# Cài và kiểm tra IMSAT MiningVolume V2.0 QA — AutoCAD 2023 x64

Bản QA phục vụ thử nghiệm, chưa nghiệm thu phát hành chính thức. Nhà phát triển: **Bùi Thế Nam — ĐT/Zalo: 0967280686**. Gói tải kèm Setup EXE, SHA256SUMS.txt, FINAL_BUILD_EVIDENCE.json, báo cáo QA và test. Chỉ dùng EXE có SHA-256 khớp hồ sơ cùng gói.

## Cài hoặc nâng cấp

1. Lưu và tạo bản sao DWG/project trước khi thử; đóng tất cả AutoCAD. Không cần xóa dữ liệu CAD, file Excel hay giấy phép.
2. Trong Windows Settings → Apps → Installed apps, gỡ bản IMSAT MiningVolume cũ bằng bộ gỡ của chính ứng dụng. Nếu đã cài thủ công nhiều nơi, ghi lại đường dẫn; không xóa hàng loạt thư mục ApplicationPlugins hoặc DLL của phần mềm khác.
3. Giải nén gói QA. Kiểm hash bằng PowerShell `Get-FileHash .\IMSAT_MiningVolume_V2_0_QA_CANDIDATE.exe -Algorithm SHA256`, đối chiếu SHA256SUMS.txt.
4. Chạy Setup, cho phép quyền quản trị khi Windows yêu cầu. Bộ cài kiểm manifest và hash DLL trước khi thay Bundle; dữ liệu người dùng không thuộc phạm vi xóa. Các thao tác GUI cài/nâng cấp/gỡ chưa được thử thực trong môi trường kiểm thử này.
5. Mở AutoCAD 2023 x64, chạy `MVBUILDINFO`. Đối chiếu source SHA với FINAL_BUILD_EVIDENCE.json; kiểm mỗi DLL cùng thư mục Bundle mới, kết quả manifest hash MATCH. MVID/đường dẫn thực được in để phát hiện DLL cũ. Nếu sai SHA/đường dẫn, đóng CAD và xử lý bản cài cũ trước khi dùng kết quả tính.
6. Chạy `MVSELFTEST` trong bản vẽ thử mới; lưu log nguyên văn. Có thêm ca viết 3DFACE rất nhỏ. Đây là ca cần thực thi trên máy CAD; không được coi build hoặc core PASS thay cho bước này.

## Kiểm lỗi TIN đã báo

- Mở bản sao dữ liệu SQ. Tạo riêng hiện trạng `!HT`, sau đó thiết kế `!TK`; ghi số điểm nguồn, tam giác support/mặt đứng, cảnh báo, thời gian và lỗi nếu có. Lõi đã tạo 1.681.755 tam giác HT và 1.672.875 TK trên fixture DXF; số trên DWG có thể khác nếu tập đối tượng chọn khác.
- Bật/tắt HT/TK, View TIN 3D và Esc; hủy giữa dựng TIN, hủy giữa ghi CAD, thử lại; kiểm mô hình cũ không bị mất sau hủy. Chụp ảnh cửa sổ và lưu log, không chỉ thông báo hoàn tất.
- Lưu/đóng/mở DWG; kiểm mặt TIN và dữ liệu gốc. Sửa Z/loại/khôi phục chỉ trong mô hình; xác nhận ảnh hưởng đúng HT hoặc TK và vô hiệu kết quả cũ.
- Xuất MC bước 20m; kiểm mặt đứng/xung đột, chỉnh polyline và CẬP NHẬT MẶT CẮT trước khi chốt V. Test đoạn10m tăng Z toàn đoạn1m phải tăng F10m²; tăng một đầu1m tăng F5m². Kiểm theo dải/tầng, tổng, nhãn và XLSX đồng bộ.
- Lưới 2.5D cố ý không cho tính thể tích từ TIN có mặt đứng đa Z. Không thay bằng mean-Z để vượt kiểm tra. TIN trung bình Z là thử nghiệm, không dùng làm nguồn thể tích chính thức.
- Thực hiện ma trận T01–T22 trong `V2_FINAL_TRACEABILITY_20261012.md`, ghi PASS/FAIL kèm DWG/log/ảnh. Chưa thực thi phải để NOT TESTED.

## Gỡ và cài lại

Đóng AutoCAD; dùng Windows Installed apps hoặc shortcut Gỡ IMSAT VOLUME. Kiểm Bundle/autoload/shortcut IMSAT đã gỡ, CAD khởi động không còn nạp add-in đó; bản vẽ/project/Excel/license còn nguyên. Cài lại không được reset trial. Những kết quả này phải ghi từ máy thật, hiện NOT TESTED.

## Bảo mật và giấy phép

Setup QA chưa có chữ ký Authenticode. Quét Microsoft Defender và phiên bản engine/signature được ghi theo artifact trong FINAL_BUILD_EVIDENCE.json; không phải cam kết mọi phần mềm bảo mật đều chấp nhận. Không tắt Defender/SmartScreen hoặc thêm ngoại lệ để vượt cảnh báo. Nếu bị chặn, giữ nguyên bảo vệ và gửi tên phát hiện, SHA-256, ảnh/log để kiểm tra đúng artifact.

Trial/RSA có kiểm thử tự động nhưng public trust anchor sản xuất chưa provision. Kích hoạt phát hành chính thức còn BLOCKED. Không đóng gói private key, không dùng khóa thử để giả nghiệm thu. AutoCAD 2012+ không thuộc binary QA 2023 này.
