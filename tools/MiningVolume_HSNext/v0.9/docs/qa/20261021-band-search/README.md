# Monotone elevation-band search — 2026-10-21

Baseline `0bf80b8e0dfbd66d358f629bc593f0992e3a2e69`; its Windows CI 39584593621 succeeded. Local checkpoint: `checkpoint/volume-before-band-search-20261021`.

## Change

Each section piece formerly scanned every band even when only one intersected its elevation extent. Validate monotonicity of both padded band bounds once per Calculate call, then use binary searches to find the possible overlap window. Descending bands use a virtual ascending view; result order and accumulation order remain unchanged. Small lists, unordered/nested bounds and NaN inputs retain the full scan. Gaps, duplicates and monotone overlapping bands remain valid. The existing exact disjoint predicate and polygon clipping still execute for every candidate. No new allocations beyond the prior per-call buffers. Cancellation remains cooperative.

## Verification

- 125 C# tests passed, 0 failed. New deterministic regression covers 400 profiles with sloped cut/fill crossings, near-tolerance levels, ascending/descending/shuffled bands, gaps, overlaps and nested bounds. All 9,600 band outputs match individually clipped bands by exact double equality; existing analytic strip and volume checks also pass.
- 333 Python reference/static checks passed.
- AutoCAD 2023 net48 add-in cross-compile: 0 warnings/errors.
- HOST_RUNTIME_BLOCKED. FUNCTIONAL_RELEASE_PASS not established. No official Setup. Full P0–P6 acceptance still open.

## Same-fixture before/after

Linux .NET SDK 8.0.408/runtime 8.0.15, four processors, tiered compilation off, one warm-up and five timed samples. Baseline and candidate compiled with the same existing runner and fixture. Raw allocations and times retained in adjacent files. Run `tests/performance/run_section_benchmark.py --dotnet-root /tmp/imsat-dotnet` with/without `--source-ref 0bf80b8`, and repeat with `--volume-only`.

| Core stage | Before median ms | After median ms | Fixture conclusion |
|---|---:|---:|---|
| Band area, 15 profiles | 16.690 | 1.905 | PASS |
| Band area, 85 profiles | 98.250 | 10.888 | PASS |
| Band area, 300 profiles | 350.932 | 39.805 | PASS |
| Full volume, 15 profiles | 21.228 | 12.731 | PASS |
| Full volume, 85 profiles | 116.287 | 76.726 | PASS |
| Full volume, 300 profiles | 388.639 | 273.085 | PASS |

Area fixture: 1,000 segments/profile, 200 bands. Volume fixture includes middle-section sampling and verifies 19,960 m3 cut / 0 fill. Allocation medians remain essentially unchanged: 1,149,576 bytes for 85-profile area; 32,021,176 bytes for 85-profile full volume. These are allocation counts, not peak memory.

Sampling-only code is unchanged; its 300-profile median fluctuated 87.876 -> 101.522 ms. Do not attribute that fluctuation to band search or claim sampler performance acceptance. This run demonstrates improvement only for these synthetic band-area/volume fixtures; CAD export, real large terrain, UI responsiveness, Excel and Windows peak-memory measurements remain pending.
