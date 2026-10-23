# V2.0 RELEASE/host-QA provisioning. Public-only; never reads a private issuer key.
# A missing, malformed or unexpected public key MUST stop the production build.
param(
    [Parameter(Mandatory=$true)][string]$PublicKeyFile,
    [Parameter(Mandatory=$true)][string]$ExpectedPublicKeySha256,
    [string]$SourceFile = (Join-Path $PSScriptRoot '../src/MiningVolume.Plugin2023/Services/LicenseService.cs'),
    [string]$ReceiptFile = (Join-Path $PSScriptRoot '../dist/V2_LICENSE_PUBLIC_KEY_PROOF.txt')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $PublicKeyFile -PathType Leaf)) {
    throw 'BLOCKED: thiếu issuer.public.xml do License Manager trên máy quản trị tạo.'
}
if (-not (Test-Path -LiteralPath $SourceFile -PathType Leaf)) {
    throw 'Không tìm thấy LicenseService.cs trong cây nguồn build.'
}
if ($ExpectedPublicKeySha256 -notmatch '^[0-9a-fA-F]{64}$') {
    throw 'BLOCKED: chưa cấu hình SHA-256 public key đã được quản trị viên xác nhận.'
}
$raw = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $PublicKeyFile).Path).Trim()
if ($raw.Length -gt 16000 -or $raw.Length -lt 250) {
    throw 'BLOCKED: kích thước RSA public key không hợp lệ.'
}
[xml]$key = $raw
$node = $key.DocumentElement
if ($node.LocalName -ne 'RSAKeyValue' -or
    $node.ChildNodes.Count -ne 2 -or
    -not $node.Modulus -or -not $node.Exponent) {
    throw 'BLOCKED: chỉ chấp nhận public-only RSAKeyValue với Modulus và Exponent.'
}
foreach ($privateName in @('P','Q','D','DP','DQ','InverseQ','RSAKeyPair','PrivateKey')) {
    if ($raw -match ('(?i)<' + $privateName + '\b')) {
        throw 'BLOCKED: phát hiện thành phần khóa bí mật. Không được đưa private key vào plugin/GitHub.'
    }
}
$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider
try {
    $rsa.PersistKeyInCsp = $false
    $rsa.FromXmlString($raw)
    if ($rsa.KeySize -lt 3072 -or $rsa.PublicOnly -ne $true) {
        throw 'BLOCKED: khóa RSA phải từ 3072 bit và chỉ có phần public.'
    }
    $canonical = $rsa.ToXmlString($false)
}
finally { $rsa.Dispose() }
$bytes = [System.Text.Encoding]::UTF8.GetBytes($canonical)
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    $actual = ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant()
}
finally { $sha.Dispose() }
if ($actual -ne $ExpectedPublicKeySha256.ToLowerInvariant()) {
    throw ('BLOCKED: RSA public key không khớp SHA-256 quản trị viên chốt. Actual=' + $actual)
}
$source = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $SourceFile).Path)
$placeholder = 'internal const string RsaPublicKeyXml = "";'
$escaped = $canonical.Replace('"','""')
$replacement = 'internal const string RsaPublicKeyXml = @"' + $escaped + '";'

# A user may rerun provisioning after a successful earlier run. Permit only
# the identical, approved PUBLIC verifier. Never silently change a verifier
# already embedded for an issuer that may have issued permanent licenses.
$marker = 'internal const string RsaPublicKeyXml = '
$first = $source.IndexOf($marker, [StringComparison]::Ordinal)
if ($first -lt 0 -or
    $source.IndexOf($marker, $first + $marker.Length,
        [StringComparison]::Ordinal) -ge 0) {
    throw 'BLOCKED: RsaPublicKeyXml missing or declared more than once.'
}
if ($source.Contains($placeholder)) {
    $updated = $source.Replace($placeholder, $replacement)
    [System.IO.File]::WriteAllText((Resolve-Path -LiteralPath $SourceFile).Path,
        $updated, (New-Object System.Text.UTF8Encoding($false)))
    $provisionMode = 'INSERTED'
}
elseif ($source.Contains($replacement)) {
    # Idempotent success: the compiled verifier will match the pinned hash.
    $provisionMode = 'ALREADY_MATCHED'
}
else {
    throw 'BLOCKED: source contains a different RSA public key or invalid verifier. Do not overwrite.'
}
$parent = Split-Path -Parent $ReceiptFile
if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Force $parent | Out-Null }
@(
    'IMSAT VOLUME V2.0 — PUBLIC VERIFIER PROVISIONING'
    ('TimestampUTC=' + [DateTime]::UtcNow.ToString('o'))
    ('PublicKeySha256=' + $actual)
    'KeyType=RSA'
    'KeyBits=3072-or-greater'
    'PrivateKeyIncluded=NO'
    'SourceProvisioned=YES'
    ('ProvisionMode=' + $provisionMode)
    'RuntimeLicenseActivation=NOT_YET_TESTED'
    'ReleaseStatus=BLOCKED_UNTIL_HOST_AND_T01_T22_PASS'
) | Set-Content -Encoding UTF8 $ReceiptFile
Write-Host ('[PASS] Public RSA verifier pinned: ' + $actual + ' (' + $provisionMode + ')')
Write-Host '[SECURITY] No issuer private key loaded or printed.'
