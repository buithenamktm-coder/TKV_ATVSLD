; NAMVECTA VOLUME 2.0 – professional Windows wizard for AutoCAD 2020–2024
; STAGING ONLY until all host/runtime/branding acceptance gates are signed.
; Build from v0.9 with: ISCC /DStageDir="D:\\build\\NAMVECTA" installer\NAMVECTA_Volume_V2.iss
#ifndef StageDir
  #error "Set /DStageDir to the verified staging directory containing Bundles and Branding."
#endif
#define ProductName "NAMVECTA VOLUME 2.0"
#define ProductVersion "2.0.0"

[Setup]
AppId={{D1A3C41D-5829-4388-89C2-54260641D7A2}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppVerName={#ProductName}
AppPublisher=NAMVECTA Engineering Software
AppCopyright=Phát triển: Bùi Thế Nam
DefaultDirName={autopf}\NAMVECTA\Volume 2.0
DefaultGroupName=NAMVECTA VOLUME
OutputBaseFilename=NAMVECTA-VOLUME-2.0-Setup-QA-NOT-RELEASE
OutputDir={#StageDir}\Output
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
SetupLogging=yes
Uninstallable=yes
CreateUninstallRegKey=yes
DisableProgramGroupPage=yes
ShowLanguageDialog=yes
VersionInfoVersion=2.0.0.0
VersionInfoDescription=NAMVECTA VOLUME 2.0 — QA Installer
VersionInfoProductName=NAMVECTA VOLUME 2.0
SetupIconFile={#StageDir}\Branding\namvecta-setup.ico
WizardImageFile={#StageDir}\Branding\welcome.bmp
UninstallDisplayIcon={app}\namvecta-setup.ico
CloseApplications=yes
RestartApplications=no
ChangesEnvironment=no
ChangesAssociations=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "vi"; MessagesFile: "compiler:Default.isl,lang\NAMVECTA_Vietnamese.isl"

[CustomMessages]
en.HostTitle=Choose AutoCAD versions
vi.HostTitle=Chọn phiên bản AutoCAD
en.HostSubtitle=Only installed, detected AutoCAD 2020–2024 versions can be selected.
vi.HostSubtitle=Chỉ có thể chọn AutoCAD 2020–2024 đã được phát hiện trên máy.
en.SummaryTitle=Review installation details
vi.SummaryTitle=Kiểm tra thông tin trước khi cài đặt
en.SummarySubtitle=Review the selected hosts and installation locations.
vi.SummarySubtitle=Kiểm tra phiên bản CAD và đường dẫn cài đặt.
en.ChooseOne=Select at least one installed AutoCAD version.
vi.ChooseOne=Hãy chọn ít nhất một phiên bản AutoCAD đã cài.
en.NoAutoCAD=No supported AutoCAD 2020–2024 installation was verified. Setup will not continue.
vi.NoAutoCAD=Không tìm thấy AutoCAD 2020–2024 hợp lệ. Bộ cài sẽ không tiếp tục.
en.CloseAcad=Close all AutoCAD processes before continuing.
vi.CloseAcad=Hãy đóng toàn bộ AutoCAD trước khi cài đặt.
en.EmbeddedStatus=Package validation only. Real AutoCAD functional acceptance is still required.
vi.EmbeddedStatus=Chỉ kiểm tra bộ cài. Vẫn phải nghiệm thu chức năng trong AutoCAD thực tế.
en.AcadPath=Autodesk auto-load directory (fixed)
vi.AcadPath=Thư mục tự nạp Autodesk (cố định)
en.InstallSelection=Selected AutoCAD versions
vi.InstallSelection=Các phiên bản AutoCAD đã chọn
en.BrandNotice=NAMVECTA VOLUME 2.0 — offline Pass is entered inside AutoCAD.
vi.BrandNotice=NAMVECTA VOLUME 2.0 — nhập Pass offline bên trong AutoCAD.

[Files]
; This {app} directory is for setup metadata and help, not the Autodesk loader.
Source: "{#StageDir}\Branding\namvecta-setup.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\STAGING_QA.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\Bundles\NAMVECTAVolume2020.bundle\*"; DestDir: "{commonappdata}\Autodesk\ApplicationPlugins\NAMVECTAVolume2020.bundle"; Flags: recursesubdirs createallsubdirs ignoreversion; Check: Install2020
Source: "{#StageDir}\Bundles\NAMVECTAVolume2021.bundle\*"; DestDir: "{commonappdata}\Autodesk\ApplicationPlugins\NAMVECTAVolume2021.bundle"; Flags: recursesubdirs createallsubdirs ignoreversion; Check: Install2021
Source: "{#StageDir}\Bundles\NAMVECTAVolume2022.bundle\*"; DestDir: "{commonappdata}\Autodesk\ApplicationPlugins\NAMVECTAVolume2022.bundle"; Flags: recursesubdirs createallsubdirs ignoreversion; Check: Install2022
Source: "{#StageDir}\Bundles\NAMVECTAVolume2023.bundle\*"; DestDir: "{commonappdata}\Autodesk\ApplicationPlugins\NAMVECTAVolume2023.bundle"; Flags: recursesubdirs createallsubdirs ignoreversion; Check: Install2023
Source: "{#StageDir}\Bundles\NAMVECTAVolume2024.bundle\*"; DestDir: "{commonappdata}\Autodesk\ApplicationPlugins\NAMVECTAVolume2024.bundle"; Flags: recursesubdirs createallsubdirs ignoreversion; Check: Install2024

[Icons]
Name: "{group}\NAMVECTA VOLUME 2.0 – AutoCAD"; Filename: "{code:LauncherPath}"; IconFilename: "{app}\namvecta-setup.ico"; Check: AnySelected
Name: "{group}\Gỡ NAMVECTA VOLUME 2.0"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NAMVECTA VOLUME 2.0"; Filename: "{code:LauncherPath}"; IconFilename: "{app}\namvecta-setup.ico"; Tasks: desktopicon; Check: AnySelected

[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng Desktop / Create Desktop icon"; GroupDescription: "NAMVECTA VOLUME"

[Code]
var
  HostPage: TWizardPage;
  CheckList: TNewCheckListBox;
  SummaryPage: TWizardPage;
  SummaryText: TNewMemo;
  HostDir: array[0..4] of String;
  HostFound: array[0..4] of Boolean;

function YearAt(Index: Integer): Integer;
begin
  Result := 2020 + Index;
end;

function SeriesAt(Index: Integer): String;
begin
  case Index of
    0: Result := 'R23.1';
    1: Result := 'R24.0';
    2: Result := 'R24.1';
    3: Result := 'R24.2';
    4: Result := 'R24.3';
  else
    Result := '';
  end;
end;

function LocateCAD(Index: Integer; var Directory: String): Boolean;
var
  Key, Root, S: String;
  Children: TArrayOfString;
  I: Integer;
begin
  Result := False;
  Directory := '';
  Root := 'SOFTWARE\Autodesk\AutoCAD\' + SeriesAt(Index);
  if RegQueryStringValue(HKLM64, Root, 'AcadLocation', S) and
     FileExists(AddBackslash(S) + 'acad.exe') then
  begin
    Directory := S;
    Result := True;
    exit;
  end;
  if not RegGetSubkeyNames(HKLM64, Root, Children) then exit;
  for I := 0 to GetArrayLength(Children) - 1 do
  begin
    Key := Root + '\' + Children[I];
    if RegQueryStringValue(HKLM64, Key, 'AcadLocation', S) and
       FileExists(AddBackslash(S) + 'acad.exe') then
    begin
      Directory := S;
      Result := True;
      exit;
    end;
  end;
end;

function Selected(Index: Integer): Boolean;
begin
  Result := (Index >= 0) and (Index < 5) and
            HostFound[Index] and CheckList.Checked[Index];
end;

function AnySelected: Boolean;
var I: Integer;
begin
  Result := False;
  for I := 0 to 4 do
    if Selected(I) then begin Result := True; exit; end;
end;

function Install2020: Boolean; begin Result := Selected(0); end;
function Install2021: Boolean; begin Result := Selected(1); end;
function Install2022: Boolean; begin Result := Selected(2); end;
function Install2023: Boolean; begin Result := Selected(3); end;
function Install2024: Boolean; begin Result := Selected(4); end;

function LauncherPath(Param: String): String;
var I: Integer;
begin
  Result := '';
  for I := 0 to 4 do
    if Selected(I) then
    begin
      Result := AddBackslash(HostDir[I]) + 'acad.exe';
      exit;
    end;
end;

procedure InitializeWizard;
var
  I, J: Integer;
  LabelText: String;
begin
  HostPage := CreateCustomPage(wpSelectDir,
    CustomMessage('HostTitle'), CustomMessage('HostSubtitle'));
  CheckList := TNewCheckListBox.Create(WizardForm);
  CheckList.Parent := HostPage.Surface;
  CheckList.Left := 0;
  CheckList.Top := ScaleY(10);
  CheckList.Width := HostPage.SurfaceWidth;
  CheckList.Height := HostPage.SurfaceHeight - ScaleY(12);
  CheckList.BorderStyle := bsNone;
  for I := 0 to 4 do
  begin
    HostFound[I] := LocateCAD(I, HostDir[I]);
    if HostFound[I] then
      LabelText := 'AutoCAD ' + IntToStr(YearAt(I)) + ' — ' + HostDir[I]
    else
      LabelText := 'AutoCAD ' + IntToStr(YearAt(I)) + ' — Not detected / Chưa tìm thấy';
    J := CheckList.AddCheckBox(LabelText, '', 0, HostFound[I],
      HostFound[I], False, False, nil);
  end;

  SummaryPage := CreateCustomPage(HostPage.ID,
    CustomMessage('SummaryTitle'), CustomMessage('SummarySubtitle'));
  SummaryText := TNewMemo.Create(WizardForm);
  SummaryText.Parent := SummaryPage.Surface;
  SummaryText.Left := 0;
  SummaryText.Top := ScaleY(8);
  SummaryText.Width := SummaryPage.SurfaceWidth;
  SummaryText.Height := SummaryPage.SurfaceHeight - ScaleY(16);
  SummaryText.ReadOnly := True;
  SummaryText.ScrollBars := ssVertical;
end;

function InitializeSetup: Boolean;
var I, FoundCount: Integer; Dir: String;
begin
  FoundCount := 0;
  for I := 0 to 4 do
    if LocateCAD(I, Dir) then FoundCount := FoundCount + 1;
  Result := FoundCount > 0;
  if not Result then
    MsgBox(CustomMessage('NoAutoCAD'), mbError, MB_OK);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = HostPage.ID) and (not AnySelected) then
  begin
    MsgBox(CustomMessage('ChooseOne'), mbError, MB_OK);
    Result := False;
  end;
end;

function AcadRunning: Boolean;
var ExitCode: Integer;
begin
  Result := True;  {fail closed if Tasklist is unavailable}
  if Exec(ExpandConstant('{cmd}'),
      '/C tasklist /FI "IMAGENAME eq acad.exe" /FO CSV /NH | find /I "acad.exe" >nul',
      '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Result := ExitCode = 0;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if AcadRunning then Result := CustomMessage('CloseAcad');
end;

procedure CurPageChanged(CurPageID: Integer);
var
  I: Integer;
  TextValue: String;
begin
  if CurPageID <> SummaryPage.ID then exit;
  TextValue := '{#ProductName}' + #13#10 +
    CustomMessage('InstallSelection') + ':' + #13#10;
  for I := 0 to 4 do
    if Selected(I) then TextValue := TextValue +
      '  AutoCAD ' + IntToStr(YearAt(I)) + ': ' + HostDir[I] + #13#10;
  TextValue := TextValue + #13#10 +
    'Installation folder / Thư mục ứng dụng: ' + WizardDirValue + #13#10 +
    CustomMessage('AcadPath') + ': ' +
    ExpandConstant('{commonappdata}\Autodesk\ApplicationPlugins') + #13#10 +
    CustomMessage('BrandNotice') + #13#10 + #13#10 +
    CustomMessage('EmbeddedStatus');
  SummaryText.Text := TextValue;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  I: Integer;
  BundleDir, Module: String;
begin
  if CurStep <> ssPostInstall then exit;
  for I := 0 to 4 do
    if Selected(I) then
    begin
      BundleDir := ExpandConstant('{commonappdata}\Autodesk\ApplicationPlugins\NAMVECTAVolume')
        + IntToStr(YearAt(I)) + '.bundle';
      if not FileExists(AddBackslash(BundleDir) + 'PackageContents.xml') then
        RaiseException('The installed AutoCAD bundle manifest is missing: ' + BundleDir);
      if I = 3 then Module := 'MiningVolume2023.dll'
      else if I = 0 then Module := 'IMSATVolume.ACAD2020_NET47.dll'
      else Module := 'IMSATVolume.ACAD' + IntToStr(YearAt(I)) + '_NET48.dll';
      if not FileExists(AddBackslash(BundleDir) + 'Contents\Windows\' + Module) then
        RaiseException('The installed main CAD DLL is missing: ' + Module);
    end;
end;
