# NAMVECTA VOLUME 2.0 — P0 grid microface/empty-region integrity gate

Date: 2026-10-25. Baseline: PR #52 head `9e3bd0b9f8309724cef5dbd7db3295e049f5995e`.
Branch: `namvecta-volume-v2-grid-accuracy-gate-20261025`.
Status: **SOURCE PATCH; NOT TESTED on AutoCAD/Windows; NOT A RELEASE.**

## Why

The CAD/TIN quality gate accepts a triangle if its projected XY area is strictly positive, but
`ExactGridCellCalculator` skips intersections whose XY area is at or below
`Eps = 1e-9 m2`. Large elevation differences mean a small omitted footprint need not have
negligible volume. Previously an incomplete grid cell could still pass `IsComplete`
because its no-data footprint was below absolute coverage tolerance `1e-8 m2`.
Furthermore, a grid with no resulting cells passed `Cells.All(...)` vacuously.

## Changes (conservative, no change to signed integration formulas)

1. Keep every cell with **strictly positive** accepted calculation area in the report,
   including sub-threshold slivers; its volume is not automatically certified.
2. Mark cells with `CalculationArea <= 1e-9 m2` as incomplete rather than
   pretending an unevaluated positive-area cell represents zero earthwork. A partially
   covered sliver also cannot pass solely due to the absolute 1e-8 m2 no-data tolerance.
3. For each accepted cell, detect positive faces with area `<= 1e-9 m2` present
   in the accepted region (subtracting holes) and expose
   `HasUnresolvedSmallTriangles`. Continue to show partial results but never mark
   them ready for technical acceptance.
4. A report with no cells is **not** `IsComplete`.
5. Show flagged cell counts in the CAD UI and Excel. Any microface cell with
   unresolved volume has blank volume cells across Excel detail, linked grid
   summary and method sheets; verified cells retain their numeric/formula values.
   Nodes sharing an unresolved cell are marked as geometry-unverified.
6. Add four executable C# regression cases:
   `grid_empty_report_is_not_accepted`,
   `grid_positive_subthreshold_cell_is_not_accepted`,
   `grid_partially_covered_sliver_is_not_accepted`, and
   `grid_positive_tiny_tin_face_is_not_silently_accepted`.
7. Add real C# report-builder regression
   `grid_unresolved_microface_is_blank_in_all_excel_volume_sheets`, checking
   both blank unconfirmed quantities and intact confirmed-cell formulas.
   Update (do not disable) older static contract checks for the stronger guard.

## What this does not prove

- It does not fix the underlying integration of microfaces. This is a
  fail-closed **diagnostic gate** only; the signed integral is unchanged.
- An extremely small face whose footprint collapses under the clipping geometry's
  coordinate tolerance might not be detected by this preliminary diagnostic;
  testing numerical conditioning and large survey coordinates remains required.
- No claims about 85-section CAD speed, DWG editing, CAD/Excel parity, AutoCAD
  2020–2024 runtime or Setup. These remain independent release blockers.

## Test commands on a development host

From `tools/MiningVolume_HSNext/v0.9`:

```sh
python tests/core/run_core.py
python -m pytest tests/reference compat/test_compatibility.py -q
```

On the licensed Windows/AutoCAD 2023 host, run `MVSELFTEST` and the 16 independent
DXF fixtures; compare excavated/backfill totals and CalculationId between CAD
and Excel. Record CPU/RAM, source SHA and repeat timings for the same 85
sections; do not infer speedups from synthetic .NET test runs.

## Acceptance checklist

- [ ] C# executable core/report regression (four geometry + one Excel QA case) passes
- [ ] Python regressions pass
- [ ] Windows plugin compile succeeds for each targeted CAD version
- [ ] Host `MVSELFTEST` passes with raw log
- [ ] No false 'Đủ' on empty/tiny/unresolved inputs
- [ ] Complete normal inputs retain identical unrounded volume totals
- [ ] CAD & Excel show the same CalculationId and volumes after edits
- [ ] Runtime performance regression checks pass

Do not merge or publish a Setup while any release gate is unverified.


## Follow-up 2026-10-25 — Section TIN shared-knot sampling (P0 performance)

A first source-level optimization reuses a successfully queried DIRECT elevation of
the previous segment's ending knot as the next segment's starting elevation.
It is confined to `TinSectionSampler.BuildProfileIndexed`, with two independent
last-value caches for HT and TK, reset for each `SectionSpan`.

Safety boundaries:
- Only an exact shared sorted knot `t0 == previousEndT` may reuse a value.
- A boundary fallback query (offset toward the segment interior) is **not**
  cached: adjacent segments may need different interior-side queries.
- Neither knot list construction, midpoint coverage checks, cut/fill formula,
  triangle index, TIN topology nor output CAD geometry is modified.
- Cancellation polling and missing-coverage invalidation remain in place.

Added executable geometry test:
`section_knot_reuse_preserves_analytic_cut_fill` with adjacent 3D TIN strips,
positive/negative signed differences, and a disconnected two-span route.
The expected full-section cut/fill is 3.25/2.75 square metres; with a span
gap, 3.25/1.25 square metres.

This eliminates repeated direct endpoint lookups; **no speedup is claimed**
until same-data, same-hardware before/after benchmarks and native AutoCAD
85-section CAD export logs are available. The existing
`tests/performance/report_autocad2023_host.py` remains the host comparison tool.
