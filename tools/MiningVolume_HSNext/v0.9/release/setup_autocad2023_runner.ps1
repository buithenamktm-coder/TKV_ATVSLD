param(
    [string]$Repository = 'buithenamktm-coder/TKV_ATVSLD',
    [string]$RunnerRoot = 'C:\\actions-runner-miningvolume',
    [string]$RunnerName = '',
    [string]$WorkFolder = '_work',
    [switch]$Reconfigure
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Require-InteractiveAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Hãy mở PowerShell bằng Run as administrator rồi chạy lại script.'
    }
    if (-not [Environment]::UserInteractive) {
        throw 'Runner AutoCAD phải chạy trong phiên Windows tương tác, không chạy dưới Windows Service.'
    }
}

function Find-AutoCAD2023 {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Autodesk\AutoCAD 2023'),
        'C:\Program Files\Autodesk\AutoCAD 2023'
    ) | Select-Object -Unique
    foreach ($dir in $candidates) {
        if (-not $dir) { continue }
        $acad = Join-Path $dir 'acad.exe'
        if ((Test-Path $acad) -and (Test-Path (Join-Path $dir 'AcMgd.dll'))) {
            $version = (Get-Item $acad).VersionInfo.FileVersion
            if ($version -match '^R?24\.2') {
                return [pscustomobject]@{ Directory = $dir; Acad = $acad; Version = $version }
            }
        }
    }
    throw 'Không tìm thấy AutoCAD 2023 R24.2 trên máy.'
}

function Require-GitHubCli {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) { throw 'Chưa có GitHub CLI (gh). Cài GitHub CLI và đăng nhập bằng: gh auth login' }
    & gh auth status *> $null
    if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI chưa đăng nhập. Chạy: gh auth login' }
}

function Get-RegistrationToken([string]$Repo) {
    if ($env:GH_RUNNER_TOKEN) { return $env:GH_RUNNER_TOKEN.Trim() }
    $token = (& gh api -X POST "repos/$Repo/actions/runners/registration-token" --jq .token).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $token) {
        throw 'Không lấy được runner registration token. Tài khoản GitHub cần quyền quản trị Actions của repository.'
    }
    return $token
}

function Get-RunnerDownload([string]$Repo) {
    $json = & gh api "repos/$Repo/actions/runners/downloads"
    if ($LASTEXITCODE -ne 0) { throw 'Không đọc được danh sách GitHub Actions runner.' }
    $items = $json | ConvertFrom-Json
    $match = $items | Where-Object { $_.os -eq 'win' -and $_.architecture -eq 'x64' } | Select-Object -First 1
    if (-not $match) { throw 'GitHub không trả về gói Actions runner Windows x64.' }
    return $match
}

Require-InteractiveAdministrator
$cad = Find-AutoCAD2023
Require-GitHubCli

if (Get-Process acad -ErrorAction SilentlyContinue) {
    throw 'Hãy đóng AutoCAD trước khi cấu hình runner.'
}
if (-not $RunnerName) { $RunnerName = 'MiningVolume-AutoCAD2023-' + $env:COMPUTERNAME }
$repoUrl = "https://github.com/$Repository"
New-Item -ItemType Directory -Force $RunnerRoot | Out-Null

$config = Join-Path $RunnerRoot 'config.cmd'
if ((Test-Path $config) -and -not $Reconfigure) {
    Write-Host "Runner đã tồn tại tại $RunnerRoot."
} else {
    if ($Reconfigure -and (Test-Path (Join-Path $RunnerRoot '.runner'))) {
        try {
            $removeToken = Get-RegistrationToken $Repository
            Push-Location $RunnerRoot
            try { & .\config.cmd remove --token $removeToken } finally { Pop-Location }
        } catch { Write-Warning "Không gỡ được cấu hình runner cũ: $($_.Exception.Message)" }
    }

    Remove-Item (Join-Path $RunnerRoot '*') -Recurse -Force -ErrorAction SilentlyContinue
    $download = Get-RunnerDownload $Repository
    $zip = Join-Path $env:TEMP 'actions-runner-win-x64.zip'
    Remove-Item $zip -Force -ErrorAction SilentlyContinue
    Write-Host "Tải GitHub Actions runner: $($download.download_url)"
    Invoke-WebRequest -Uri $download.download_url -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath $RunnerRoot -Force
    Remove-Item $zip -Force -ErrorAction SilentlyContinue

    $token = Get-RegistrationToken $Repository
    Push-Location $RunnerRoot
    try {
        & .\config.cmd --unattended --url $repoUrl --token $token --name $RunnerName --labels 'autocad2023' --work $WorkFolder --replace
        if ($LASTEXITCODE -ne 0) { throw "config.cmd trả mã lỗi $LASTEXITCODE." }
    } finally { Pop-Location }
}

$launcher = Join-Path $RunnerRoot 'START_MiningVolume_Runtime_Runner.cmd'
@(
    '@echo off',
    '>nul 2>&1 fltmc',
    'if %errorlevel% neq 0 (',
    '  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath ''%~f0'' -Verb RunAs"',
    '  exit /b',
    ')',
    'title MiningVolume AutoCAD 2023 Runtime Runner',
    ('cd /d "' + $RunnerRoot + '"'),
    'echo MiningVolume runtime runner - AutoCAD 2023',
    'echo KHONG dong cua so nay trong khi dang kiem thu.',
    'run.cmd',
    'pause'
) | Set-Content -Path $launcher -Encoding ASCII

$desktop = [Environment]::GetFolderPath('Desktop')
if ($desktop) { Copy-Item $launcher (Join-Path $desktop 'START_MiningVolume_Runtime_Runner.cmd') -Force }

Write-Host ''
Write-Host '[READY] MiningVolume AutoCAD 2023 self-hosted runner đã được cấu hình.'
Write-Host "AutoCAD: $($cad.Acad)"
Write-Host "Version: $($cad.Version)"
Write-Host "Runner: $RunnerName"
Write-Host 'Labels: self-hosted, Windows, X64, autocad2023'
Write-Host ''
Write-Host 'Để nhận runtime job:'
Write-Host "  1. Đóng AutoCAD nếu đang mở."
Write-Host "  2. Chạy: $launcher"
Write-Host '  3. Giữ cửa sổ runner mở cho tới khi workflow runtime hoàn tất.'
Write-Host ''
Write-Host 'Không cài runner dưới dạng Windows Service vì MVSELFTEST cần AutoCAD chạy trong phiên desktop tương tác.'