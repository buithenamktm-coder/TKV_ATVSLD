# MC-013 — false outside-boundary TIN rejection at mine-coordinate corners

Severity: P1. Host AutoCAD: **NOT TESTED**. Release: **BLOCKED**.

## Reproduction and cause

The Vỉa 4 existing-terrain fixture with its selected boundary failed the final
region-integrity gate on triangle 119278. This was reproduced on both the
pre-optimization baseline `e7aec388bc2d796215ca8dd04c34a6900a2c3de4` and the
optimized `6181b6f33c21060e7afadc97a7ef540498b39487`. Thus it is not introduced
by the candidate-bounds performance change. CI for 6181b6f passed all 14 jobs
(run 38421384902), illustrating why build/unit PASS alone is insufficient.

Full-precision diagnostics show that the rejected vertex is only one binary64
ULP (5.820766091346741e-11 m) west of the selected polygon corner. The old
point-on-segment endpoint test compared a dot product to tolerance squared.
Outside an endpoint this dot product grows approximately as distance times
segment length, so the accepted endpoint distance collapsed on long edges.

## Fix and scope

In boundary containment, check distance to polygon vertices before the existing
edge/interior tests. Use the caller's existing tolerance unchanged.
This is a circular distance tolerance, not a padded rectangle. The final region
gate remains enabled and outside points still fail. Error messages now retain
round-trip coordinates so sub-millimetre failures can be diagnosed.

An initial experiment changed the shared point-on-segment predicate. Real SQ
tests showed changed conflict provenance and vertical-face counts, so that
broader change was withdrawn. The final fix is restricted to polygon boundary
containment; breakline noding keeps its existing predicate. No source CAD
entities or elevations are edited and no failing triangles are discarded.

## Evidence

- New regression fails before the fix and passes after it: near-corner point,
  reversed segment, genuinely outside corner, circular endpoint distance and
  perpendicular-distance rejection.
- 119 C# regression tests PASS; 330 Python tests and 4 subtests PASS.
- KL mb.dxf independent GEOS/NumPy overlay: 38/38 PASS after the change.
- Vỉa 4 HT: 455152 support triangles, 20615 vertical faces; final region gate PASS.
- Vỉa 4 TK: 518321 support triangles, 28214 vertical faces; final region gate PASS.
- Aggregated fixture results and regression logs accompany this document.
  Real-fixture timings in this round include concurrent checks and must not be
  used as isolated performance benchmarks. `managedBytes` is not peak RAM.

AutoCAD load, 3DFACE transactions, save/reopen, actual install/uninstall and
runtime performance still require host testing. Legacy 2012–2014 adapters and
the multi-version installer remain unfinished. No official Setup is released.
