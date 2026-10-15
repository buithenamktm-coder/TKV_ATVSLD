# IMSAT VOLUME V2 — chưa đủ điều kiện nghiệm thu phát hành

**Kết luận hiện tại: BLOCKED. Không có Setup V2.0 chính thức bàn giao.**

Đã tiếp tục trên nhánh yêu cầu, kế thừa HEAD `25e8931`, không tạo project mới,
không sửa nhánh V1, không thay đổi giao diện đã chốt. Đã sửa các lỗi lưới,
tương thích .NET 4.5, bổ sung hủy trong tính toán và dependency build thử.
Nội dung sửa và chứng cứ trước/sau ở `BASELINE.md`, `IMSAT_V2_5_ROUND_AUDIT.md`.

| Giai đoạn | Trạng thái |
|---|---|
| P0 | Baseline được khóa, test trước sửa đã chạy, nguồn/hash được ghi nhận |
| P1 | Các lỗi số học phát hiện đã sửa và có đối chứng; gate tổng thể còn thiếu workflow host thực |
| P2 | 2015–2026 đã COMPILE PASS tại 23d5c59 / CI 38380789988; 2012–2014 thiếu adapter |
| P3 | Chưa hoàn thiện Setup tự nhận diện/chọn module đa CAD; chưa nghiệm thu cài/gỡ/rollback |
| P4 | Có regression lõi và DXF thực; các thao tác end-to-end trong AutoCAD NOT TESTED |
| P5 | Chưa chốt năm vòng cuối vì còn P1 mở và thiếu nghiệm thu host |

COMPILE PASS không phải HOST_RUNTIME_PASS, càng không phải FUNCTIONAL_RELEASE_PASS.
Tất cả các năm 2012–2026 hiện chưa có FUNCTIONAL_RELEASE_PASS trong hồ sơ này.
Nguồn CI/binary phải lấy từ cùng SHA trong manifest của lần build được sử dụng;
không dùng Setup a7f879a hoặc các QA cũ làm bản của HEAD mới.

## Đối chứng dữ liệu thực

- SQ hiện trạng: 1.681.755 tam giác hỗ trợ; 14 tam giác dương rất nhỏ từng bị
  ngưỡng renderer cũ từ chối vẫn qua cùng quality predicate dùng ở CAD.
- KL mb.dxf SHA-256 `4bf0d325588fd29a5265aecf403e8348eb5363b93a57a7ad93759e78e8156893`:
  1.592 face HT, 1.655 face TK, 19 ô đã xuất. Kiểm tra mỗi ô trên từng ranh
  233B và 2343 của chính file này, tổng 38 trường hợp, không chọn thay ranh cho
  người dùng. GEOS giao đa giác, NumPy giải mặt phẳng, tích phân theo trọng tâm
  độc lập với kernel C#. Sai số cho phép: 1e-5 m² diện tích, 1e-4 m³ khối lượng.
  38 trường hợp PASS. Đây là kiểm tra các TIN đã lưu trong DXF, không phải bằng
  chứng đã tạo lại các TIN từ điểm khảo sát trong AutoCAD.
- Không tìm thấy CSV đối chứng gốc 19 ô trong tệp local. Không dùng nhãn CAD
  hoặc số liệu chat làm oracle. Không suy từ no-data ra khối lượng bằng 0.

## Tài nguyên bắt buộc cho bước nghiệm thu tiếp theo

1. Máy/runner Windows x64 có AutoCAD full bản quyền, ưu tiên 2023 để chạy lại
   full regression DWG; tiếp đó từng release/patch được công bố.
2. Bộ reference/ObjectARX đúng AutoCAD 2012, 2013, 2014 và host tương ứng để
   phát triển/kiểm chứng adapter legacy. Hiện thiếu cả triển khai adapter,
   không chỉ thiếu một lần chạy test.
3. Sau khi Setup đa CAD được triển khai: máy thử có nhiều CAD, log/ảnh
   install, uninstall, upgrade, repair, rollback, giữ nguyên dữ liệu/license.
4. Phê duyệt nghiệm thu thực tế của chủ nhiệm sản phẩm sau khi các gate đạt.

Không có link Setup phát hành hoặc SHA-256 Setup phát hành vì chưa có artifact
đủ điều kiện đó. Các artifact compile-only trong CI chỉ phục vụ kỹ thuật.
