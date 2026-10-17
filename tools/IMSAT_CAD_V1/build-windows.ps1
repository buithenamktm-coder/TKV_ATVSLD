$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
py -3.12 -m pip install -r requirements.txt
py -3.12 generate-brand.py
py -3.12 -m PyInstaller --noconfirm --clean --windowed --onedir --name IMSAT_CAD --icon assets/imsatcad.ico --add-data "assets/imsat_icon.png;assets" app.py
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { throw "Install Inno Setup 6 before running this script." }
& $iscc imsatcad-v1.iss
Get-ChildItem installer-output/*.exe
