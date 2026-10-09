param(
    [string]$BundlePath = (Join-Path $PSScriptRoot '..\bundle\MiningVolume2023.bundle'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\dist\RUNTIME_VERIFICATION.txt'),
    [string]$SelfTestPath = (Join-Path $PSScriptRoot '..\dist\MVSELFTEST_RUNTIME.txt'),
    [int]$TimeoutSeconds = 180,
    [switch]$KeepInstalled
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Runtime verifier phải chạy bằng tài khoản Administrator để cài tạm bundle vào ProgramData.'
}
if (-not [Environment]::UserInteractive) {
    throw 'Runtime verifier phải chạy trong phiên Windows tương tác, không chạy dưới service session.'
}

function Find-AutoCAD2023 {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Autodesk\AutoCAD 2023'),
        'C:\Program Files\Autodesk\AutoCAD 2023'
    ) | Select-Object -Unique

    foreach ($dir in $candidates) {
        if ($dir -and (Test-Path (Join-Path $dir 'acad.exe')) -and (Test-Path (Join-Path $dir 'AcMgd.dll'))) {
            return $dir
        }
    }

    $roots = @(
        'HKLM:\SOFTWARE\Autodesk\AutoCAD\R24.2',
        'HKLM:\SOFTWARE\WOW6432Node\Autodesk\AutoCAD\R24.2'
    )
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        foreach ($key in Get-ChildItem $root -ErrorAction SilentlyContinue) {
            try {
                $p = (Get-ItemProperty $key.PSPath -ErrorAction Stop).AcadLocation
                if ($p -and (Test-Path (Join-Path $p 'acad.exe'))) { return $p }
            } catch { }
        }
    }
    throw 'Không tìm thấy AutoCAD 2023 (R24.2) trên runtime runner.'
}

function Copy-Directory([string]$Source, [string]$Destination) {
    if (-not (Test-Path $Source)) { throw "Không tồn tại bundle nguồn: $Source" }
    New-Item -ItemType Directory -Force $Destination | Out-Null
    Copy-Item (Join-Path $Source '*') $Destination -Recurse -Force
}

$cadDir = Find-AutoCAD2023
$acad = Join-Path $cadDir 'acad.exe'
$acadVersion = (Get-Item $acad).VersionInfo.FileVersion
if ($acadVersion -notmatch '^R?24\.2') {
    throw "acad.exe không phải AutoCAD 2023 R24.2. FileVersion=$acadVersion"
}
if (Get-Process acad -ErrorAction SilentlyContinue) {
    throw 'AutoCAD đang chạy. Runtime gate yêu cầu đóng toàn bộ acad.exe trước khi kiểm thử.'
}

$bundle = (Resolve-Path $BundlePath).Path
$required = @(
    'PackageContents.xml',
    'Contents\Windows\MiningVolume2023.dll',
    'Contents\Windows\MiningVolume.Core.dll',
    'Contents\Windows\MiningVolume.Surface.dll',
    'Contents\Windows\MiningVolume.Cad2023.dll'
)
foreach ($rel in $required) {
    if (-not (Test-Path (Join-Path $bundle $rel))) { throw "Thiếu runtime file: $rel" }
}

$programData = if ($env:ProgramData) { $env:ProgramData } else { 'C:\ProgramData' }
$appPlugins = Join-Path $programData 'Autodesk\ApplicationPlugins'
$target = Join-Path $appPlugins 'MiningVolume2023.bundle'
$startupLog = Join-Path $programData 'MiningVolume2023\Logs\startup.log'
$backup = Join-Path $appPlugins 'MiningVolume2023.bundle.runtimeverify.bak'
$temp = Join-Path $env:TEMP ("MiningVolumeRuntime_" + [Guid]::NewGuid().ToString('N'))
$scriptPath = Join-Path $temp 'runtime_selftest.scr'
New-Item -ItemType Directory -Force $temp | Out-Null
New-Item -ItemType Directory -Force (Split-Path $OutputPath -Parent) | Out-Null
Remove-Item $SelfTestPath -Force -ErrorAction SilentlyContinue
Remove-Item $OutputPath -Force -ErrorAction SilentlyContinue
Remove-Item $startupLog -Force -ErrorAction SilentlyContinue

# Recover from an interrupted earlier runtime verification before starting.
if (Test-Path $backup) {
    Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
    Move-Item $backup $target -Force
}

