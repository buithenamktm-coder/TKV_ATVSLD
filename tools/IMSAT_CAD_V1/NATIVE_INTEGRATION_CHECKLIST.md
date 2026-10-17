# IMSAT CAD V1.0 — Native integration and acceptance matrix

Branch: `imsat-cad-foundation`. Scope: native QCAD CE Windows CAD, not Tkinter preview. This file is a worklist, **not evidence of acceptance**.

| Component | Implemented source | Required verification | Current gate |
|---|---|---|---|
| Draw / Modify / Annotation / Layers / Utilities | `qcad_addon/IMSATCAD/IMSATCAD.js` | Open Windows GUI; click every toolbar action; draw and edit real entities | BLOCKED: GUI test |
| AutoCAD-like aliases | `IMSATCAD._aliases` | Type each alias in QCAD command line; verify operation and parameters | BLOCKED: GUI test |
| COPY and EXTEND | No equivalent native workflow yet | Implement explicit copy/extend semantics, not alias to move/trim | NOT IMPLEMENTED |
| Properties panel | QCAD built-in property editor | Confirm selection, property edits, persistence | BLOCKED: GUI test |
| Command line | QCAD built-in command interface | Prompt flow, numeric and relative coordinates, ESC, repeat | BLOCKED: GUI test |
| DXF | QCAD native reader/writer | 20 open-edit-save-reopen cases with dimensions, layers, blocks, hatches | BLOCKED: sample test |
| VI/EN | Native QCAD translation plus IMSAT strings | Audit menus, actions, prompts and runtime switching | NOT VERIFIED |
| Printing | QCAD Print action | A3/A4, scale, lineweight, PDF, actual output | BLOCKED: GUI test |
| Installer | Native Windows ZIP currently exists | Build native Inno Setup installer; uninstall, shortcuts, licensing notices | NOT IMPLEMENTED |
| Licensing | QCAD CE GPLv3 | Ship license notices and corresponding source obligations | NOT VERIFIED |

## Release policy
- CI passing is evidence of a successful automated check, **not** GUI acceptance.
- No claim of full AutoCAD command parity until interactive functional tests pass.
- Do not distribute a Python preview installer as the native QCAD product.
- Any release blocker means V1.0 remains NOT READY.
- Do not modify IMSAT VOLUME.
