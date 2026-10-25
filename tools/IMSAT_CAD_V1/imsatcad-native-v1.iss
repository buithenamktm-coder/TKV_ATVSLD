; IMSAT CAD V1.0 native installer (QCAD Community Edition GPLv3 core).
; Built only from native-cad-stage after successful Windows native CI.
; The branded launcher must be staged as IMSATCAD.exe; upstream GPL notices remain intact.
#define AppName "IMSAT CAD"
#define AppVersion "1.0.0"
#define AppPublisher "IMSAT"
[Setup]
AppId={{D6E11497-43E8-4B16-A0E7-5C91C0E45E18}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\IMSAT CAD
DefaultGroupName=IMSAT CAD
OutputDir=.
OutputBaseFilename=IMSAT-CAD-V1.0-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
#ifexist "native-cad-stage\branding\imsatcad.ico"
UninstallDisplayIcon={app}\branding\imsatcad.ico
#else
UninstallDisplayIcon={app}\IMSATCAD.exe
#endif
AppComments=IMSAT CAD V1.0 — CAD 2D based on QCAD Community Edition (GPLv3)
AppCopyright=IMSAT CAD integration; QCAD Community Edition copyright belongs to its respective authors
VersionInfoProductName=IMSAT CAD
VersionInfoProductVersion=1.0.0
VersionInfoCompany=IMSAT
VersionInfoDescription=IMSAT CAD V1.0 Installer
#ifexist "native-cad-stage\branding\imsatcad.ico"
SetupIconFile=native-cad-stage\branding\imsatcad.ico
#endif
LicenseFile=native-cad-stage\licenses\QCAD-LICENSE.txt
[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "vi"; MessagesFile: "compiler:Default.isl,installer\vi-override.isl"
[Files]
Source: "native-cad-stage\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
#ifexist "native-cad-stage\branding\imsatcad.ico"
Name: "{group}\IMSAT CAD V1.0"; Filename: "{app}\IMSATCAD.exe"; WorkingDir: "{app}"; IconFilename: "{app}\branding\imsatcad.ico"
Name: "{autodesktop}\IMSAT CAD V1.0"; Filename: "{app}\IMSATCAD.exe"; WorkingDir: "{app}"; IconFilename: "{app}\branding\imsatcad.ico"
#else
Name: "{group}\IMSAT CAD V1.0"; Filename: "{app}\IMSATCAD.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\IMSAT CAD V1.0"; Filename: "{app}\IMSATCAD.exe"; WorkingDir: "{app}"
#endif
Name: "{group}\Uninstall IMSAT CAD"; Filename: "{uninstallexe}"
[CustomMessages]
en.LaunchCAD=Launch IMSAT CAD V1.0
vi.LaunchCAD=Khởi động IMSAT CAD V1.0
[Run]
Filename: "{app}\IMSATCAD.exe"; Description: "{cm:LaunchCAD}"; Flags: nowait postinstall skipifsilent
