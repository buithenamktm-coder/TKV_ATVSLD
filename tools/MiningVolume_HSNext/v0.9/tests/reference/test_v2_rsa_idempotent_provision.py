"""RSA provisioning may be repeated only if it already embeds the same public key.

The test creates an ephemeral key in memory on Windows CI; it is NOT an
issuer signing key and is never written to the repo or published to artifacts.
"""
import os
import shutil
import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
PROVISION = ROOT / "release" / "provision_v2_public_key.ps1"


def test_source_requires_exact_pinned_match_on_repeat():
    code = PROVISION.read_text(encoding="utf-8-sig")
    assert "SourceProvisioned=YES" in code
    assert "ProvisionMode=" in code
    assert "ALREADY_MATCHED" in code
    assert "INSERTED" in code
    assert "$source.Contains($replacement)" in code
    assert "$source.Contains($placeholder)" in code
    assert "contains a different RSA public key" in code
    assert "IndexOf($marker" in code
    assert "[System.IO.File]::WriteAllText" in code
    assert "RsaPublicKeyXml = \"\";" in code
    assert "rsa.PublicOnly -ne $true" in code
    assert "ExpectedPublicKeySha256" in code


@pytest.mark.skipif(os.name != "nt", reason="requires Windows PowerShell and RSA CryptoServiceProvider")
def test_pinned_public_key_repeat_is_safe_and_different_key_is_blocked():
    shell = shutil.which("pwsh") or shutil.which("powershell.exe")
    assert shell, "Windows PowerShell not found"
    # End-to-end public-key fixture. Private signing material stays in memory,
    # is never used to sign a token and is not written to any file.
    script = r'''
$ErrorActionPreference = 'Stop'
$temp = Join-Path $env:TEMP ('v2-public-key-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
try {
    $issuer = New-Object System.Security.Cryptography.RSACryptoServiceProvider(3072)
    try {
        $issuer.PersistKeyInCsp = $false
        $public = $issuer.ToXmlString($false)
    } finally { $issuer.Dispose() }
    $xmlFile = Join-Path $temp 'fixture-public.xml'
    $source = Join-Path $temp 'fixture-license.cs'
    $proof = Join-Path $temp 'public-proof.txt'
    [IO.File]::WriteAllText($xmlFile,$public)
    [IO.File]::WriteAllText($source,'internal const string RsaPublicKeyXml = "";')
    $bytes = [Text.Encoding]::UTF8.GetBytes($public)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {$pin = ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant()}
    finally {$sha.Dispose()}
    & './release/provision_v2_public_key.ps1' -PublicKeyFile $xmlFile -ExpectedPublicKeySha256 $pin -SourceFile $source -ReceiptFile $proof
    if ((Get-Content $proof -Raw) -notmatch 'ProvisionMode=INSERTED') {throw 'first provisioning must insert'}
    $before = [IO.File]::ReadAllBytes($source)
    & './release/provision_v2_public_key.ps1' -PublicKeyFile $xmlFile -ExpectedPublicKeySha256 $pin -SourceFile $source -ReceiptFile $proof
    if ((Get-Content $proof -Raw) -notmatch 'ProvisionMode=ALREADY_MATCHED') {throw 'second provisioning must match'}
    $after = [IO.File]::ReadAllBytes($source)
    if (-not [Linq.Enumerable]::SequenceEqual([byte[]]$before,[byte[]]$after)) {
        throw 'idempotent run changed source'
    }
    $modified = [IO.File]::ReadAllText($source).Replace('RsaPublicKeyXml = @"','RsaPublicKeyXml = @"CONFLICT')
    [IO.File]::WriteAllText($source,$modified)
    $blocked = $false
    try {
        & './release/provision_v2_public_key.ps1' -PublicKeyFile $xmlFile -ExpectedPublicKeySha256 $pin -SourceFile $source -ReceiptFile $proof
    } catch { $blocked = $true }
    if (-not $blocked) {throw 'different verifier was silently accepted'}
    'PUBLIC RSA REPEAT/CONFLICT TEST PASS'
}
finally {
    Remove-Item $temp -Force -Recurse -ErrorAction SilentlyContinue
}
'''
    result = subprocess.run(
        [shell, "-NoProfile", "-NonInteractive", "-Command", script],
        cwd=ROOT, text=True, capture_output=True, timeout=110,
    )
    assert result.returncode == 0, result.stdout + "\n" + result.stderr
    assert "PUBLIC RSA REPEAT/CONFLICT TEST PASS" in result.stdout
