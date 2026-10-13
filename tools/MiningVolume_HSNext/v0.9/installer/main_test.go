//go:build windows

package main

import (
    "os"
    "crypto/sha256"
    "encoding/hex"
    "os/exec"
    "path/filepath"
    "strings"
    "testing"
)

func TestCADReleaseAndRuntimeClassification(t *testing.T) {
    cases:=[]struct{major,minor,clr,year int;flavor string}{
        {20,0,0,2015,"ACAD2015_NET45"},{20,1,0,2016,"ACAD2016_NET45"},
        {21,0,0,2017,"ACAD2017_NET46"},{22,0,0,2018,"ACAD2018_NET46"},
        {23,0,0,2019,"ACAD2019_NET47"},{23,1,0,2020,"ACAD2020_NET47"},
        {24,0,0,2021,"ACAD2021_NET48"},{24,1,0,2022,"ACAD2022_NET48"},
        {24,2,0,2023,"ACAD2023_NET48"},{24,3,0,2024,"ACAD2024_NET48"},
        {25,0,8,2025,"ACAD2025_NET8"},{25,0,10,2025,"ACAD2025_NET10"},
        {25,1,8,2026,"ACAD2026_NET8"},{25,1,10,2026,"ACAD2026_NET10"},
    }
    for _,c:=range cases {
        h,e:=classifyCAD(c.major,c.minor,c.clr)
        if e!=nil || h.Year!=c.year || h.Flavor!=c.flavor || h.HostAcceptance!="NOT_TESTED" {t.Fatalf("Wrong host classification: %+v %v",h,e)}
    }
    for _,c:=range [][3]int{{18,2,0},{19,0,0},{19,1,0},{25,0,0},{25,1,9},{26,0,10},{24,9,0}} {
        if _,e:=classifyCAD(c[0],c[1],c[2]);e==nil {t.Fatalf("Unknown or unimplemented host accepted: %v",c)}
    }
}

func TestRuntimeConfigFailsClosedOnUnknownOrConflictingMajor(t *testing.T) {
    for _,data:=range []string{
        `{}`,`{"runtimeOptions":{"framework":{"name":"Other.App","version":"8.0.0"}}}`,
        `{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}`,
        `{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"8.0.0garbage"}}}`,
        `{"runtimeOptions":{"rollForward":"LatestMajor","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}`,
        `{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"8.0.0"},{"name":"Microsoft.WindowsDesktop.App","version":"10.0.0"}]}}`,
    } {if _,e:=runtimeFromConfig([]byte(data));e==nil {t.Fatalf("Ambiguous runtime accepted: %s",data)}}
    for _,c:=range []struct{json string;major int}{
        {`{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"8.0.31"}}}`,8},
        {`{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.WindowsDesktop.App","version":"10.0.0"}]}}`,10},
    } {if m,e:=runtimeFromConfig([]byte(c.json));e!=nil||m!=c.major {t.Fatal(m,e)}}
}

func TestWindowsExecutableVersionAndInvalidHostDetection(t *testing.T) {
    file:=filepath.Join(os.Getenv("SystemRoot"),"System32","kernel32.dll")
    ms,_,e:=executableVersion(file)
    if e!=nil || ms==0 {t.Fatalf("Cannot read actual Windows version resource: %v",e)}
    dir:=t.TempDir()
    os.WriteFile(filepath.Join(dir,"acad.exe"),[]byte("not a PE executable"),0644)
    os.WriteFile(filepath.Join(dir,"AcMgd.dll"),[]byte("not an SDK"),0644)
    h:=inspectCADDirectory(dir)
    if h.Error=="" || h.Flavor!="" {t.Fatal("Folder name alone was treated as an installed CAD host")}
}

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

func TestInstalledSetupCleanupTargetsOnlyOwnedVersions(t *testing.T) {
    root := t.TempDir()
    current := filepath.Join(root, "MiningVolume_Setup_v2.0_QA.exe")
    targets := installedSetupCleanupTargets(current, root)
    if len(targets) != 2 { t.Fatalf("expected only two distinct owned IMSAT setup files; got %v", targets) }
    expected := map[string]bool{
        current: true,
        filepath.Join(root, "MiningVolume_Setup_v1.0.exe"): true,
    }
    for _, target := range targets {
        if !expected[target] { t.Fatalf("unexpected cleanup target, possible user file: %s", target) }
    }
    future := filepath.Join(root, "MiningVolume_Setup_v2.0_Release.exe")
    next := installedSetupCleanupTargets(future, root)
    if len(next) != 3 { t.Fatalf("release setup and two known upgrades must be handled; got %v", next) }
    for _, target := range next {
        if filepath.Dir(target) != root { t.Fatalf("cleanup escaped installed directory: %s", target) }
    }
}

func TestBuildIdentityRejectsTamperedDLL(t *testing.T) {
    dir:=t.TempDir();data:=[]byte("verified DLL bytes");sum:=sha256.Sum256(data)
    os.WriteFile(filepath.Join(dir,"test.dll"),data,0644)
    manifest:="SOURCE_SHA="+strings.Repeat("a",40)+"\n"+hex.EncodeToString(sum[:])+"  test.dll\n"
    os.WriteFile(filepath.Join(dir,"BUILD_IDENTITY.txt"),[]byte(manifest),0644)
    allowed:=map[string]bool{"test.dll":true}
    if err:=verifyBuildIdentity(dir,allowed);err!=nil{t.Fatal(err)}
    os.WriteFile(filepath.Join(dir,"test.dll"),[]byte("old or modified DLL"),0644)
    if verifyBuildIdentity(dir,allowed)==nil{t.Fatal("tampered DLL accepted")}
}
