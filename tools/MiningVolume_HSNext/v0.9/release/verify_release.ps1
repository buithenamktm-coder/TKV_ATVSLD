$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..\bundle\MiningVolume2023.bundle'
$win = Join-Path $root 'Contents\Windows'
$required = @(
  'MiningVolume2023.dll',
  'MiningVolume.Core.dll',
  'MiningVolume.Surface.dll',
  'MiningVolume.Cad2023.dll'
)
foreach ($name in $required) {
  $p = Join-Path $win $name
  if (-not (Test-Path $p)) { throw "Thiếu Release DLL: $name" }
  if ((Get-Item $p).Length -lt 1024) { throw "DLL bất thường/qua nhỏ: $name" }
}
$banned = @('AcMgd.dll','AcDbMgd.dll','AcCoreMgd.dll','AcWindows.dll','AdWindows.dll','NetTopologySuite.dll')
foreach ($name in $banned) {
  if (Test-Path (Join-Path $win $name)) { throw "Không được đóng gói runtime/reference DLL: $name" }
}
$pkg = Get-Content (Join-Path $root 'PackageContents.xml') -Raw
if ($pkg -notmatch 'AppVersion="1\.0\.0"') { throw 'PackageContents chưa phải V1.0' }
if ($pkg -notmatch 'SeriesMin="R24\.2"' -or $pkg -notmatch 'SeriesMax="R24\.2"') { throw 'Bundle không khóa đúng AutoCAD 2023 R24.2' }
Write-Host '[PASS] Release bundle policy.'