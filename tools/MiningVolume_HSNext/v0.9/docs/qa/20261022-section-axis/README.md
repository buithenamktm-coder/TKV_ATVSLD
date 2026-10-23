# Section axis termination and final cancellation — 2026-10-22

Baseline: 3005ca98473c8186786f43c5b539f44a9b0a0ae6. Its GitHub Actions run 39716301697 completed successfully; that is not host acceptance.

Audit found that the existing 250-label budget did not guarantee loop termination. With baseZ=topZ=1000000 and step=1e-12, the budget computes one tick, but baseZ+step equals baseZ in double precision. Both official and quick-section renderers used repeated floating addition. A malformed/imported configuration could therefore continue allocating annotations until cancellation or failure. This is a demonstrated numeric failure mechanism, not a claim that it caused the reported field freeze.

Both renderers now obtain an explicitly bounded axis list, calculated from integer indices. The helper rejects invalid ranges, budgets over 250 and non-increasing/unrepresentable steps. It never changes the chosen step or volume-band computation. Ordinary fractional steps retain endpoints and strictly increasing ticks. The official renderer additionally polls cancellation immediately before commit, including when invoked without the UI's outer transaction.

| Review | Evidence |
|---|---|
| Build | AutoCAD 2023 net48, zero warnings/errors; build.log |
| Numerical regression | 132 C# cases, zero failures; includes fractional/negative elevation axis, exact budget limit, over-budget, NaN/Infinity and unrepresentable steps; core.log |
| Reference/compatibility regression | 342 passed, 4 subtests passed, 1 PowerShell case skipped; python.log |
| CAD regression | Direct renderer final-poll rollback test added and compiled; NOT EXECUTED |
| Runtime / installer | HOST_RUNTIME_BLOCKED; no AutoCAD/Windows available; no official Setup |

Initial SDK startup aborted before compiling in Process.GetStat; unchanged-source retry succeeded. Both logs retained. No before/after AutoCAD timing or memory result is available. No claim of complete P0–P6 or FUNCTIONAL_RELEASE_PASS. Section Block grouping and host editing/save/reopen remain open in the integrated matrix.
