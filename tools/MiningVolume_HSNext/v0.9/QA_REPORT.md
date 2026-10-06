# QA REPORT — MiningVolume HS-Next v0.10.4

## Kết luận hiện tại

**PRE-RELEASE — SOURCE/RELEASE PIPELINE READY, AUTOCAD 2023 RUNTIME NOT YET VERIFIED.**

Không gọi đây là Release 1.0 cho đến khi binary build trên Windows được nạp và chạy `MVSELFTEST` thành công trong AutoCAD 2023 thật.

## Kết quả kiểm tra tự động

- Reference/static tests: **129 PASS / 0 FAIL**.
- `go vet installer/main.go`: PASS với payload QA.
- Cross-build installer shell `GOOS=windows GOARCH=amd64`: PASS.
- Kết quả PE: `PE32+ GUI x86-64`.

## Các hạng mục v0.10.4 đã khép kín ở mức mã nguồn

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

## Hạng mục chưa thể xác nhận trong GitHub-hosted CI

Windows CI đã compile thật solution `net48` với AutoCAD.NET 24.2 và tạo Setup. Tuy nhiên runner GitHub không có AutoCAD 2023 host, vì vậy các mục sau **chưa được ghi PASS**:

1. Nạp DLL vào AutoCAD 2023 thật.
2. Ribbon/Palette khởi tạo trong host thật và thao tác Ẩn/Hiện palette.
3. Chọn entity trong DWG thật.
4. Tạo/ghi/đếm hai layer TIN bằng 3DFACE trong database AutoCAD thật.
5. Kiểm tra màu ACI 1/3, khóa layer và bật/tắt TIN trong host thật.
6. `MVSELFTEST` runtime PASS.

Setup bắt buộc tự chạy `MVSELFTEST` trong AutoCAD 2023 và rollback nếu không nhận `Status=PASS`. Chỉ sau runtime gate này mới được coi là đủ điều kiện phát hành chính thức.

## Regression v0.10.4 — UI initialization

- Đã tái hiện lỗi thực tế: `Value '1000' is not valid for 'Value'` khi `NumericUpDown.Value` được gán trước `Maximum`.
- Đã sửa helper `SectionPage.Num`: range trước, value sau.
- Đã thêm static scan toàn plugin để chặn Value-first NumericUpDown.
- Runtime self-test giờ khởi tạo `MainPaletteControl` thật và kiểm tra startup exception.
- Mốc lỗi UI ban đầu đã được khóa bằng regression test; CI hiện tại đã tăng lên 129 test.


## Regression v0.10.4 — Section page layout

- Loại bỏ cơ chế xếp chồng nhiều vùng `Dock=Top + BringToFront` ở trang Mặt cắt.
- Nút chính đổi thành `XUẤT / VẼ MẶT CẮT` và luôn nằm trong hàng thao tác chính.
- Kiểm tra đủ các hàng Khoảng cách / Mức tầng / Từ mức / Đến mức / Tỷ lệ ngang / Tỷ lệ đứng.
- Runtime self-test kiểm tra nút vẽ mặt cắt hiển thị trong palette tối thiểu.


## TIN integrity gate — v0.10.4

- Hai TIN là điều kiện bắt buộc trước mặt cắt/khối lượng.
- TIN được gắn revision của SurfaceModel; sửa dữ liệu nguồn làm TIN invalid ngay.
- CAD output được kiểm chứng: số 3DFACE trên layer phải bằng số tam giác lõi.
- Có thao tác tạo riêng từng TIN và tạo đồng thời cặp TIN.
- Pair build chỉ ghi CAD sau khi cả hai core TIN build thành công; nếu ghi một trong hai thất bại thì xóa cả cặp để không để lại đầu vào nửa vời.
- Runtime self-test có smoke test layer TIN thật trên AutoCAD host.


### Windows CI v0.10.4 — lượt kiểm soát hiện tại
- 129 PASS / 0 FAIL.
- Build succeeded: 0 warning, 0 compile errors.
- Release bundle policy PASS.
- `go vet installer/main.go`: PASS.
- Native Windows Setup build: PASS.
- CI sinh `BUILD_VERIFICATION.txt` và `SHA256SUMS.txt`.
- CAD-host TIN layer smoke test **không chạy trên GitHub-hosted runner**; nó được Setup thực thi bằng `MVSELFTEST` trên máy có AutoCAD 2023 thật.


## QA gate v0.10.4 — TIN calculation input

