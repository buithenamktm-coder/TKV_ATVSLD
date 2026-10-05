# MiningVolume HS-Next v0.10.2 — AutoCAD 2023 GUI-first Release Candidate

Mốc v0.10.2 tập trung biến bộ mã v0.8 thành **Release cài như phần mềm bình thường**: binary add-in được build sẵn trên Windows CI, Setup chỉ cài bundle và tự kiểm tra trong AutoCAD 2023. Máy người dùng **không cần Visual Studio, MSBuild hoặc Build Tools**.

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

3. **Setup v0.10.2 không biên dịch trên máy người dùng**
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

- `pytest`: **88/88 PASS**.
- Installer shell: `go vet` PASS.
- Installer shell cross-build: Windows PE64 PASS.
- `PackageContents.xml`: AutoCAD 2023 R24.2 only.
- Runtime `MVSELFTEST`: đã có trong mã và là cổng bắt buộc của Setup.

### Chưa được ghi là PASS

Môi trường làm việc hiện tại không có Windows + AutoCAD 2023, vì vậy **chưa có quyền ghi PASS cho runtime AutoCAD 2023** và chưa gọi v0.10.2 là Release 1.0. File Setup chính thức chỉ được tạo bởi Windows release pipeline sau khi DLL compile thật.

## Build Release

Workflow: `.github/workflows/build-autocad2023-release.yml`

Workflow chạy trên `windows-2022`:
1. chạy QA;
2. restore/build `net48` với `UseAutoCADNuGet=true`;
3. đóng 4 DLL MiningVolume vào bundle;
4. chạy `release/verify_release.ps1`;
5. tạo `installer/payload.zip`;
6. build `MiningVolume_HSNext_AutoCAD2023_Setup_v0.10.2.exe`;
7. tạo SHA-256 và upload artifact.

## Nguyên tắc phát hành

Không giao cho người dùng một Setup chỉ chứa source hoặc yêu cầu compile tại máy cài. Setup v0.10.2 hợp lệ phải mang sẵn các DLL Release và phải qua `MVSELFTEST` trong AutoCAD 2023.

CI trigger marker: AutoCAD 2023 Windows release candidate.


## Kiến trúc GUI-first v0.10.2

- Ribbon gọi trực tiếp C# và mở các trang Project / Dữ liệu / Mô hình / Mặt cắt / Khối lượng / Excel.
- Luồng người dùng không gửi chuỗi lệnh xuống Command Line.
- Package tự nạp khi AutoCAD 2023 khởi động; không đăng ký danh sách lệnh MV_* trong PackageContents.xml.
- Các CommandMethod kỹ thuật chỉ giữ cho tự kiểm tra/chẩn đoán và tương thích nội bộ, không phải giao diện sử dụng chính.


## Classic workspace / Ribbon tắt — v0.10.2

MiningVolume không phụ thuộc Ribbon để xuất hiện. Khi add-in được AutoCAD 2023 nạp và có bản vẽ hoạt động, palette `MINING VOLUME` tự mở từ sự kiện Idle. Ribbon chỉ là điểm truy cập bổ sung nếu workspace có Ribbon.

Nếu giao diện khởi động thất bại, add-in không bỏ qua lỗi im lặng. Nó hiển thị thông báo và ghi log tại:

`C:\ProgramData\MiningVolume2023\Logs\startup.log`


## Sửa lỗi khởi tạo giao diện — v0.10.2

Trên AutoCAD 2023 thật, v0.10.1 đã phát hiện lỗi WinForms khi tạo ô tỷ lệ ngang mặc định 1/1000: `NumericUpDown.Value` được gán trước khi `Maximum` được nâng từ mặc định 100 lên 100000. v0.10.2 sửa theo thứ tự bắt buộc: `Minimum/Maximum` trước, sau đó mới gán `Value` đã clamp.

Cổng phát hành mới:
- quét toàn bộ plugin để chặn mẫu khởi tạo `NumericUpDown` theo kiểu Value-first;
- `MVSELFTEST` khởi tạo thật toàn bộ `MainPaletteControl` và tất cả trang;
- nếu startup palette từng phát sinh exception thì self-test phải FAIL;
- khi Setup chạy self-test, thông báo modal bị tắt để không treo kiểm thử; kết quả lỗi được trả về log và Setup rollback.
