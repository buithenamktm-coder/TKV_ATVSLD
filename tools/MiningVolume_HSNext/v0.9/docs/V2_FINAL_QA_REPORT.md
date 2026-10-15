# Trạng thái mới nhất — QA TIN/Excel 12/10/2026

**QA CANDIDATE — NOT OFFICIAL RELEASE.** Báo cáo hiện hành: [V2_FINAL_TRACEABILITY_20261012.md](V2_FINAL_TRACEABILITY_20261012.md); nguyên nhân và bằng chứng P0: [P0_TIN_RENDER_AUDIT_20261012.md](P0_TIN_RENDER_AUDIT_20261012.md); [hướng dẫn cài/gỡ/kiểm DLL](V2_QA_INSTALL_VERIFY_20261012.md).

Lỗi renderer từ chối tam giác dương nhỏ đã sửa ở `c13f321`; SQ HT đúng **1.681.755** và TK **1.672.875** support faces qua native/core/shared renderer validation. HT có **14** face dương dưới ngưỡng cũ, không bị loại. Windows CI c13 run38212838355 PASS (88 C#,249 Python,5 Go). Sau sửa Excel/danh mục: local **89 C# +249 Python PASS**, clip GEOS4/4 và3 workbook recalc độc lập PASS. CI/checksum final HEAD được đóng trong `FINAL_BUILD_EVIDENCE.json` của gói tải, không tái sử dụng hash lịch sử.

**G1:** bằng chứng build/test/Setup ở mốc c13 có thật; final HEAD phải CI lại. **G2/G3:** lõi và fixture SQ PASS, AutoCAD/DWG/HSMo NOT TESTED. **G4:** toàn luồng CAD/Excel Desktop/UI NOT TESTED. **G5:** Windows GUI install/uninstall NOT TESTED, khóa RSA sản xuất BLOCKED, Setup unsigned. **G6:** hồ sơ QA có, chưa biên bản nghiệm thu ký xác nhận. Không được công bố hoàn thành toàn bộ V2.0.

---
## Hồ sơ lịch sử dưới đây — số test, SHA, hash và trạng thái chỉ đúng tại mốc ghi rõ, không phải kết quả final HEAD

# IMSAT MiningVolume V2.0 — báo cáo QA ngày 11/10/2026

**Trạng thái: QA CANDIDATE, CHƯA HOÀN THÀNH NGHIỆM THU. Không phát hành chính thức.**

Nhánh `miningvolume-v2-hs-all-sources-tin`, PR #26. Kế thừa dự án hiện có; không sửa nhánh `miningvolume-v1.0`.
Áp dụng toàn bộ PlanCode `PLANCODE_WORK_MININGVOLUME_V2_FINAL_20261011.md`.

## Mốc triển khai và lỗi đã khắc phục

| Mốc | Commit | Sửa lỗi / bằng chứng |
|---|---|---|
| P0.1 | `1b4d6f8d7c0c623b43cf267845eb3f08343e1b39` | Bổ sung handler loại/sửa Z; hợp nhất hai cửa sổ; xác nhận tiếng Việt mặc định Không; chặn nguồn cũ/điểm sinh; giữ provenance khi clipping; cắt TIN thử đúng polygon. 216 reference + 62 C# PASS. Windows run 38158413718 PASS. |
| P0.2 | `4a83b44a440f3fe81a2da8f9f7dc33f6e5bfdc27` | Nhật ký old/new/time/user, khôi phục điểm, fingerprint hình học khi mở DWG, giới hạn giải nén, triển khai Setup V2 QA trên nhánh PR26. 216 reference + 63 C# PASS; Windows run 38158783762 PASS, có V2 QA installer. |
| P0.3 | Commit chứa báo cáo này | Khôi phục nhiều điểm qua chỉ mục, kiểm tra toàn bộ lựa chọn trước sửa; giữ nguyên tọa độ đầu mút; CSV từng nguồn; 218 reference + 63 C# PASS; SHA256 và thông tin nguồn riêng cho Setup V2. CI cuối phải đọc theo chính HEAD và V2_BUILD_VERIFICATION.txt trong artifact. |

Các lỗi UI đã sửa ở mức mã/build: thiếu `ExcludeVertexAsync(Row)`/`EditVertexZAsync(Row)`; Zoom hàng nhóm gây null; bản quét cũ còn cho sửa sau hủy; lọc layer làm mất thành viên khác trong nhóm; nút thao tác nằm ngoài phần nhìn thấy; cửa sổ trung bình Z có luồng xử lý riêng thiếu thống nhất. Cửa sổ dùng chung bảng VirtualMode, mặc định >3 m, mỗi điểm có Zoom/Loại/Sửa; điểm nội suy không có provenance thật không được sửa/xóa. Chỉ sửa mô hình và vô hiệu dữ liệu dẫn xuất. Thao tác model không mở CAD nguồn để ghi.

Fingerprint chặn áp lại chỉ số đỉnh khi hình học DWG đã đổi. Bản lưu cũ thiếu fingerprint được cảnh báo và không áp sửa đỉnh tự động; không tự dựng lại kết quả khi có cảnh báo nguồn. Cần nghiệm thu lưu/mở DWG trong host, không coi test codec là bằng chứng đầy đủ.

## Kiểm thử đã thực hiện

| Kiểm tra | Actual | Kết luận / giới hạn |
|---|---|---|
| pytest reference | 218 passed | PASS; có kiểm tra cấu trúc, không phải toàn bộ test runtime |
| C# CoreRegression + CDT C++ thật | 63 tests, 0 failed | PASS; nguồn Core/Surface thật, không mock hình học |
| GEOS độc lập so mesh C# | 4/4 | PASS; biên lõm, dải hẹp, polygon xiên, cắt hull; sai số diện tích/Z lớn nhất ~7.11e-15 |
| Ngưỡng preview | 0.001/3.000 không vượt; 3.001/94.862 vượt | PASS; TIN chính thức vẫn từ chối xung đột theo dung sai 1e-4 m |
| Mô hình / persistence | loại đúng 1 đỉnh; restore không sửa đỉnh khác; reject NaN; roundtrip nhật ký; fingerprint đổi theo hình học gốc | PASS tại lõi/codec, DWG thực NOT TESTED |
| Windows build và LicenseManager self-test | run 38158783762 PASS | Chỉ chứng minh build/CLI self-test; chưa chứng minh máy quản trị độc lập |
| AutoCAD 2023 tương tác, DPI, Esc, nhiều DWG | Không có Windows/AutoCAD host trong Work này | NOT TESTED |

Lệnh tái lập (tại thư mục sản phẩm):

```text
python -m pytest tests/reference -q
python tests/core/run_core.py --dotnet-root <dotnet-sdk-8-root>
python tests/core/run_core.py --dotnet-root <root> -- --clip-oracle > oracle.jsonl
python tests/core/verify_clip_oracle.py oracle.jsonl
python tests/core/run_core.py --dotnet-root <root> -- --review-fixture <private-model.bin> <boundary.bin-or-dash> 600
```

Log tổng hợp nằm tại `docs/qa/20261011/`. Binary fixture, tọa độ và handle mỏ được giữ riêng, không commit.

## Hai DXF và dữ liệu HSMo

Số đo dữ liệu thật ghi nhận trên lõi ở mốc P0.2; các log giữ nguyên, không đổi số đo thành kết quả host. Adapter đọc DXF theo WCS; LW arc bước tối đa 0.5 m. Đây là quét dữ liệu qua adapter, **không phải thao tác AutoCAD**.

| Nguồn / mô hình | Đỉnh đầu vào | Nhóm preview >3 m | Nhóm strict | Thời gian tổng quét mean + strict |
|---|---:|---:|---:|---:|
| Vỉa 4 HT, LO_TINHKL | 1,220,951 | 20 | 4,137 | 35.826 s |
| Vỉa 4 TK, LO_TINHKL | 858,952 | 15 | 11,737 | 30.131 s |
| SQ cũ HT, không biên | 952,593 | 93 | 1,297 | 24.179 s |
| SQ cũ TK, không biên | 945,685 | 96 | 1,259 | 24.348 s |
| Test HS HT, LO_TINHKL | 952,593 | 93 | 1,294 | 30.350 s |
| Test HS TK, LO_TINHKL | 945,685 | 96 | 1,256 | 30.502 s |

**Cả sáu trường hợp đều BLOCKED đúng vì còn xung đột nguồn; chưa dựng thành công hai TIN chính thức trên các file này.** Không tự chọn Z cao/thấp/trung bình thay quyết định khảo sát. Strict còn xét giao/chồng breakline; số nhóm không thể đồng nhất với preview chỉ xét đỉnh trùng XY. Không tuyên bố khớp con số 1.293 của ảnh cũ.

SQ có ΔZ tối đa 94.862 m. Mức managed memory tại cuối quét ~0.61–1.38 GB, không phải peak RAM/working set; chưa nghiệm thu RAM/UI trong AutoCAD. Các bài kiểm tra chạy có timeout 600 s, đều hoàn thành. Chưa chứng minh vẽ hàng triệu mặt CAD không treo.

Đã lấy lại `Test HS.dxf`, `Tinh kl SQ 2026.09.091.dxf`, `Tinh kl SQ 2026.09.09.dwg`. Hai DXF có các lớp !HT/!TK, mỗi lớp gồm 6 CIRCLE và 5 SPLINE ngoài danh sách loại nguồn hỗ trợ; adapter báo rõ unsupported, không tính chúng như điểm/polyline đã hỗ trợ. Test HS có HS_MATCAT và LO_TINHKL. DWG chưa mở trong AutoCAD; chưa giải mã HDAI_TIN_MODEL_OBJ, chưa đối chứng đường mặt cắt HSMo. Không nhận định thuật toán HSMo là trung bình Z.

## P1 → P2 → P3: trạng thái từng chức năng

PlanCode yêu cầu nghiệm thu P0 trước khi tập trung sâu vào khối lượng. Vì TIN mỏ còn cần người có thẩm quyền chọn/sửa Z và thiếu host, các mục sau **không được đánh dấu hoàn tất**.

| Chức năng | Chứng cứ hiện có | Chưa đạt / chưa kiểm thử |
|---|---|---|
| Menu/Ribbon/Palette/bo nút/logo | Kế thừa, build PASS | 1366×768/FullHD, DPI 100/125/150%, mọi nút host |
| Hai TIN + View 3D | Core test/build | TIN mỏ đã sửa nguồn; Esc/orbit/chuyển DWG/layer |
| Mặt cắt + khoảng giãn | Kế thừa build/reference | MC 20m HSMo, đo CAD, giãn không đổi cự ly |
| Lưới ô vuông | Native C# cut/fill, xoay, biên lõm, lỗ, no-data PASS | Grid lớn CAD; nghiệm thu tổng và ô thực |
| Khối lượng / TT34 | Kế thừa mã và checks | Rà soát toàn bộ lựa chọn công thức/đơn vị/điều kiện TT34 sau P0; chưa nghiệm thu nghiệp vụ |
| Nhiều vỉa/thân quặng | Core nhiều vỉa, thiếu dữ liệu, miền đào, chồng lấn PASS | Grip/XData, đổi tên, lưu lại, hình học và địa chất thực |
| CAD TABLE / Excel | Build và reference | Excel desktop tính lại, layout Arial12/logo; bảng CAD thực |
| Trial 15 ngày / RSA | Windows issuer CLI self-test PASS | Public trust anchor còn rỗng; trial thời gian thật và máy quản trị độc lập NOT TESTED; admin UI chưa nghiệm thu |
| Setup V2 | QA .exe build PASS, manifest NOT RELEASE | Cài/nâng cấp/rollback/gỡ trên AutoCAD thực; chữ ký số chưa có |
| AutoCAD 2012+ | Không có chứng cứ | KHÔNG hỗ trợ phát hành theo tuyên bố hiện tại; mục tiêu 2023 x64 |

## Bàn giao và bước bắt buộc

Setup chỉ phục vụ người kiểm thử: artifact **IMSAT-MiningVolume-V2.0-QA-CANDIDATE-NOT-RELEASE** trong workflow đúng HEAD. `V2_SHA256SUMS.txt` dành riêng cho V2 .exe; không dùng SHA256 của installer V1 hoặc ZIP thay thế. `V2_BUILD_VERIFICATION.txt` ghi source commit/run/hash. Cảnh báo C4068 của thư viện CDT về GCC pragma trên MSVC đã thấy trong build; không có compile error, không coi đây là crash host.

Chạy `V2_AUTOCAD_ACCEPTANCE.md` trên Windows AutoCAD 2023, cung cấp log/ảnh/DWG bản kiểm thử và quyết định xử lý cao độ nguồn. Cần PUBLIC RSA key do máy quản trị tạo và chứng cứ backup/restore độc lập; không gửi private key vào repo/chat. Không tạo tag hoặc Release V2.0 khi còn gate bắt buộc chưa đạt.

---

## Phụ lục A — cập nhật nghiệm thu mã nguồn 12/10/2026 (THAY THẾ số liệu CI cũ)

**Chú ý:** Các dòng thống kê 218 reference/63 C# và nhánh PR #26 ở phần đầu báo cáo là **LỊCH SỬ 11/10**, không phải kết quả trên mã nguồn mới nhất. Mốc kiểm tra code hiện hành là nhánh `miningvolume-v2-chatgpt-p0-continuation` (PR #27), source SHA `983e3e27aeffac67c89a5a5328eff44f87f7411f`.

### Các sửa đổi thực hiện và đã commit

- `eb512e01` (Work): xử lý RF-01…RF-05 và RF-08, RF-13, RF-14: XData và nhãn mặt cắt, khối lượng sửa tay theo tầng, kiểm toán 3 workbook, atomic write/rollback Excel, installer và test định lượng. Commit ban đầu có **4 lỗi compile CS1503** do `FingerprintGuid.ToString("D")` không khớp API.
- `ebfc611a` và `b6df5cbc`: sửa 4 chỗ gọi `FingerprintGuid.ToString("D")` trong `EditableSectionCadService.cs`, `SectionCadRenderer.cs` sang `FingerprintGuid.ToString()`, giữ logic so sánh hai đầu nhất quán. Sau đó Windows CI hai luồng **PASS**.
- `c71a84b4`: khi uninstall V2 sau nâng cấp V1→V2, kiểm tra và xóa có giới hạn các tên Setup IMSAT đã biết trong thư mục cài, không quét xóa DWG/license/báo cáo.
- `983e3e27`: bổ sung regression test Windows xác minh chỉ các binary Setup IMSAT đã biết bị chọn để dọn, không ra ngoài thư mục cài.

### Test, build và gói V2.0 QA đã đạt

| Hạng mục | Actual | Trạng thái |
|---|---|---|
| Python reference | 228 passed | PASS |
| C# Core + native CDT, report builders và XML/XLSX/logo | 73 passed, 0 failed | PASS |
| Go installer tests trên Windows | 4 passed | PASS |
| Build .NET add-in AutoCAD 2023 API | Zero compile errors | PASS |
| License issuer backup/self-test (CLI) | PASS | PASS trong CI, CHƯA xác thực máy quản trị độc lập |
| Windows source QA | Run [38184927012](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38184927012) | PASS trên đúng code SHA |
| Windows pull-request QA | Run [38184930156](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38184930156) | PASS trên đúng code SHA |
| Setup QA V2 | Artifact `IMSAT-MiningVolume-V2.0-QA-CANDIDATE-NOT-RELEASE`, ID 11719058004 | BUILD PASS, AutoCAD runtime NOT TESTED |
| SHA256 V2 EXE | `5cec0e087215d4361c0aac9bd0a34fc69a1bb0dd71335ffd0a34b5d1e50ebb3e` | PASS, lấy từ chính CI |
| T01–T22 thao tác AutoCAD thực | Chưa có host AutoCAD 2023 trong phạm vi kiểm thử này | NOT TESTED |
| Excel Desktop mở, recalculation, print | Chỉ có kiểm tra cấu trúc/engine XLSX | NOT TESTED |
| TIN chính thức trên dữ liệu SQ thực tế | Còn xung đột ΔZ >3,000 m chưa được quyết định | BLOCKED |
| RSA PUBLIC trust anchor và kích hoạt license thật | `RsaPublicKeyXml=""` | BLOCKED |

**Giới hạn:** 228/73/4 PASS không chứng minh trải nghiệm AutoCAD hay cài đặt Windows thực tế. Dữ liệu thử nghiệm có đáp án biết trước không thay cho dữ liệu SQ đang có xung đột khảo sát. Phần cần host chưa được phép nhận hoàn thành.

### Kết luận kỹ thuật và bước nghiệm thu tiếp theo

Cả hai workflow ở source SHA `983e3e27` đã đủ bằng chứng để đóng chốt *build tự động* và tạo QA Setup. Tuy nhiên, các chốt *nghiệm thu AutoCAD, DXF SQ thật, public RSA và cài/gỡ trực tiếp* vẫn chưa thể PASS. Xem bảng trạng thái mới tại `V2_RELEASE_GATES.md`; Work tiếp tục thực hiện PlanCode `PLANCODE_WORK_MININGVOLUME_V2_FINAL_AUDIT_RELEASE_20261012.md`. Không cấp nhãn FINAL/RELEASE nếu thiếu chứng cứ bắt buộc.

---

## Phụ lục B — triển khai chống mất mặt cắt và kiểm toàn bộ nguồn (12/10/2026)

**Đây là bản cập nhật mới nhất thay thế bảng test tự động cũ trong Phụ lục A.** Source SHA đã PASS: `e97a4a6e07ee4d38f8ecbc51857c728b054e6f6a`. Hai luồng Windows cùng SHA: [push 38185748918](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38185748918) và [PR 38185752716](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38185752716) **SUCCESS**.

### Triển khai trực tiếp sau đợt audit 983e3e27

- ProjectSnapshot có `HadEditableSectionCad`: khi lưu DWG ghi lại CAD output đã xuất hoặc đã được chỉnh tay và nhận kết quả; nếu thiếu toàn bộ CAD paths lúc reopen, không tự chép kết quả gốc từ TIN thay cho hình đã hiệu chỉnh. Project cũ định dạng 2 vẫn giải mã được.
- `ModelSnapshot.FullSourceFingerprint` và `SourceFingerprint.ComputeModel`: lấy SHA-256 nguồn ban đầu của tất cả entities/vertices, có layer/handle/loại/closed và Z gốc, sắp theo handle, không tính loại điểm hay Z override chỉ trong IMSAT. Nạp lại DWG đối chiếu hash trước khi áp chỉnh sửa lưu, thay đổi bất kỳ source entity nào đều chặn kết quả dẫn xuất và hiện cảnh báo. Dùng một luồng hash thay vì tạo từng triệu SHA256 riêng.
- `SectionPage.BuildProfiles`: khi có MC CAD, yêu cầu xác nhận Yes/No mặc định No trước khi xuất đè, không tự xóa CAD đã chỉnh nếu người dùng từ chối. Hủy điểm chèn trước draw không commit mặt cắt mới thay trạng thái cũ.
- `SectionWorkflowService.DrawProfiles` nhận candidate profiles riêng; chỉ commit khi vẽ xong. Nếu active DWG hoặc TIN/hệ MC đổi khi Task.Run đang tính và khi chọn điểm chèn, dừng kết quả stale.
- Cập nhật `V2_AUTOCAD_ACCEPTANCE.md` để gom thêm 5 ca kiểm thử host: xác nhận vẽ đè, hủy chọn điểm, xóa toàn bộ CAD đã lưu, đổi bản vẽ giữa tính, Excel và uninstall. Đây là hướng dẫn nghiệm thu, không phải bằng chứng runtime.
- Test mới `tests/reference/test_v2_section_persistence_safety.py` và bổ sung `tests/core/CoreRegression.cs`: roundtrip marker/digest và kiểm fingerprint nhạy với CAD gốc nhưng không nhạy với override nội bộ, bất biến theo thứ tự đối tượng.

### Kết quả CI độc lập trên đúng source SHA

| Loại | Bằng chứng | Trạng thái |
|---|---|---|
| Python reference | **230/230 passed** | PASS |
| C# Core + native CDT | **73/73 passed** | PASS |
| Go installer trên Windows | **4/4 passed** | PASS |
| .NET AutoCAD 2023 Plugin Release build | Windows Actions | PASS |
| License issuer self-test, native installer + QA packaging | Windows Actions | PASS cho CI |
| V2 QA artifact | `IMSAT-MiningVolume-V2.0-QA-CANDIDATE-NOT-RELEASE`, ID **11720040080** | Có, NOT RELEASE |
| V2 QA Setup EXE SHA-256 | `ee3e5a6aed388f6b9d9ddc0c6f873e63b97ad276857a05997442fca555e99cde` | Đã đối chiếu log CI |
| AutoCAD tương tác T01–T22 + SAVE/SAVEAS, Excel Desktop | Không có host chứng thực trong phiên sửa | NOT TESTED |
| DXF SQ nguồn có ΔZ >3 m, RSA verifier public key | Chưa xử lý dữ liệu chuyên môn/cấp khóa công khai | BLOCKED |

**Bản QA có thể tải từ artifact của run 38185748918 để thử trên môi trường kiểm soát.** Không dùng artifact tên V1.0, không phát hành chính thức khi host, SQ và licensing chưa qua gate. Chi tiết gate mới nhất: `V2_RELEASE_GATES.md`.

---

## Phụ lục C — chống kết quả khối lượng cũ và chuyển nhiều DWG (12/10/2026)

**Bản code đã xác minh:** `536f3aaa8875ebf4ab9fe4fcf7ba677db2e018de`. Hai CI độc lập cùng SHA [38186674934](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38186674934), [38186670773](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38186670773) đều **SUCCESS**.

### Các lỗi vừa sửa thực tế

- Chọn lại đường bao/hướng mặt cắt tự vô hiệu kết quả khối lượng trước; không cho Excel đọc lại V cũ.
- Trang khối lượng so document/TIN/source revision/danh sách mặt cắt/giới hạn mức trước khi commit phép tính chạy ở Task.Run; nếu thay đổi, giữ nguyên kết quả cũ và yêu cầu tính lại.
- Lưới ô vuông lưu boundary handle và thông số ô cùng kết quả; khi tính bất đồng bộ phải kiểm lại active DWG, TIN, boundary handle, tọa độ đa giác, kích thước và góc quay. Core regression xác minh thay boundary hoặc size không thể commit output cũ.
- Khi dựng cặp TIN HT/TK bất đồng bộ, chặn ghi kết quả từ DWG trước sang DWG khác đã được kích hoạt.
- Khi AutoCAD kích hoạt DWG khác, Palette kết quả cũ được ẩn và report lưới bị vô hiệu; Ribbon khi mở lại sẽ nạp Project theo DWG đang hoạt động. Toggle Palette, 3D và lệnh lưu Project đều qua kiểm tra nguồn hiện hành. Chưa nghiệm thu hiển thị/khôi phục trên AutoCAD.
- Các kiểm thử cũ về layout-gaps và Toggle Palette được cập nhật để đánh giá hành vi mới (giữ xác nhận giãn cách, bật/tắt thật), không bỏ test.

### Bằng chứng tự động

| Hạng mục | Bằng chứng | Trạng thái |
|---|---|---|
| Python static/reference | **235/235 passed** | PASS |
| C# CoreRegression + native CDT, bao gồm kiểm stale-grid | **74/74 passed** | PASS |
| Go Windows installer | **4/4 passed** | PASS |
| Release build AutoCAD 2023 API + issuer test + QA Setup | 2 workflow cùng SHA | PASS trên CI |
| QA V2 artifact | `IMSAT-MiningVolume-V2.0-QA-CANDIDATE-NOT-RELEASE`, ID **11720415555** (run 38186674934) | Đã build, không phải Release |
| SHA256 QA EXE | `8793337e020a4398d64675a48a3ac9d63ab4599bdfcc35c4e3846ed72eb30b69` | Đối chiếu Windows CI |
| AutoCAD 2023 tương tác, T01–T22, data SQ, Excel Desktop, license RSA thật | Chưa có bằng chứng host/nguồn được quyết định | **NOT TESTED / BLOCKED** |

**Trạng thái bàn giao:** QA CANDIDATE, chưa đạt hard release gates. Cần một đợt nghiệm thu trên Windows AutoCAD 2023 thật trước khi kết luận V2.0 chính thức. Không sửa nhánh `miningvolume-v1.0`.

---

## Phụ lục D — xác thực CAD grip khi xuất và nhận diện SAVEAS (12/10/2026)

**Mốc mã nguồn PASS:** `32c0cfedbbd177eb0c97ccac91f71845dac27ff4`. Hai lượt [CI Push 38187451763](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38187451763), [CI PR 38187454588](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38187454588) cùng source SHA **SUCCESS**.

### Phát hiện và sửa lỗi trực tiếp

1. **Lưới ô vuông / dữ liệu biên:** `GridWorkflowService.EnsureBoundaryCurrent` đối chiếu tọa độ XY tất cả đỉnh CAD hiện tại với polygon lúc tính (dung sai 1e-7 m). Vẽ CAD/Excel phải đọc lại polygon thật và hủy quyền sử dụng báo cáo cũ nếu bị Grip, **kể cả handle không đổi**; kiểm thử lõi xác nhận invalidation sau dịch một đỉnh.
2. **Mặt cắt / số liệu xuất:** `EditableSectionCadService.EnsureProfilesMatch` đọc metadata và hình học HT/TK mới nhất, so với bộ đã chốt; `ExportPage` kiểm trước và sau giai đoạn dựng báo cáo, trước CAD TABLE. Nếu người dùng sửa Grip nhưng chưa cập nhật F/V, không ghi Excel sai; bản vẽ/TIN/source revision/VolumeResult đổi cũng chặn.
3. **So sánh hình CAD sau phép biến đổi tọa độ:** `EditedSectionProfileBuilder.SameGeometry` dùng dung sai tuyệt đối 1e-6 m để tránh coi sai số double của phép tịnh tiến/phóng đại thành sửa tay, nhưng vẫn phát hiện dịch Grip 0,001 m.
4. **Save As / nhiều DWG:** AutoCAD có thể giữ cùng `Database.FingerprintGuid` khi Save As thành file mới. `EntryPoint` dùng thêm tham chiếu `Document` của phiên mở để phân biệt A/B dù GUID trùng, ẩn Palette cũ và nạp Project thích hợp. Trước đây chỉ so GUID.
5. Cập nhật `tests/core/FinalAuditRegression.cs`, `tests/reference/test_v2_async_result_safety.py` và `tests/reference/test_cad_volume_export_static.py`; test cũ chuyển sang kiểm hàm guard tập trung thay vì kỳ vọng kiểm tra inline cũ. Checklist `V2_AUTOCAD_ACCEPTANCE.md` bổ sung đối chứng thực Save As cùng GUID, sửa polygon/mặt cắt sau tính và Excel.

### Bằng chứng CI trên code SHA 32c0cfed

| Kiểm tra | Kết quả |
|---|---|
| Python reference | **238/238 PASS** |
| C# CoreRegression / native CDT / XLSX production | **76/76 PASS** |
| Windows Go Installer | **4/4 PASS** |
| .NET AutoCAD 2023 Plugin / license issuer self-test / Setup QA | **PASS** |
| 2 luồng CI Windows (Push, PR) | **SUCCESS** |
| Artifact Setup QA V2 | `IMSAT-MiningVolume-V2.0-QA-CANDIDATE-NOT-RELEASE`, artifact ID `11719703622` từ run `38187451763` |
| SHA-256 của EXE Setup V2 QA | `dac67dda9e398ff42f379e3491fdd8f7e215323fa7316fe625e9ef8cb4766dac` |
| Host AutoCAD 2023, Excel Desktop, cài/gỡ trên máy thật | **NOT TESTED** |
| Nguồn SQ còn ΔZ >3 m, khóa công khai RSA chưa provision | **BLOCKED** |

**Kết luận:** mã nguồn và Setup QA xây dựng thành công. Release V2.0 chính thức vẫn **BLOCKED** theo `V2_RELEASE_GATES.md`; không có bằng chứng Windows/AutoCAD tương tác, cũng không tự quyết định sửa Z khảo sát hoặc tự tạo khóa kích hoạt giả.

---

## Phụ lục E — TIN tự xử lý điểm trùng XY khác Z (HSMo-like, 12/10/2026)

**Yêu cầu chốt:** bỏ bước bắt buộc loại/sửa thủ công từng đỉnh trùng XY khác Z trước khi tạo TIN. Sau khi nạp dữ liệu Point/đường đồng mức/Polyline/Line, phần mềm thống kê nhóm trùng XY nhiều Z và đỉnh trùng cả XYZ, cảnh báo trước khi dùng khối lượng, nhưng vẫn dựng TIN 3D có mặt đứng. Cho phép hiệu chỉnh MC đã vẽ và bấm CẬP NHẬT MẶT CẮT để chốt F/V.

### Đã thực hiện bằng mã nguồn

- `SurfaceInputPreparer`: chế độ `PreserveDuplicateXYVerticalFaces` (mặc định bật tại workflow CAD) chuẩn hóa một XY hỗ trợ CDT bằng Z thấp nhất, trong khi toàn bộ Z khác và nguồn gốc nằm ở các `VerticalConflictGroups`, **không sửa/chỉnh dữ liệu CAD gốc**. Hỗ trợ cả dữ liệu tọa độ thô không có handle, tránh mất Z âm thầm.
- `VerticalFaceBuilder`: tạo tam giác 3D có cạnh đứng từ từng cặp cao độ liên tiếp và cạnh tam giác kề; giữ ở `TinSurface.VerticalFaces` tách khỏi tam giác planar CDT.
- `TinCadRenderer` + `SurfaceWorkflowService`: ghi cả các mặt đứng ở dạng `3DFACE` đúng layer kết quả hiện trạng/thiết kế, kiểm tra số mặt, dựng lại từ Project, kể cả vùng dữ liệu lớn/file-backed.
- `ConflictScanner` + `TinSurface` + DataPage: thống kê số nhóm XY khác Z, số đỉnh nguồn và số đỉnh trùng hoàn toàn XYZ; một cảnh báo sau khi dựng TIN yêu cầu kiểm tra dữ liệu điểm/đường đồng mức, **không yêu cầu loại đỉnh thủ công**.
- `TinSectionSampler` đánh dấu mặt cắt cần kiểm tra khi có mặt đứng; `SectionPage` hiển thị `CẦN KIỂM TRA MC`; `VolumeWorkflowService` và `SectionVolumeCalculator` không chốt khối lượng cho tới khi mặt cắt được duyệt/cập nhật Grip. `GridVolumeCalculator` không tạo kết quả ô lưới 2,5D sai giả khi có nhiều Z trên cùng XY.
- Mọi chức năng strict 2,5D khi gọi core không bật tùy chọn vẫn kiểm tra lỗi nguồn, không vô tình làm thay đổi nghiệp vụ các phiên bản cũ.

### Kiểm thử trên mã nguồn

**Source SHA:** `b81011af0149565585d081a9ba3fb0a9a458c02b`.  
**CI SUCCESS cùng SHA:** [Push 38192286646](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38192286646), [PR 38192289465](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38192289465).

| Hạng mục | Kết quả |
|---|---:|
| Python regression/reference | **242 PASS** |
| C# CoreRegression + native CDT | **81 PASS** |
| Go Windows Installer regression | **4 PASS** |
| Plugin AutoCAD 2023 .NET Release / QA Setup | **PASS trong CI** |
| Dữ liệu thử Point XY=(5,5), Z=10/20 và contour trùng XY khác Z | TIN có `VerticalFaces`, nguồn Z giữ nguyên, MC có cảnh báo: **PASS core** |
| Trùng hoàn toàn XYZ, dữ liệu raw không handle, vùng multiple-Z 2,5D grid | Đếm đúng, giữ nguyên nguồn, không phát sinh mặt đứng giả, chặn khối lượng lưới không phù hợp: **PASS core** |
| Windows artifact V2 QA | `IMSAT-MiningVolume-V2.0-QA-CANDIDATE-NOT-RELEASE` ID **11722695028** |
| SHA-256 của EXE QA | `a928f542d4905c02f39e4fdc57bf9ec4847875fe7ed974fc23b3cbdd2ea057d0` |

**Chưa nghiệm thu:** AutoCAD 2023 thực tế, TIN/mặt cắt đối chứng với HSMo tại mỏ SQ, lưu/khôi phục trong DWG, Excel Desktop và Setup trên Windows thực. Đặc biệt hình học 3D mặt đứng kiểu fan *chưa được chứng minh trùng hoàn toàn thuật toán HSMo*; phần MC hiện lấy đường từ support 2,5D và báo hiệu chỉnh, chưa tự biểu diễn nhánh đoạn đứng ở một station trên profile. Một số giao cắt breakline khác Z **không có hai đỉnh gốc trùng XY** còn có thể cần thuật toán topology 3D chuyên biệt. Không coi phần này là RELEASE chính thức chỉ nhờ CI xanh.

Xem `ADR_V2_DUPLICATE_XY_VERTICAL_FACE_SECTION_EDIT_20261012.md`, `V2_AUTOCAD_ACCEPTANCE.md` ca V-01 đến V-05 và `V2_RELEASE_GATES.md` bản mới.
