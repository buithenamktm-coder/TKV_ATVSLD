# Edited section cancellation — 2026-10-23

Base commit: 733b018b3558635a52f40663903fc946122299a5.

ReadEditedProfiles now polls for cancellation before filtering every 256 ModelSpace objects, during polyline decoding every 256 vertices, between profile rebuilds and before returning. UpdateAreaLabels polls during the scan, between writes and immediately before committing its existing single transaction. Native progress uses the existing host-thread SectionCadProgress; no background database access or DoEvents was introduced. Dense TIN faces remain unopened. Geometry and volume formulas are unchanged.

The AutoCAD regression fixture now checks unchanged geometry after re-reading CAD profiles, canceled reads at first/middle/final callback, and rollback of genuinely changed label text at first/middle/final callback. These assertions compile but have NOT run in an AutoCAD host here.

| Gate | Current evidence |
|---|---|
| COMPILE_PASS | AutoCAD 2023 plugin Release/net48 with UseAutoCADNuGet=true; 0 warnings, 0 errors |
| AUTOMATED_TEST_PASS | Python reference + compatibility: 382 passed, 3 skipped, 4 subtests passed |
| HOST_RUNTIME_BLOCKED | Linux workspace has no Windows AutoCAD runtime; native regression and physical Esc behavior unverified |
| FUNCTIONAL_RELEASE_PASS | NOT established; no final Setup released |

Commands from tools/MiningVolume_HSNext/v0.9:

```sh
python -m pytest tests/reference compat/test_compatibility.py -q
/tmp/imsat-dotnet/dotnet build src/MiningVolume.Plugin2023/MiningVolume.Plugin2023.csproj -c Release -p:UseAutoCADNuGet=true -m:1 --disable-build-servers
```

An initial SDK launch aborted in Process.GetStat in this container; retry with unchanged source succeeded. Three Python skips require Windows (PowerShell parser, RSA provisioning, executable compatibility packaging). Raw successful logs are adjacent. No new performance measurements or runtime acceptance are claimed. Earlier benchmark numbers are not measurements of this change.
