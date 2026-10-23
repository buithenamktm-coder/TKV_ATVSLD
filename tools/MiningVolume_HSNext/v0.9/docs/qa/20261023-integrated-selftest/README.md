# Integrated continuation / MVSELFTEST diagnostics — 2026-10-23

Repository: buithenamktm-coder/TKV_ATVSLD. Branch: miningvolume-v2-chatgpt-p0-continuation.

Rebased the unfinished axis fix onto 4a6d46c956c386ea72e326df52ebce15d4296da6, retaining all 21 intervening commits (TIN indices, virtualized source grid, CAD scanning, atomic clear/pair replacement, Excel cancellation, runtime timing, offline RSA provisioning and installed build identification). Baseline CI run 39883652863 succeeded; not evidence for final host acceptance.

## Actual changes

- Official and quick-section elevation axes use a finite, index-based tick list. Reject steps that cannot advance at the supplied coordinate precision; preserve annotation budget and volume calculations.
- Official section renderer polls cancellation immediately before commit. A real-host fixture checks rollback when called directly without the UI outer transaction.
- MVSELFTEST CAD regression runs seven independent side-database groups and aggregates named failures with original exception stacks. A TIN pair failure no longer prevents table, grid and section groups from being attempted. Passed groups are individually recorded; any failure keeps overall FAIL. No failed check is weakened or suppressed.

## Five review rounds

| Round | Evidence / remaining limit |
|---|---|
| 1 Build/source | AutoCAD 2023 net48 compile: 0 warnings/errors, build.log. Other host versions not rebuilt in this batch. SDK startup initially failed before compile; unchanged source retried successfully. |
| 2 Algorithms | 136 executable C# cases pass, core.log. Includes TIN, sections, volumes, grid, Excel and new axis precision regression. |
| 3 Performance | New axis failure mechanism reproduced by numeric regression; no new native CAD timing claim. Prior shared-index benchmark remains historical. Host timing instrumentation inherited, no matching before/after TSV available here. |
| 4 Exports/UI | 380 Python checks + 4 subtests pass. Three Windows/PowerShell checks skipped, details in python.log. Native CAD rollback, UI and Excel Desktop remain unexecuted locally. |
| 5 Host/installer | HOST_RUNTIME_BLOCKED. AutoCAD 2023 runtime, Windows install/upgrade/uninstall and full user workflow cannot be executed on this Linux host. FUNCTIONAL_RELEASE_PASS not achieved. |

## Previously reported MVSELFTEST issues and installed build

Prior conversation retrieval identified reports of TIN pair rollback failure and eOnLockedLayer in VerifyTinPairRollbackAfterSecondSurfaceFailure. This is contextual evidence, not a raw host log or a passing rerun. Source inspection confirms the inherited pair preflight and scoped unlock/restore in the fixture. Neither fix is marked HOST_RUNTIME_PASS.

Inherited installer source checks installed DLL hashes after copying and restores the previous bundle on validation failure. MVBUILDINFO and the UI read build identity; Setup logs source SHA. These provisions address stale installation detection, but actual overwrite/restart/load behavior still requires Windows runtime evidence. No user build step is required or requested.

## Remaining acceptance

All TIN/section/grid/CAD/Excel/UI paths retain the prior integrated matrix. Pending items include section Block editing/save/reopen, large drawing responsiveness and measured 85-section output, CAD/Excel parity after edits, Windows installer lifecycle, offline activation and supported AutoCAD versions. Deliver one verified final Setup only after those gates pass; no intermediate installer is handed off by this batch.

Earlier evidence under 20261022-section-axis records the pre-rebase 132/342 test totals only. Current totals are the logs in this directory.
