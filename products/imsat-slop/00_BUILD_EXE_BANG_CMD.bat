@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
cd /d "%~dp0"
title IMSAT Slope Stability V6.2 - Build EXE bang CMD

set "PRODUCT=IMSAT_Slope_Stability_V6_2_Professional"
set "VENV=.venv_cmd_build"
set "PY_BASE="
set "PY_VERSION=3.13.13"
set "PY_SHA256=3c9c81d80f91c002ced86d645422d81432c68c7d9b6b0e974768ca2e449a4d00"
set "PY_URL=https://www.python.org/ftp/python/%PY_VERSION%/python-%PY_VERSION%-amd64.exe"
set "PY_RUNTIME=%CD%\_python313"
set "DOWNLOAD_DIR=%CD%\_downloads"
set "PY_INSTALLER=%DOWNLOAD_DIR%\python-%PY_VERSION%-amd64.exe"
set "LOG_DIR=%CD%\build_logs"

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo ================================================================
echo   IMSAT SLOPE STABILITY V6.2 - BUILD ONE-FILE EXE BANG CMD
echo ================================================================
echo.

rem 1. Tim Python 64-bit da co tren may, ke ca khi khong nam trong PATH.
for %%V in (3.13 3.12 3.11) do (
  if not defined PY_BASE (
    for /f "usebackq delims=" %%P in (`py -%%V -c "import sys; print(sys.executable)" 2^>nul`) do set "PY_BASE=%%P"
  )
)

if not defined PY_BASE (
  for %%P in (
    "%LocalAppData%\Programs\Python\Python313\python.exe"
    "%LocalAppData%\Programs\Python\Python312\python.exe"
    "%LocalAppData%\Programs\Python\Python311\python.exe"
    "%ProgramFiles%\Python313\python.exe"
    "%ProgramFiles%\Python312\python.exe"
    "%ProgramFiles%\Python311\python.exe"
    "C:\Python313\python.exe"
    "C:\Python312\python.exe"
    "C:\Python311\python.exe"
  ) do (
    if not defined PY_BASE if exist "%%~P" set "PY_BASE=%%~P"
  )
)

if not defined PY_BASE (
  for %%V in (3.13 3.12 3.11) do (
    if not defined PY_BASE (
      for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Python\PythonCore\%%V\InstallPath" /ve 2^>nul ^| find "REG_SZ"') do (
        if exist "%%Bpython.exe" set "PY_BASE=%%Bpython.exe"
      )
    )
    if not defined PY_BASE (
      for /f "tokens=2,*" %%A in ('reg query "HKLM\Software\Python\PythonCore\%%V\InstallPath" /ve 2^>nul ^| find "REG_SZ"') do (
        if exist "%%Bpython.exe" set "PY_BASE=%%Bpython.exe"
      )
    )
  )
)

if exist "%PY_RUNTIME%\python.exe" set "PY_BASE=%PY_RUNTIME%\python.exe"

rem 2. Neu khong co Python, CMD tu tai va cai rieng trong thu muc du an.
if not defined PY_BASE (
  echo [1/11] Khong tim thay Python 64-bit 3.11-3.13 tren may.
  echo        CMD se tai Python %PY_VERSION% x64 va cai cuc bo, khong sua PATH.
  if not exist "%DOWNLOAD_DIR%" mkdir "%DOWNLOAD_DIR%"
  if not exist "%PY_INSTALLER%" (
    where curl.exe >nul 2>nul || goto :no_curl
    curl.exe -L --fail --retry 3 --connect-timeout 30 -o "%PY_INSTALLER%" "%PY_URL%" || goto :download_fail
  )
  echo        Dang kiem tra SHA-256...
  set "ACTUAL_SHA="
  for /f "skip=1 tokens=* delims=" %%H in ('certutil -hashfile "%PY_INSTALLER%" SHA256') do if not defined ACTUAL_SHA set "ACTUAL_SHA=%%H"
  set "ACTUAL_SHA=!ACTUAL_SHA: =!"
  if /i not "!ACTUAL_SHA!"=="%PY_SHA256%" goto :hash_mismatch
  echo        Dang cai Python cuc bo...
  "%PY_INSTALLER%" /quiet InstallAllUsers=0 TargetDir="%PY_RUNTIME%" AssociateFiles=0 Shortcuts=0 Include_doc=0 Include_debug=0 Include_test=0 Include_launcher=0 Include_pip=1 Include_tcltk=1 Include_dev=1 Include_exe=1 Include_lib=1 Include_tools=1 PrependPath=0 || goto :python_install_fail
  if not exist "%PY_RUNTIME%\python.exe" goto :python_install_fail
  set "PY_BASE=%PY_RUNTIME%\python.exe"
) else (
  echo [1/11] Da tim thay Python:
  echo        %PY_BASE%
)

