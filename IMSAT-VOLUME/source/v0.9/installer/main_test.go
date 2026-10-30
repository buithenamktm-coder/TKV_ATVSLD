//go:build windows

package main

import (
    "archive/zip"
    "bytes"
    "os"
    "crypto/sha256"
    "encoding/hex"
    "os/exec"
    "path/filepath"
    "strings"
    "testing"
)

func validPackageXML() string {
    return `<ApplicationPackage AppVersion="`+version+`"><RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R24.2" SeriesMax="R24.2"/><Components><RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="R24.2" SeriesMax="R24.2"/><ComponentEntry AppName="MiningVolume2023" Version="`+version+`" ModuleName="./Contents/Windows/MiningVolume2023.dll" LoadOnAutoCADStartup="True"/></Components></ApplicationPackage>`
}

func TestPackageLoaderStructureRejectsWrongModuleAndCommentTokens(t *testing.T) {
    good:=validPackageXML()
    if e:=validatePackageXML([]byte(good));e!=nil{t.Fatal(e)}
    oldVersion:=version
    version="2.0.0"
    err:=validatePackageXML([]byte(validPackageXML()))
    version=oldVersion
    if err!=nil{t.Fatal("V2 package rejected",err)}
    cases:=[]string{
        `<ApplicationPackage AppVersion="`+version+`"><!-- SeriesMin="R24.2" SeriesMax="R24.2" LoadOnAutoCADStartup="True" --></ApplicationPackage>`,
        strings.Replace(good,`ModuleName="./Contents/Windows/MiningVolume2023.dll"`,`ModuleName="../old/MiningVolume2023.dll"`,1),
        strings.Replace(good,`SeriesMax="R24.2"`,`SeriesMax="R25.1"`,1),
        strings.ReplaceAll(good,`OS="Win64"`,`OS="Win32"`),
        strings.Replace(good,`LoadOnAutoCADStartup="True"`,`LoadOnAutoCADStartup="False"`,1),
        strings.Replace(good,`</Components>`,`<ComponentEntry AppName="Foreign" ModuleName="foreign.dll"/></Components>`,1),
        strings.Replace(good,`AppVersion="`+version+`"`,`AppVersion="0.0.0" AppVersion="`+version+`"`,1),
        good+good,
        strings.Replace(good,`<ApplicationPackage `,`<ApplicationPackage xmlns="urn:other" `,1),
        good[:len(good)-5],
    }
    for i,data:=range cases {if validatePackageXML([]byte(data))==nil{t.Fatalf("Invalid loader structure accepted: %d",i)}}
}

func testZip(t *testing.T, names []string, mode os.FileMode) []byte {
    t.Helper();var b bytes.Buffer;w:=zip.NewWriter(&b)
    for _,name:=range names {
        header:=&zip.FileHeader{Name:name,Method:zip.Store};header.SetMode(mode)
        f,e:=w.CreateHeader(header);if e!=nil{t.Fatal(e)}
        if _,e=f.Write([]byte("fixture"));e!=nil{t.Fatal(e)}
    }
    if e:=w.Close();e!=nil{t.Fatal(e)};return b.Bytes()
}

func TestPayloadRejectsEscapeAliasesAndLinksBeforeWriting(t *testing.T) {
    for _,name:=range []string{"../escape.dll",`..\escape.dll`,"/escape.dll",`C:\escape.dll`,
        `C:escape.dll`,`\\server\share\escape.dll`,"bundle/../escape.dll","bundle/./file.dll",
        "bundle//file.dll","bundle/file.dll:stream","bundle/CON","bundle/file.dll.","bundle/file.dll "} {
        root:=t.TempDir()
        // A valid first entry must not be written if a later path is invalid.
        if e:=extractZipPayload(testZip(t,[]string{"valid.dll",name},0644),root);e==nil{t.Fatalf("Invalid path accepted: %s",name)}
        entries,e:=os.ReadDir(root);if e!=nil||len(entries)!=0{t.Fatal("Archive partially written before path validation",e)}
    }
    for _,names:=range [][]string{{"bundle/a.dll","BUNDLE/A.DLL"},{"bundle","bundle/a.dll"}} {
        if e:=extractZipPayload(testZip(t,names,0644),t.TempDir());e==nil{t.Fatal("Duplicate or file-parent archive accepted")}
    }
    if e:=extractZipPayload(testZip(t,[]string{"bundle/link"},os.ModeSymlink|0777),t.TempDir());e==nil{t.Fatal("ZIP symlink accepted")}
}

func TestPayloadExtractsVerifiedPathsWithoutOverwritingExistingFiles(t *testing.T) {
    root:=t.TempDir();data:=testZip(t,[]string{"bundle/a.dll",`bundle\b.dll`},0644)
    if e:=extractZipPayload(data,root);e!=nil{t.Fatal(e)}
    p:=filepath.Join(root,"bundle","a.dll")
    got,e:=os.ReadFile(p);if e!=nil||string(got)!="fixture"{t.Fatal("Extraction failed",e)}
    if e=os.WriteFile(p,[]byte("keep existing"),0644);e!=nil{t.Fatal(e)}
    if extractZipPayload(data,root)==nil{t.Fatal("Existing staging file overwritten")}
    got,e=os.ReadFile(p);if e!=nil||string(got)!="keep existing"{t.Fatal("Existing data changed",e)}
}

