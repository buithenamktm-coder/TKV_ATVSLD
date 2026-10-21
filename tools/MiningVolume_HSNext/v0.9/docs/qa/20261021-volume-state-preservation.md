# Volume cancellation state preservation — 2026-10-21

Baseline 23c6bd269b80f29fd8cb02c2a1b73e8ad54d521a passed Windows CI run 39578055094.

## Findings and changes
The auto-range branch in VolumePage changed project FromLevel/ToLevel and cleared VolumeResult before background calculation. Cancellation or calculation failure therefore lost the prior result even though the new result was never committed.

Keep input levels as the concurrency snapshot, calculate candidate levels locally using the existing floor/ceiling rule, and pass candidates to CalculateCore. Commit candidate levels and the new result only after cancellation and input-validity gates succeed. User parameter edits during calculation still invalidate/reject the task normally; this change does not undo user edits.

Commit already synchronously notifies RefreshView on the UI thread. Remove the subsequent explicit BindResult(result), which previously cleared and populated both grids a second time. No measured UI speedup is claimed. Preserve the auto-range notice in all success/warning status branches.

## Verification
Updated the existing async commit source-contract assertion for candidate arguments; added a regression forbidding direct accepted-state assignments before the commit and checking candidate computation/cancellation ordering. These checks do not execute WinForms or AutoCAD.

Local Work environment remains unavailable. Current-commit compilation and tests must be verified in GitHub Actions; baseline results are not current validation. HOST_RUNTIME_BLOCKED. Full P0-P6 and FUNCTIONAL_RELEASE_PASS remain open; no official Setup.

Host checks pending: auto-range followed by cancel/error keeps previous result/levels; success publishes adjusted levels and matching volume; changing input levels while calculating rejects the result; result grids refresh once after commit.