"%PY_BASE%" -c "import sys,struct; assert (3,11) <= sys.version_info[:2] <= (3,13); assert struct.calcsize('P')*8 == 64; print(sys.version); print(sys.executable)" || goto :bad_python

rem 3. Tao moi truong build rieng.
if not exist "%VENV%\Scripts\python.exe" (
  echo [2/11] Tao moi truong build...
  "%PY_BASE%" -m venv "%VENV%" || goto :fail
) else (
  echo [2/11] Su dung lai moi truong build hien co.
)
set "PY=%CD%\%VENV%\Scripts\python.exe"

rem 4. Cai thu vien va PyInstaller.
echo [3/11] Cai/cap nhat thu vien...
"%PY%" -m pip install --upgrade pip setuptools wheel || goto :fail
"%PY%" -m pip install -r requirements-windows-test.txt pyinstaller==6.21.0 || goto :fail

rem 5. Kiem tra thu vien.
echo [4/11] Kiem tra thu vien...
"%PY%" -c "import PySide6,numpy,shapely,ezdxf; print('PySide6',PySide6.__version__); print('numpy',numpy.__version__); print('shapely',shapely.__version__); print('ezdxf',ezdxf.__version__)" || goto :fail

rem 6. Kiem tra ma nguon va bo giai, bao gom mo hinh mo lo thien lon.
echo [5/11] Kiem tra ma nguon va chuoi tinh toan...
set "PYTHONPATH=%CD%"
"%PY%" -m compileall -q app main.py tests tools || goto :fail
"%PY%" tools\release_acceptance.py || goto :fail
"%PY%" tools\generate_v6_2_audit.py || goto :fail

rem 7. Kiem tra giao dien Qt, Entry/Exit va computational preflight.
echo [6/11] Kiem tra giao dien Qt offscreen...
set "QT_QPA_PLATFORM=offscreen"
"%PY%" tools\windows_ui_smoke.py || goto :fail
set "QT_QPA_PLATFORM="

rem 8. Tao input smoke that truoc khi dong goi.
echo [7/11] Tao bai toan smoke cho frozen solver...
"%PY%" tools\create_solver_smoke_input.py || goto :fail

rem 9. Build mot file EXE duy nhat.
echo [8/11] Build ONE-FILE EXE bang PyInstaller...
if exist build rmdir /s /q build
if exist "dist\%PRODUCT%.exe" del /q "dist\%PRODUCT%.exe"
"%PY%" -m PyInstaller --noconfirm --clean IMSAT_Slope_Stability_V6_2_Professional_ONEFILE.spec || goto :fail
if not exist "dist\%PRODUCT%.exe" goto :missing_exe

rem 10. Bat buoc frozen EXE phai tinh duoc Morgenstern-Price va ghi Fs huu han.
echo [9/11] Kiem tra frozen EXE worker bang bai toan that...
if exist "tests\_windows_solver_smoke_output.issv6" del /q "tests\_windows_solver_smoke_output.issv6"
if exist "tests\_windows_solver_smoke_output.issv6.diagnostics.json" del /q "tests\_windows_solver_smoke_output.issv6.diagnostics.json"
start "IMSAT_V6_2_SOLVER_SMOKE" /wait "%CD%\dist\%PRODUCT%.exe" --solver-job "%CD%\tests\_windows_solver_smoke_input.issv6" "%CD%\tests\_windows_solver_smoke_output.issv6"
set "SMOKE_RC=!ERRORLEVEL!"
if not "!SMOKE_RC!"=="0" goto :frozen_solver_fail
"%PY%" tools\verify_solver_smoke_output.py || goto :frozen_solver_fail

