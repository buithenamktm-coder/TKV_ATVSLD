# Section CAD export continuation — 2026-10-21

Baseline cfe20d6b66dcb5d4ff77826f19b243047f3ea6ec. Its Windows CI run 39515629621 completed successfully. That evidence is NOT host runtime acceptance.

## Changes
- Carry the renderer's exact ending Y coordinate into the next batch, retaining the reconstruction path for independent callers. Avoid 200 repeated prior-profile height calculations for 85 profiles (20 per batch), or 2,100 for 300 profiles. No geometric thinning, scale change or volume formula change.
- Add AutoCAD progress meter and Esc polling while deleting previous layer output, building profile runs, inserting polyline vertices and drawing elevation ticks. Screen repaint is throttled to 250 ms. No DoEvents or worker-thread CAD access.
- Retain the outer document transaction; poll again before commit. Cancellation propagates through nested batch transactions. Polyline objects are registered with the transaction before the cancellable vertex loop.
- UI handles OperationCanceledException separately; calculated profiles are still committed only after the drawing succeeds.
- Add a real-host regression to the existing self-test: compare full and batched output in disposable side databases, then inject cancellation at three stages and compare the original entity handles/geometry/labels after rollback. Test data: 25 profiles, 300 segments/profile, two surfaces, unequal plot scales. It does not edit the user's drawing.

## Evidence
- 120/120 C# Core+Surface executable tests passed (Linux .NET 8).
- 329/329 Python reference/static checks passed. Updated the old call-shape assertion to reflect carried Y and cancellation callback.
- The new side-database regression is COMPILED by the Windows plugin build, but requires AutoCAD to EXECUTE. It is NOT counted among the 120 local C# tests.
- HOST_RUNTIME_BLOCKED: no AutoCAD Windows in this environment. Esc rollback, visual layout, keyboard input latency, screen repaint behavior, readonly/locked-layer cases and actual export timing still need host execution.
- This is cooperative cancellation between API calls. It does not promise interruption during a single slow native call or full UI responsiveness while the document transaction remains open.
- No before/after CAD time is invented. No release Setup or final acceptance is claimed.

## Remaining
Background sampling/volume calculation cancellation is not implemented by this change. Quick-section export still uses its existing path. Full P0–P6 requirements and runtime release gates remain open.
