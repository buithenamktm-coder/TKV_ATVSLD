# IMSAT CAD — foundation (P0, DXF interoperability)

This directory contains an independently testable DXF backend for the future IMSAT CAD platform. It is **not** a standalone 2D/3D CAD editor, does not read or write DWG, and is not a fork of QCAD. The goal is to establish repeatable DXF regression tests before choosing / integrating a CAD GUI engine.

## Features implemented

- Open existing DXF and report modelspace entities, layers, DXF version, units, and audit results.
- Add 3D polylines and supplied TIN triangles as native 3DFACE entities, without flattening Z.
- Reject non-finite coordinates and degenerate XY-projected triangles, without partially modifying the DXF document.
- Save only to an independent file, with a new DXF audit after writing; refuse input overwrite.
- Automated regression tests on save / reopen, preservation of ordinary entities, units, error handling, and 3D elevations.

## Run tests

Install Python dependencies: python -m pip install -r requirements.txt

Run: python -m pytest -q tests

## Important scope / license decisions

- Proposed upstream CAD application: https://github.com/qcad/qcad ; **QCAD core is GPLv3**. Its proprietary DWG features are not available from the open community repository. A commercial closed-source derivative requires a separate license/architecture review. Never remove upstream license notices.
- Proposed 3D renderer: https://github.com/Kitware/VTK (BSD-3-Clause). Rendering and a benchmark with real TINs have not yet been integrated.
- These first tests use ezdxf (MIT). They do not establish lossless DWG interoperability or compatibility with every DXF extension.
- A real product still requires CAD UI engine integration, Windows builds, DWG I/O under suitable license, regression corpus, performance benchmarks, native installer, and runtime acceptance testing.

## P0 acceptance status

| Item | State |
| --- | --- |
| DXF open + inspect | Implemented, tested |
| DXF safe save-as + audit | Implemented, tested |
| 3D polyline / TIN face storage | Implemented, tested |
| QCAD runtime build for Windows | Not attempted |
| DWG read/write | Not implemented |
| 3D viewer, mining workflows | Not implemented |
