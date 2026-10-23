# Edited section core cancellation and merge — 2026-10-23

Baseline: 029dfc17ce1d147c71bbc87f310abda35cacdba3. Its GitHub Actions run 39920320606 was verified completed/success before this change.

## Change

Propagate host cancellation through EditedSectionProfileBuilder.Rebuild, checking original ranges, validation, station merge, segment construction, and final result publication. Existing HT/TK paths are validated as strictly increasing; merge their stations in linear time instead of sorting a concatenated temporary list. Preserve tolerance deduplication, endpoint snapping, interpolation, areas and the original immutable accepted profile. The caller passes its existing host-thread poll callback. No AutoCAD database access is moved to a worker thread.

## Verification

- COMPILE_PASS: AutoCAD 2023/net48 Release against Autodesk references, 0 warnings/errors.
- AUTOMATED_TEST_PASS: 137 executed Core/Surface/report C# cases; Python 382 passed, 3 skipped, 4 subtests passed.
- New executable case: 4,097 HT + 6,001 TK vertices, analytic cut/fill 12.5 m² each, all segment elevations checked, cancellation at EVERY observed callback including publication, accepted original unchanged.
- Existing Python source-contract assertion updated for the new cancellation argument; initial stale assertion failure was corrected, then full suite rerun.
- HOST_RUNTIME_BLOCKED: no Windows/AutoCAD here. Native Esc, MVSELFTEST, UI responsiveness, save/reopen, upgrade/uninstall remain unexecuted.
- FUNCTIONAL_RELEASE_PASS: NOT established. No final Setup delivered.

## Paired core benchmark

Same process, SDK 8.0.408/runtime 8.0.15, DOTNET_TieredCompilation=0, DOTNET_PROCESSOR_COUNT=4, synthetic linear crossing profiles, 85 rebuilds per sample, ten warmup pairs and five measured pairs alternating order. Baseline source is extracted from the exact commit above and compiled alongside current production code. Exact area equality and geometry within 1e-12 are checked before timings. Each row reports medians; allocation is cumulative managed bytes per 85 rebuilds, NOT peak RAM. Native CAD drawing is NOT included. Current branch is tested without a host polling callback to isolate the core algorithm.

| HT vertices (TK = HT + 12) | Before median ms | After median ms | Before allocated bytes | After allocated bytes |
|---|---:|---:|---:|---:|
| 17 | 2.2277 | 2.3748 | 680720 | 588920 |
| 1025 | 152.6491 | 144.3583 | 28796000 | 28031000 |
| 10001 | 1727.1257 | 1514.0603 | 269163952 | 252404728 |

All raw samples and the reproduction script are adjacent. Run `python docs/qa/20261023-edited-core-cancel/benchmark.py` from v0.9 with SDK installed at /tmp/imsat-dotnet and baseline git object available. These figures do not establish end-to-end AutoCAD performance or real mine acceptance.

## Remaining acceptance work

1. Actual AutoCAD 2023 MVSELFTEST and edited section/update/export/save/reopen sequence, including cancel/rollback and locked layers.
2. Same real terrain before/after measurements for HT/TK TIN, 85 CAD sections, quantities, CAD table, Excel and grid; peak memory and physical UI responsiveness.
3. Windows install-over-existing V2 build identity, offline activation, uninstall, full UI and CAD/Excel numerical acceptance. Broader version support remains build evidence unless runtime logs exist.

These are material remaining gates; neither commit count nor a green CI run is a completion percentage.