$hadPrevious = Test-Path $target
try {
    if ($hadPrevious) { Move-Item $target $backup -Force }
    Copy-Directory $bundle $target

    $pluginDll = Join-Path $target 'Contents\Windows\MiningVolume2023.dll'
    if (-not (Test-Path $pluginDll)) {
        throw "Không tìm thấy DLL plugin để NETLOAD: $pluginDll"
    }

    # Runtime CI must not block on AutoCAD's unsigned-DLL dialog. The production
    # bundle still keeps its normal startup behavior; only this temporary runtime
    # copy is changed to manual NETLOAD. SECURELOAD is disabled only inside the
    # dedicated CI AutoCAD session, then restored before QUIT.
    $runtimePackage = Join-Path $target 'PackageContents.xml'
    $packageText = Get-Content $runtimePackage -Raw
    $packageText = $packageText -replace 'LoadOnAutoCADStartup="True"', 'LoadOnAutoCADStartup="False"'
    Set-Content -Path $runtimePackage -Value $packageText -Encoding UTF8

    @(
        'FILEDIA'
        '0'
        'CMDDIA'
        '0'
        '(setq mv_secureload_old (getvar "SECURELOAD"))'
        '(setvar "SECURELOAD" 0)'
        '_.NETLOAD'
        ('"' + $pluginDll + '"')
        'MVSELFTEST'
        '(setvar "SECURELOAD" mv_secureload_old)'
        '_.QUIT'
    ) | Set-Content -Path $scriptPath -Encoding ASCII

    $oldSelfTest = $env:MININGVOLUME_SELFTEST_FILE
    $env:MININGVOLUME_SELFTEST_FILE = $SelfTestPath
    try {
        $proc = Start-Process -FilePath $acad -ArgumentList @('/nologo','/b',"`"$scriptPath`"") -PassThru
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $result = $null

        while ((Get-Date) -lt $deadline) {
            if (Test-Path $SelfTestPath) {
                $result = Get-Content $SelfTestPath -Raw
                if ($result -match 'Status=FAIL') { break }
                if ($result -match 'Status=PASS') { break }
            }
            if ($proc.HasExited -and -not (Test-Path $SelfTestPath)) { break }
            Start-Sleep -Seconds 1
        }

        # MVSELFTEST writes its PASS/FAIL proof before AutoCAD needs to exit.
        # On some user profiles a startup command (for example MASSPROP) can seize
        # the command line after MVSELFTEST and keep acad.exe waiting for input.
        # Once the proof file exists, terminate this dedicated CI AutoCAD process
        # immediately instead of exposing or waiting on unrelated profile commands.
        if (-not $proc.HasExited -and (Test-Path $SelfTestPath)) {
            try { $proc.Kill() } catch { }
            try { $null = $proc.WaitForExit(5000) } catch { }
        } elseif (-not $proc.HasExited) {
            if (-not $proc.WaitForExit(5000)) { $proc.Kill() }
        }

        if (-not (Test-Path $SelfTestPath)) {
            $startupDetail = if (Test-Path $startupLog) {
                Get-Content $startupLog -Raw
            } else {
                '(không có startup.log mới từ lần chạy này)'
            }
            throw "AutoCAD không sinh log MVSELFTEST trong $TimeoutSeconds giây.
Startup log hiện tại:
$startupDetail"
        }
        $result = Get-Content $SelfTestPath -Raw
        if ($result -notmatch 'MiningVolume HS-Next v0\.10\.4 runtime self-test') {
            throw 'MVSELFTEST log không đúng phiên bản v1.0.'
        }
        if ($result -notmatch 'Status=PASS') {
            throw "MVSELFTEST không PASS.\n\n$result"
        }

        $requiredPass = @(
            'PASS | Khởi tạo đầy đủ giao diện MiningVolume',
            'PASS | Bật/tắt palette MiningVolume hoạt động',
            'PASS | AutoCAD tạo/ghi/đếm đúng layer TIN hiện trạng',
            'PASS | AutoCAD tạo/ghi/đếm đúng layer TIN thiết kế',
            'PASS | Layer TIN đúng màu quy ước: hiện trạng ACI 1, thiết kế ACI 3',
            'PASS | Layer TIN chỉ chứa 3DFACE và được khóa sau khi ghi',
            'PASS | Có thể ẩn cả hai TIN và layer vẫn khóa',
            'PASS | Có thể hiện lại cả hai TIN và layer vẫn khóa'
        )
        foreach ($line in $requiredPass) {
            if ($result -notmatch [regex]::Escape($line)) { throw "Thiếu runtime check bắt buộc: $line" }
        }

        $dllDir = Join-Path $target 'Contents\Windows'
        $hashLines = @()
        foreach ($dll in @('MiningVolume2023.dll','MiningVolume.Core.dll','MiningVolume.Surface.dll','MiningVolume.Cad2023.dll')) {
            $h = (Get-FileHash (Join-Path $dllDir $dll) -Algorithm SHA256).Hash.ToLowerInvariant()
            $hashLines += "SHA256.$dll=$h"
        }

        $sourceCommit = if ($env:SOURCE_SHA) { $env:SOURCE_SHA } elseif ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { 'local' }
        $lines = @(
            'MiningVolume HS-Next v1.0 - AUTOCAD 2023 RUNTIME VERIFICATION',
            ('Timestamp=' + (Get-Date).ToString('o')),
            'Status=PASS',
            ('SourceCommit=' + $sourceCommit),
            ('AutoCADPath=' + $acad),
            ('AutoCADFileVersion=' + $acadVersion)
        ) + $hashLines + @('', '--- MVSELFTEST ---', $result)
        $lines | Set-Content -Path $OutputPath -Encoding UTF8
        Write-Host '[PASS] AutoCAD 2023 runtime verification.'
        Write-Host "Verification: $OutputPath"
    }
    finally {
        $env:MININGVOLUME_SELFTEST_FILE = $oldSelfTest
    }
}
finally {
    if (-not $KeepInstalled) {
        Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path $backup) { Move-Item $backup $target -Force }
    } else {
        Remove-Item $backup -Recurse -Force -ErrorAction SilentlyContinue
    }
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
