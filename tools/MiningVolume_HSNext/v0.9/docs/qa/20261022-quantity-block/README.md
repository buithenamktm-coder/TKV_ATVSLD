# Quantity table Block export — 2026-10-22

Continuation of the integrated pipeline audit at `f5eafde9d878b80b4194d37af46f462a8b277b2b`; no architecture or volume formula changes.

Each quantity export now inserts one uniquely named IMSAT_KL_ BlockReference containing the existing three native TABLE objects. Tables use local coordinates; the reference preserves the previous 2D insertion position. CalculationId remains in the summary. A new export preserves earlier exports, including user edits. Grouping does not automatically update earlier deliverables.

The definition, reference and tables share one transaction. Cancellation, invalid row data or a callback exception before commit abort the entire export. Finite insertion coordinates are checked before mutation. The output layer checks remain in place. The compiled AutoCAD regression now checks cancellation at start/middle/final poll, invalid row rollback, orphan definition count, three tables per block, and a second export preserving the first and carrying its own CalculationId.

| Gate | Current evidence |
|---|---|
| COMPILE_PASS | AutoCAD 2023 net48 project, 0 warnings, 0 errors; build.log |
| AUTOMATED_TEST_PASS | 342 Python tests, 4 subtests; 1 PowerShell test skipped; python.log |
| C# core tests | 131 passed at parent integrated batch; not rerun for this host-only change |
| Host regression | Compiled, not executed: Linux environment has no AutoCAD |
| HOST_RUNTIME_BLOCKED | Actual table rendering, BEDIT, DWG save/reopen, locked layers and rollback still need Windows AutoCAD |
| FUNCTIONAL_RELEASE_PASS | Not achieved; no official Setup |

The first SDK invocation failed in Process.GetStat before compilation. An unchanged-source retry compiled successfully; both logs are retained.

This reduces ModelSpace entries per export from three TABLEs to one INSERT, but adds a block definition and retains the three TABLEs internally. No DWG size, memory or timing improvement is claimed without measurement. Section drawing grouping is a separate outstanding requirement. Existing integrated benchmark results apply to the unchanged geometry core only, not CAD Block performance.

Host acceptance: run the existing section CAD regression command; then export a representative 85-section result, inspect all three tables and CalculationId against Excel, cancel exports at multiple stages, verify earlier edited blocks remain unchanged, and save/reopen the DWG. Measure on the same drawing and host before claiming a speedup. These checks are pending, not user acceptance requested now.
