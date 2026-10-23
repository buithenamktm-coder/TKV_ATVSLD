# Integrated TIN → sections → quantities → CAD/Excel review — 2026-10-22

Baseline/checkpoint: `1d2351d4edd48f87515fd6fd87e66dcd6c8c9132`, local branch `checkpoint/imsat-integrated-20261022`. Baseline Windows CI 39684710587 succeeded. This batch is one integrated development checkpoint, NOT acceptance of all P0–P6 and NOT a released installer.

## Findings addressed together

1. Normal sections, quick sections and interval midpoint volume calculations constructed separate spatial indices for identical accepted TINs. A weak-key, per-surface cache now shares only completed indices. A rebuilt TIN gets a new key. Construction/waiting can be canceled; failed/canceled construction is retryable. A per-sampler two-entry fast path avoids locking on every midpoint. Index memory remains available while the surface is retained; this reduces repeated allocations, not necessarily peak resident memory. No geometric arithmetic or triangulation policy changed.
2. Readiness checks counted faces and unexpected entities separately for each TIN: four model-space traversals, plus layer existence transactions. The combined check now inspects both layers in one traversal with periodic Esc polling. No stale CAD-count cache is retained. Full triangle/vertical-face counts and unexpected-entity rejection remain in place; repairs use the existing renderer. Host speed and nested lock/progress behavior are unverified.
3. Quick-section geometry and edited-profile volume recomputation lacked cancellation paths. Their buttons now request cancellation. Quick CAD output polls through profiles/ticks/polyline vertices and before its single transaction commit. Edited results are not published after cancellation. Existing stale-document/source/geometry guards are preserved.
4. Excel cancellation now reaches row serialization and is checked before replacing the destination. Existing nested staging/atomic replacement protects an accepted workbook and removes temporary files on cancellation. Section/grid pages expose cancel on the export button; grid row construction also polls. Cancellation during section workbook construction waits for that builder to return; branding/ZIP validation/native commit are not instantly interruptible. No deadline is promised.

## Five review rounds — actual status

| Round | Evidence | Result / limits |
|---|---|---|
| 1: source/build/exception paths | Final net48 Autodesk-reference build; diff whitespace check; staging/cache/transaction review | COMPILE_PASS, 0 warnings/errors. Initial .NET CLI process-info failures retained; unchanged-source retry succeeded. |
| 2: numerical/regression | core.log, 131 executable cases including independent analytic sections, three volume formulas, edited profiles, TIN and grids | AUTOMATED_TEST_PASS in Linux core/report scope. New cases: concurrent index reuse + new-TIN invalidation; quick-route cancellation; mid-XLSX cancellation preserving old file and successful retry. Existing canceled-index recovery now uses a new sampler. |
| 3: performance | Same benchmark source, .NET 8.0.408, tiered compilation off, processor count 4, same synthetic fixtures, five measured runs after warmup | Core results below. Retain all preliminary and reverse-order runs, including slower/noisy samples. CAD and actual mine host timing BLOCKED. |
| 4: output/UI flow | Real XLSX/formula/metadata cases; 333 reference/static checks; compiled host regression for quick cancellation rollback and batched layer inspection | Automated report checks pass. Host regression NOT EXECUTED. Desktop Excel recalculation, UI/DPI, actual save/reopen and CAD rollback HOST_RUNTIME_BLOCKED. |
| 5: regression/compatibility/installation | final-python.log: 342 passed, 1 skipped, 4 subtests; final build | 9 compatibility contracts pass in addition to 333 reference checks. PowerShell packaging executable case skipped (pwsh unavailable). Windows installer/activation/startup/runtime NOT TESTED. No official Setup. |

## Final core benchmark

Warm accepted TIN reuse, 20,000 triangles per surface. Samples and median allocated bytes are retained in JSONL. The baseline is source-ref above, not a historical timing from another environment. The final paired volume run follows the per-sampler fast-path correction. Earlier `after` files describe the provisional cache implementation; they are retained for transparency, not used as final acceptance. Final first-pair baseline timings also showed large variability; use repeat pair as a scoped observation, not a general responsiveness guarantee.

