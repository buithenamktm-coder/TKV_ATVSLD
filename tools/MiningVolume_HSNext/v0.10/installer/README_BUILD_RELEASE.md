# Release installer v0.9

`main.go` chỉ đóng gói **binary đã build sẵn**. Máy người dùng không biên dịch C# và không cần Visual Studio/Build Tools.

Pipeline Release phải thực hiện theo thứ tự:
1. Build solution `Release/net48` với `UseAutoCADNuGet=true` và AutoCAD.NET 24.2.0.
2. Copy 4 DLL MiningVolume vào `bundle/MiningVolume2023.bundle/Contents/Windows/`.
3. Kiểm tra không có `AcMgd.dll`, `AcDbMgd.dll`, `AcCoreMgd.dll`, `AcWindows.dll`, `AdWindows.dll` hoặc `NetTopologySuite.dll` trong bundle.
4. Zip thư mục `bundle` thành `installer/payload.zip`.
5. `GOOS=windows GOARCH=amd64 go build -trimpath -ldflags "-s -w -H=windowsgui"`.
6. Chỉ phát hành Setup sau khi kiểm tra PE64 và chạy runtime `MVSELFTEST` trên AutoCAD 2023 thật.