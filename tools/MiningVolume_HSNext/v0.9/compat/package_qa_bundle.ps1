# Package only an already compiled, hashed and correctly flavored add-in.
# QA artifacts are NOT installation-approved until host runtime tests pass.
param(
    [Parameter(Mandatory=$true)][ValidateSet('ACAD2020_NET47','ACAD2021_NET48','ACAD2022_NET48','ACAD2023_NET48','ACAD2024_NET48','ACAD2025_NET8','ACAD2025_NET10','ACAD2026_NET8','ACAD2026_NET10')][string]$Flavor,
    [Parameter(Mandatory=$true)][string]$CompiledDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$series=@{
    ACAD2020_NET47='R23.1'; ACAD2021_NET48='R24.0'; ACAD2022_NET48='R24.1'
    ACAD2023_NET48='R24.2'; ACAD2024_NET48='R24.3'
    ACAD2025_NET8='R25.0'; ACAD2025_NET10='R25.0'
    ACAD2026_NET8='R25.1'; ACAD2026_NET10='R25.1'
}
$manifestPath=Join-Path $CompiledDirectory 'COMPILE_MANIFEST.json'
if(!(Test-Path $manifestPath)){throw "Missing compile manifest for $Flavor"}
$m=Get-Content $manifestPath -Raw | ConvertFrom-Json
if($m.flavor -cne $Flavor -or $m.compile -cne 'PASS' -or $m.host -cne 'NOT_TESTED' -or $m.release -cne 'BLOCKED'){
    throw "Invalid QA build manifest for $Flavor"
}
if($m.source_sha -notmatch '^[0-9a-fA-F]{40}$'){throw 'Invalid source SHA'}
$entry=if($Flavor -eq 'ACAD2023_NET48'){'MiningVolume2023.dll'}else{"IMSATVolume.$Flavor.dll"}
$expected=@($entry,'MiningVolume.Core.dll','MiningVolume.Surface.dll','MiningVolume.Cdt.Native.dll')
$cadAdapter=if($Flavor -eq 'ACAD2023_NET48'){'MiningVolume.Cad2023.dll'}else{"IMSATVolume.Cad.$Flavor.dll"}
$expected+= $cadAdapter
$found=@($m.files | ForEach-Object {$_.name})
foreach($name in $expected){if($found -cnotcontains $name){throw "Required compiled DLL missing: $name"}}
$target=Join-Path $OutputDirectory ("IMSATVolume_"+$Flavor+".bundle")
if(Test-Path $target){throw "Refusing to overwrite existing candidate: $target"}
$windowsDir=Join-Path $target 'Contents/Windows'
New-Item -ItemType Directory -Path $windowsDir -Force | Out-Null
try {
    foreach($f in $m.files) {
        $name=[string]$f.name
        if($name -notmatch '^[A-Za-z0-9_.-]+\\.dll$'){throw "Unsafe DLL path: $name"}
        $source=Join-Path $CompiledDirectory $name
        if(!(Test-Path $source -PathType Leaf)){throw "Missing $source"}
        $actual=(Get-FileHash $source -Algorithm SHA256).Hash
        if($actual -ine [string]$f.sha256){throw "Hash mismatch: $name"}
        Copy-Item -LiteralPath $source -Destination $windowsDir
    }
    $v='2.0.0'; $r=$series[$Flavor]
    $safe=[System.Security.SecurityElement]::Escape($Flavor)
    $loader="./Contents/Windows/$entry"
    $xml=@"
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage SchemaVersion="1.0" AutodeskProduct="AutoCAD" ProductType="Application" Name="IMSAT VOLUME" AppVersion="$v" Description="IMSAT VOLUME V2.0 QA compatibility build - NOT RELEASE" Author="Bùi Thế Nam">
  <CompanyDetails Name="Viện Khoa học Công nghệ Mỏ - Vinacomin" />
  <RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="$r" SeriesMax="$r" />
  <Components>
    <RuntimeRequirements OS="Win64" Platform="AutoCAD*" SeriesMin="$r" SeriesMax="$r" />
    <ComponentEntry AppName="$safe" Version="$v" ModuleName="$loader" LoadOnAutoCADStartup="True" />
  </Components>
</ApplicationPackage>
"@
    [xml]$parsed=$xml
    if($parsed.ApplicationPackage.Components.ComponentEntry.ModuleName -ne $loader){throw 'Invalid loader XML'}
    [IO.File]::WriteAllText((Join-Path $target 'PackageContents.xml'),$xml,[Text.UTF8Encoding]::new($false))
    @{flavor=$Flavor;series=$r;source_sha=$m.source_sha;host_runtime='NOT_TESTED';release='BLOCKED';payload_hashes=$m.files} |
        ConvertTo-Json -Depth 6 | Set-Content (Join-Path $target 'QA_MANIFEST.json') -Encoding UTF8
    Write-Host "QA compatibility bundle created: $target ; AutoCAD host NOT TESTED."
}
catch {Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue;throw}
