# MiningVolume HS-Next v0.10.4 — AutoCAD 2023 GUI-first Release Candidate

Mốc v0.10.4 tập trung biến bộ mã v0.8 thành **Release cài như phần mềm bình thường**: binary add-in được build sẵn trên Windows CI, Setup chỉ cài bundle và tự kiểm tra trong AutoCAD 2023. Máy người dùng **không cần Visual Studio, MSBuild hoặc Build Tools**.

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

3. **Setup v0.10.4 không biên dịch trên máy người dùng**
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

Môi trường làm việc hiện tại không có Windows + AutoCAD 2023, vì vậy **chưa có quyền ghi PASS cho runtime AutoCAD 2023** và chưa gọi v0.10.4 là Release 1.0. File Setup chính thức chỉ được tạo bởi Windows release pipeline sau khi DLL compile thật.

## Build Release

Workflow: `.github/workflows/build-autocad2023-release.yml`

Workflow chạy trên `windows-2022`:
1. chạy QA;
2. restore/build `net48` với `UseAutoCADNuGet=true`;
3. đóng 4 DLL MiningVolume vào bundle;
4. chạy `release/verify_release.ps1`;
5. tạo `installer/payload.zip`;
6. build `MiningVolume_HSNext_AutoCAD2023_Setup_v0.10.4.exe`;
7. tạo SHA-256 và upload artifact.

## Nguyên tắc phát hành

Không giao cho người dùng một Setup chỉ chứa source hoặc yêu cầu compile tại máy cài. Setup v0.10.4 hợp lệ phải mang sẵn các DLL Release và phải qua `MVSELFTEST` trong AutoCAD 2023.

CI trigger marker: AutoCAD 2023 Windows release candidate.


## Kiến trúc GUI-first v0.10.4

- Ribbon gọi trực tiếp C# và mở các trang Project / Dữ liệu / Mô hình / Mặt cắt / Khối lượng / Excel.
- Luồng người dùng không gửi chuỗi lệnh xuống Command Line.
- Package tự nạp khi AutoCAD 2023 khởi động; không đăng ký danh sách lệnh MV_* trong PackageContents.xml.
- Các CommandMethod kỹ thuật chỉ giữ cho tự kiểm tra/chẩn đoán và tương thích nội bộ, không phải giao diện sử dụng chính.


## Classic workspace / Ribbon tắt — v0.10.4

MiningVolume không phụ thuộc Ribbon để xuất hiện. Khi add-in được AutoCAD 2023 nạp và có bản vẽ hoạt động, palette `MINING VOLUME` tự mở từ sự kiện Idle. Ribbon chỉ là điểm truy cập bổ sung nếu workspace có Ribbon.

Nếu giao diện khởi động thất bại, add-in không bỏ qua lỗi im lặng. Nó hiển thị thông báo và ghi log tại:

`C:\ProgramData\MiningVolume2023\Logs\startup.log`


## Sửa lỗi khởi tạo giao diện — v0.10.4

Trên AutoCAD 2023 thật, v0.10.1 đã phát hiện lỗi WinForms khi tạo ô tỷ lệ ngang mặc định 1/1000: `NumericUpDown.Value` được gán trước khi `Maximum` được nâng từ mặc định 100 lên 100000. v0.10.4 sửa theo thứ tự bắt buộc: `Minimum/Maximum` trước, sau đó mới gán `Value` đã clamp.

Cổng phát hành mới:
- quét toàn bộ plugin để chặn mẫu khởi tạo `NumericUpDown` theo kiểu Value-first;
- `MVSELFTEST` khởi tạo thật toàn bộ `MainPaletteControl` và tất cả trang;
- nếu startup palette từng phát sinh exception thì self-test phải FAIL;
- khi Setup chạy self-test, thông báo modal bị tắt để không treo kiểm thử; kết quả lỗi được trả về log và Setup rollback.


## Sửa giao diện mặt cắt — v0.10.4

Ảnh chạy thực tế trên AutoCAD 2023 cho thấy trang Mặt cắt bị chồng các control do nhiều vùng `Dock=Top` kết hợp `BringToFront()`. Hậu quả: các hàng Tính từ mức / Đến mức / Tỷ lệ bị che và nút thành lập mặt cắt không nhìn thấy.

v0.10.4 thay trang Mặt cắt bằng một `TableLayoutPanel` dọc duy nhất, chia hàng cố định:
1. Tiêu đề;
2. 8 hàng tham số;
3. hai thao tác chính `XEM TRƯỚC TUYẾN` và `XUẤT / VẼ MẶT CẮT`;
4. công cụ thêm/dịch/xóa tuyến;
5. trạng thái;
6. bảng mặt cắt.

Runtime self-test kiểm tra nút `btnDrawSections` ở kích thước palette tối thiểu 580x620.


## Cổng TIN bắt buộc — v0.10.4

TIN hiện trạng và TIN thiết kế được coi là **đầu vào tính toán bắt buộc**, không còn chỉ là lớp hiển thị.

Quy trình kiểm soát:
- Khi nạp layer nguồn, MiningVolume chuẩn bị layer kết quả riêng: `MV_TIN_HIENTRANG` và `MV_TIN_THIETKE`, đồng thời xóa TIN cũ của mô hình vừa nạp lại.
- Trang Mô hình có ba thao tác rõ ràng: tạo/cập nhật TIN hiện trạng, tạo/cập nhật TIN thiết kế, hoặc tạo/cập nhật **cả hai TIN**.
- TIN chỉ được công nhận khi lõi triangulation tạo được tam giác hợp lệ **và** số 3DFACE ghi trên layer AutoCAD đúng bằng số tam giác của lõi.
- Mỗi TIN lưu dấu thời điểm dữ liệu X-Y-Z nguồn dùng để dựng. Nếu loại điểm, loại đối tượng, khôi phục hoặc sửa Z thì TIN tương ứng bị hủy và layer TIN cũ được làm sạch.
- Thành lập mặt cắt và tính khối lượng bị khóa nếu thiếu một trong hai TIN hoặc TIN đã cũ so với dữ liệu nguồn.
- Nếu người dùng xóa/chỉnh thủ công các 3DFACE trên layer TIN, trước khi tính MiningVolume tự kiểm tra số face và dựng lại layer từ TIN lõi đã xác minh.
- Danh sách layer nguồn không hiển thị các layer đầu ra `MV_TIN_*` / `MV_MC_*`, tránh nạp nhầm kết quả làm dữ liệu đầu vào.

Runtime `MVSELFTEST` v0.10.4 còn tạo thật hai layer TIN tạm trong AutoCAD, ghi/đếm 3DFACE và chỉ PASS nếu cả TIN hiện trạng và TIN thiết kế đều được tạo đúng.
