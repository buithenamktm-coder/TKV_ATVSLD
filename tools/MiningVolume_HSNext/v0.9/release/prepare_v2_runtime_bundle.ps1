# Stage a separate V2 bundle for the licensed AutoCAD 2023 host runtime smoke.
# Never mutate the V1 working bundle or mark an installer FINAL.
param(
    [string]$Root = (Join-Path $PSScriptRoot '..'),
    [string]$Destination = (Join-Path $PSScriptRoot '../dist/v2-host/MiningVolume2023.bundle')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $Root).Path
$source = Join-Path $root 'bundle/MiningVolume2023.bundle'
if (-not (Test-Path (Join-Path $source 'Contents/Windows/MiningVolume2023.dll'))) {
    throw 'BLOCKED: V2 AutoCAD DLL chưa build.'
}
$proof = Join-Path $root 'dist/V2_LICENSE_PUBLIC_KEY_PROOF.txt'
if (-not (Test-Path $proof) -or
    (Get-Content $proof -Raw) -notmatch 'SourceProvisioned=YES') {
    throw 'BLOCKED: chưa provision RSA public verifier của nhà phát hành.'
}
if (Test-Path $Destination) { Remove-Item $Destination -Recurse -Force }
New-Item -ItemType Directory -Path (Split-Path -Parent $Destination) -Force | Out-Null
Copy-Item -Path $source -Destination $Destination -Recurse -Force
$package = Join-Path $Destination 'PackageContents.xml'
$xml = [System.IO.File]::ReadAllText($package)
if (-not $xml.Contains('AppVersion="1.0.0"') -or
    -not $xml.Contains('Version="1.0.0"')) {
    throw 'BLOCKED: template bundle không còn V1 gốc; không thể dựng V2 theo kiểm soát.'
}
$xml = $xml.Replace('AppVersion="1.0.0"', 'AppVersion="2.0.0"')
$xml = $xml.Replace('Version="1.0.0"', 'Version="2.0.0"')
$xml = $xml.Replace('GUI-first V1.0', 'V2.0 Host QA Candidate - NOT RELEASE')
[System.IO.File]::WriteAllText($package, $xml,
    (New-Object System.Text.UTF8Encoding($false)))
if ($xml -notmatch 'AppVersion="2\.0\.0"' -or
    $xml -notmatch 'Version="2\.0\.0"') {
    throw 'BLOCKED: không sinh được V2.0 PackageContents.'
}
Write-Host "[PASS] V2.0 host bundle staged at $Destination (NOT RELEASE)."
