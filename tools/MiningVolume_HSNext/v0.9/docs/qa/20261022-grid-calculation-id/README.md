# Grid calculation identity — 2026-10-22

Baseline: 047e8a2b5a5b33b07bc3566792fbda868e8618d5. Its Windows CI run 39627368536 completed successfully.

GridVolumeReport now owns one read-only CalculationId. Re-exporting the result reuses that ID; recalculation produces a new ID. The Excel information sheet displays the ID. Each native CAD cell polyline stores the same ID as XData under IMSAT_GRID_CALCULATION. Text labels are not tagged. Multiple exports retain their individual identities. No visible entity is added for metadata, and formulas/quantity algorithms are unchanged.

The CAD exporter registers the application once in its existing transaction and reuses one ResultBuffer. XData introduces one native assignment per cell; its host time and DWG size overhead have NOT been measured. This is an output traceability fix, not a claimed performance improvement. Persisted DWG save/reopen still requires host verification. Mineral outputs and combined grid export orchestration are outside this change.

## Evidence

- AUTOMATED_TEST_PASS: 127 executable C# core/report tests, zero failures; 333 Python reference/static checks passed.
- New C# regression writes a real XLSX, parses worksheet XML, and verifies that a later recalculation cannot replace the original workbook ID. Both calculations retain the same volume on identical terrain.
- COMPILE_PASS: AutoCAD 2023 net48 add-in built against Autodesk references, zero warnings/errors. Initial CLI attempt failed while reading process information; unchanged-source retry succeeded. Both logs retained.
- HOST_RUNTIME_BLOCKED: the added side-database regression exports two grid results and checks per-cell XData identities and entity counts. It compiles but has NOT run here. AutoCAD interaction, save/reopen, performance, Windows installation and end-to-end CAD/Excel acceptance remain unverified.
- FUNCTIONAL_RELEASE_PASS: NOT CLAIMED. No official Setup released.

Commands (project root):

```sh
python tests/core/run_core.py --dotnet-root /tmp/imsat-dotnet
python -m pytest tests/reference -q
/tmp/imsat-dotnet/dotnet build src/MiningVolume.Plugin2023/MiningVolume.Plugin2023.csproj -c Release -p:UseAutoCADNuGet=true -m:1 --disable-build-servers
```
