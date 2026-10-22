"""Run the native PowerShell AST parser against V2 release/security scripts.

A string-based regression cannot establish that the issuer-provisioning script
parses correctly. GitHub Windows CI has PowerShell; Linux developers skip.
"""
import os
import shutil
import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]


@pytest.mark.skipif(os.name != "nt", reason="PowerShell AST parser requires Windows CI")
def test_v2_release_scripts_parse_on_windows():
    shell = shutil.which("pwsh") or shutil.which("powershell")
    assert shell, "PowerShell must exist on Windows release QA"
    script = r"""
$paths = @(
  'release/provision_v2_public_key.ps1',
  'release/prepare_v2_runtime_bundle.ps1',
  'release/verify_autocad2023_runtime.ps1',
  'installer/build_v2_qa.ps1'
)
foreach ($rel in $paths) {
  $tokens = $null
  $errors = $null
  $file = (Resolve-Path -LiteralPath $rel).Path
  [void][System.Management.Automation.Language.Parser]::ParseFile(
    $file, [ref]$tokens, [ref]$errors)
  if ($errors.Count -gt 0) {
    foreach ($err in $errors) { Write-Error "$rel : $err" }
    exit 1
  }
}
"""
    run = subprocess.run(
        [shell, "-NoProfile", "-NonInteractive", "-Command", script],
        cwd=ROOT, capture_output=True, text=True, timeout=45,
    )
    assert run.returncode == 0, f"PowerShell parse failed: {run.stdout}\n{run.stderr}"
