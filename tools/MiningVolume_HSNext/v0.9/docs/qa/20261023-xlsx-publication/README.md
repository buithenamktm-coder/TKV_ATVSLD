# Validate asynchronous Excel output before publication — 2026-10-23

Base: 1fc98be48a545496eb60eb76757fd3b6ca6da6f3. Its CI run 39924349418 was verified completed/success before this work.

## Defect and correction

The section and grid pages wrote directly to the destination from a worker after preflight. A DWG/data change while workbook I/O was running could still publish an obsolete report. Grid output also lacked revalidation after asynchronous workbook construction.

WriteValidatedXlsxAsync now prepares a private workbook on the worker, returns to the captured calling synchronization context, checks the source again, checks cancellation, then atomically replaces/moves the destination without an intervening await. Rejected/canceled work removes staging files and preserves the existing destination. Existing synchronous WriteXlsx remains available for current callers. No CAD API is called on the I/O worker.

Section output reuses its existing approved volume/profile/TIN/revision/metadata/CAD geometry guard. Grid output captures the accepted report/options/document and unit/period, revalidates report identity and current TIN/source/options state, and re-reads actual boundary geometry before publishing. The report approved before an incomplete-grid confirmation is retained; it cannot silently switch to another report during that dialog. CalculationId and formulas are unchanged.

## Evidence

- COMPILE_PASS: AutoCAD 2023/net48 plugin Release, 0 warnings/errors.
- AUTOMATED_TEST_PASS: 138 C# cases; Python 383 passed, 3 skipped, 4 subtests passed.
- Executable regression forces a data-change rejection after successful workbook preparation, and cancellation at the final validation callback. Both preserve prior file bytes and leave no staging files. Success opens the resulting XLSX as a ZIP and verifies a worksheet exists.
- Source contracts cover both UI integrations and placement of final validation after awaited I/O, before File.Replace. The WinForms/AutoCAD synchronization context itself is not executed in Linux.
- Initial new test used an incorrect ReportCell collection type; fixed before the successful C# run. Three old source assertions referenced WriteXlsx directly; updated for the new guarded call and full suite rerun.
- HOST_RUNTIME_BLOCKED: actual DWG switching, grip edits, UI cancellation and Windows file-lock behavior require AutoCAD/Windows runtime.
- FUNCTIONAL_RELEASE_PASS not established. This is not a final Setup release.

Commands from v0.9:

```sh
python tests/core/run_core.py --dotnet-root /tmp/imsat-dotnet
python -m pytest tests/reference compat/test_compatibility.py -q
/tmp/imsat-dotnet/dotnet build src/MiningVolume.Plugin2023/MiningVolume.Plugin2023.csproj -c Release -p:UseAutoCADNuGet=true -m:1 --disable-build-servers
```

No new native CAD benchmark, installer acceptance or end-to-end performance claim is made by this change. Existing Windows install-over-old-version acceptance remains outstanding.
