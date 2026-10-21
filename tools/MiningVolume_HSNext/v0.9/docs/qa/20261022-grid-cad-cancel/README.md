# Grid CAD cancellation — 2026-10-22

Baseline: ccf64cac8d20e68ca8d8d465d5e6c17901fb61d6.

## Defect and fix

GridCadRenderer supported a cancellation token, but GridPage did not provide one during synchronous native CAD export. Its calculation Cancel button therefore did not cancel CAD drawing. The page now connects the existing host-thread SectionCadProgress helper to the renderer: Esc polling before mutation, every 64 cells, and immediately before commit. Existing progress callbacks update the native meter and palette. Polling uses the existing throttled screen refresh, never DoEvents or background database access.

The existing single transaction is retained. OperationCanceledException unwinds it before the page displays cancellation, and Regen is reached only on successful export. Earlier exports remain untouched. A new optional callback preserves existing call signatures/token callers. This changes cancellation availability, not quantity formulas or CAD cell geometry.

## Validation and limits

- COMPILE_PASS: AutoCAD 2023 net48 add-in, zero warnings/errors (build.log).
- AUTOMATED_TEST_PASS: 333 Python reference/static checks (python.log). The unchanged core/report sources were checked with 127 passing executable cases in the immediately preceding grid identity evidence directory; they were not rerun for this host-only change.
- Host regression extended to 100 cells per export. After two successful exports it attempts cancellation before mutation, after 64 inserted cells, and just before commit, with labels enabled. The final snapshot requires exactly the original two sets of cell identities and no leaked labels/entities.
- HOST_RUNTIME_BLOCKED: this regression compiles but has not executed. Esc focus behavior, actual transaction rollback, refresh latency, cancellation delay during a native operation/commit and memory overhead require AutoCAD Windows. No claim of measured speedup or unrestricted UI responsiveness is made.
- FUNCTIONAL_RELEASE_PASS is not claimed. No official installer released; full P0–P6 acceptance remains open.
