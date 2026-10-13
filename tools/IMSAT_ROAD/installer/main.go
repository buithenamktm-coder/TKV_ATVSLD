//go:build windows

// IMSAT ROAD per-user AutoCAD 2023 Preview installer.
package main

import (
    "archive/zip"
    "bytes"
    _ "embed"
    "errors"
    "fmt"
    "io"
    "os"
    "os/exec"
    "path/filepath"
    "strings"
    "syscall"
    "unsafe"
)

//go:embed payload.zip
var payload []byte

const bundleName = "IMSATROAD2023.bundle"

func main() {
    if err := run(); err != nil {
        message("IMSAT ROAD - CÀI ĐẶT THẤT BẠI\n\n"+err.Error(), 0x10)
        os.Exit(1)
    }
}

func run() error {
    appData := os.Getenv("APPDATA")
    if appData == "" { return errors.New("Không tìm thấy thư mục APPDATA của Windows.") }
    target := filepath.Join(appData,"Autodesk","ApplicationPlugins",bundleName)
    if len(os.Args)>1 && os.Args[1]=="--uninstall" {
        if err:=os.RemoveAll(target);err!=nil{return err}
        message("Đã gỡ IMSAT ROAD khỏi hồ sơ Windows hiện tại.\nKhởi động lại AutoCAD nếu đang mở.",0x40)
        return nil
    }

    running, err := isAutoCADRunning()
    if err == nil && running {
        return errors.New("Hãy đóng AutoCAD trước khi cài đặt để không ghi đè DLL đang nạp.")
    }
    if len(payload)<2048 { return errors.New("Gói cài không chứa các DLL đã biên dịch.") }
    temp,err:=os.MkdirTemp("","imsat-road-")
    if err!=nil{return err}
    defer os.RemoveAll(temp)
    if err=unpack(temp);err!=nil{return err}
    source:=filepath.Join(temp,"bundle",bundleName)
    required:=[]string{
        "PackageContents.xml",
        "Contents/Windows/IMSAT.Road.Plugin2023.dll",
        "Contents/Windows/IMSAT.Road.Core.dll",
        "Contents/Windows/IMSAT.Road.TerrainBridge.dll",
        "Contents/Windows/MiningVolume.Core.dll",
    }
    for _,f:=range required {
        info,e:=os.Stat(filepath.Join(source,filepath.FromSlash(f)))
        if e!=nil||info.IsDir()||info.Size()<200 {return fmt.Errorf("Thiếu tệp DLL/cấu hình: %s",f)}
    }
    if err:=os.MkdirAll(filepath.Dir(target),0755);err!=nil{return err}
    backup:=target+".before-preview"
    if _,e:=os.Stat(backup);e==nil{return errors.New("Có bản sao lưu cài đặt trước. Hãy kiểm tra: "+backup)}
    hadOld:=false
    if _,e:=os.Stat(target);e==nil {
        if err:=os.Rename(target,backup);err!=nil{return fmt.Errorf("Sao lưu bản cũ: %w",err)}
        hadOld=true
    }
    if err:=copyDirectory(source,target);err!=nil {
        os.RemoveAll(target)
        if hadOld { _=os.Rename(backup,target) }
        return fmt.Errorf("Cài đặt không thành công; đã thử phục hồi: %w",err)
    }
    if hadOld { _=os.RemoveAll(backup) }
    message("IMSAT ROAD PREVIEW đã được cài cho tài khoản Windows hiện tại.\n\n"+
        "Mở AutoCAD 2023, gõ IRROAD hoặc IR_SELFTEST.\n"+
        "Chức năng: chọn tuyến, một TIN hiện trạng, cọc, trắc dọc/trắc ngang tự nhiên.\n\n"+
        "Chưa nghiệm thu runtime AutoCAD. Nếu phần mềm báo lỗi, lưu log và gửi lại để khắc phục.",0x40)
    return nil
}

func isAutoCADRunning()(bool,error) {
    out,err:=exec.Command("tasklist","/FI","IMAGENAME eq acad.exe").Output()
    if err!=nil{return false,err}
    return strings.Contains(strings.ToLower(string(out)),"acad.exe"),nil
}

func unpack(dir string) error {
    zr,err:=zip.NewReader(bytes.NewReader(payload),int64(len(payload)))
    if err!=nil{return err}
    root:=filepath.Clean(dir)+string(os.PathSeparator)
    for _,f:=range zr.File {
        // Compress-Archive can emit explicit root directory entries.
        if f.FileInfo().IsDir() && (f.Name=="bundle/" || f.Name=="bundle/"+bundleName+"/") { continue }
        normalized:=filepath.Clean(filepath.FromSlash(f.Name))
        if !strings.HasPrefix(f.Name,"bundle/"+bundleName+"/") ||
            strings.Contains(f.Name,"\\") || filepath.IsAbs(normalized) || normalized==".." ||
            strings.HasPrefix(normalized,".."+string(os.PathSeparator)) {
            return fmt.Errorf("Tệp không hợp lệ trong gói cài: %s",f.Name)
        }
        dest:=filepath.Join(dir,normalized)
        if !strings.HasPrefix(dest,root) {return errors.New("Unsafe ZIP path")}
        if f.FileInfo().IsDir(){if err:=os.MkdirAll(dest,0755);err!=nil{return err};continue}
        if err:=os.MkdirAll(filepath.Dir(dest),0755);err!=nil{return err}
        src,e:=f.Open();if e!=nil{return e}
        output,e:=os.Create(dest);if e!=nil{src.Close();return e}
        _,e=io.Copy(output,src)
        output.Close();src.Close()
        if e!=nil{return e}
    }
    return nil
}

func copyDirectory(src,dest string) error {
    entries,err:=os.ReadDir(src);if err!=nil{return err}
    if err:=os.MkdirAll(dest,0755);err!=nil{return err}
    for _,entry:=range entries{
        s:=filepath.Join(src,entry.Name())
        d:=filepath.Join(dest,entry.Name())
        if entry.IsDir(){
            if err=copyDirectory(s,d);err!=nil{return err}
            continue
        }
        inf,e:=entry.Info();if e!=nil{return e}
        in,e:=os.Open(s);if e!=nil{return e}
        out,e:=os.OpenFile(d,os.O_CREATE|os.O_EXCL|os.O_WRONLY,inf.Mode().Perm())
        if e!=nil{in.Close();return e}
        _,e=io.Copy(out,in);out.Close();in.Close()
        if e!=nil{return e}
    }
    return nil
}

func message(text string,icon uintptr) {
    user32:=syscall.NewLazyDLL("user32.dll")
    box:=user32.NewProc("MessageBoxW")
    title,_:=syscall.UTF16PtrFromString("IMSAT ROAD - AutoCAD 2023 Preview")
    body,_:=syscall.UTF16PtrFromString(text)
    box.Call(0,uintptr(unsafe.Pointer(body)),uintptr(unsafe.Pointer(title)),icon)
}
