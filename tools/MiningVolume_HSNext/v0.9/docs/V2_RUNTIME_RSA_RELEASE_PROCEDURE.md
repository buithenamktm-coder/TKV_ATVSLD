# IMSAT VOLUME V2.0 — AutoCAD 2023 runtime và Pass offline

**Trạng thái: PIPELINE IMPLEMENTED; PRODUCT RELEASE STILL BLOCKED.**

Tài liệu này chỉ áp dụng V2.0, không thay đổi workflow phát hành V1.0. Workflow V2 mới:
`.github/workflows/verify-imsat-volume-v2-autocad2023.yml`.

## 1. Lập khóa trên máy quản trị Windows (một lần)

Dùng `IMSAT.LicenseManager.exe` trên **máy cấp giấy phép do chủ sở hữu quản lý**.
Không tạo issuer key trong CI hoặc máy ChatGPT và không đưa private key lên GitHub.

```powershell
# Trong thư mục chứa IMSAT.LicenseManager.exe, trên máy quản trị:
./IMSAT.LicenseManager.exe selftest
./IMSAT.LicenseManager.exe init
./IMSAT.LicenseManager.exe backup "E:\\OFFLINE-SECURE\\imsat-issuer.imsbackup"
./IMSAT.LicenseManager.exe fingerprint
```

- `init` chỉ chạy một lần. RSA 3072-bit private key được bảo vệ bằng Windows DPAPI trong hồ sơ tài khoản quản trị.
- `backup`: khóa dự phòng AES+HMAC, mật khẩu tối thiểu 16 ký tự, lưu ngoại tuyến. Phải thử khôi phục trên máy quản trị thứ hai.
- `fingerprint`: in **SHA-256 của XML khóa công khai chuẩn hóa**. Ghi lại để đối chiếu và không thay đổi khóa tùy tiện khi đã phát hành license.
- Tệp chỉ chứa khóa công khai tại `%APPDATA%\IMSAT\MiningVolume\LicenseManager\issuer.public.xml`.

## 2. Thiết lập GitHub Actions (chỉ nội dung PUBLIC)

Trong repository `buithenamktm-coder/TKV_ATVSLD`, tại *Settings → Secrets and variables → Actions*:

**Secret** `IMSAT_VOLUME_V2_RSA_PUBLIC_XML_B64`: nội dung Base64 của `issuer.public.xml`. Có thể lấy trên máy quản trị:

```powershell
$p = Join-Path $env:APPDATA 'IMSAT\MiningVolume\LicenseManager\issuer.public.xml'
[Convert]::ToBase64String([IO.File]::ReadAllBytes($p))
```

**Variable** `IMSAT_VOLUME_V2_RSA_PUBLIC_SHA256`: 64 ký tự hex in bằng lệnh `fingerprint`.

Đây là khóa **công khai**, có thể nhúng trong phần mềm xác thực; secret giúp kiểm soát quy trình phát hành. **Không đặt** `issuer.private.dpapi`, XML private, file `.imsbackup`, mật khẩu hoặc khóa ký vào GitHub, nguồn C#, artifact hay tin nhắn.

Nếu thiếu hoặc SHA không khớp, script `release/provision_v2_public_key.ps1` sẽ FAIL-CLOSED, không đóng gói bản host-QA mang danh bản thương mại. Script từ chối XML chứa phần tử private, yêu cầu RSA public-only tối thiểu 3072 bit, chỉ cho phép provision placeholder một lần trong bản checkout CI và ghi dấu vân tay kiểm toán.

## 3. Chạy nghiệm thu trên AutoCAD 2023 thực tế

Workflow V2 phải chạy trên Windows x64 có AutoCAD 2023 **R24.2** và GitHub self-hosted runner `[self-hosted, Windows, X64, autocad2023]`; cần phiên tương tác Windows với quyền Administrator, không dùng runner dạng Windows Service. Yêu cầu đóng AutoCAD trước khi chạy.

Tại **Actions → IMSAT VOLUME V2.0 AutoCAD 2023 Host QA (NOT RELEASE) → Run workflow**, chọn nhánh `miningvolume-v2-chatgpt-p0-continuation` và nhập cùng nhánh ở `source_ref`, hoặc commit thuộc lịch sử nhánh đó.

Workflow thực hiện:

1. Xác nhận SHA thuộc V2 và AutoCAD 2023 cài đặt thực.
2. Chạy tests và RSA issuer selftest, nạp public key đúng fingerprint trước khi build.
3. Build .NET Framework bằng DLL host thật, dựng riêng bundle PackageContents **V2.0**.
4. Chạy `MVSELFTEST` trong AutoCAD thực với nhận diện **IMSAT VOLUME V2.0**, ghi log `V2_RUNTIME_VERIFICATION.txt`.
5. Nếu smoke PASS, tạo artifact `IMSAT-VOLUME-V2.0-AutoCAD2023-HOST-QA-NOT-RELEASE`, Setup **IMSAT_MiningVolume_V2_0_QA_CANDIDATE.exe**.

**Không dùng workflow V1 `verify-miningvolume-autocad2023-runtime.yml` để nghiệm thu V2.** Bản V1 vẫn giữ nguyên và có chốt version riêng.

## 4. Cấp Pass offline

```powershell
./IMSAT.LicenseManager.exe issue <MACHINE_ID_64HEX> UNIT "Tên đơn vị" 1M
./IMSAT.LicenseManager.exe issue <MACHINE_ID_64HEX> UNIT "Tên đơn vị" 3M
./IMSAT.LicenseManager.exe issue <MACHINE_ID_64HEX> UNIT "Tên đơn vị" 6M
./IMSAT.LicenseManager.exe issue <MACHINE_ID_64HEX> UNIT "Tên đơn vị" 1Y
./IMSAT.LicenseManager.exe issue <MACHINE_ID_64HEX> UNIT "Tên đơn vị" PERMANENT
```

Chỉ phát hành Pass sau khi đã xác nhận MachineId ở trang Bản quyền. Phần mềm xác minh chữ ký RSA-SHA256 từ public key đã nhúng, machine binding, ngày hiệu lực và thời hạn, **không cần Internet**. File token `.mvlic` chỉ có hiệu lực với thiết bị được ký; không có Master Pass nhập trực tiếp vào add-in.

## 5. Những chốt chưa đạt khi chỉ chạy workflow

MVSELFTEST PASS không chứng minh toàn bộ T01–T22, đối chứng khối lượng mỏ thực, in CAD/Excel, UI DPI, kích hoạt thực tế bằng token đã ký, sao lưu/khôi phục issuer, nâng cấp/gỡ Setup. Các bước này phải nghiệm thu **riêng**, ghi chứng cứ và SHA của đúng bản build. Artifact **HOST-QA-NOT-RELEASE** không được đổi tên thành FINAL.

**Không có khóa công khai thực từ máy quản trị thì không thể hoàn tất kích hoạt RSA sản xuất.** Chúng ta có thể hoàn thiện quy trình và phép kiểm tra trước, nhưng không được tự tạo key của chủ sở hữu thay chủ sở hữu.
