# V2.0 QA-only packaging. Uses Release DLLs already built by the Windows CI,
# stages a separate bundle and supplies version variables through Go linker flags.
# Does not mutate V1.0 source/bundle or claim the binary is certified.
param([string]$Root = "")

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Split-Path -Parent $PSScriptRoot
}
$Root = (Resolve-Path $Root).Path
$existing = Join-Path $Root "bundle/MiningVolume2023.bundle"
$main = Join-Path $Root "installer/main.go"
$license = Join-Path $Root "src/MiningVolume.Plugin2023/Services/LicenseService.cs"
$dist = Join-Path $Root "dist"
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("imsat_v2qa_" + [Guid]::NewGuid().ToString("N"))
$stage = Join-Path $work "bundle"
$setupDir = Join-Path $work "setup"
try {
    $dll = Join-Path $existing "Contents/Windows/MiningVolume2023.dll"
    if (!(Test-Path $dll)) { throw "Chưa build DLL Release; không thể đóng gói V2.0 QA." }
    if (!(Test-Path $main)) { throw "Thiếu nguồn installer Go." }
    New-Item -ItemType Directory -Path $stage, $setupDir, $dist -Force | Out-Null
    Copy-Item -Path $existing -Destination $stage -Recurse -Force
    $package = Join-Path $stage "MiningVolume2023.bundle/PackageContents.xml"
    $xml = [System.IO.File]::ReadAllText($package)
    if (-not $xml.Contains('AppVersion="1.0.0"')) { throw "Package V1 gốc không khớp; không tự đổi bừa." }
    $xml = $xml.Replace('AppVersion="1.0.0"', 'AppVersion="2.0.0"')
    $xml = $xml.Replace('Version="1.0.0"', 'Version="2.0.0"')
    $xml = $xml.Replace('GUI-first V1.0', 'V2.0 QA Candidate - NOT RELEASE')
    [System.IO.File]::WriteAllText($package, $xml, (New-Object System.Text.UTF8Encoding $false))

    # Bind the real staged V2 DLLs to source SHA and verified SHA256 digests.
    $windows = Join-Path $stage 'MiningVolume2023.bundle/Contents/Windows'
    $sourceSha = [string]$env:SOURCE_SHA
    if ($sourceSha -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'BLOCKED: invalid source SHA for V2 package.'
    }
    $dllNames = @(
        'MiningVolume2023.dll',
        'MiningVolume.Core.dll',
        'MiningVolume.Surface.dll',
        'MiningVolume.Cdt.Native.dll',
        'MiningVolume.Cad2023.dll'
    )
    $identityLines = @('SOURCE_SHA=' + $sourceSha.ToLowerInvariant())
    foreach ($name in $dllNames) {
        $dllPath = Join-Path $windows $name
        if (!(Test-Path -LiteralPath $dllPath -PathType Leaf)) {
            throw "BLOCKED: missing V2 DLL: $name"
        }
        $digest = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $identityLines += ($digest + ' ' + $name)
    }
    $identityLines | Set-Content -LiteralPath (Join-Path $windows 'BUILD_IDENTITY.txt') -Encoding ascii

    # One installer implementation; V1 defaults are preserved. No global source replacements.
    # Rebrand only the temporary V2 installer source, never the V1 Go source.
    $v2main = [System.IO.File]::ReadAllText($main).Replace('IMSAT VOLUME', 'NAMVECTA VOLUME')
    [System.IO.File]::WriteAllText((Join-Path $setupDir 'main.go'), $v2main, (New-Object System.Text.UTF8Encoding $false))
    # Copy the full Windows installer package, not only main.go: otherwise
    # new compatibility planning code and its tests are silently left out.
    foreach ($file in @("main_test.go", "host_install_plan.go", "host_install_plan_test.go")) {
        $source = Join-Path $Root ("installer/" + $file)
        if (!(Test-Path $source)) { throw "Thiếu mã nguồn hoặc kiểm thử Setup: $file" }
        Copy-Item $source (Join-Path $setupDir $file)
    }

    Compress-Archive -Path $stage -DestinationPath (Join-Path $setupDir 'payload.zip') -CompressionLevel Optimal
    Push-Location $setupDir
    try {
        # Use the exact approved PA3 symbol raster; generate a Windows icon
        # only for this V2 QA staging directory (the V1 ICO stays unchanged).
        $pa3 = Join-Path $Root 'docs/branding/namvecta-pa3-reference/image2.png'
        if (!(Test-Path -LiteralPath $pa3 -PathType Leaf)) { throw 'BLOCKED: missing PA3 symbol.' }
        $pa3Hash = (Get-FileHash -LiteralPath $pa3 -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($pa3Hash -ne '91aa653548ce361d158fc6be01893c87aea5bad54726a1d6083b9c234ade9161') {
            throw 'BLOCKED: PA3 approved raster checksum mismatch.'
        }
        Add-Type -AssemblyName System.Drawing
        $sourceImage = [System.Drawing.Image]::FromFile($pa3)
        $bitmap = New-Object System.Drawing.Bitmap 256, 256
        try {
            $g = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $g.Clear([System.Drawing.Color]::White)
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $ratio = [Math]::Min(240.0 / $sourceImage.Width, 240.0 / $sourceImage.Height)
                $w = [int][Math]::Round($sourceImage.Width * $ratio)
                $h = [int][Math]::Round($sourceImage.Height * $ratio)
                $g.DrawImage($sourceImage, [int]((256 - $w) / 2), [int]((256 - $h) / 2), $w, $h)
            }
            finally { $g.Dispose() }
            $handle = $bitmap.GetHicon()
            $icon = [System.Drawing.Icon]::FromHandle($handle)
            $ico = Join-Path $setupDir 'namvecta-pa3.ico'
            $iconStream = [System.IO.File]::Create($ico)
            try { $icon.Save($iconStream) }
            finally { $iconStream.Dispose(); $icon.Dispose() }
        }
        finally { $bitmap.Dispose(); $sourceImage.Dispose() }
        $rsrc = Join-Path ((go env GOPATH).Trim()) 'bin/rsrc.exe'
        if (-not (Test-Path $rsrc)) { throw 'Icon resource compiler unavailable.' }
        & $rsrc -ico $ico -arch amd64 -o 'rsrc_windows_amd64.syso'
        if ($LASTEXITCODE -ne 0) { throw 'Cannot embed approved PA3 icon in V2 QA Setup.' }
        go mod init namvecta-v2-qa-candidate | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "go mod init V2 thất bại." }
        # Validate the rewritten V2 XML and the DLL hashes in this exact ZIP.
        # A passing V1 payload test does not validate the V2 packaging transform.
        $previousTestVersion = $env:IMSAT_TEST_PAYLOAD_VERSION
        try {
            $env:IMSAT_TEST_PAYLOAD_VERSION = '2.0.0'
            go test -v -count=1 .
            if ($LASTEXITCODE -ne 0) { throw "Kiểm thử bộ cài V2 (XML/DLL/checksum/host compatibility/rollback) thất bại; không tạo Setup." }
        }
        finally { $env:IMSAT_TEST_PAYLOAD_VERSION = $previousTestVersion }
        $exe = Join-Path $dist "NAMVECTA_VOLUME_2_0_QA_NOT_RELEASE.exe"
        $flags = '-s -w -H=windowsgui -X main.version=2.0.0 -X main.backupSuffix=.v200qabak -X main.storedSetupName=MiningVolume_Setup_v2.0_QA.exe -X "main.productName=NAMVECTA VOLUME 2.0 QA (AutoCAD 2023)"'
        go build -trimpath -ldflags $flags -o $exe .
        if ($LASTEXITCODE -ne 0) { throw "Không build được V2 QA Setup." }
    }
    finally { Pop-Location }

    $keyState = 'PUBLIC_KEY_UNVERIFIED'
    if (Test-Path $license) {
        $src = [System.IO.File]::ReadAllText($license)
        if ($src.Contains('RsaPublicKeyXml = "";')) {
            $keyState = 'TRIAL_ONLY: PUBLIC_KEY_NOT_PROVISIONED'
        }
        else {
            # A non-empty source constant is not proof of the approved issuer.
            # Require the SHA-pinned public-only receipt from this build.
            $publicProof = Join-Path $dist "V2_LICENSE_PUBLIC_KEY_PROOF.txt"
            if (Test-Path $publicProof) {
                $receipt = [System.IO.File]::ReadAllText($publicProof)
                $m = [regex]::Match($receipt, '(?m)^PublicKeySha256=([0-9a-fA-F]{64})\r?$')
                if ($receipt -match '(?m)^SourceProvisioned=YES\r?$' -and
                    $receipt -match '(?m)^PrivateKeyIncluded=NO\r?$' -and
                    $m.Success) {
                    $keyState = 'RSA_PUBLIC_VERIFIER_PINNED_SHA256=' +
                        $m.Groups[1].Value.ToLowerInvariant()
                }
            }
        }
    }
    if ($env:IMSAT_REQUIRE_APPROVED_PUBLIC_KEY -eq '1' -and
        -not $keyState.StartsWith('RSA_PUBLIC_VERIFIER_PINNED_SHA256=')) {
        throw "BLOCKED: V2 QA Setup missing approved issuer RSA public verifier."
    }
    $hash = (Get-FileHash -Algorithm SHA256 $exe).Hash
    $lines = @(
        'NAMVECTA VOLUME 2.0 QA - NOT RELEASE',
        ('Build UTC: ' + [DateTime]::UtcNow.ToString('o')),
        ('Source SHA: ' + $env:SOURCE_SHA),
        ('SHA256 EXE: ' + $hash),
        ('License: ' + $keyState),
        'Requires: Windows 64-bit + AutoCAD 2023; DO NOT install on production computers.',
        'Runtime AutoCAD host acceptance: NOT VERIFIED.',
        'NO production distribution until signed key provisioning, backup/recovery, real CAD tests and Release Gates PASS.'
    )
    $lines | Set-Content -Encoding UTF8 (Join-Path $dist "V2_QA_CANDIDATE_NOT_RELEASE.txt")
    Write-Host 'Built V2 QA candidate with explicit non-release manifest:' $exe
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
