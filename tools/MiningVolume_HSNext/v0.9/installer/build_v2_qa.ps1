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

    # One installer implementation; V1 defaults are preserved. No global source replacements.
    Copy-Item $main (Join-Path $setupDir "main.go")
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
        # Reuse the IMSAT icon resource compiler already installed by the V1 CI step.
        $rsrc = Join-Path ((go env GOPATH).Trim()) 'bin/rsrc.exe'
        if (-not (Test-Path $rsrc)) { throw "Không tìm thấy rsrc.exe; không build Setup V2 thiếu icon IMSAT." }
        & $rsrc -ico (Join-Path $Root 'installer/imsat.ico') -arch amd64 -o 'rsrc_windows_amd64.syso'
        if ($LASTEXITCODE -ne 0) { throw "Không nhúng được icon IMSAT trong Setup V2." }
        go mod init imsat-v2-qa-candidate | Out-Null
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
        $exe = Join-Path $dist "IMSAT_MiningVolume_V2_0_QA_CANDIDATE.exe"
        $flags = '-s -w -H=windowsgui -X main.version=2.0.0 -X main.backupSuffix=.v200qabak -X main.storedSetupName=MiningVolume_Setup_v2.0_QA.exe -X "main.productName=IMSAT VOLUME V2.0 QA Candidate (AutoCAD 2023)"'
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
        'IMSAT MININGVOLUME V2.0 QA CANDIDATE — NOT A RELEASE',
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
