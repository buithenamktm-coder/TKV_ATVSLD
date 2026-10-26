# NAMVECTA VOLUME 2.0 — pending CAD runtime acceptance

Accepted baseline: `cc0a6cb7cddcecee6c5d27074fb0ea6647297aac`.
This branch MUST keep the approved UI, station-system block semantics and professional Inno Setup for AutoCAD 2020–2024.

## Evidence and outstanding gates

- Setup QA compiled successfully at workflow #40289413129; this is **not runtime acceptance**.
- A broader PR54 CAD regression suite was attempted and generated real `MVSELFTEST Status=FAIL` evidence in workflows #40289895043, #40290343430 and #40291210520.
- The introduced regression suite expects PR54-only `ReplaceProfilesBatched` journal callbacks and `RecoverFailedSectionExport`. The accepted station-block renderer does not expose those APIs. Do **not** replace the approved renderer merely to make this suite compile.
- In side-database tests, style-record `eWrongDatabase` was observed and the corresponding targeted fix was ported.
- The stronger regression cases require a separate implementation against the accepted station-system renderer: canceling an area-label update must restore original labels; deleting all official CAD section polylines must invalidate or reject stale quantities; canceling a multi-batch section replacement must not leave orphaned or partially erased CAD entities; quantity block export and grid calculation identities must remain stable.
- The accepted original CAD regression runner is restored as the compatible baseline test. This **does not mean** those additional cases PASS. They remain **NOT VERIFIED / BLOCKING RELEASE** until implemented and evidenced inside actual AutoCAD 2023.
- Do not merge PR #80 or label the installer as RELEASE until all relevant runtime gates, UI inspection, 2020–2024 host coverage, live upgrade/uninstall and offline pass tests are complete.

Never delete a failing test merely to claim QA PASS; port its scenario to the accepted renderer with compatible code and retain the failed evidence until independently verified.
