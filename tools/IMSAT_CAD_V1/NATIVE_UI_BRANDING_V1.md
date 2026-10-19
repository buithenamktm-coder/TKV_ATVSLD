# IMSAT CAD V1.0 — Native UI and branding implementation contract

Status: APPROVED DESIGN DIRECTION; NOT IMPLEMENTED OR GUI-ACCEPTED.
Branch: `imsat-cad-foundation`. Preserve the working QCAD-based DXF editing core.

## Reference and boundaries
The user's nine screenshots of VinaCAD 2026.9.4 are *functional and layout references only*.
Do not copy VinaCAD code, proprietary icons, imagery, trade dress, binaries or login mechanisms.
Retain QCAD GPLv3 license notices, copyright, source and redistribution obligations.
The application's primary visible identity should be IMSAT CAD V1.0; attribution to the
underlying QCAD engine remains accessible in About / license notices.
Use the existing `assets/imsat_icon.b64` / `generate-brand.py` assets; do not fabricate a new logo.

## UI structure
- Main window: dark neutral theme, legible contrast, compact controls, no ALL CAPS labels.
- Ribbon tabs: Home, Insert, Annotate, View, Manage, 3D Modeling, IMSAT Tools.
- Home groups: Draw, Modify, Layers, Properties, Block; actual actions only.
- Insert: Block and references, but do not advertise unsupported DWG/XREF capabilities.
- Annotate: Text, dimensions, leaders, tables only where engine supports them.
- View: Zoom, Pan, navigation and panels; property panel on the right.
- Manage: language, aliases, settings, licensing notices.
- 3D Modeling: show disabled with an explicit future-phase indication; QCAD CE is 2D.
- IMSAT Tools: future integration area for separately implemented mining workflows.
- Preserve full-size CAD canvas, dockable Layer List and Property Editor on right,
  command line at bottom, coordinate/snap/status bar.
- Ribbon must be collapsible; minimum acceptance resolutions: 1366x768 and 1920x1080.
- English/Vietnamese switch must actually update UI; persist selection across restarts.
- Icons and button labels must remain readable at 100%, 125%, and 150% scaling.

## Engineering approach
1. Establish a baseline from the last user-tested installer: app launches, opens,
   edits, saves and reopens DXF. Capture commit, artifact hash and startup logs.
2. Build a native Qt ribbon shell using **original** code around the existing QCAD
   actions. Do not simply rename or remove QCAD's functional toolbars until
   equivalent ribbon controls are wired and tested.
3. Map each ribbon button to a registered, verified `RGuiAction`; disabled when
   unavailable. Never create inert controls or silently remap commands.
4. Implement main-window title and app icon through supported Qt/QCAD APIs;
   confirm title persists while switching documents. Keep engine attribution in About.
5. Implement translation resources for both languages and language persistence.
6. Package icons, Qt plugins, translations, and shell resources into the *native*
   installer. Preserve qcadjsapi loader smoke tests.
7. UI screenshot comparison and real keyboard/mouse tests on Windows, including
   a DXF round-trip and startup from installed Desktop shortcut.

## Release gates
- No missing Qt plugin or JavaScript handler.
- IMSAT CAD V1.0 title and IMSAT logo visible; QCAD attribution preserved.
- Every enabled ribbon control performs its advertised operation.
- Command line remains accessible and unchanged; AutoCAD alias parity is a
  separate, not-yet-accepted workstream.
- Existing DXF test remains PASS after changes; no regressions.
- Native installer/uninstaller works, with real desktop shortcut.
- Any unsupported 3D/DWG feature is not represented as operational.
- CI-only GUI window creation is insufficient: a real interactive Windows test
  is mandatory before declaring V1.0 ready.