| Stage | Before median ms | Final median ms | Before allocated bytes | Final allocated bytes |
|---|---:|---:|---:|---:|
| volume_15_profiles_200_bands_including_mid_sections | 20.298 | 15.201 | 8148952 | 4973832 |
| volume_85_profiles_200_bands_including_mid_sections | 100.969 | 84.677 | 32021272 | 28846152 |
| volume_300_profiles_200_bands_including_mid_sections | 360.916 | 311.358 | 105522056 | 102346936 |

The oracle checks cut 19,960 m³, fill 0 and complete coverage each run. No changes to formula selection/rounding were made. Sampling-only final medians: 15 profiles 6.083 ms; 85 profiles 29.901 ms; 300 profiles 96.755 ms. These are not TIN construction or CAD drawing measurements. The cache avoids approximately 3.18 MB of repeated allocation per call in this fixture; cached memory is retained with live TINs.

## Mandatory host benchmark matrix

| Required stage | Before | After | Status |
|---|---|---|---|
| Existing TIN construction | Not measured in host | Not measured in host | HOST_RUNTIME_BLOCKED |
| Design TIN construction | Not measured in host | Not measured in host | HOST_RUNTIME_BLOCKED |
| Create 85 sections | Core sampling measured separately | Core sampling measured separately | Host timing blocked |
| Draw 85 CAD sections | Not measured | Not measured | HOST_RUNTIME_BLOCKED |
| Volume calculation | Core medians above | Core medians above | Host timing blocked |
| CAD quantity table | Not measured | Not measured | HOST_RUNTIME_BLOCKED |
| Excel export | Real file/cancellation correctness tested | No full before/after export timing | NOT MEASURED |
| Grid calculation | Core regression includes 10k/50k/100k cell fixtures | No paired before/after optimization claim | Host timing blocked |

## Requirements and remaining work

| Scope | Source/evidence reviewed | Remaining gate |
|---|---|---|
| P0 pipeline/cache/cancel | TinSectionSampler, SectionVolumeCalculator, SurfaceWorkflowService, UI Section/Volume/Export/Grid, SpreadsheetReport; above logs | Real terrain peak RAM, end-to-end elapsed time and host responsiveness; section workbook builder cancellation granularity |
| P1 sections/editing | QuickSectionProfileBuilder, SectionCadRenderer, edited-profile core regression and existing stale guards | CAD edit/update/save/reopen; several hundred sections in AutoCAD |
| P2 quantities | Core three-method, band, edited-section, analytic and real-fixture cases | Independent mine-data acceptance, CAD/Excel displayed totals after manual edits |
| P3 TIN | Source revision/region guards, cache lifetime, full CAD face counting; existing core duplicate/vertical/degenerate/clip cases | Very large real TIN, 3D preview, delete/restore on host; no claim all input conflicts resolved |
| P4 grid | Existing exact-intersection/no-data/rotation tests, async builder and output ID; cancel/staging changes | Label placement, large CAD grid, host cell edits and outputs |
| P5 output | Atomic XLSX cancellation; CAD rollback tests compiled | Required Block-based output is not accepted by this batch: current quantity output remains native TABLE. Unified grid CAD+Excel orchestration and interactive formatting still need review. |
| P6 UI/license/setup | Existing UI/static/compat contracts; LicenseTrustAnchor.RsaPublicKeyXml remains empty | DPI/workflow acceptance, production offline public trust anchor provisioning, installation/upgrade/uninstall on Windows |

AutoCAD version matrix: 2023 COMPILE_PASS against Autodesk references. 2020–2022/2024 and other versions 2012–2026 were not rebuilt in this batch; compatibility declarations/contracts are not runtime passes. All host versions HOST_RUNTIME_BLOCKED here. FUNCTIONAL_RELEASE_PASS is not established.

No request for another intermediate user installation is made. Continue from this integrated checkpoint; do not restart the application or treat commit count as completion percentage.
