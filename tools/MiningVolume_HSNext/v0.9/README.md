# MiningVolume HS-Next v0.9 — AutoCAD 2023 Pre-Release

Mốc v0.9 tập trung biến bộ mã v0.8 thành **Release cài như phần mềm bình thường**: binary add-in được build sẵn trên Windows CI, Setup chỉ cài bundle và tự kiểm tra trong AutoCAD 2023. Máy người dùng **không cần Visual Studio, MSBuild hoặc Build Tools**.

## Các thay đổi chính so với v0.8

1. **Bỏ phụ thuộc NetTopologySuite ở runtime**
   - TIN dùng Bowyer-Watson Delaunay nội bộ.
   - Breakline được khôi phục bằng constrained edge-flip và khóa cạnh đã khôi phục.
   - Pre-validator v0.3 vẫn chặn trùng XY khác Z, breakline cắt/chồng lấn và tự tách breakline tại site nằm trên tuyến.

2. **Chuẩn hóa build AutoCAD 2023**
   - Target `net48`.
   - CI tham chiếu gói Autodesk `AutoCAD.NET 24.2.0` tương ứng AutoCAD 2023.
   - Bundle khóa `SeriesMin=R24.2`, `SeriesMax=R24.2`.
   - Các DLL API của Autodesk chỉ dùng lúc compile, không đóng vào bộ cài.

3. **Setup v0.9 không biên dịch trên máy người dùng**
   - Release pipeline build sẵn 4 DLL MiningVolume.
   - Setup chỉ copy `.bundle` vào `C:\ProgramData\Autodesk\ApplicationPlugins`.
   - Sau cài, Setup tự mở AutoCAD 2023 và chạy `MVSELFTEST`.
   - Nếu không nhận `Status=PASS`, Setup tự rollback về bản trước.

4. **Release policy**
   Bundle cuối chỉ chứa binary của MiningVolume. Không được đóng kèm:
   - `AcMgd.dll`, `AcDbMgd.dll`, `AcCoreMgd.dll`, `AcWindows.dll`, `AdWindows.dll`;
   - `NetTopologySuite.dll`.

## Chuỗi chức năng đã tích hợp

`Project → Dữ liệu X-Y-Z → TIN → Hệ mặt cắt → Mặt cắt hiện trạng/thiết kế → Diện tích đào/đắp → Khối lượng chi tiết → Theo tầng → Excel → Lưu/khôi phục Project trong DWG`.

Dữ liệu nguồn hỗ trợ `POINT`, `LINE`, `LWPOLYLINE`, `POLYLINE`, `3D POLYLINE` và đường đồng mức có cao độ.

## Kiểm thử hiện tại

- `pytest`: **74/74 PASS**.
- Installer shell: `go vet` PASS.
- Installer shell cross-build: Windows PE64 PASS.
- `PackageContents.xml`: AutoCAD 2023 R24.2 only.
- Runtime `MVSELFTEST`: đã có trong mã và là cổng bắt buộc của Setup.

### Chưa được ghi là PASS

Môi trường làm việc hiện tại không có Windows + AutoCAD 2023, vì vậy **chưa có quyền ghi PASS cho runtime AutoCAD 2023** và chưa gọi v0.9 là Release 1.0. File Setup chính thức chỉ được tạo bởi Windows release pipeline sau khi DLL compile thật.

## Build Release

Workflow: `.github/workflows/build-autocad2023-release.yml`

Workflow chạy trên `windows-2022`:
1. chạy QA;
2. restore/build `net48` với `UseAutoCADNuGet=true`;
3. đóng 4 DLL MiningVolume vào bundle;
4. chạy `release/verify_release.ps1`;
5. tạo `installer/payload.zip`;
6. build `MiningVolume_HSNext_AutoCAD2023_Setup_v0.9.exe`;
7. tạo SHA-256 và upload artifact.

## Nguyên tắc phát hành

Không giao cho người dùng một Setup chỉ chứa source hoặc yêu cầu compile tại máy cài. Setup v0.9 hợp lệ phải mang sẵn các DLL Release và phải qua `MVSELFTEST` trong AutoCAD 2023.