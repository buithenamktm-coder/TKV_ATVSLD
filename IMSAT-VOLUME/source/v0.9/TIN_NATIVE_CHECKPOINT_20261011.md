# Native CDT checkpoint — NOT release acceptance

Baseline: `6186879f64da4c031f7af37176295a4fdb4dcdb1`.
Plan-only commits through `5c625fd` are preserved. No merge to main.

## Causes and changes

- Region failure: the old per-tile builder rejected empty results after clipping
  to a concave global region. Support triangulation now precedes core/region
  filtering; an empty exterior tile is valid, an empty entire surface is not.
- Shared elevation failure: overlapping segments were rebuilt with stale split
  elevations. Canonical XY/Z nodes now propagate to every endpoint before edge
  deduplication, using the explicit user's Stop/Upper/Lower policy.
- Legacy constraint recovery still failed on whole-drawing fixtures after those
  fixes. Production `ConformingTinBuilder` now calls the pinned native CDT engine;
  the old engine is archived exclusively as a test baseline. No fallback drops
  constraints or substitutes a nearby path.
- Native export checks that every prepared site is referenced, every fixed or
  split constraint piece occurs in the output, and every input constraint has
  been recovered. Small positive-area triangles are retained and reported.

The adapter preserves the existing `ITinBuilder` contract, targets net48, uses
an x64 C ABI, and vendors CDT at `3765e08ec4bbef0c6b721204d7c0e45d279084e9`.
MPL-2.0 notices and complete corresponding source are included. Windows builds
use a static CRT; the end user does not need Build Tools or Internet access.

## Headless real-file results

The private fixture adapter reads the two supplied DXFs, not synthetic substitutes.
Vỉa 4 uses `ht`, `- nam4`, and the `LO_TINHKL` region support rectangle.
SQ uses `!HT` and `!TK`, whole source extents. SQ's manually selected region from
the screenshot has not been recovered as an exact polygon fixture.
Both elevation policies below are diagnostic scenarios, not a business choice.
Elapsed time includes fixture reading, region support extraction, normalization,
triangulation, validation and file-backed output. Two fixtures ran concurrently.

| File / surface | Policy | Time, ms | Triangles | Exit |
|---|---|---:|---:|---:|
| Vỉa 4 / existing | Upper | 66,976 | 424,025 | 0 |
| Vỉa 4 / existing | Lower | 67,079 | 424,025 | 0 |
| Vỉa 4 / design | Upper | 62,045 | 437,139 | 0 |
| Vỉa 4 / design | Lower | 63,381 | 437,139 | 0 |
| SQ / existing | Upper | 92,468 | 1,681,951 | 0 |
| SQ / existing | Lower | 93,044 | 1,681,951 | 0 |
| SQ / design | Upper | 87,331 | 1,673,070 | 0 |
| SQ / design | Lower | 86,800 | 1,673,070 | 0 |

Legacy baseline: Vỉa 4 failed constraint recovery for both surfaces and policies;
all four SQ legacy trials were cancelled at the configured 60-second deadline.
This is evidence of completed native builds, NOT a measured 3x speedup over a
completed legacy run. Final managed heap snapshots range from 311,816,648 to
885,834,424 bytes; these are NOT peak RAM and exclude native memory.

Core executable regression: 34/34 on Linux; reference checks: 177/177.
Historical implementation assertions explicitly prefixed `test_legacy_` validate
the archived implementation only; executable geometry tests use production.
Windows hosted build and AutoCAD runtime must be recorded against the final SHA.
Private drawings, coordinate logs and fixture binaries are not committed.

## Open release gates

This checkpoint does NOT finish either Work plan. Still pending:

- Adaptive density-based tiling and shared seam guarantees. The existing halo
  acceptance heuristic remains; whole-file completion is not seam certification.
- Exact region-boundary acceptance beyond the support rectangle/intersection
  filter, including the user's actual SQ selection.
- Peak working-set measurement and the 10k/100k/500k/1M stress matrix.
- Full AutoCAD 2023 workflow on both files: source import, region selection,
  TIN, sections, volume, Excel formulas, save/reopen, installer/uninstaller.
- Runtime acceptance of this native DLL under AutoCAD at the exact source SHA.

No runtime-pass installer is authorized by this report. Do not ask the user to
retry intermediate installers or describe V1.0 as complete based on these tests.
