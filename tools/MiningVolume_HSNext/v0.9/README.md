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

- `pytest`: **124/124 PASS**.
- Installer shell: `go vet` PASS.
- Installer shell cross-build: Windows PE64 PASS.
- `PackageContents.xml`: AutoCAD 2023 R24.2 only.
- Runtime `MVSELFTEST`: đã có trong mã và là cổng bắt buộc của Setup.

### Chưa được ghi là PASS

Môi trường làm việc hiện tại không có Windows + AutoCAD 2023, vì vậy **chưa có quyền ghi PASS cho runtime AutoCAD 2023** và chưa gọi v0.10.4 là Release 1.0. File Setup chính thức chỉ được tạo bởi Windows release pipeline sau khi DLL compile thật.

## Build Release

Workflow: `.github/workflows/build-miningvolume-autocad2023.yml`

Workflow chạy trên `windows-2022`:
1. chạy QA;
2. restore/build `net48` với `UseAutoCADNuGet=true`;
3. đóng 4 DLL MiningVolume vào bundle;
4. chạy `release/verify_release.ps1`;
5. tạo `installer/payload.zip`;
6. build `MiningVolume_HSNext_AutoCAD2023_Setup_v0.10.4.exe`;
7. tạo SHA-256 + `BUILD_VERIFICATION.txt` và upload artifact.

`BUILD_VERIFICATION.txt` chỉ ghi PASS cho các gate thực sự chạy trên GitHub-hosted Windows runner. Runtime AutoCAD 2023 được ghi rõ là chưa chạy trên GitHub; Setup vẫn bắt buộc chạy `MVSELFTEST` trong AutoCAD 2023 và tự rollback nếu không nhận `Status=PASS`.

## Nguyên tắc phát hành

Không giao cho người dùng một Setup chỉ chứa source hoặc yêu cầu compile tại máy cài. Setup v0.10.4 hợp lệ phải mang sẵn các DLL Release và phải qua `MVSELFTEST` trong AutoCAD 2023.

CI trigger marker: AutoCAD 2023 Windows release candidate.


## Kiến trúc GUI-first v0.10.4

- Ribbon gọi trực tiếp C# và mở các trang Project / Dữ liệu / Mô hình / Mặt cắt / Khối lượng / Excel.
- Luồng người dùng không gửi chuỗi lệnh xuống Command Line.
- Package tự nạp khi AutoCAD 2023 khởi động; không đăng ký danh sách lệnh MV_* trong PackageContents.xml.
- Các CommandMethod kỹ thuật chỉ giữ cho tự kiểm tra/chẩn đoán và tương thích nội bộ, không phải giao diện sử dụng chính.


## Classic workspace / Ribbon tắt — v0.10.4

MiningVolume không phụ thuộc Ribbon để được nạp. Khi add-in được AutoCAD 2023 nạp và có bản vẽ hoạt động, phần khởi tạo/TIN layer vẫn được chuẩn bị nhưng palette `MINING VOLUME` **không tự mở theo mặc định**. Người dùng mở bảng từ Ribbon khi cần; nếu đang dùng Classic workspace/Ribbon tắt thì có thể dùng lệnh `MVOPEN` hoặc `MV_TOGGLE`. Tùy chọn tự mở có thể bật lại trong trang Dự án.

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


Windows CI v0.10.4 hiện tại: **124/124 reference/static tests PASS**, build AutoCAD 2023 add-in thành công, **0 warning, 0 compile errors**, `go vet` PASS và release bundle policy PASS. Smoke test tạo layer TIN thật nằm trong `MVSELFTEST` và được Setup thực thi trên máy có AutoCAD 2023.


## Cổng TIN bắt buộc — v0.10.4

TIN hiện trạng và TIN thiết kế là dữ liệu đầu vào bắt buộc cho mặt cắt và khối lượng.

