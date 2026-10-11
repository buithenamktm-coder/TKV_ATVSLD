# IMSAT MiningVolume V2.0 — Handover kiểm thử tích hợp (11/10/2026)

**Trạng thái: PRE-RELEASE / DRAFT — KHÔNG PHẢI BẢN ĐÃ NGHIỆM THU**

## 1. Nhánh gốc và nguyên tắc tích hợp
- Repository: `buithenamktm-coder/TKV_ATVSLD`, thư mục `tools/MiningVolume_HSNext/v0.9/`.
- Work đang thực hiện V1.0 trên `miningvolume-v1.0`, hiện gốc nhánh cho đợt tích hợp tại commit `4c815086`.
- PR #21: `miningvolume-v2-integration`: chứa chức năng từ #14–#20 đã hòa giải xung đột UI, Commands và Project Snapshot.
- PR #22: `miningvolume-v2-trial15-license`, base #21: dùng thử 15 ngày, key offline + công cụ admin ban đầu.
- PR #23: `miningvolume-v2-mineral-section-regions`, base #21: vùng khoáng sản CAD và phép kiểm tra diện tích mặt cắt (kiểm tra CI trên đúng HEAD trước khi coi là đạt).
- **Không xóa chức năng V1.0, không tự merge vào V1.0, không đổi trạng thái release trước khi đủ kiểm thử host**.
- #22 và #23 là hai nhánh con song song; phải tích hợp tuần tự vào nhánh QA mới và xử lý xung đột như code thật, không copy tệp đè.

## 2. Đối chiếu các chức năng đã có mã
| PR | Chức năng | Phạm vi xác minh |
|---|---|---|
| #14 | Giãn bố trí các mặt cắt xuất CAD, 90 đơn vị mặc định | Kiểm tra khoảng giãn chỉ đổi layout, không đổi khối lượng |
| #15 | TABLE AutoCAD + XLSX hoặc cả hai | Kiểm tra TABLE native, bảng tầng, khoảng MC; không xuất trùng ngoài ý muốn |
| #16 | View 3D TIN hiện trạng/thiết kế, Esc khôi phục Palette | Kiểm tra trong AutoCAD 2023, không chỉ static/test |
| #17, #20 | Lõi giao hai TIN, ô lưới xoay, Ztn/Ztk và 6 sheet Excel | Đối chứng độc lập thể tích, performance, no-data |
| #18 | Bo tròn nút, màu tương phản | Thử 1366×768, Full HD, 100%/125%/150% DPI |
| #19 | Nhiều vỉa, đặt/đổi tên, chọn cặp lộ vỉa, thể trọng | Không suy ra thể tích khoáng sản từ lộ vỉa 2D |
| #22 | Trial 15 ngày, RSA-SHA256 theo máy, License Manager admin CLI | Public signing verification key CHƯA provision trong build thử nghiệm |
| #23 | Polyline khoáng sản trên layer riêng, sửa Grip và cập nhật m² | CHƯA hoàn thành tính thể tích m³/tấn chính thức theo vỉa |

## 3. Test hình học và khối lượng bắt buộc
1. Ô 20×20 m, chênh cao +2 m đồng nhất: đào = **800 m³**, đắp 0.
2. Ô 20×20 m, ΔZ thay đổi tuyến tính −10 đến +10 m: đào **1.000 m³**, đắp **1.000 m³**.
3. Ô biên 175 m² × 2 m: đào **350 m³**.
4. Lưới quay 0°/45° phải cho cùng tổng thể tích toàn vùng, không hở/chồng.
5. Đường bao lõm, vùng có lỗ, no-data phải cho kết quả riêng và trạng thái đủ/thiếu; **không tự coi thiếu TIN là đắp/đào =0**.
6. Mặt cắt có khoáng sản: diện tích vật liệu do người dùng vẽ/chỉnh phải nằm trong vùng đào Ztn–Ztk, không tự cắt và không chồng với vùng cùng MC; đọc lại sau Grip edit.
7. Phép tính hình chóp/hình chóp cụt/TB hai đầu cho khối lượng mặt cắt phải rà soát TT34 và các điều kiện áp dụng thực tế. **Đặc biệt kiểm tra ngưỡng 40% trong V1.0 hiện tại so với quy tắc chốt tại PlanCode**.
8. Nhiều vỉa / nhiều đoạn lộ vỉa: gán đúng GUID; tên vỉa được đổi mà không mất liên kết; trạng thái thiếu 1 cặp hoặc thiếu mặt cắt KHÔNG được coi là diện tích 0.

## 4. Excel và xuất CAD
- Bảng `MiningVolume_LuoiOVuong.xlsx`: 6 sheet đủ, công thức tham chiếu đúng dòng, Arial 12, đóng khung, logo IMSAT, tên liên hệ cố định.
- Kiểm tra việc đổi một ô Excel chỉ làm thay đổi các công thức đã liên kết; Excel không thể tái giao tam giác TIN chỉ bằng chỉnh một ô; phải tính lại trong CAD.
- Xuất TABLE AutoCAD, kiểm tra tọa độ chèn, layer, số dòng, chữ tiếng Việt, không xóa dữ liệu người dùng, kiểm soát phiên tính cũ/đang cập nhật.

## 5. Bản quyền, Setup và bảo vệ dữ liệu
- Vào Palette lần đầu → trial 15 ngày liên tục; đúng trạng thái khi hết ngày 15; thử chạy offline, lùi đồng hồ, mất/corrupt 1 bản sao ledger; DWG vẫn mở được sau hết hạn.
- Key RSA đúng/máy sai/đã sửa dữ liệu/sai mode/đã hết hạn: kiểm tra từ chối. `SUPER KEY` chỉ thuộc quản trị cấp phép và **không phải** chuỗi bypass ở máy khách.
- Khóa riêng không được commit hoặc đóng gói trong Setup. Admin LicenseManager đang ở bước CLI, phải làm UI, sao lưu/khôi phục khóa ký và quy trình cấp key lô trước release.
- `LicenseTrustAnchor.RsaPublicKeyXml` còn là placeholder rỗng: **không được phát hành bản build trả phí** trước khi provision bằng public key chính thức và tự kiểm thử ký/nhận end-to-end.
- Cần bộ cài chọn thư mục đúng cơ chế Autodesk ApplicationPlugins, upgrade/repair/uninstall và runtime AutoCAD host thật.

## 6. Đề nghị Work thực hiện sau khi vào rà soát
1. Fetch PR #21, #22, #23; xác nhận SHA và kết quả CI của từng HEAD trước khi tích hợp.
2. Dựng nhánh QA riêng từ V1.0 lõi đủ chứng cứ, merge/cherry-pick theo thứ tự phụ thuộc và đối chiếu diff, không phá tính năng.
3. Build Windows và chạy cả core/reference tests, sau đó cài add-in vào AutoCAD 2023 thực tế.
4. Bổ sung **thể tích m³, tấn, hệ số bóc theo từng vỉa** dựa trên bộ mặt cắt xác nhận hoặc mô hình địa chất 3D được duyệt; không lấy hai đường lộ vỉa 2D làm dữ liệu chiều dày.
5. Hoàn thiện các UI còn thiếu, Setup, License Manager admin thực tế, nhật ký audit và test hiệu năng.
6. Tạo `QA_REPORT`, ma trận Release Gate, log/ảnh host runtime và danh sách lỗi chưa đạt. KHÔNG tuyên bố V2.0 hoàn thành khi còn cổng chưa nghiệm thu.

**Đây là handoff kỹ thuật — không tự động thay thế PlanCode gốc hoặc bằng chứng runtime.**
