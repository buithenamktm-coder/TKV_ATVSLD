# QA REPORT — MiningVolume HS-Next v0.9

## Kết luận hiện tại

**PRE-RELEASE — SOURCE/RELEASE PIPELINE READY, AUTOCAD 2023 RUNTIME NOT YET VERIFIED.**

Không gọi đây là Release 1.0 cho đến khi binary build trên Windows được nạp và chạy `MVSELFTEST` thành công trong AutoCAD 2023 thật.

## Kết quả kiểm tra tự động

- Reference/static tests: **74 PASS / 0 FAIL**.
- `go vet installer/main.go`: PASS với payload QA.
- Cross-build installer shell `GOOS=windows GOARCH=amd64`: PASS.
- Kết quả PE: `PE32+ GUI x86-64`.

## Các hạng mục v0.9 đã khép kín ở mức mã nguồn

### TIN
- Bỏ `NetTopologySuite` khỏi source và csproj.
- Bowyer-Watson Delaunay nội bộ.
- Khôi phục breakline bằng edge-flip.
- Khóa breakline đã khôi phục; cạnh mới không được cắt breakline đã khóa.
- Giữ validator dữ liệu đầu vào từ các mốc trước.

### Build
- `net48`.
- Autodesk `AutoCAD.NET 24.2.0` chỉ dùng compile trên CI.
- `Microsoft.NETFramework.ReferenceAssemblies.net48` pin `1.0.3`.
- Có chế độ local developer build bằng DLL AutoCAD 2023 thật.

### Installer
- Không có `runBuild`, không gọi MSBuild/C# compiler trên máy người dùng.
- Kiểm tra 4 DLL Release phải tồn tại trước khi cài.
- Backup bản đang có.
- Cài vào ProgramData/ApplicationPlugins.
- Tự chạy `MVSELFTEST`.
- FAIL/timeout → rollback và lưu log.
- PASS → ghi Uninstall entry.

### Release policy
`release/verify_release.ps1` chặn:
- AutoCAD reference/runtime DLL bị đóng nhầm vào bundle;
- `NetTopologySuite.dll`;
- sai version hoặc sai R24.2.

## Runtime self-test đã thiết kế

`MVSELFTEST` kiểm tra bài toán chuẩn, trong đó có:
- TIN phẳng;
- hệ mặt cắt;
- lấy profile từ TIN;
- diện tích;
- prismoid;
- chia tầng;
- XLSX;
- ground truth tổng đào `100000.0 m³`.

## Hạng mục chưa thể xác nhận trong môi trường hiện tại

1. Compile solution thật bằng Windows/.NET Framework toolchain.
2. Nạp DLL vào AutoCAD 2023 thật.
3. Ribbon/Palette khởi tạo trong host thật.
4. Chọn entity trong DWG thật.
5. Vẽ TIN/mặt cắt trong database AutoCAD thật.
6. `MVSELFTEST` runtime PASS.

Các mục này phải do Windows release runner + AutoCAD 2023 runtime gate xác nhận trước khi phát hành chính thức.