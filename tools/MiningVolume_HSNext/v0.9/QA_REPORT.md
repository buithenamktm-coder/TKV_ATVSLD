# QA REPORT — MiningVolume HS-Next v0.10.4

## Kết luận hiện tại

**PRE-RELEASE — SOURCE/RELEASE PIPELINE READY, AUTOCAD 2023 RUNTIME NOT YET VERIFIED.**

Không gọi đây là Release 1.0 cho đến khi binary build trên Windows được nạp và chạy `MVSELFTEST` thành công trong AutoCAD 2023 thật.

## Kết quả kiểm tra tự động

- Reference/static tests: **100 PASS / 0 FAIL**.
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

## Hạng mục chưa thể xác nhận trong môi trường hiện tại

1. Compile solution thật bằng Windows/.NET Framework toolchain.
2. Nạp DLL vào AutoCAD 2023 thật.
3. Ribbon/Palette khởi tạo trong host thật.
4. Chọn entity trong DWG thật.
5. Vẽ TIN/mặt cắt trong database AutoCAD thật.
6. `MVSELFTEST` runtime PASS.

Các mục này phải do Windows release runner + AutoCAD 2023 runtime gate xác nhận trước khi phát hành chính thức.

## Regression v0.10.4 — UI initialization

- Đã tái hiện lỗi thực tế: `Value '1000' is not valid for 'Value'` khi `NumericUpDown.Value` được gán trước `Maximum`.
- Đã sửa helper `SectionPage.Num`: range trước, value sau.
- Đã thêm static scan toàn plugin để chặn Value-first NumericUpDown.
- Runtime self-test giờ khởi tạo `MainPaletteControl` thật và kiểm tra startup exception.
- Windows CI cuối: 88 PASS, build thành công, 0 compile errors, release bundle policy PASS.


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


### Windows CI v0.10.4
- 100 PASS / 0 FAIL.
- Build succeeded, 0 compile errors.
- Release bundle policy PASS.
- CAD-host TIN layer smoke test được thực thi bởi `MVSELFTEST` sau khi cài trên AutoCAD 2023 thật; CI không giả lập kết quả runtime này.


## QA gate v0.10.4 — TIN calculation input

- Hai layer TIN chính thức phải tồn tại trong DWG.
- Dữ liệu nguồn không được lấy từ chính layer TIN đầu ra.
- TIN phải có tam giác hợp lệ, không NaN/Infinity và không tam giác suy biến.
- Số tam giác trong lõi phải bằng số 3DFACE đã ghi và đếm lại trên layer.
- Chỉnh sửa nguồn X-Y-Z phải invalidate TIN cũ.
- Mặt cắt và khối lượng phải gọi cổng `EnsureBothTinsReady`.
- TIN layer khóa sau build; update do phần mềm kiểm soát.
- Self-test runtime dựng TIN bằng production builder và smoke-test layer AutoCAD thật.
