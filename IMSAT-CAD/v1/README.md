# IMSAT CAD V1.0 — Windows Preview (English / Tiếng Việt)

**Status:** preview build source, *not* a full CAD product. The installer is produced by a Windows build with PyInstaller and Inno Setup 6. Do not confuse the source archive with a compiled Windows Setup.

## Features

- Standalone 2D DXF drawing with no AutoCAD dependency; powered by open-source [ezdxf](https://github.com/mozman/ezdxf) (MIT).
- UI switch between **Tiếng Việt / English** at runtime; translated menus, buttons, dialogs and prompts.
- Uses user-provided **IMSAT application emblem** as window icon, executable icon and installer icon. This image was cropped from the user-provided 'Biểu tượng ứng dụng IMSAT Mine Planner.png'; replace when the official transparent IMSAT logo is supplied.
- Open DXF, show lines, circles, arcs, polylines, points, 3D faces and basic text; other DXF entities are *preserved* in the underlying drawing but not yet rendered (e.g. BLOCK INSERT / HATCH / dimensions).
- Draw line, polyline, circle; select/delete; layers; undo/redo; zoom extents, scroll zoom, right-click pan.
- Safe **Save As** only; original DXF is never overwritten. Output undergoes audit and re-open validation.

## Not included

- DWG read/write; 3D shaded TIN renderer; TIN triangulation; mining volume calculation; advanced snapping; layout/printing; full CAD symbol/block reference rendering; QCAD GUI integration. **This V1 preview currently does not use the QCAD engine**, only the `ezdxf` CAD library. The QCAD/Qt engine integration is a separate, significantly larger workstream.
- Large dataset full rendering; currently capped at 30,000 entities. For engineering use, geometry and performance require additional validation. Currently V1 opens and saves DXF, not DWG.

## Testing (Linux)

`pip install ezdxf pillow pytest`

`xvfb-run -a python -m pytest -q tests` (run `PYTHONPATH=.`)

`python app.py` to open the application.

## Windows installer build

1. Install Python 3.12, Inno Setup 6.
2. Run `powershell -ExecutionPolicy Bypass -File build-windows.ps1` from this directory.
3. Output is `installer-output/IMSAT_CAD_V1.0_Preview_Setup_x64.exe` after a successful build.

Alternatively use the GitHub Actions workflow `IMSAT CAD V1 Preview Windows Installer` on branch `imsat-cad-foundation` and download the artifact from the **successful run**. Never claim a Windows installer exists until build jobs succeed and produce the artifact.

## Licenses

- IMSAT CAD preview app code: Copyright 2026, prototype authored for IMSAT; inspect product ownership and distribution permission before commercial release.
- ezdxf: MIT — https://github.com/mozman/ezdxf
- Pillow: HPND — https://github.com/python-pillow/Pillow
- Python Tcl/Tk libraries: respective open-source licenses.
- IMSAT emblem: user-provided image, ownership and brand use require authorization.
- PyInstaller: GPL-2.0 with exception, Inno Setup: separate distribution license.

## Command line / Dòng lệnh kiểu AutoCAD (đang phát triển)

Trong thanh **Dòng lệnh**, nhập tên lệnh hoặc alias và nhấn Enter. Một số ví dụ:

| Alias | Lệnh | Cú pháp thử nghiệm |
|---|---|---|
| `L` | LINE | `L 0,0 100,0` |
| `PL` | PLINE | `PL 0,0 @20,0 @0,10` |
| `C` | CIRCLE | `C 0,0 25` |
| `REC` | RECTANG | `REC 0,0 100,50` |
| `A` | ARC | `A 0,0 15 0 90` |
| `M` | MOVE | Chọn đối tượng, `M 10,5` |
| `CO` | COPY | Chọn đối tượng, `CO 10,5` |
| `RO` | ROTATE | Chọn đối tượng, `RO 30 0,0` |
| `SC` | SCALE | Chọn đối tượng, `SC 2 0,0` |
| `MI` | MIRROR | Chọn đối tượng, `MI 0,0 0,10` |
| `O` | OFFSET | Chọn LINE/CIRCLE, `O 5` |
| `TR` | TRIM | Chọn LINE, `TR <handle-đường-cắt> 10,0` |
| `EX` | EXTEND | Chọn LINE, `EX <handle-ranh-giới> 10,0` |
| `DI` | DIST | `DI 0,0 3,4` |
| `Z` | ZOOM EXTENTS | `Z E` |
| `U` | UNDO | `U` |

Gõ đơn độc `L`, `PL`, `C`, `REC` để vẽ bằng chuột. Hỗ trợ `@dx,dy` và `@length<angle` cho tọa độ nhập trên dòng lệnh. Không có tính năng trong bảng này được coi là hoàn thiện tương đương AutoCAD; đây là mô hình thử nghiệm. Đặc biệt TRIM và EXTEND chưa có thao tác chọn ranh giới bằng chuột, OFFSET hiện chỉ hỗ trợ LINE/CIRCLE.

**Chú ý:** Các lệnh chưa triển khai trả về thông báo rõ ràng, không hiển thị kết quả giả. Tiêu chí để gọi bản V1.0 chính thức nằm ở [V1_RELEASE_GATES.md](V1_RELEASE_GATES.md).