rem 11. Tao thu muc phat hanh.
echo [10/11] Tao goi phat hanh...
if not exist release mkdir release
copy /y "dist\%PRODUCT%.exe" "release\%PRODUCT%.exe" >nul
copy /y README_V6_2.txt "release\README_V6_2.txt" >nul
copy /y KNOWN_LIMITATIONS_V6_2.md "release\KNOWN_LIMITATIONS_V6_2.md" >nul
copy /y RELEASE_NOTES_V6_2.md "release\RELEASE_NOTES_V6_2.md" >nul
copy /y "tests\V6_2_FULL_AUDIT.json" "release\V6_2_FULL_AUDIT.json" >nul
copy /y "tests\V6_2_CAD_CLOSED_SECTION_VALIDATION.json" "release\V6_2_CAD_CLOSED_SECTION_VALIDATION.json" >nul
if exist "release\%PRODUCT%_ONEFILE.zip" del /q "release\%PRODUCT%_ONEFILE.zip"
where tar.exe >nul 2>nul && tar.exe -a -c -f "release\%PRODUCT%_ONEFILE.zip" -C release "%PRODUCT%.exe" README_V6_2.txt KNOWN_LIMITATIONS_V6_2.md RELEASE_NOTES_V6_2.md V6_2_FULL_AUDIT.json V6_2_CAD_CLOSED_SECTION_VALIDATION.json

(
  echo BUILD_STATUS=PASS
  echo PYTHON=%PY_BASE%
  echo EXE=%CD%\dist\%PRODUCT%.exe
  echo FROZEN_SOLVER_SMOKE=PASS
  echo RELEASE=%CD%\release\%PRODUCT%.exe
) > "%LOG_DIR%\LAST_BUILD_RESULT.txt"

echo [11/11] Hoan tat.
echo.
echo ================================================================
echo [PASS] DA TAO EXE VA FROZEN SOLVER DA TINH DUOC Fs
echo.
echo EXE de test:
echo   %CD%\dist\%PRODUCT%.exe
echo.
echo EXE de sao chep sang may khac:
echo   %CD%\release\%PRODUCT%.exe
echo ================================================================
echo.
start "" "%CD%\dist"
pause
exit /b 0

:no_curl
echo [FAIL] Windows khong tim thay curl.exe de tai Python.
goto :fail

:download_fail
echo [FAIL] Khong tai duoc Python tu python.org.
goto :fail

:hash_mismatch
echo [FAIL] SHA-256 bo cai Python khong dung.
echo Mong doi: %PY_SHA256%
echo Thuc te : !ACTUAL_SHA!
del /q "%PY_INSTALLER%" >nul 2>nul
goto :fail

:python_install_fail
echo [FAIL] Khong cai duoc Python cuc bo vao %PY_RUNTIME%.
goto :fail

:bad_python
echo [FAIL] Python tim thay khong phai 64-bit hoac khong thuoc 3.11-3.13.
goto :fail

:missing_exe
echo [FAIL] PyInstaller ket thuc nhung khong co dist\%PRODUCT%.exe.
goto :fail

:frozen_solver_fail
echo [FAIL] EXE da tao nhung frozen solver khong tinh duoc Fs.
echo        Kiem tra tests\_windows_solver_smoke_output.issv6.diagnostics.json
goto :fail

:fail
(
  echo BUILD_STATUS=FAIL
  echo TIME=%DATE% %TIME%
) > "%LOG_DIR%\LAST_BUILD_RESULT.txt"
echo.
echo ================================================================
echo [FAIL] KHONG TAO DUOC BAN EXE DAT NGHIEM THU
echo Xem loi o phia tren va file build_logs\LAST_BUILD_RESULT.txt
echo ================================================================
pause
exit /b 1
