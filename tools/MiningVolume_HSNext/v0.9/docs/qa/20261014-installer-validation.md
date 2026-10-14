# Installer validation — MC-014 / MC-015

Baseline: `15407a62676b8c1bde92ce44d359b83740816e42`.
Scope: existing installer implementation and its Windows regression suite.
This does not complete multi-version installation or host acceptance.

## MC-014 — validate the actual AutoCAD loader configuration

The old validator searched XML text for version, series and startup tokens.
Those tokens could occur in comments or in a different component, while the
real module pointed somewhere else. The validator now parses the XML tree and
requires the current payload's exact application version, R24.2/Win64 scope at
both package and component levels, one component, and the known startup DLL.
Duplicate attributes, multiple roots, directives, wrong modules/versions,
extra components and empty required files are rejected. No broader CAD series
is enabled by this change. V1 and V2 metadata are both covered by tests.

## MC-015 — validate the whole ZIP before extraction

The old prefix-only traversal check did not cover the full Windows path model.
The new preflight rejects absolute/drive/UNC paths, parent/current segments,
device names, alternate data streams, trailing-dot/space aliases, case-insensitive
duplicates, file-parent conflicts and symbolic links. Every path is checked
before writing the first file. Extraction uses exclusive creation, propagates
close/write errors, and does not overwrite an existing staging file.

Installation now uses a fresh randomized staging directory instead of removing
a predictable PID-based directory before extraction. Bundle installation is
still performed only after payload validation succeeds.

## Verification and limits

- Local Python suite: 330 tests and 4 subtests PASS.
- Windows Go tests include malformed loader configurations, path aliases and
  traversal, ZIP links, valid extraction, preservation of existing files, empty
  DLL rejection, and extraction/loader/hash validation of the **actual embedded
  CI-built payload**, in addition to the existing registration rollback tests.
- V2 packaging repeats the embedded-payload test after changing the XML version
  to 2.0.0 and compressing the final V2 ZIP, before building the EXE. Both release
  payload checks require a nonempty build identity even for the legacy V1 path.
- Windows execution/build results must be taken from the Actions run for the
  commit containing this report. Source inspection is not a Windows test PASS.
- Test fixtures use temporary directories and an isolated HKCU registry key;
  they do not prove real AutoCAD installation, upgrades or uninstall behavior.

The current payload remains AutoCAD 2023-only. Legacy adapters, multi-CAD
module selection/install/rollback and real host acceptance remain open.
AutoCAD runtime: NOT TESTED. Official release: BLOCKED. No Setup is delivered
as a certified release from these tests alone.