- Hai layer TIN chính thức phải tồn tại trong DWG.
- Dữ liệu nguồn không được lấy từ chính layer TIN đầu ra.
- TIN phải có tam giác hợp lệ, không NaN/Infinity và không tam giác suy biến.
- Số tam giác trong lõi phải bằng số 3DFACE đã ghi và đếm lại trên layer.
- Chỉnh sửa nguồn X-Y-Z phải invalidate TIN cũ.
- Mặt cắt và khối lượng phải gọi cổng `EnsureBothTinsReady`.
- TIN layer khóa sau build; update do phần mềm kiểm soát.
- Self-test runtime dựng TIN bằng production builder và smoke-test layer AutoCAD thật.


## Runtime gate tự động — AutoCAD 2023 thật

- Có script `release/verify_autocad2023_runtime.ps1` để cài tạm bundle vào `ProgramData/Autodesk/ApplicationPlugins`, chạy `MVSELFTEST` trong AutoCAD 2023 thật, kiểm đúng phiên bản v0.10.4 và khôi phục bundle trước đó sau kiểm thử.
- Runtime verifier xuất `RUNTIME_VERIFICATION.txt` cùng log `MVSELFTEST_RUNTIME.txt`, có version AutoCAD và SHA-256 của 4 DLL MiningVolume.
- Workflow thủ công `.github/workflows/verify-miningvolume-autocad2023-runtime.yml` chỉ chạy trên self-hosted runner có nhãn `autocad2023`.
- Runner phải là Windows x64, có AutoCAD 2023 đã kích hoạt bản quyền và chạy GitHub Actions Runner trong phiên người dùng tương tác; không chạy dưới Windows service không có desktop.
- `main` vẫn không được merge/phát hành nếu workflow runtime chưa PASS.


## Runtime runner hardening

- Launcher self-hosted runner tự yêu cầu UAC và nâng quyền Administrator trước khi chạy `run.cmd`.
- Workflow runtime không còn khóa cứng một đường dẫn AutoCAD; nó dò AutoCAD 2023 R24.2 từ thư mục chuẩn hoặc Registry và truyền đúng `AutoCAD2023Dir` vào build.
- Runtime workflow chỉ cleanup các run cũ khi nhận đúng lệnh `/runtime-autocad2023`; các comment PR khác không được phép hủy runtime gate đang chờ.


## Tách artifact hosted và artifact phát hành

- GitHub-hosted CI chỉ sinh **PRE-RELEASE**: tên artifact và tên Setup đều ghi rõ `PRE_RELEASE`, kèm `PRE_RELEASE_NOTICE.txt`.
- Workflow AutoCAD 2023 thật chỉ build Setup `_RUNTIME_PASS.exe` sau khi đọc được `RUNTIME_VERIFICATION.txt` với `Status=PASS`.
- Artifact `RUNTIME-PASS` kèm `RUNTIME_RELEASE_VERIFICATION.txt`, `RUNTIME_RELEASE_SHA256.txt`, log MVSELFTEST và bundle đã kiểm thử.
- Nhờ đó artifact hosted không thể bị nhầm với bản đã qua runtime gate.


## Build provenance và source archive sạch

- Hosted workflow checkout trực tiếp đúng `SOURCE_SHA` (PR head hoặc push SHA), sau đó so sánh `git rev-parse HEAD`; mismatch làm build FAIL.
- `BUILD_VERIFICATION.txt` ghi cả Source commit và Checked out commit để tránh trường hợp PR merge-ref bị ghi nhầm là head commit.
- Source ZIP được tạo bằng `git archive` từ đúng Git HEAD, không lấy workspace đã build.
- CI kiểm bắt buộc source ZIP có `installer/main.go`, regression tests, runtime verifier và SelfTestService.
- CI chặn `bin/`, `obj/` và `installer/payload.zip` lọt vào source archive.


## Installer elevation và thông báo palette

- Setup dùng `fltmc` để kiểm tra token Administrator, không còn phụ thuộc `net session`/Windows Server service.
- Thông báo cài thành công đã đồng bộ với chính sách UI: MiningVolume **không tự mở palette theo mặc định**; người dùng mở từ Ribbon hoặc bật tùy chọn tự động mở.


## Installer payload hardening

- Setup tự kiểm tra lại payload sau giải nén, không chỉ dựa vào CI.
- Thư mục `Contents/Windows` chỉ được phép có 4 DLL MiningVolume; mọi DLL lạ (kể cả DLL Autodesk hoặc `NetTopologySuite.dll`) làm cài đặt dừng trước khi thay đổi AutoCAD.
- Setup kiểm `PackageContents.xml` phải đúng `AppVersion=0.10.4`, `SeriesMin/SeriesMax=R24.2` và `LoadOnAutoCADStartup=True`.
