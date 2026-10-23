# CAD quantity-table cancellation — 2026-10-21

Baseline: b46febeb400baf729e77bea596eed19056b778e2, Windows CI 39625307631 succeeded.

## Finding and change

CadVolumeTableService had no cancellation or progress inside row population/styling. A table could also remain unowned by the transaction if validation or formatting threw before Append. All three native TABLE objects now have explicit disposal scopes; table initialization disposes on failure. Optional host callbacks poll cancellation during population/styling and immediately before the single transaction commits. The existing host progress meter is reused with a quantity-table label; no AutoCAD API work moves to a background thread and no DoEvents is introduced.

ExportPage distinguishes cancellation from failure. If Excel has already been successfully written, cancellation reports that file path and the canceled CAD stage; it never reports combined success. Existing quantities, formulas, table structure, locked-layer checks, and the 1,500-row limit are retained.

## Verification and limits

COMPILE_PASS: actual AutoCAD 2023 net48 plugin cross-build against Autodesk references, 0 warnings/errors. See compile.log. This includes compilation of the new host regression, not its execution.

333 Python reference/static checks passed. One existing call-shape assertion was updated for the cancellation parameter. Core geometry code is unchanged in this commit; prior core passes are not new host evidence.

The existing AutoCAD self-test now includes an 85-interval/85-band side-database fixture: successful export creates exactly three tables; injected cancellation at the beginning, middle, and final pre-commit poll must leave existing table handles, positions, layers and cell strings unchanged. A non-finite row after summary insertion must also roll back. This regression requires AutoCAD and has NOT run here. Its presence/compilation does not count as HOST_RUNTIME_PASS.

HOST_RUNTIME_BLOCKED. GenerateLayout and individual native TABLE calls are synchronous and cannot be interrupted inside the call. The meter can poll between them; there is no guaranteed cancellation latency. No CAD speedup, peak-memory result, or no-freeze claim is made. Actual keyboard Esc, default/custom TABLE styles, rollback, saved/reopened DWG and mixed Excel/CAD output require host testing. Full P0–P6 and FUNCTIONAL_RELEASE_PASS remain open; no official Setup released.
