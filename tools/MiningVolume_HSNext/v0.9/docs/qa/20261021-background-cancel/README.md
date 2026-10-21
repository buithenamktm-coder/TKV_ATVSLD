# Background cancellation — 2026-10-21

Baseline b0d05c5ec17d77c079089313cc2b780f19af0616; its Windows CI run 39544892228 succeeded. No AutoCAD host execution is implied.

## Changes
- Optional CancellationToken passed from section/volume UI through workflow services into core geometry. Existing callers retain default non-cancelable behavior.
- Poll during in-memory TIN index construction, section sampling, area integration and volume intervals/bands. Parallel.For receives the same token. Check again before returning/committing any result.
- Reuse existing action buttons as cancel actions during background work; ignore duplicate starts. Dispose requests cancellation. Restore button text and dispose the token source in finally.
- Ignore delayed progress from completed/canceled section sampling. Existing document/TIN/profile revision guards remain intact. CAD DB work stays on the host thread.
- Canceled index construction cannot enter the sampler cache. No partial profile array or volume result is returned to callers.

## Verification
- COMPILE_PASS: actual AutoCAD 2023 net48 add-in cross-compiled on Linux against Autodesk NuGet reference assemblies, 0 warnings/errors.
- AUTOMATED_TEST_PASS: 123 real C# core/surface cases; 329 Python reference/static cases.
- Three deterministic cancellation cases use cancellation during triangle reads, a parallel progress callback, and profile segment reads. Each verifies that canceled work throws, then the same input/sampler can compute a correct result again.
- The UI handlers compile, but actual button interaction and panel disposal under AutoCAD are NOT runtime tested.

## Overhead check
Same .NET 8 runtime, optimized code, four processors, tiered compilation disabled, one warm-up and five timed runs per fixture. Existing synthetic volume fixtures include middle-section sampling and analytic 19,960 m3 cut / 0 fill checks. Median milliseconds below; small differences are not claimed as a speedup.

| Fixture | Before ms | After ms |
|---|---:|---:|
| volume_15_profiles_200_bands_including_mid_sections | 24.082 | 17.263 |
| volume_85_profiles_200_bands_including_mid_sections | 114.792 | 113.159 |
| volume_300_profiles_200_bands_including_mid_sections | 405.749 | 404.487 |

## Limits / remaining work
HOST_RUNTIME_BLOCKED and FUNCTIONAL_RELEASE_PASS not established. Cancellation is cooperative, not a guaranteed wall-clock deadline: individual tile reads, candidate-query construction, sorting and conflict annotation can finish before the next check. No forced thread abort or off-thread CAD access.

This change covers the main calculation-section and volume actions. Quick sections and edited-profile update retain their existing paths. The prior automatic level-range adjustment still occurs before background volume work; cancel prevents publication of unfinished output, it does not undo that existing parameter adjustment. Full P0–P6 acceptance, real DWG profiling and official Setup remain open.
