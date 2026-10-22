# IMSAT VOLUME V2.0 — AutoCAD 2023 native performance evidence

**Status: SOURCE / CI CANDIDATE — HOST_RUNTIME_BLOCKED until a licensed
AutoCAD 2023 workstation/runner executes the operations.**

This change adds real-host timing to CAD writes without changing numerical
geometry or publishing a Setup. Unlike Linux/.NET Core timings, these readings
come from the native AutoCAD 2023 process.

## Measurements written automatically

During official app operations, a local TSV is appended at:

`%LOCALAPPDATA%\IMSAT_VOLUME\Logs\autocad2023-perf.tsv`

| Operation | Data | Boundaries |
| --- | --- | --- |
| `TIN_CAD_EXISTING` | HT face count and actual CAD write duration | Lock, Replace, layer visibility |
| `TIN_CAD_DESIGN` | TK face count and actual CAD write duration | Lock, Replace, layer visibility |
| `TIN_CAD_PAIR` | Combined HT+TK face count and write duration | One transaction / rollback |
| `SECTION_CAD_EXPORT` | Requested and completed profiles, segment count, total milliseconds, elapsed per 20-profile batch | Includes CAD layer cleanup on first batch and final atomic parent commit |

Each row includes UTC timestamp, drawing fingerprint (not DWG file name), source
SHA from `BUILD_IDENTITY.txt` (or `UNKNOWN`), and assembly MVID. `PASS` is
written only after the corresponding CAD operation commits. `CANCELED` and
`FAILED` are never eligible for performance comparisons. A telemetry I/O
failure is nonfatal and never changes/rolls back accepted CAD geometry.

## Required end-to-end comparison

1. In licensed AutoCAD 2023, open a **copy** of the same large mining DWG. Keep
   the HT/TK source, region, 85 section locations, scales and output settings
   unchanged. Record CPU/RAM and AutoCAD build separately in the QA minutes.
2. On baseline code, create HT TIN, TK TIN, draw at least **85 sections**, and
   copy the local TSV to `before.tsv`. Verify official volume results separately.
3. Run the new code on the **same DWG and inputs**, repeat the operations (ideally
   three times), and save `after.tsv`.
4. For an apples-to-apples report run:
   ```powershell
   python tests/performance/report_autocad2023_host.py --current after.tsv --baseline before.tsv --min-sections 85
   ```
5. Compare only matching drawing fingerprints, 85+ profile count and exact
   segment count. Keep evidence of Excel/CAD output parity, volume values,
   source geometry and undo/ESC behavior.

**No matching baseline = no speedup claim.** The script outputs
`NO_HOST_85_SECTION_EVIDENCE` if no qualifying native event exists, or
`NOT_COMPARABLE_DIFFERENT_DWG_OR_GEOMETRY` when an earlier sample is not a
valid reference. Its `release_accepted` field remains `false` in every case:
full runtime workflow, license activation, Setup install/uninstall and T01–T22
acceptance must pass independently.

## Code review proof

- `HostPerformanceLog.cs` is side-effect-free on CAD/model state.
- `SectionWorkflowService.DrawProfiles` takes timing at each 20-profile
  batch and after the transaction owner commits. On cancel/failure, partial
  completion is only diagnostic (all geometry rolls back).
- `SurfaceWorkflowService.DrawTinInternal/DrawTinPair` records CAD writes,
  without changing the existing TIN source/provenance contract.
- `tests/reference/test_v2_host_performance_proof.py` and
  `test_v2_host_perf_report.py` guard source behavior and reporting logic.

Until a host run provides matching rows, record **not measured** rather than
inventing an improvement percentage or a completion percentage.
