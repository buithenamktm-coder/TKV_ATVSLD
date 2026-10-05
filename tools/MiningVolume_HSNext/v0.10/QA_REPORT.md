# QA REPORT — MiningVolume HS-Next v0.10

## Mục tiêu kiểm tra
- Giữ nguyên lõi TIN / mặt cắt / khối lượng / Excel / Project đã qua v0.9.
- Loại bỏ command-driven UX khỏi đường sử dụng bình thường.
- Không để Setup mở AutoCAD bằng script chỉ để self-test.
- Build thật AutoCAD 2023 trên Windows CI trước khi giao.

## Gate bắt buộc
- pytest: phải PASS toàn bộ.
- RibbonBuilder.cs: không có `SendStringToExecute`.
- Ribbon button: handler giữ `Action` C# trực tiếp.
- SelectionService.cs: có cơ chế ẩn/hiện lại Palette khi pick trên canvas.
- installer/main.go: không có `.scr`, `/b`, `MVSELFTEST` hoặc tự mở acad.exe.
- Bundle: R24.2 only.
- net48 compile: PASS trên Windows CI.
- installer: build PE64 PASS.

## Ghi chú
CommandMethod `MV...` vẫn tồn tại như shortcut kỹ thuật/diagnostic. Chúng không được Ribbon gọi và không phải luồng thao tác chính của người dùng.
