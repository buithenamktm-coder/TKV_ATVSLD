; Experimental native QCAD Community Edition based IMSAT CAD installer.
; Built only from native-cad-stage after successful Windows native CI.
#define AppName "IMSAT CAD Native"
#define AppVersion "0.1.0-dev"
#define AppPublisher "IMSAT"
[Setup]
AppId={{D6E11497-43E8-4B16-A0E7-5C91C0E45E18}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\IMSAT CAD Native
DefaultGroupName=IMSAT CAD Native
OutputDir=.
OutputBaseFilename=IMSAT-CAD-Native-Development-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\qcad.exe
LicenseFile=native-cad-stage\licenses\QCAD-LICENSE.txt
[Files]
Source: "native-cad-stage\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\IMSAT CAD Native (Development)"; Filename: "{app}\qcad.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\IMSAT CAD Native (Development)"; Filename: "{app}\qcad.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall IMSAT CAD Native"; Filename: "{uninstallexe}"
[Run]
Filename: "{app}\qcad.exe"; Description: "Launch IMSAT CAD Native (Development)"; Flags: nowait postinstall skipifsilent
