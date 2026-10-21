# Section revision guards — 2026-10-21

Baseline: 73615fae1ed8a17d01d9b2f904d50bb4b9c04e83.

## Finding and change
BuildProfiles checked TIN object identity after background sampling and after the insertion prompt, but did not check whether the same objects had become stale because source or clipping-region revisions changed. UpdateEditedProfiles had the same gap before writing labels and publishing volume results.

Capture both source revisions and region revision before background work. At both BuildProfiles write gates and the edited-profile update gate, require both TINs to remain current and all three revisions to match. Existing document, surface identity, system, geometry and parameter checks remain. No volume formula or clipping tolerance changes.

## Evidence and limits
Three Python source-contract regressions cover snapshot order and validation before the insertion prompt, CAD drawing and edited-profile labels. These are static checks, not simulated AutoCAD runtime tests.

Work execution environment is offline during this change. Local compile, pytest and benchmark have NOT run for this commit. GitHub Actions must provide current-commit build/reference/core results; prior 123 C# and 329 Python passes apply only to the baseline.

HOST_RUNTIME_BLOCKED. A host test must change source/region during sampling, during insertion-point selection, and during edited-profile recalculation, then verify rejection with no stale output published. Unchanged inputs must still finish normally.

Full P0–P6 acceptance remains open. No official installer release.
