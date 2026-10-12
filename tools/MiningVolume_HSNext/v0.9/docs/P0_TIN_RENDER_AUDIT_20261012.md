# P0 — TIN render failure, 12 October 2026

Baseline fetched: `f7496d8e565d0a13a405b0c20e7e7f9e8c638b58`, branch `miningvolume-v2-chatgpt-p0-continuation`. V1 branch untouched. Earlier unfinished RF06 edits checkpointed in Git before fast-forward; new upstream fixes inherited rather than overwritten.

## Confirmed root cause

`TinCadRenderer.ValidateTriangle` emitted the exact reported message when `Area2D <= 1e-12`. Native CDT/storage retained positive-area triangles to preserve source vertices and breaklines. Thus a valid tiny triangle survived construction and failed CAD output. The previous fixes changed storage and builder, not this renderer cutoff.

The section sampler independently compared an area determinant to a linear tolerance; grid interpolation also had a fixed area cutoff. These could silently omit or reject the same small faces downstream.

## Correction

- Shared `TinTriangleQuality.ValidateForCad` accepts every finite strictly positive XY face, regardless of survey coordinates or arbitrary area thresholds.
- `RobustOrientation` computes local differences, applies the IEEE-754 orient2d error bound `(3+16u)*u*(|left|+|right|)`, `u=2^-53`, and uses exact binary-integer arithmetic for ambiguous determinants. This is a numerical error filter, not a chosen minimum surface area.
- Nonfinite coordinates/areas fail closed. Exact-zero native faces may be removed only after checking that their vertices and constrained edges remain represented in positive faces. Removed projected area is exactly 0 m². Positive faces are never dropped by this policy. Storage warnings disclose count and affected area.
- CAD transaction checks Esc and reports progress; cancellation rolls back the replacement, retaining the old layer. No source CAD entity is edited.
- Sampler and grid elevation use the same robust determinant without a fixed area cutoff.
- `MVBUILDINFO` and startup diagnostics expose loaded DLL paths, source SHA, MVID and bundle file hash checks. CI embeds source SHA in managed assembly identity and includes hashes for all five delivered DLLs. Setup validates hashes before replacement.
- A real AutoCAD self-test now writes/counts a triangle with positive area below `1e-12` at X=2,500,000. **Added, not executed here.**

## Actual evidence, Linux native/core (not AutoCAD)

| Dataset | Vertices supplied | Support triangles | Vertical faces | Build + complete mesh validation | Process-tree peak RSS |
|---|---:|---:|---:|---:|---:|
| SQ `!HT` DXF-derived fixture | 952,593 | 1,681,755 | 7,818 | PASS, 94.766 s inside fixture | 1,728,920 KiB |
| SQ `!TK` DXF-derived fixture | 945,685 | 1,672,875 | 7,681 | PASS, 95.205 s inside fixture | 1,984,888 KiB |

Peak RSS is Linux `getrusage(RUSAGE_CHILDREN).ru_maxrss`, including compilation/child processes; not managed-memory end size. End-to-end harness time: HT 101.8085 s, TK 101.9408 s. Private source geometry/handles are not published. These use the existing DXF adapter: supported Point/LINE/LW/2D/3D/contour; six CIRCLE and five SPLINE per source layer are explicitly unsupported, not silently counted as processed.

HT reproduces the reported **1,681,755** support-triangle count. Both use the actual native CDT, file-backed storage, and the exact shared renderer validation for every support triangle. This does **not** prove that AutoCAD accepted each 3DFACE. No skipped-degenerate warning occurred in either real mesh; small positive triangles were retained. Existing vertical-conflict/topology relaxation warnings remain, so section review is required before accepted volumes.

Automated regressions: **88 C# PASS / 0 FAIL**, **249 Python static/reference PASS / 0 FAIL**. Added: full 1,681,755-face synthetic validation with a tiny final face; exact determinant cancellation where ordinary arithmetic returns zero; zero-face removal with retained/lost site and breakline; nonfinite rejection; tiny-face sampler. Historical snapshot test supplies a real UTC saved time instead of an invalid year-1 local timestamp.

## Runtime and release status

- AutoCAD 2023 actual 3DFACE rendering, cancel/rollback, HT/TK toggling, SAVE/reopen, section output and volume acceptance: **NOT TESTED**.
- Windows interactive uninstall/upgrade/reinstall, duplicate installation discovery on the user's PC: **NOT TESTED**. `MVBUILDINFO` is the diagnostic, not proof of what that PC currently loads.
- Signing/private issuer key not supplied; release activation **BLOCKED**. No private key generated or packaged.
- CI on final pushed SHA and QA Setup/security/hash evidence must be checked before handoff. No official-release claim.
- Security workflow checks nonempty Setup, records Authenticode and attempts an enabled Defender custom scan. If no scan engine is available it records NOT TESTED, never PASS; any reported threat blocks distribution. Do not disable protection.
