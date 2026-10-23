# Installer rollback error handling — 2026-10-23

Base: a2045f90905c2ab18a5ef6c09c2af5bc5471cf21. Its CI run 39925868443 was verified completed/success before this change.

## Defect and change

Copy, installed-DLL verification and registration-snapshot failures discarded cleanup/restore errors. One branch claimed the previous version was restored even when restoration failed. All four installation failure branches now use checked rollback: cleanup failure prevents restoration over a partial destination; restoration failure identifies the retained backup location. Early failure branches record the failing stage, original error and rollback result in install_error.txt, and no longer unconditionally claim successful recovery. Registration failure logging now writes actual CRLF instead of literal escaped text.

restoreBackup now uses Lstat and propagates access errors instead of treating every Stat error as a missing backup. Non-directory and symlink backups are rejected. Existing destinations are never overwritten. No license files, product identity, AutoCAD detection or business formulas were changed.

## Verification

- COMPILE_PASS: Go 1.24.2 cross-compiled the full Windows/amd64 installer test binary with all installer sources and tests. The temporary embedded ZIP contains a compile-only placeholder. It is NOT a usable Setup and was NOT executed.
- AUTOMATED_TEST_PASS: two tests executed on Linux against production rollbackBundle and restoreBackup functions extracted unchanged. Covers cleanup failure/no restore call, restore failure reporting, success, invalid backup preservation, existing-target protection, successful directory restoration and missing-backup handling.
- Python reference/compatibility: 384 passed, 3 skipped, 4 subtests passed. New source contract checks all affected install failure paths.
- HOST_RUNTIME_BLOCKED: actual Windows ACL/locked DLL, registry restoration, install-over-old-version, activation and uninstall are NOT accepted by portable tests or cross-compilation.
- FUNCTIONAL_RELEASE_PASS: not established; no final Setup released.

Reproduction from v0.9:

```sh
python docs/qa/20261023-installer-rollback/verify.py /path/to/go
python -m pytest tests/reference compat/test_compatibility.py -q
```

Raw logs and verification script are adjacent. Existing CI runs the complete Go suite on Windows after publication. This report does not predeclare its result.
