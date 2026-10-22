# PLANCODE — IMSAT VOLUME V2.0 Setup Wizard + AutoCAD 2020–2026
Date: 2026-10-15
Status: IMPLEMENTATION REQUIRED; NOT A RELEASE CERTIFICATE
Branch: miningvolume-v2-chatgpt-p0-continuation
Scope supersedes prior 2012–2026 installer target for current V2.0 delivery. Do not remove earlier compatibility source/branches.

## 0. Product contract
Deliver one Windows x64 graphical installer `IMSAT_VOLUME_V2.0_Setup.exe`, Vietnamese, branded with the EXISTING OFFICIAL IMSAT icon/logo asset (do not improvise a replacement). Installation experience should match the professional IMSAT HRM wizard: Welcome → Detect AutoCAD → Choose host(s) → Install/progress → Verify/Finish. Back/Next/Cancel; correct font/contrast/layout at 1366×768, FHD and Windows DPI 100/125/150%. No technical dialogs, command-line, manual NETLOAD, DLL copying or edits to AutoCAD support paths for ordinary users. Preserve IMSAT VOLUME's approved Ribbon/menu and all calculations.

## 1. Source audit and compatibility
Existing installer/main.go, install paths, registry uninstall entries, payload.zip, PackageContents.xml, bundle name and validatePackageXML are HARD-CODED to 2023/R24.2. Existing compat/target_matrix.json records compile candidates, NOT tested runtime support. DO NOT rename a 2023 DLL to claim 2020–2026 support.
Host series: 2020 R23.1; 2021 R24.0; 2022 R24.1; 2023 R24.2; 2024 R24.3; 2025 R25.0; 2026 R25.1. Per existing target_matrix.json, .NET target differs, with 2025/26 update-dependent runtime variants. Verify the exact runtime/SDK/flavor and use an explicit detector; do not assume update from year alone. No shared .NET Framework plugin for .NET 8/10 hosts. Fail closed if host update/runtime flavor is unknown; show reason and avoid modifying it.

## 2. Build architecture / launcher
Build and test seven year-targeted plugins (plus update-flavor-specific builds as necessary). Build from existing solution and exact Autodesk SDK/NuGet versions, no fake compilation or renaming; confirm DLL references via PE/metadata tests. Package individual version-scoped Autodesk ApplicationPlugins bundles with correct PackageContents.xml, ModuleName, SeriesMin/Max, dependency set and binary integrity manifests, or use a proved safe equivalent. Never autoload two incompatible plugins into one host. Avoid shipping Autodesk API DLLs. Keep executable installer independent of SDK or Visual Studio on user machine.

## 3. Detection and selection
Enumerate 32/64-bit uninstall and Autodesk AutoCAD registry keys with resilient fallback to known install locations, verify `acad.exe` exists, resolve product year/series/build/update and bitness; handle multiple AutoCAD products/profiles, invalid stale entries and installations outside C:. Only display hosts with a positively verified mapping to packaged flavor. List each detected installation once with checkboxes and compatibility indication. Default select all *compatible* detected installations, allow partial selection. Unknown versions must be listed as unavailable with rationale, not silently mapped to 2023.

## 4. Setup transactional execution
Preflight OS, elevation, running acad.exe, space, package hashes/signature/build identity, exact loader XML and compatibility. Show real stage-based progress; cancel before commit safely, never kill acad.exe. Stage payload under random protected temporary directory, validate every ZIP path and every file before extraction. Back up ONLY own previously installed bundles/registry; install selected hosts atomically or report per-host partial outcomes with rollback. Preserve licenses, settings, user projects, DWG and Excel files on update/repair/uninstall. Ensure idempotent clean install, upgrade, repair and uninstall. Record per-host selection, exact installed DLL SHA256, original/existing version and rollback outcome in logs without secrets. Register one Windows Apps entry with Modify/Repair/Uninstall paths and official IMSAT icon. Avoid duplicate bundles and orphan autoload registrations.

## 5. Branding/UI
Wizard layout: 1 welcome (IMSAT official logo and version), 2 host detection table (year, series, install directory, supported or reason), 3 install options and license/settings preservation summary, 4 measured progress with operation names, 5 final installation report (per host PASS/FAIL and clear next steps). Correct Vietnamese sentence case; do not put every button title in ALL CAPS. Make close/Cancel robust against live writes. Progress completion cannot imply AutoCAD host has passed without a real runtime probe. Keep developer identity Bùi Thế Nam, phone 0967280686 in About/help where required, not blocking wizard screens.

## 6. Runtime gates: per host, 2020–2026
G0 source/compile+unit tests and SCA; G1 installer integrity/rollback tests; G2 actual clean Win x64 install and uninstall; G3 start acad.exe then Ribbon/menu visible without NETLOAD; G4 open/save/reopen DWG, source import, independently create HT/TK TIN with duplicate XY/different Z preserved, CAD preview, quick sections, edit/Update section; G5 cut/fill via sections and grid in applicable 2.5D cases, CAD TABLE and Excel formulas, cross-check totals; G6 upgrade/repair and retained settings/licenses/data; G7 Windows DPI and cancellation/multiple DWG and stress performance. Record each version/build/update separately with evidence (host screenshot+acad log+Setup log+test DWG+Excel and hashes). CI green is NOT host PASS. Unavailable hosts remain NOT TESTED and block any assertion of 2020–2026 support.

## 7. Required work order
P0 audit and protect existing working 2023 release path and existing QA tests.
P1 extract installer engine from UI, implement AutoCAD inventory + exact flavor matching with unit tests; add Vietnamese wizard without altering calculation UI.
P2 versioned payload builder and per-version loader manifest/validation; build 2020–2024 netFramework flavors and 2025/26 version/update-specific flavors.
P3 transactional multi-select install/repair/upgrade/uninstall + migration from existing 2023-only bundle. Re-run zip-traversal, xml and rollback negative tests.
P4 end-to-end fixture runtime tests per host and compatibility report, rebuild the Setup with the official icon.
P5 perform 5 rounds audit: geometry/calculation; installer/security; UI/DPI; version/runtime behavior; release artifact/QA completeness.

## 8. Acceptance and blocker policy
Do NOT ship a generic installer that contains only MiningVolume2023.dll and broadens SeriesMin/Max to R23.1–R25.1. Keep previously validated 2023 installation usable. Product branding and year matrix alone do not complete implementation. Only mark each specific host supported after tests on that real version; where absent label NOT TESTED. Publish EXE only after checksum, signature policy, actual host compatibility and full release gates are met. Preserve all previous business/UI requirements, including 2 independent TIN create actions, section editing, Excel formulas, CAD TABLE output, and user data.
