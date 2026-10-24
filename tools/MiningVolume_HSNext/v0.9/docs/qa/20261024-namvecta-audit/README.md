# NAMVECTA PR #52 — source audit and regression checkpoint, 2026-10-24

**NOT A RELEASE. HOST_RUNTIME_BLOCKED. No NAMVECTA Setup has been produced or accepted by this checkpoint.**

Source checkpoint: `e855f9b20577fbd5870f7bb19ee5b3f07ab99ae4`, branch
`namvecta-volume-v2-streamline-20261024`, inherited from `3bd03351f83a417567c48e6cc99f9de6906c4ba0`.
No merge into the continuation or V1 branches. This audit implements the new
`PLANCODE_NAMVECTA_VOLUME_V2_WORK_AUDIT_20261024.md`; older RSA/five-group
requirements are superseded by that document, not silently treated as passing.

## Defects corrected

1. Protected activation replicas were decoded but the file replica was ignored whenever Registry existed. Interrupted writes could lose the first-use history of a Pass. Both replicas now reconcile earliest activation per tier and latest clock watermark; the newer record selects the active tier. Invalid, duplicate, missing, future-dated and ambiguous histories fail closed. No machine fingerprint or network activation is added; the five approved hash constants and calendar-month expiry rules are unchanged.
2. Trial replicas now reconcile earliest first-use and latest observed time. Legacy IMSAT migration reads both the historical file and Registry, without checking the old MachineId. Activation also checks the trial clock watermark; it can no longer bypass a detected trial clock rollback by initializing a new activation ledger.
3. `ShowGroup` now guards its combo-box updates before changing selection, preventing recursive navigation events and redundant page rebuilds.
4. Default section sheets use `01 Tổng hợp` and `02 Chi tiết`, with all summary references updated. `Chi tiết tầng` remains required when band formulas depend on it. No formula is replaced with a static value to force two sheets.
5. Without the optional level-summary sheet, the cut/fill control totals previously stayed static after editing band cells. They now SUM the live band detail cells. Fixed worksheet row coordinates and the three volume formulas remain unchanged.
6. Empty organizational names could return before the NAMVECTA no-IMSAT-logo policy ran. All production report builders using the shared header now disable the legacy logo even in that case.
7. Updated obsolete reference assertions for the explicitly approved three-step UI, Pass licensing, changed labels/defaults and removed IMSAT logo. Existing geometry, cancellation, revision, identity and output safety tests remain active. Technical-sheet tests explicitly opt in instead of expecting them by default.

## Reproducible validation

Linux, .NET SDK 8.0.408, AutoCAD NuGet reference assemblies. No AutoCAD process is installed here.
Commands are run from `tools/MiningVolume_HSNext/v0.9`:

- `python -m pytest tests/reference compat/test_compatibility.py -q`
- `python tests/core/run_core.py --dotnet-root /tmp/imsat-dotnet`
- `python tests/license/run_license.py --dotnet-root /tmp/imsat-dotnet`
- `dotnet build src/MiningVolume.Plugin2023/MiningVolume.Plugin2023.csproj -t:Rebuild -c Release -p:UseAutoCADNuGet=true -p:IMSATCadFlavor=<flavor> -m:1 --disable-build-servers`

| Check | Before edits | After edits | Meaning |
|---|---|---|---|
| AutoCAD 2023 source compile | PASS, 0 warnings/errors | PASS, 0 warnings/errors | COMPILE_PASS only |
| Reference + compatibility | 19 failed, 368 passed, 3 skipped | 387 passed, 3 skipped; 4 subtests passed | AUTOMATED_TEST_PASS for executed tests |
| Actual core/Surface/report C# | 6 failures / 138 tests | 141 / 141 passed | No CAD mocks used for geometry; not host runtime |
| New actual ledger reconciliation C# | Not present | 12 / 12 passed | DPAPI stubs throw; Windows storage is deliberately NOT claimed tested |

The C# baseline was observed before report corrections; its failures were calculation-ID expectation, two organization metadata sheet expectations, optional spacing-information sheet expectation, and two old-logo expectations. Baseline reference log is retained; final core output is in `core-after.txt`.

| AutoCAD full 64-bit | Flavor | Rebuild | Runtime / MVSELFTEST / install-upgrade-uninstall |
|---|---|---|---|
| 2020 | ACAD2020_NET47 | COMPILE_PASS | HOST_RUNTIME_BLOCKED |
| 2021 | ACAD2021_NET48 | COMPILE_PASS | HOST_RUNTIME_BLOCKED |
| 2022 | ACAD2022_NET48 | COMPILE_PASS | HOST_RUNTIME_BLOCKED |
| 2023 | ACAD2023_NET48 | COMPILE_PASS | HOST_RUNTIME_BLOCKED |
| 2024 | ACAD2024_NET48 | COMPILE_PASS | HOST_RUNTIME_BLOCKED |

Final rebuilds ran sequentially with `-t:Rebuild`, one per flavor; separate logs are included. Early SDK startup attempts failed in Linux `Process.GetStat()` before compilation; launching SDK under Python subprocess resolved this environment issue. Older experimental 2025/2026 build definitions are not a NAMVECTA support claim.

## PA3 source evidence

Read the supplied Word identity document, including its production-asset approval requirement. Exact embedded raster bytes are preserved under `docs/branding/namvecta-pa3-reference/`; `manifest.json` contains source/document/asset SHA-256. The complete reference JPEG matches the document's published hash `1b84e4d63a7a615d7255ff0eb55677d83d0cf9e3593c2cec27e70527bc934eb4`.
These are **reference rasters**, not approved SVG/ICO production assets. No inferred/redesigned logo, web logo, or old Setup is substituted.

## Outstanding work — do not hide these behind passing tests

- BrandConfig, remaining Ribbon/Palette/dialog/Windows/installer branding, approved PA3 production vector/icon integration: NOT COMPLETE.
- Dedicated NAMVECTA wizard, upgrade ownership/migration, host range limited to 2020–2024, packaging without RSA license-manager: NOT COMPLETE. Existing IMSAT installer and pipeline are historical, not NAMVECTA deliverables.
- Full Windows five-Pass activation (including actual secrets/case changes), DPAPI/Registry persistence, simultaneous AutoCAD processes, restart/upgrade/rollback: NOT TESTED. Current tests prove reconciliation, not tamper resistance or all activation flows.
- Independent Excel recalculation after edits: NOT TESTED in Excel/LibreOffice. C# verifies emitted formulas/OOXML, metadata, CalculationId and cancellation; that is not an independent spreadsheet engine.
- Compact UI at actual dock widths/DPI, DWG save/reopen, TIN/breakline/multi-Z behavior inside CAD, edited sections, table blocks, cancellation/Undo, all T01–T22 and real-data end-to-end acceptance: HOST_RUNTIME_BLOCKED.
- New before/after same-data host performance benchmarks for TIN, 85 sections, CAD output and grid: NOT MEASURED. Do not reuse old timings as proof for this PR.
- G1 compile/tests has evidence above; G2–G6 are not fully accepted. FUNCTIONAL_RELEASE_PASS is NOT achieved. Do not rename an existing IMSAT EXE to NAMVECTA or publish an official Setup from this checkpoint.
