# IMSAT VOLUME V2.0 — explicit AutoCAD API compile candidates.
# This script deliberately DOES NOT create a generally installable EXE.
# The existing 2023 production QA installer remains unchanged.
param(
    [ValidateSet('ACAD2021_NET48','ACAD2022_NET48','ACAD2023_NET48',
                 'ACAD2024_NET48','ACAD2025_NET8','ACAD2025_NET10',
                 'ACAD2026_NET8','ACAD2026_NET10')]
    [string]$Flavor = 'ACAD2023_NET48',
    [switch]$NuGetReferences,
    [string]$AutoCADDir = '',
    [string]$Output = ''
)
$ErrorActionPreference='Stop'
$base=Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output=Join-Path $base ("dist/compat/"+$Flavor)
}
$opts=@("-p:IMSATCadFlavor=$Flavor")
if ($NuGetReferences) {$opts+= '-p:UseAutoCADNuGet=true'}
else {
    if([string]::IsNullOrWhiteSpace($AutoCADDir)) {
        throw 'Only the exact target AutoCAD SDK/installed host may be used without -NuGetReferences.'
    }
    if(-not (Test-Path (Join-Path $AutoCADDir 'AcDbMgd.dll'))) {
        throw "Missing target AcDbMgd.dll: $AutoCADDir"
    }
    $opts+= '-p:UseAutoCADNuGet=false'
    $opts+= "-p:IMSATCadHostDir=$AutoCADDir"
}
# Fail closed if building with stale intermediates from a different host.
$opts+= "-p:BaseIntermediateOutputPath=obj_compat/$Flavor/"
$opts+= "-p:IntermediateOutputPath=obj_compat/$Flavor/"
$opts+= "-p:BaseOutputPath=bin_compat/$Flavor/"
Push-Location $base
try {
    & dotnet restore 'MiningVolume.HSNext.sln' @opts
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $Flavor" }
    & dotnet build 'MiningVolume.HSNext.sln' --no-restore -c Release @opts
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $Flavor" }
    $project = Join-Path $base 'src/MiningVolume.Plugin2023'
    $assemblies = Get-ChildItem -Path (Join-Path $project 'bin_compat') -Filter 'IMSATVolume.*.dll' -Recurse |
        Where-Object { $_.Name -eq "IMSATVolume.$Flavor.dll" }
    if ($Flavor -eq 'ACAD2023_NET48') {
        $assemblies=Get-ChildItem -Path (Join-Path $project 'bin_compat') -Filter 'MiningVolume2023.dll' -Recurse
    }
    if (@($assemblies).Count -ne 1) { throw "Expected exactly one built entry assembly for $Flavor" }
    New-Item -Path $Output -ItemType Directory -Force | Out-Null
    Copy-Item $assemblies[0].FullName -Destination $Output -Force
    # A compile is NOT a host-runtime PASS; preserve provenance and audit.
    $sha=(git rev-parse HEAD 2>$null)
    @(
        'IMSAT VOLUME COMPATIBILITY BUILD — QA ONLY, NOT FOR INSTALLATION'
        "Flavor=$Flavor"
        "GitSHA=$sha"
        "APIReference=$(if($NuGetReferences){'NuGet compile-only'}else{$AutoCADDir})"
        'HostRuntimeAcceptance=NOT_TESTED'
        'InstallerApproval=BLOCKED'
        'Do not rename a .NET 8 DLL to make it run on .NET 10 or vice versa.'
    ) | Set-Content -Path (Join-Path $Output 'BUILD_STATUS.txt') -Encoding UTF8
    Write-Host "Compiled compatibility candidate: $Flavor (NOT RUNTIME VERIFIED)"
} finally { Pop-Location }
