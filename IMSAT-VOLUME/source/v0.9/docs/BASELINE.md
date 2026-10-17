# IMSAT VOLUME V2 — Multi-CAD baseline, 2026-10-13

Task: IMSAT-VOLUME-V2-MULTICAD-FINAL-20261013.
Branch: `miningvolume-v2-chatgpt-p0-continuation`.
Inherited commit: `25e89317c99e1e9a6f09aa0828a518db2322e399` (fast-forward from
`6aed249`, no reset and no changes to V1). The supplied PlanCode is retained at
`PLANCODE_IMSAT_VOLUME_V2_2012_2026_WORK.md`.

## Evidence before edits

- AutoCAD 2023 Windows compile/QA workflow: [38372400617](https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/38372400617), SUCCESS.
- Real Core/Surface C# regression: 113 tests, 0 failed, Linux .NET 8.0.31.
- Python tests plus compatibility contracts: 326 passed, 4 subtests passed.
- Original files and SHA-256: `qa/20261013-multicad/source-inventory.json`.
- Raw baseline/reproduction/post-fix C# logs: `qa/20261013-multicad/`.
- Existing production modules: Core (geometry, sections, grids, volumes,
  minerals, reporting); Surface (native CDT, tiled storage); Cad2023 adapter;
  Plugin2023 UI/workflows; native Go Setup; separate existing license issuer.
  No new application/project was created.

## Verified limitations / blockers

| Area | Baseline condition |
|---|---|
| AutoCAD 2023 | CI compile PASS; actual AutoCAD host NOT TESTED here |
| 2015–2026 | Version-specific compile candidates present; .NET 4.5 sources still call unavailable Array.Empty |
| 2012–2014 | No implemented compatible legacy adapter; PORT REQUIRED |
| Setup | Existing single-version R24.2 bundle; multi-version setup incomplete |
| Host functional gates | NOT TESTED for all 15 years; no Windows/AutoCAD workstation exposed |
| Actual fixtures | SQ and via 4 DXF accessible; KL mb not in local attachment directory |
| Five final audit rounds | NOT COMPLETE; cannot substitute source inspection for actual host operation |

## Reproduced defects and corrections in this work

- MC-001 / P1: overlapping sheets **outside** the selected calculation boundary
  wrongly abort an otherwise valid grid cell. Regression failed before fix;
  validate the overlap intersected with the outer boundary minus holes.
- MC-002 / P1: a shared XY epsilon applied to both height-sign half-planes
  cancels positive and negative sub-nanometre deltas. Analytic oracle: a 100 m
  square with linear -1e-10…+1e-10 m difference has cut = fill = 2.5e-7 m3.
  Use the actual zero contour, retaining separate cut/fill integrals.
- MC-003 / P1: net45 invokes net46 Array.Empty API. Replace with one cached
  zero-length array per type, shared in Core. Framework targets unchanged.
- MC-004 / P1: compatibility artifacts copied only the entry DLL and could
  inspect stale outputs from other flavors. Copy an explicit dependency
  allowlist from the exact flavor, record SHA-256/source SHA, exclude Autodesk
  binaries; remain compile-only, NOT FOR INSTALLATION.

## Autodesk runtime reference checked 2026-10-13

[Application compatibility](https://help.autodesk.com/cloudhelp/2026/ENU/AutoCAD-Customization/files/GUID-D54B0935-1638-4F97-8B37-1EC3635A1E71.htm)
and [2025/2026 .NET transition](https://help.autodesk.com/view/ACD/2025/ENU/?caas=caas%2Fsfdcarticles%2Fsfdcarticles%2FAutoCAD-and-Toolsets-Requirements-for-products-affected-by-the-Microsoft-NET-10-transition.html).
Preserve the distinct .NET 8 and .NET 10 flavors; series/year alone is not a
sufficient runtime discriminator. No support/release claim follows from this
table or a successful compilation. Runtime, installation and functional gates
remain separate.
