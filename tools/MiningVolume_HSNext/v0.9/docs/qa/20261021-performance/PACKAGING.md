# Compatibility bundle repair — 2026-10-21

Baseline Actions run: https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38690827855

The eight compatibility jobs compiled but failed packaging. The filename pattern used two backslashes in a PowerShell single-quoted string. This matches a literal backslash, rejecting normal DLL filenames. Use one regex escape before the dot and retain the end anchor.

Executable PowerShell 7.4.6 packaging regression:
- Old production script: FAIL on valid MiningVolume2023.dll (expected reproduction).
- Fixed script: all 10 compatibility tests PASS, none skipped.
- Five synthetic payload files copied with matching SHA-256; PackageContents XML loader checked.
- ../escape.dll, evil.dll.exe, folder/evil.dll rejected; partial output removed each time.
- Synthetic bytes are packaging fixtures, never advertised as loadable CAD binaries.

Run: `python -m unittest discover -s compat -p 'test_*.py' -v` with pwsh on PATH.

This fixes packaging only. It does not establish AutoCAD host compatibility, Windows install/uninstall, activation or FUNCTIONAL_RELEASE_PASS. New Windows CI must run against the committed source.
