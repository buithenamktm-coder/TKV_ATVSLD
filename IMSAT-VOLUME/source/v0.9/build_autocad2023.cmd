@echo off
setlocal EnableExtensions EnableDelayedExpansion
set ROOT=%~dp0
set CAD=%AutoCAD2023Dir%
if "%CAD%"=="" set CAD=C:\Program Files\Autodesk\AutoCAD 2023

if not exist "%CAD%\AcMgd.dll" (
  echo [ERROR] Khong tim thay AutoCAD 2023 tai: %CAD%
  exit /b 2
)

set MSBUILD=
for /f "delims=" %%M in ('where msbuild.exe 2^>nul') do if not defined MSBUILD set "MSBUILD=%%M"
if not defined MSBUILD if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
  for /f "usebackq delims=" %%M in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do if not defined MSBUILD set "MSBUILD=%%M"
)
if not defined MSBUILD (
  echo [ERROR] Khong tim thay MSBuild. Script nay chi dung cho may phat trien.
  echo [INFO] Ban Release v0.9 duoc build san tren CI; may nguoi dung KHONG can Build Tools.
  exit /b 4
)

echo [INFO] AutoCAD: %CAD%
echo [INFO] MSBuild: %MSBUILD%
"%MSBUILD%" "%ROOT%MiningVolume.HSNext.sln" /restore /t:Build /p:Configuration=Release /p:AutoCAD2023Dir="%CAD%" /p:UseAutoCADNuGet=false /m
if errorlevel 1 exit /b 5

set OUT=%ROOT%bundle\MiningVolume2023.bundle\Contents\Windows
if not exist "%OUT%" mkdir "%OUT%"
copy /y "%ROOT%src\MiningVolume.Plugin2023\bin\Release\net48\MiningVolume2023.dll" "%OUT%\" >nul || exit /b 6
copy /y "%ROOT%src\MiningVolume.Core\bin\Release\net48\MiningVolume.Core.dll" "%OUT%\" >nul || exit /b 6
copy /y "%ROOT%src\MiningVolume.Surface\bin\Release\net48\MiningVolume.Surface.dll" "%OUT%\" >nul || exit /b 6
copy /y "%ROOT%src\MiningVolume.Cad2023\bin\Release\net48\MiningVolume.Cad2023.dll" "%OUT%\" >nul || exit /b 6

if not exist "%OUT%\MiningVolume2023.dll" exit /b 8
if exist "%OUT%\NetTopologySuite.dll" del /q "%OUT%\NetTopologySuite.dll"

echo [OK] Bundle da build: %ROOT%bundle\MiningVolume2023.bundle
endlocal