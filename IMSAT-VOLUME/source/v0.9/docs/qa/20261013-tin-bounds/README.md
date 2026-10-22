# TIN: lọc ứng viên breakline trước kiểm tra hình học

Baseline: e7aec388bc2d796215ca8dd04c34a6900a2c3de4.

SegmentGridIndex trước đây trả mọi đoạn dùng chung ô lưới. Trên đường đồng mức dày, nhiều đoạn chung ô nhưng hộp bao hoàn toàn tách nhau, khiến các phép kiểm tra giao/chồng được lặp vô ích. Thay đổi lưu hộp bao từng đoạn, mở rộng mỗi phía bằng XyTolerance hiện hành; chỉ bỏ ứng viên khi hai hộp bao đã mở rộng không giao. Không thay đổi dung sai, cao độ, breakline, chính sách điểm trùng hoặc các phép kiểm tra hình học phía sau. Không loại tam giác hoặc sửa CAD nguồn.

118 C# regression PASS; 330 Python tests + 4 subtests PASS. Test mới so toàn bộ ứng viên với vét cạn trên tọa độ mỏ, gồm đường giao, chồng, gần dung sai và đoạn dài bằng 0. CI Windows trên commit chứa thay đổi này phải được kiểm tra riêng.

| Dữ liệu | Trước (giây) | Sau (giây) | Giảm thời gian | Tam giác | Mặt đứng |
|---|---:|---:|---:|---:|---:|
| SQ HT | 115.557 | 67.570 | 41.53% | 1681755 | 7568 |
| SQ TK | 111.287 | 66.012 | 40.68% | 1672875 | 7412 |

Số tam giác, mặt đứng, tổng diện tích vùng phủ, diện tích tam giác nhỏ nhất và thống kê mã cảnh báo trước/sau bằng nhau trong hai lượt đo. Đây không phải so sánh hash toàn bộ mesh và không phải nghiệm thu AutoCAD. Các phép đo là một lượt mỗi bộ tại baseline và sau sửa, không cam kết tỷ lệ tăng tốc trên máy người dùng. Hộp bao bổ sung 32 byte/đoạn cho mỗi index; managedBytes cuối lượt không phải RAM đỉnh. MC-010 tiếp tục OPEN để đo/tối ưu và kiểm chứng trên AutoCAD thực.

HOST AutoCAD: NOT TESTED. Không phát hành Setup chính thức. Còn thiếu adapter 2012–2014, Setup đa CAD và các gate nghiệm thu trước đó.
