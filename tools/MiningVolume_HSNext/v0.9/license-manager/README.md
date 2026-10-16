# IMSAT License Manager (Offline – quản trị viên)

Công cụ cấp license **dành riêng cho quản trị viên IMSAT**, không phân phối cùng add-in MiningVolume.

## Mô hình an toàn
- **RSA-3072/SHA-256:** private key chỉ có ở tài khoản Windows của máy cấp key, bảo vệ bởi DPAPI.
- Public key có thể đưa vào source bản phát hành qua lệnh `provision`; KHÔNG bao giờ đưa private key vào GitHub, Setup hay add-in.
- Mỗi license ràng buộc `MachineId`, chế độ `INTERNAL` / `UNIT` và hạn sử dụng.
- Không có khóa vạn năng bỏ qua giấy phép. "Super Key" = quyền phát hành giấy phép từ công cụ admin và private signing key.
- Mỗi lần phát hành có bản ghi trong `audit.tsv`.

## Quy trình
```bat
dotnet build IMSAT.LicenseManager.csproj -c Release
IMSAT.LicenseManager.exe init
IMSAT.LicenseManager.exe public
IMSAT.LicenseManager.exe provision "C:\\path\\to\\LicenseService.cs"
IMSAT.LicenseManager.exe issue <64HEX_MACHINE_ID> UNIT "Công ty mỏ" PERMANENT
IMSAT.LicenseManager.exe verify "C:\\path\\to\\MV2-....mvlic"
```

Người dùng nhận `MachineId` từ trang **Bản quyền** trên Palette IMSAT VOLUME. Gửi file `.mvlic` tương ứng cho đúng máy, mở phần mềm → Bản quyền → Đọc file → Kích hoạt.

**Quan trọng:** Private key DPAPI của máy quản trị hiện chưa có phương án backup có thể khôi phục an toàn trên máy khác; vì vậy **không được nghiệm thu phát hành thương mại** trước khi bổ sung quy trình sao lưu/khôi phục và phân quyền/cấp lại. Key công khai của bản phát hành phải được provision, mã nguồn build phải được ký/xác minh và kiểm thử đầy đủ. Người dùng không mất dữ liệu DWG nếu hết hạn license.

Offline không thể thu hồi giấy phép tức thời khi máy khách không kết nối; việc thu hồi phải quy định qua ngày hết hạn hoặc quy trình đổi giấy phép có kiểm soát. 
