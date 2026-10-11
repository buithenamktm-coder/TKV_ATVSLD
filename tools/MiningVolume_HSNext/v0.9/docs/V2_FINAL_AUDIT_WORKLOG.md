# Final audit work log — 2026-10-12

Branch: `miningvolume-v2-chatgpt-p0-continuation`. Baseline fetched and verified: `c04de7b3ff434fb29373943edf5b228c30d8b35a`. V1 branch unchanged. No release approval.

## Implementation batch 1

- RF-01: register Frame/Edit/Area RegApps inside direct export transaction.
- RF-04/05: bind editable polylines and area labels to drawing fingerprint and original handle. Moved labels update by ID; copied, missing and duplicate labels fail before writing. Legacy metadata fails closed, retaining CAD geometry (save a copy before re-export).
- RF-03: edited profiles carry provenance; do not sample a TIN midpoint for an edited interval. Compute each elevation-band contribution once; interval and level summaries sum the same contributions. XLSX adds `Chi tiết tầng` and linked totals. This explicitly applies the agreed adaptive formulas **per band**, not the generally different nonlinear formula applied to the aggregate end areas.
- RF-03: reject nonfinite/oversized elevation bands, no silent 100,000-band truncation.
- RF-04: reread CAD geometry and check calculation parameters before committing an asynchronous update.
- RF-02/13: mineral reports have unit, category, reporting period, successful calculation timestamp/duration, separate export time, two blank signature fields and IMSAT logo. Do not shift four-sheet mineral data starts. Mineral formulas adapt after area edits.
- RF-13: invalidating volume/grid clears calculation metadata.
- RF-14: stage XLSX and branding before atomic replacement; failed branding preserves the previously accepted workbook.
- RF-08: Go linker parameters replace global source rewriting for QA version, backup suffix and installed executable name. One registry entry for the shared bundle. Keep old bundle until registration succeeds; restore registration/files on failure. Version-check uninstall against installed bundle.

## Actual local evidence

Linux, SDK 8.0.425/runtime 8.0.31, real C# Core/Surface + native CDT and production report builders/writer/branding (no CAD mock): **73/73 PASS**. Python static/reference: **228/228 PASS**. These are not AutoCAD runtime tests.

New independent checks: 10 m section, +1 m at both ends => +10 m²; one end => +5 m². Two profiles 20 m apart, both +1 m => +200 m³. Multiband nonlinear fixture expected `1000 + 20*(50+10+sqrt(500))/3`, identical interval and level totals. Three seams at 2 m², 20 m spacing, densities 1.1/1.2/1.3 => 120 m³ and 144 t. Three actual XLSX archives validate XML, metadata, signatures, font, formulas and logo; failed-logo export keeps previous bytes.

Windows Go tests added for bundle validation, rollback of registration/files without touching a synthetic DWG, and interrupted backup recovery. **Not yet run locally (Linux); await Windows CI.**

## Still open — no release claim

RF-06 legacy edited sections need a safe migration and stronger source-context identity; drawing identity alone does not prove source revision. RF-10 actual docking/DPI remains NOT TESTED. RF-11 catalogue authoritative review pending. RF-09 actual issuer PUBLIC key not provisioned; activation acceptance BLOCKED. Excel Desktop recalc/print NOT TESTED. Windows interactive install/upgrade/uninstall and AutoCAD T01–T22 host flows NOT TESTED. CI must be rerun on the final pushed SHA. This document records implementation evidence, not closure of all RF findings or release gates.
