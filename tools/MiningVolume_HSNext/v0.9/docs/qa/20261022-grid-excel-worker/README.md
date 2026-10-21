# Grid Excel worker — 2026-10-22

Baseline d14304ae47429fc9875a2a4436b9bd712e30a6d7; Windows CI run 39683592338 succeeded.

## Cause and change

GridPage previously constructed all six workbook sheets synchronously before starting background file I/O. Node deduplication and per-cell row/formula allocation therefore occupied the AutoCAD UI thread even though writing the ZIP/XLSX ran in Task.Run.

BuildAsync now captures the information sheet, organization, period and accepted grid on the host thread, then constructs the remaining five sheets in a worker. No worker accesses AutoCAD objects or ProjectState.Current. The synchronous Build entry point remains available and shares the same data-sheet implementation. Formulas, row order, rounding and calculation algorithms are unchanged. Export completion still reports the captured workbook/file; changing live state cannot relabel its information or signature sheets. A page flag prevents duplicate exports, including re-enabling the button through project change notifications.

## Evidence

- AUTOMATED_TEST_PASS: 128 executable C# cases; 333 Python reference/static checks. New regression builds synchronous and asynchronous reports, changes the live organization after starting the latter, compares all six sheet titles and row counts, writes XLSX and verifies original CalculationId and absence of changed organization in information and signature sheets.
- COMPILE_PASS: AutoCAD 2023 net48 add-in, zero warnings/errors. First final-build attempt aborted inside the .NET CLI process-information lookup; unchanged-source retry succeeded. Logs retained.
- Static organization-header assertion updated to require captured strings rather than mutable global state in the worker/summary builder.

## Limits

HOST_RUNTIME_BLOCKED. This is a thread-placement fix, not a measured reduction in total export time. Information-sheet capture still scans grid summary data on the caller thread. Workbook memory usage is not reduced; Excel cancellation and percentage progress remain outstanding. No before/after AutoCAD responsiveness or host export benchmark is available. Actual DWG switching during export and UI behavior require host verification. FUNCTIONAL_RELEASE_PASS remains unestablished; no official Setup.
