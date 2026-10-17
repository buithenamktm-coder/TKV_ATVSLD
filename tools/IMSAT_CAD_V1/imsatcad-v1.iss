#define AppName "IMSAT CAD"
#define AppVersion "1.0.0-preview"
#define AppExe "IMSAT_CAD.exe"
[Setup]
AppId={{50B7E345-B4FA-4A07-B117-25B9038B8C14}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=IMSAT
DefaultDirName={autopf}\IMSAT CAD
DefaultGroupName=IMSAT CAD
PrivilegesRequired=lowest
OutputDir=installer-output
OutputBaseFilename=IMSAT_CAD_V1.0_Preview_Setup_x64
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
WizardStyle=modern
SetupIconFile=assets\imsatcad.ico
UninstallDisplayIcon={app}\{#AppExe}
[Languages]
Name: "vietnamese"; MessagesFile: "compiler:Default.isl,installer\vi-override.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "Create desktop icon / Tạo biểu tượng trên màn hình"; GroupDescription: "Additional icons / Biểu tượng bổ sung"; Flags: unchecked
[Files]
Source: "dist\IMSAT_CAD\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\IMSAT CAD"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\IMSAT CAD"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
[Run]
Filename: "{app}\{#AppExe}"; Description: "Run IMSAT CAD / Mở IMSAT CAD"; Flags: nowait postinstall skipifsilent