func TestEmbeddedReleasePayloadExtractsAndMatchesLoaderAndHashes(t *testing.T) {
    // Exercise the actual CI-built payload, not just fixture DLL strings.
    // The V2 packaging stage runs this same test after rewriting its metadata.
    if expected:=os.Getenv("IMSAT_TEST_PAYLOAD_VERSION");expected!="" {
        if expected!="1.0.0" && expected!="2.0.0" {t.Fatal("Unsupported test payload version",expected)}
        previous:=version;version=expected;t.Cleanup(func(){version=previous})
    }
    root:=t.TempDir()
    if e:=extractPayload(root);e!=nil{t.Fatal(e)}
    bundle:=filepath.Join(root,"bundle","MiningVolume2023.bundle")
    // Never allow the legacy V1 missing-manifest exception in release CI.
    identity,e:=os.ReadFile(filepath.Join(bundle,"Contents","Windows","BUILD_IDENTITY.txt"))
    if e!=nil || len(identity)==0 {t.Fatal("Release payload has no build identity",e)}
    if e:=validatePrebuiltBundle(bundle);e!=nil{t.Fatal(e)}
}

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
    xml:=validPackageXML()
    os.WriteFile(filepath.Join(root,"PackageContents.xml"),[]byte(xml),0644)
    if err:=validatePrebuiltBundle(root);err!=nil{t.Fatal(err)}
    got,err:=bundleVersion(root);if err!=nil||got!=version{t.Fatal(got,err)}
    os.WriteFile(filepath.Join(dir,"foreign.dll"),[]byte("foreign"),0644)
    if validatePrebuiltBundle(root)==nil {t.Fatal("foreign DLL accepted")};os.Remove(filepath.Join(dir,"foreign.dll"))
    os.WriteFile(filepath.Join(root,"PackageContents.xml"),[]byte(strings.ReplaceAll(xml,version,"0.0.0")),0644)
    if validatePrebuiltBundle(root)==nil {t.Fatal("wrong AppVersion accepted")}
    os.WriteFile(filepath.Join(root,"PackageContents.xml"),[]byte(xml),0644)
    os.WriteFile(filepath.Join(dir,"MiningVolume2023.dll"),nil,0644)
    if validatePrebuiltBundle(root)==nil {t.Fatal("empty DLL accepted")}
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

func TestRetiredBundlePreservesNestedUserDataAndRepeatedVersions(t *testing.T) {
    root:=t.TempDir();source:=filepath.Join(root,"ApplicationPlugins","MiningVolume2023.bundle")
    recovery:=filepath.Join(root,"Recovery")
    files:=map[string]string{"PackageContents.xml":"old loader","Contents/Windows/old.dll":"binary","Projects/mine.dwg":"drawing","Projects/report.xlsx":"report","Projects/license.key":"license"}
    for iteration:=0;iteration<2;iteration++ {
        for name,data:=range files {
            p:=filepath.Join(source,filepath.FromSlash(name));if e:=os.MkdirAll(filepath.Dir(p),0755);e!=nil{t.Fatal(e)}
            if e:=os.WriteFile(p,[]byte(data),0644);e!=nil{t.Fatal(e)}
        }
        if e:=preserveBundle(source,recovery);e!=nil{t.Fatal(e)}
        if _,e:=os.Lstat(source);!os.IsNotExist(e){t.Fatal("Old bundle remains in autoload tree",e)}
    }
    holders,e:=os.ReadDir(recovery);if e!=nil||len(holders)!=2{t.Fatal("Recovery versions were overwritten",e)}
    for _,holder:=range holders {for name,want:=range files {
        got,e:=os.ReadFile(filepath.Join(recovery,holder.Name(),filepath.Base(source),filepath.FromSlash(name)))
        if e!=nil||string(got)!=want{t.Fatal("User or program data changed",name,e)}
    }}
    if e:=preserveBundle(source,recovery);e!=nil{t.Fatal("Missing bundle must be harmless",e)}
}

func TestRetireFailureAndRollbackCollisionNeverDeleteSource(t *testing.T) {
    root:=t.TempDir();source:=filepath.Join(root,"bundle");os.MkdirAll(source,0755)
    user:=filepath.Join(source,"mine.dwg");os.WriteFile(user,[]byte("retain drawing"),0644)
    blocked:=filepath.Join(root,"blocked");os.WriteFile(blocked,[]byte("existing file"),0644)
    if preserveBundle(source,blocked)==nil{t.Fatal("Invalid recovery destination accepted")}
    got,e:=os.ReadFile(user);if e!=nil||string(got)!="retain drawing"{t.Fatal("Source lost on failure",e)}
    backup:=filepath.Join(root,"backup");os.MkdirAll(backup,0755)
    os.WriteFile(filepath.Join(backup,"old.dll"),[]byte("old binary"),0644)
    if restoreBackup(backup,source)==nil{t.Fatal("Rollback overwrote existing target")}
    got,e=os.ReadFile(user);if e!=nil||string(got)!="retain drawing"{t.Fatal("Rollback deleted drawing",e)}
    got,e=os.ReadFile(filepath.Join(backup,"old.dll"));if e!=nil||string(got)!="old binary"{t.Fatal("Rollback deleted backup",e)}
}


func TestAcad2023UsesProductVersionNotFileVersion(t *testing.T) {
    // Autodesk AutoCAD 2023 executable: FileVersion 30.2, ProductVersion 24.2.
    // The installer must map the API product version (R24.2).
    source,err:=os.ReadFile("main.go")
    if err!=nil {t.Fatal(err)}
    code:=string(source)
    if !strings.Contains(code,"ms,ls:=info[4],info[5]") {
        t.Fatal("AutoCAD detection must use VS_FIXEDFILEINFO dwProductVersionMS/LS, not dwFileVersionMS/LS")
    }
    host,err:=classifyCAD(24,2,0)
    if err!=nil || host.Year!=2023 || host.Flavor!="ACAD2023_NET48" {
        t.Fatalf("Wrong AutoCAD 2023 product classification: %+v %v",host,err)
    }
}

