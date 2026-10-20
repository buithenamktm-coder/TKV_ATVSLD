# IMSAT CAD V1 — Native Ribbon integration gate (2026-10-20)

## Verified baseline
- `tools/IMSAT_CAD_V1/app.py` is Tkinter/ezdxf Preview, **not** the target native CAD application.
- `qcad_addon/IMSATCAD/IMSATCAD.js` has `createRibbon()` with QTabWidget fallback to dockable toolbars; this is not evidence of successful GUI integration.
- `.github/workflows/imsat-cad-qcad-core.yml` builds pinned QCAD CE v3.32.2.0 and checks runtime dependencies, but build checks alone do not prove interactive Ribbon behavior.
- `V1_RELEASE_GATES.md` remains authoritative: V1 NOT READY until GUI, command and Windows acceptance gates pass.

## P0 — Single native executable
1. Keep Tkinter Preview clearly labeled POC; do not ship it as final V1.
2. Package the native Qt/QCAD-based application, required plugins, scripts, translations and dependencies in one Windows installer.
3. Launch the native executable from the installer and record actual binary, log and screenshot; do not claim the Preview EXE is the native engine.
4. Fail CI if plugin loading or any required script action fails.

## P1 — Ribbon, not ordinary toolbars
1. Replace the QTabWidget-in-QToolBar prototype with a reliable Qt ribbon-like dock/widget in the native main window.
2. Tabs: Home, Insert, Annotate, View, Manage, Output; grouped panels, icons, labels, responsive sizing at 1366x768 and 1920x1080.
3. Bind each visible control to an actual native CAD action; never display nonfunctional commands.
4. Verify switching tabs, selecting tools, using command aliases, switching VI/EN, drawing/editing, and saving/reopening on a real Windows desktop.
5. Preserve QCAD upstream notices and license requirements; branding must not misrepresent provenance.

## P2 — Identity and legal gate
1. Audit window title, About, icon, executable metadata, installer, shortcuts, dialogs, logs, scripts and distributed assets for product branding.
2. Replace user-facing product branding with IMSAT CAD where lawful; retain mandatory GPLv3 license, copyright notices, source offers and attribution.
3. Validate personal/commercial ownership and trademark permissions before release; do not remove legal notices to disguise upstream origin.
4. No DWG compatibility claims until a properly licensed implementation passes real-file tests.

## P3 — Release evidence
- 20-file DXF regression corpus; command/action smoke tests; Windows 10/11 x64 install/uninstall; 1366x768 and FullHD screenshots.
- Produce commit SHAs, CI run URLs, installer artifact link, SHA-256, test matrix and blocker list.
- **Release only if every V1_RELEASE_GATES.md blocker passes.** Otherwise publish an explicit NOT READY report.

## Implementation order
Native runtime -> working Ribbon -> user-facing identity/legal audit -> CAD operations regression -> installer -> one consolidated acceptance test.

This document records verified gaps and acceptance criteria; it is **not** an implementation or passing test report.
