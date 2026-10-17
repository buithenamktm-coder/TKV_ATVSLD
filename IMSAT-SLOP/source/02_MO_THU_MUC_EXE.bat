@echo off
cd /d "%~dp0"
set "PRODUCT=IMSAT_Slope_Stability_V6_2_Professional"
if exist "release\%PRODUCT%.exe" (start "" "release" & exit /b 0)
if exist "dist\%PRODUCT%.exe" (start "" "dist" & exit /b 0)
echo Chua co EXE. Hay chay 00_BUILD_EXE_BANG_CMD.bat.
pause