- Hai layer đầu ra được tạo ngay khi DWG hoạt động: `MV_TIN_HIENTRANG` và `MV_TIN_THIETKE`.
- Nạp lại dữ liệu nguồn hoặc sửa/loại điểm làm TIN cũ mất hiệu lực và xóa biểu diễn cũ trên layer.
- Có thể tạo từng TIN hoặc dùng một nút `TẠO / CẬP NHẬT CẢ HAI TIN`; cặp TIN chỉ được chấp nhận khi cả hai lõi dựng thành công.
- Sau khi ghi xuống AutoCAD, phần mềm kiểm tra số `3DFACE` trên layer phải bằng đúng số tam giác của lõi TIN.
- Layer TIN được khóa sau khi tạo để tránh chỉnh tay làm sai đầu vào tính toán.
- Trước khi lấy mặt cắt hoặc tính khối lượng, phần mềm kiểm tra cả hai TIN còn đồng bộ với dữ liệu X-Y-Z hiện tại; nếu layer bị xóa/mất mặt thì phần mềm tự đồng bộ lại từ TIN đã xác minh.
- Runtime self-test của Setup dùng chính `SurfaceInputPreparer` + `ConformingTinBuilder`, sau đó ghi/đếm `3DFACE` thật trong AutoCAD; đồng thời kiểm màu ACI 1/3, trạng thái khóa layer và thao tác ẩn/hiện TIN.


Final v0.10.4 TIN release build trigger.

Final CI branch marker for v0.10.4 TIN release.


## Runtime verification trên AutoCAD 2023 thật

Repository có workflow thủ công `.github/workflows/verify-miningvolume-autocad2023-runtime.yml`. Workflow này dùng self-hosted Windows runner gắn nhãn `autocad2023`, build bằng chính DLL API của AutoCAD 2023 cài trên máy rồi chạy `release/verify_autocad2023_runtime.ps1`.

Kết quả PASS phải tạo:
- `RUNTIME_VERIFICATION.txt`;
- `MVSELFTEST_RUNTIME.txt`;
- thông tin version AutoCAD 2023;
- SHA-256 của 4 DLL MiningVolume.

Runner phải chạy trong phiên Windows tương tác có AutoCAD 2023 đã kích hoạt bản quyền. Không coi CI hosted là thay thế cho runtime gate này.


## Tùy chọn tự động mở bảng MiningVolume

- Mặc định MiningVolume **không tự bật palette** mỗi lần khởi động AutoCAD, tránh che vùng bản vẽ.
- Người dùng mở bảng từ Ribbon/menu MiningVolume khi cần.
- Trong trang **Dự án** có checkbox `Tự động mở bảng MiningVolume khi khởi động AutoCAD`.
- Tùy chọn được lưu theo tài khoản Windows và giữ nguyên cho các lần khởi động sau; không ghi vào từng DWG.
- Có lệnh kỹ thuật `MV_AUTOPEN` để đảo nhanh trạng thái bật/tắt khi cần.


## Máy kiểm thử AutoCAD 2023 / self-hosted runner

Để chạy gate runtime thật trên một máy Windows có AutoCAD 2023 R24.2:

1. Mở PowerShell bằng **Run as administrator**.
2. Cài GitHub CLI và đăng nhập `gh auth login` bằng tài khoản có quyền quản trị Actions của repository.
3. Chạy `release/setup_autocad2023_runner.ps1`.
4. Script kiểm tra AutoCAD 2023, tải/cấu hình GitHub Actions runner với nhãn `autocad2023` và tạo `START_MiningVolume_Runtime_Runner.cmd`.
5. Chạy file START đó trong phiên Windows đang đăng nhập và giữ cửa sổ mở khi kiểm thử. Launcher tự yêu cầu UAC và nâng quyền Administrator nếu cần.

Workflow runtime tự dò AutoCAD 2023 R24.2 từ thư mục cài chuẩn hoặc Registry rồi build bằng đúng các DLL API trên máy thử nghiệm.

Runner **không chạy dưới dạng Windows Service** vì AutoCAD/MVSELFTEST cần desktop session tương tác. Workflow `Verify MiningVolume AutoCAD 2023 Runtime` chỉ được coi PASS khi `RUNTIME_VERIFICATION.txt` ghi `Status=PASS`.


### Bật/tắt bảng trong khi làm việc

- Ribbon **MINING VOLUME → Dự án** có nút **Ẩn / Hiện bảng** để đóng/mở palette ngay trong phiên AutoCAD.
- Lệnh kỹ thuật tương ứng: `MV_TOGGLE`.
- Việc ẩn bảng không làm mất Project, TIN hay kết quả đang làm việc.
- `MVSELFTEST` trên AutoCAD 2023 thật bắt buộc kiểm tra chuỗi mở → ẩn → hiện lại palette; Setup chỉ PASS khi thao tác này hoạt động.
