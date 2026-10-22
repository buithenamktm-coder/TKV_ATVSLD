param([string]$File = 'dist/IMSAT_MiningVolume_V2_0_QA_CANDIDATE.exe')
$ErrorActionPreference='Stop'
$path=(Resolve-Path $File).Path
if((Get-Item $path).Length -lt 1024){throw 'Setup empty or truncated'}
$status='NOT TESTED: Microsoft Defender engine unavailable'
if(Get-Command Get-MpComputerStatus -ErrorAction SilentlyContinue) {
    try {$engine=Get-MpComputerStatus -ErrorAction Stop} catch {$engine=$null}
    if($engine -and $engine.AntivirusEnabled) {
        Start-MpScan -ScanType CustomScan -ScanPath $path -ErrorAction Stop
        $hits=Get-MpThreatDetection -ErrorAction Stop | Where-Object {($_.Resources -join '|').Contains($path)}
        if($hits){$hits | Format-List | Out-String | Set-Content dist/V2_SECURITY_SCAN.txt;throw 'Defender detected a threat in the QA Setup; do not distribute.'}
        $status="PASS: Defender custom scan; engine=$($engine.AMEngineVersion); signatures=$($engine.AntivirusSignatureVersion); signatureUpdated=$($engine.AntivirusSignatureLastUpdated)"
    }
}
@($status,"File=$path","SHA256=$((Get-FileHash $path -Algorithm SHA256).Hash)","Authenticode=$((Get-AuthenticodeSignature $path).Status)","UTC=$([DateTime]::UtcNow.ToString('o'))") | Set-Content dist/V2_SECURITY_SCAN.txt
Get-Content dist/V2_SECURITY_SCAN.txt
