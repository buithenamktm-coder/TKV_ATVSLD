# Per-call clipping buffers — 2026-10-21

Baseline: `1d824a9997557a6ae17c1bf57f1d2fd6e30adfee` (Windows CI 39581709089 succeeded). Local checkpoint branch: `checkpoint/volume-before-buffer-reuse-20261021`.

## Change and correctness

SectionBandAreaCalculator formerly allocated a polygon per piece, two output lists and four delegates per intersecting band. Three scratch lists now belong to each Calculate invocation and are cleared/reused. No shared mutable buffers, fixed-capacity truncation or cache lifetime change. List capacity can grow normally. Vertex ordering, boundary predicates, tolerance, intersection arithmetic, area summation order, cut/fill splitting and cancellation checks are retained.

Added an analytic regression: forty crossing sloped segments, shuffled occupied/empty bands and 64 concurrent calls on one calculator. Each triangle has unit-height strip areas 3.75 and 1.25; the repeated profile must give exactly 200 cut and 200 fill within 1e-7. Follow-up zero-area calls detect residual scratch contents. Existing cancellation/retry and 1,000 analytic band cases also pass.

## Current local validation

- COMPILE_PASS: AutoCAD 2023 net48 add-in against Autodesk reference packages, 0 warnings/errors.
- AUTOMATED_TEST_PASS: 124 C# core/surface/report cases, 333 Python reference/static cases.
- HOST_RUNTIME_BLOCKED: no interactive AutoCAD available. No CAD export/GUI timing claim.
- FUNCTIONAL_RELEASE_PASS not established; full P0–P6 remains open. No official installer.

## Reproducible benchmark

Same Linux host, .NET SDK 8.0.408/runtime 8.0.15, four processors, tiered compilation off, one warm-up then five timed samples per fixture. Baseline source compiled using `--source-ref 1d824a9`; candidate from working copy. Use `tests/performance/run_section_benchmark.py --dotnet-root /tmp/imsat-dotnet` with/without `--source-ref`, then with `--volume-only`. These are synthetic core fixtures, not real DWG timings or peak working-set measurements.

| Stage | Before median ms | After median ms | Before allocated bytes | After allocated bytes |
|---|---:|---:|---:|---:|
| Band area, 15 profiles | 18.987 | 17.007 | 10,395,520 | 202,840 |
| Band area, 85 profiles | 113.214 | 91.344 | 58,908,096 | 1,149,576 |
| Band area, 300 profiles | 384.946 | 321.942 | 207,909,976 | 4,056,376 |
| Full volume, 15 profiles (10 samples) | 20.996 | 19.138 | 12,010,704 | 8,148,856 |
| Full volume, 85 profiles (10 samples) | 109.043 | 103.586 | 54,854,704 | 32,021,176 |
| Full volume, 300 profiles (10 samples) | 384.548 | 401.658 | 186,626,776 | 105,522,984 |

Band area uses 1,000 segments/profile and 200 bands. Full volume includes middle-section sampling and independently checks 19,960 m3 cut / 0 fill. After a noisy first volume run, a second set reversed execution order (candidate then baseline). Both sets are retained, not selected for favorable timings. At 300 profiles the first-set medians were 381.010 -> 392.954 ms and second-set medians 422.663 -> 402.504 ms. Pooled median is 4.45% slower; time conclusion is INCONCLUSIVE, not a speed PASS. The allocation reduction is repeatable. Unchanged sampling-only timings also varied; no sampler speedup is claimed.

The focused band-area allocation/time improvement passes these fixtures. Whole-pipeline performance acceptance remains pending controlled Windows/AutoCAD runs, including real terrain, rendering, responsiveness, peak memory, exports and cancellation.
