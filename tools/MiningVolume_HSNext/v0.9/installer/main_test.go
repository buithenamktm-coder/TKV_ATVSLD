//go:build windows

package main

import (
    "os"
    "os/exec"
    "path/filepath"
    "strings"
    "testing"
)

func TestBundleVersionAndForeignDLL(t *testing.T) {
    root:=t.TempDir(); dir:=filepath.Join(root,"Contents","Windows");os.MkdirAll(dir,0755)
    for _,f:=range []string{"MiningVolume2023.dll","MiningVolume.Core.dll","MiningVolume.Surface.dll","MiningVolume.Cdt.Native.dll","MiningVolume.Cad2023.dll","LICENSE-CDT.txt","NOTICE.txt"} {os.WriteFile(filepath.Join(dir,f),[]byte("fixture"),0644)}
    xml:=`<ApplicationPackage AppVersion="`+version+`"><RuntimeRequirements SeriesMin="R24.2" SeriesMax="R24.2"/><ComponentEntry LoadOnAutoCADStartup="True"/></ApplicationPackage>`
    os.WriteFile(filepath.Join(root,"PackageContents.xml"),[]byte(xml),0644)
    if err:=validatePrebuiltBundle(root);err!=nil{t.Fatal(err)}
    got,err:=bundleVersion(root);if err!=nil||got!=version{t.Fatal(got,err)}
    os.WriteFile(filepath.Join(dir,"foreign.dll"),[]byte("foreign"),0644)
    if validatePrebuiltBundle(root)==nil {t.Fatal("foreign DLL accepted")};os.Remove(filepath.Join(dir,"foreign.dll"))
    os.WriteFile(filepath.Join(root,"PackageContents.xml"),[]byte(strings.ReplaceAll(xml,version,"0.0.0")),0644)
    if validatePrebuiltBundle(root)==nil {t.Fatal("wrong AppVersion accepted")}
}

func TestRegistrationRollbackPreservesFilesAndRegistry(t *testing.T) {
    root:=t.TempDir();stage:=t.TempDir();old:=uninstallKey
    uninstallKey=`HKCU\Software\IMSAT_Test_`+filepath.Base(root)
    defer func(){exec.Command("reg","delete",uninstallKey,"/f").Run();uninstallKey=old}()
    if err:=exec.Command("reg","add",uninstallKey,"/v","DisplayVersion","/t","REG_SZ","/d","previous","/f").Run();err!=nil{t.Fatal(err)}
    dir:=filepath.Join(root,"MiningVolume2023");os.MkdirAll(dir,0755)
    file:=filepath.Join(dir,storedSetupName);os.WriteFile(file,[]byte("previous installer"),0644)
    user:=filepath.Join(root,"mine.dwg");os.WriteFile(user,[]byte("keep drawing"),0644)
    undo,err:=captureRegistration(root,stage);if err!=nil{t.Fatal(err)}
    os.WriteFile(file,[]byte("broken replacement"),0644)
    exec.Command("reg","add",uninstallKey,"/v","DisplayVersion","/t","REG_SZ","/d","broken","/f").Run()
    if err=undo();err!=nil{t.Fatal(err)}
    data,_:=os.ReadFile(file);if string(data)!="previous installer"{t.Fatal("installer not restored")}
    data,_=os.ReadFile(user);if string(data)!="keep drawing"{t.Fatal("drawing modified")}
    data,err=exec.Command("reg","query",uninstallKey,"/v","DisplayVersion").Output();if err!=nil||!strings.Contains(string(data),"previous"){t.Fatal("registry not restored",err)}
}

func TestRestoreInterruptedBundle(t *testing.T) {
    dir:=t.TempDir();backup:=filepath.Join(dir,"bundle.bak");target:=filepath.Join(dir,"bundle")
    os.MkdirAll(backup,0755);os.WriteFile(filepath.Join(backup,"old.dll"),[]byte("accepted"),0644)
    if err:=restoreBackup(backup,target);err!=nil{t.Fatal(err)}
    b,err:=os.ReadFile(filepath.Join(target,"old.dll"));if err!=nil||string(b)!="accepted"{t.Fatal("backup lost")}
}
