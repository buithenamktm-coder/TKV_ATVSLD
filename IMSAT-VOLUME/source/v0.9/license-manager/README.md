# IMSAT License Manager (Offline – quản trị viên)

Công cụ cấp license **dành riêng cho quản trị viên IMSAT**, không phân phối cùng add-in MiningVolume.

## Mô hình an toàn
- **RSA-3072/SHA-256:** private key chỉ có ở tài khoản Windows của máy cấp key, bảo vệ bởi DPAPI.
- Public key có thể đưa vào source bản phát hành qua lệnh `provision`; KHÔNG bao giờ đưa private key vào GitHub, Setup hay add-in.
- Mỗi license ràng buộc `MachineId`, chế độ `INTERNAL` / `UNIT` và hạn sử dụng.
- Không có khóa vạn năng bỏ qua giấy phép. "Super Key" = quyền phát hành giấy phép từ công cụ admin và private signing key.
- Mỗi lần phát hành có bản ghi trong `audit.tsv`.


## Sao lưu / khôi phục khóa ký RSA (máy quản trị)

Sau lệnh `init`, bắt buộc sao lưu khóa private trước khi cấp key cho các đơn vị. Không sao chép tệp DPAPI trực tiếp sang máy khác vì phụ thuộc tài khoản Windows.

```bat
IMSAT.LicenseManager.exe backup "E:\\SECURE-OFFLINE\\imsat-issuer.imsbackup"
IMSAT.LicenseManager.exe restore "E:\\SECURE-OFFLINE\\imsat-issuer.imsbackup"
```

- `backup` yêu cầu mật khẩu nhập tương tác 2 lần (ít nhất 16 ký tự), không hiển thị trong command-line argument hoặc lịch sử lệnh. Mã hóa AES-256-CBC, PBKDF2-SHA256 250.000 vòng, HMAC-SHA256 để phát hiện dữ liệu bị thay đổi.
- `restore` chỉ hoạt động trên máy/tài khoản quản trị **chưa có private issuer key**; nếu đã có public key thì bắt buộc khớp hoàn toàn. Sau khôi phục, private key được DPAPI khóa theo tài khoản Windows mới.
- Lưu ít nhất hai bản backup ngoại tuyến ở các vị trí kiểm soát truy cập; mật khẩu và file backup phải được lưu ở hai nơi riêng biệt, có quy trình bàn giao nhiều người và nhật ký.
- **Tuyệt đối không đính kèm file .imsbackup khi gửi license cho khách hàng**. Người dùng chỉ được nhận key `.mvlic` gắn máy.
- Lệnh `selftest` thực hiện kiểm thử ký/xác minh RSA và mã hóa/giải mã backup, đồng thời từ chối sửa nội dung backup (tamper).

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

**Quan trọng:** Đã có cơ chế backup mã hóa và khôi phục private key; vẫn phải nghiệm thu khả năng phục hồi **trên một máy Windows quản trị thứ hai**, quy trình phân quyền/cấp lại và luân chuyển nhân sự trước phát hành thương mại. Key công khai của bản phát hành phải được provision, mã nguồn build phải được ký/xác minh và kiểm thử đầy đủ. Người dùng không mất dữ liệu DWG nếu hết hạn license.

Offline không thể thu hồi giấy phép tức thời khi máy khách không kết nối; việc thu hồi phải quy định qua ngày hết hạn hoặc quy trình đổi giấy phép có kiểm soát. 
