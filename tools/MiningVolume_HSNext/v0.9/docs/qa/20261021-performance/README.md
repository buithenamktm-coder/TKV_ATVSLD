# P0 performance checkpoint — 2026-10-21

Status: PARTIAL IMPLEMENTATION; NOT A RELEASE. HOST_RUNTIME_BLOCKED.

## Source audit
- Baseline: a697c3418cf2559fd3b7355e18ffc12e82aa96e9, requested continuation branch, PR #27 (draft).
- Remote branch enumeration found no commercial-v2-stabilization.
- Checkpoint: checkpoint/volume-before-perf-20261021.
- Main contains substantially older Volume source. Existing V2 grid, seam, QA and HS source branches are ancestors of this continuation branch; divergent historical branches were inventoried, not blindly merged.
- Reorganization branch IMSAT-VOLUME/source/v0.9 matches this source tree except packaging script and a relocated workflow assertion. The packaging script there has duplicated content and broken quoting; do not merge it wholesale.
- This is a focused audit of section sampling, band area, volume calculation, section CAD renderer/workflow and CI packaging; it is NOT an assertion that every project file and PLANCODE requirement has been audited.
- Existing baseline Actions run 38690827855: AutoCAD 2023 build job succeeded, all eight compatibility jobs compiled but failed packaging. Historical evidence only.

## Implemented optimization
SectionBandAreaCalculator previously allocated and clipped polygons against all bands even when their elevation extents were disjoint. Reject only strictly disjoint extents using the SAME tolerance as the original clipping predicates. All intersecting or near-boundary pieces retain the original clip and area arithmetic. Band ordering, three volume formulas, edited-section geometry and CAD/Excel interfaces are unchanged.

Trial deduplication of sampler grid-bin visits was measured and REVERTED: insufficient speed evidence and increased allocations. No sampler production change is included.

## Reproducible benchmark
Linux x64, .NET SDK 8.0.408/runtime 8.0.15; optimized C# build, DOTNET_TieredCompilation=0, DOTNET_PROCESSOR_COUNT=4. Same harness/data/settings for baseline git source and working source, sequential runs. One warm-up then five measurements, median below. Synthetic fixtures, not the user's DWG. Allocations are cumulative managed allocations, NOT peak RSS. Core compilation is outside timings.

- Band area: 15/85/300 profiles, 1,000 horizontal segments each, 200 elevation bands; analytic cut area 2,000 and fill 0 per profile.
- Sampling control: 20,000 triangles per surface; analytic cut area 200 per section. This code is unchanged; observed timings vary and no sampling acceleration is claimed.
- Complete core volume: 15/85/300 profiles, 200 bands, 20,000 triangles per TIN, includes middle-profile sampling and band integration. Analytic cut 19,960 m3, fill 0. Input end profiles are prepared outside timing.

| Stage | Before ms | After ms | Before allocated bytes | After allocated bytes |
|---|---:|---:|---:|---:|
| band_area_15_profiles_1000_segments_200_bands | 364.954 | 19.497 | 1,315,035,520 | 10,395,520 |
| band_area_85_profiles_1000_segments_200_bands | 2065.270 | 109.273 | 7,451,868,096 | 58,908,096 |
| band_area_300_profiles_1000_segments_200_bands | 7585.113 | 472.372 | 26,300,709,976 | 207,909,696 |
| sample_15_profiles_20000_triangles_per_tin | 7.181 | 9.162 | 7,413,312 | 7,413,376 |
| sample_85_profiles_20000_triangles_per_tin | 34.734 | 40.773 | 25,944,176 | 25,944,136 |
| sample_300_profiles_20000_triangles_per_tin | 115.553 | 157.767 | 81,861,680 | 81,861,760 |
| volume_15_profiles_200_bands_including_mid_sections | 174.164 | 22.541 | 497,741,904 | 12,010,704 |
| volume_85_profiles_200_bands_including_mid_sections | 971.350 | 141.114 | 2,926,633,904 | 54,854,704 |
| volume_300_profiles_200_bands_including_mid_sections | 3519.752 | 489.249 | 10,386,980,936 | 186,626,744 |

Run:
```
python tests/performance/run_section_benchmark.py --dotnet-root SDK_PATH --source-ref a697c341
python tests/performance/run_section_benchmark.py --dotnet-root SDK_PATH
python tests/performance/run_section_benchmark.py --dotnet-root SDK_PATH --source-ref a697c341 --volume-only
python tests/performance/run_section_benchmark.py --dotnet-root SDK_PATH --volume-only
```

## Verification and limits
| Requested round | Current evidence | Remaining |
|---|---|---|
| 1 Build/source | Real Core+Surface C# compiles/runs on Linux .NET 8 | Current Windows add-in builds pending CI |
| 2 Geometry/volume | Baseline 119/119; modified 120/120 executable tests. Added 1,000 analytic band cases, reversed bands and cut/fill crossing | User DWG validation and full requirements audit |
| 3 Performance | Measured synthetic area/sampling/volume at 15,85,300 sections, raw five-run logs included | TIN creation, tiled large mine datasets, peak RSS, CAD/Excel timings |
| 4 Export/UI | Existing executable report tests and 329 Python reference/static tests pass | AutoCAD export, GUI responsiveness, cancellation, locked layers, readonly drawing |
| 5 Regression/install/runtime | Core regression passes | HOST_RUNTIME_BLOCKED: no Windows AutoCAD here; installer end-to-end and all host versions untested |

COMPILE_PASS applies only to local core compilation here. AUTOMATED_TEST_PASS applies to the named suites. HOST_RUNTIME_PASS and FUNCTIONAL_RELEASE_PASS are NOT established.

## Unfinished P0-P6 work
- CAD export still writes synchronously in batches inside an outer transaction; no proof of bounded UI latency or safe interactive cancellation.
- Section UI resamples on draw; reuse must honor edited-profile provenance and revision invalidation before any caching change.
- Volume calculation still samples middle profiles sequentially for reporting; do not remove this data merely because the three volume formulas use end areas.
- Tiled TIN, boundary/duplicate-Z scenarios, full grid and CalculationId/export consistency audit remain required.
- No official Setup, release tag, merge or request for final user acceptance is made by this checkpoint.
