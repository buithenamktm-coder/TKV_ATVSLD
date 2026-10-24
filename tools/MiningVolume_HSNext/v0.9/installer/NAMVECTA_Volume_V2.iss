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
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
PrivilegesRequired=admin
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
SetupLogging=yes
Uninstallable=yes
CreateUninstallRegKey=yes
DisableProgramGroupPage=yes
ShowLanguageDialog=no
UsePreviousLanguage=no
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
Name: "vi"; MessagesFile: "compiler:Default.isl,lang\NAMVECTA_Vietnamese.isl"

[CustomMessages]
vi.HostTitle=Chọn phiên bản AutoCAD
vi.HostSubtitle=Chỉ có thể chọn AutoCAD 2020–2024 đã được phát hiện trên máy.
vi.SummaryTitle=Kiểm tra thông tin trước khi cài đặt
vi.SummarySubtitle=Kiểm tra phiên bản CAD và đường dẫn cài đặt.
vi.ChooseOne=Hãy chọn ít nhất một phiên bản AutoCAD đã cài.
vi.NoAutoCAD=Không tìm thấy AutoCAD 2020–2024 hợp lệ. Bộ cài sẽ không tiếp tục.
vi.CloseAcad=Hãy đóng toàn bộ AutoCAD trước khi cài đặt.
vi.LegacyConflict=Phát hiện tiện ích IMSAT MiningVolume cũ. Bản QA chưa thể cài song song an toàn. Hãy gỡ add-in cũ trong Ứng dụng đã cài của Windows (không xóa bản vẽ), sau đó cài NAMVECTA.
vi.EmbeddedStatus=Chỉ kiểm tra bộ cài. Vẫn phải nghiệm thu chức năng trong AutoCAD thực tế.
vi.AcadPath=Thư mục tự nạp Autodesk (cố định)
vi.InstallSelection=Các phiên bản AutoCAD đã chọn
vi.BrandNotice=NAMVECTA VOLUME 2.0 — nhập Pass offline bên trong AutoCAD.

[Files]
; This {app} directory is for setup metadata and help, not the Autodesk loader.
Source: "{#StageDir}\Branding\namvecta-setup.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\Bundles\STAGING_QA.json"; DestDir: "{app}"; Flags: ignoreversion
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
Name: "desktopicon"; Description: "Tạo biểu tượng Desktop"; GroupDescription: "NAMVECTA VOLUME"

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
      LabelText := 'AutoCAD ' + IntToStr(YearAt(I)) + ' — Chưa tìm thấy';
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

function LegacyBundlePresent: Boolean;
begin
  // Never uninstall/rename legacy products silently; two autoloaders with
  // overlapping commands would otherwise make the AutoCAD host unstable.
  Result := DirExists(ExpandConstant('{commonappdata}\Autodesk\ApplicationPlugins\MiningVolume2023.bundle'))
    or DirExists(ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins\MiningVolume2023.bundle'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if AcadRunning then
    Result := CustomMessage('CloseAcad')
  else if LegacyBundlePresent then
    Result := CustomMessage('LegacyConflict');
end;

procedure CurPageChanged(CurPageID: Integer);
var
  I: Integer;
  TextValue: String;
begin
  if CurPageID = wpFinished then
  begin
    // All post-install guidance belongs to the final Setup page.
    // Do not launch a console, website, extra activation form or popup.
    // Setup has no access to the user's actual activation state.
    WizardForm.FinishedLabel.Caption :=
      'NAMVECTA VOLUME 2.0 đã hoàn tất sao chép tệp.' + #13#10 +
      'Người phát triển: Bùi Thế Nam   |   ĐT/Zalo: 0967280686' + #13#10 + #13#10 +
      'SỬ DỤNG VÀ KÍCH HOẠT' + #13#10 +
      '1. Mở AutoCAD đã chọn, gõ TKL để mở NAMVECTA VOLUME.' + #13#10 +
      '2. Dùng thử 15 ngày tính từ lần sử dụng đầu tiên.' + #13#10 +
      '3. Vào Thông tin & Bản quyền để xem thời hạn và nhập Pass.' + #13#10 +
      '4. Pass offline: 1 tháng, 3 tháng, 6 tháng, 1 năm hoặc vĩnh viễn.' + #13#10 +
      'Không cần Internet hoặc mã máy; bộ cài không yêu cầu nhập Pass.' + #13#10 +
      'Lưu ý: Cần chạy MVSELFTEST trong AutoCAD để kiểm thử chức năng.';
    exit;
  end;
  if CurPageID <> SummaryPage.ID then exit;
  TextValue := '{#ProductName}' + #13#10 +
    CustomMessage('InstallSelection') + ':' + #13#10;
  for I := 0 to 4 do
    if Selected(I) then TextValue := TextValue +
      '  AutoCAD ' + IntToStr(YearAt(I)) + ': ' + HostDir[I] + #13#10;
  TextValue := TextValue + #13#10 +
    'Thư mục ứng dụng: ' + WizardDirValue + #13#10 +
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
