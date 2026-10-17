# V1 core checkpoint — 2026-10-11

Authority: IMSAT-MV-MASTER-V1-TO-V2-VIEW3D-TRIAL15-UI-20261011.
Base commit: `135ab3e3b7722e91c7a8743f15b5bdaa27affa03`.
This checkpoint is **not V1-CORE READY**, not runtime acceptance and not release approval.

## Implemented in this checkpoint

- Remove automatic high/low elevation behavior from production preparation. Legacy numeric enum values remain readable but resolve to Stop and require manual review.
- Per-surface conflict report: duplicate vertices, point-on-breakline, crossing and overlap; source handles/layers/types and derived geometry provenance.
- Modeless virtual conflict table, source filter/zoom, cancellable rescan, explicit whole-object exclusion from the calculation model. CAD source is not erased by exclusion. Source revision changes invalidate calculations; document switching closes this window.
- Replace independent halo triangulations with one native constrained mesh, followed by adaptive spatial storage. Storage does not introduce geometry seams. Real triangle bounds determine tile metadata; leaf limit 8192 triangles, preview limit 200000.
- Clip triangles geometrically to simple concave polygons and interpolate source-face Z; reject self-intersecting/repeated-ring boundaries. Polygon holes remain unsupported. A triangle-intersection predicate alone is no longer the clipping operation.
- Dispose temporary disk-backed meshes when the source changes or the second surface build fails.

## Executed evidence

Environment: Linux, .NET SDK 8.0.425, native g++ build, production C# Core/Surface sources compiled by Roslyn. No AutoCAD host.

| Check | Result | Scope |
|---|---|---|
| CoreRegression | PASS 39/39 | Includes conflict migration, source report/rescan, native constraints, deterministic repeat, storage seams/bounds, exact concave area and plane Z |
| Python reference/static suite | PASS 176/176 | Executable reference checks plus source guards; not CAD runtime |
| Independent GEOS polygon oracle | PASS 4/4 | C# output against polygon intersection: concave, narrow strip, diagonal polygon, partial hull overlap; 2000 seeded sites plus hull per case |
| Oracle missing area | <= 1.84e-15 square units | Synthetic coordinates only |
| Oracle outside area | 0 | All four cases |
| Oracle absolute overlap residual | <= 7.11e-15 square units | Sum triangle areas minus GEOS union |
| Oracle plane Z error | <= 7.11e-15 length units | z = 2x - 3y + 7 |
| Windows build at this checkpoint | NOT TESTED yet | Prior Windows CI at base SHA does not validate these changes |
| AutoCAD 2023 runtime | NOT TESTED | Host unavailable |

Reproduce from project directory:

```text
python tests/core/run_core.py --dotnet-root <dotnet-sdk>
python -m pytest tests/reference -q
python tests/core/run_core.py --dotnet-root <dotnet-sdk> -- --clip-oracle > clip-oracle.jsonl
python tests/core/verify_clip_oracle.py clip-oracle.jsonl
```

## Open work / limits

- Manual review is not complete: transient markers, reverse CAD selection, safe edit/delete/Undo, source fingerprints, all contributing sources at derived intersections, exact original vertex/parameter mapping after region clipping.
- Global triangulation removes the demonstrated halo-seam defect but is not an out-of-core mesh construction algorithm. Native and managed working sets grow with input. Exact-clipped storage currently retains clipped faces while partitioning. No 10-million-point capacity claim.
- Exact clip oracle covers synthetic areas/plane elevation; full topology, near-degenerate, large-coordinate and constraint-subdivision coverage remains to be expanded. No unsupported holes are represented as accepted geometry.
- Region support selection still uses a padded bounding rectangle; full-vs-region support invariance on real mine data is not yet verified.
- The two private mine DXFs contain conflicting source elevations. Historical high/low diagnostics are not acceptance of the new policy. Their coordinates, handles and files must not enter the public repository.
- Atomic CAD pair replacement/rollback, project fingerprint/migration, independent section/volume oracle, TT34 modes/edge blocks, linked Excel, section layout and full performance matrix remain open.
- V2 branch must not be created as a verified baseline until V1-CORE READY is actually met. Grid/minerals/View3D/trial/license/UI gates remain open; administrative signatures and mine acceptance are external gates.

Release decision: **NOT RELEASE READY**.
