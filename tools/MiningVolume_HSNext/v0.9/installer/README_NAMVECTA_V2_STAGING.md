# NAMVECTA VOLUME 2.0 — stage bundle for AutoCAD 2020–2024

**Status: tooling only. NOT a distributable Setup or a runtime-accepted product.**
These tools intentionally do **not** modify the historic IMSAT V1/V2 QA installer,
the previous `MiningVolume2023.bundle`, or the five-Pass mechanism.

## Why a distinct staging path?

Existing `installer/main.go` and `installer/build_v2_qa.ps1` remain
IMSAT/AutoCAD 2023-focused (legacy one-host, machine-bound signing workflow).
Do not rename their output or pretend that the old EXE is NAMVECTA.
The NAMVECTA V2 release needs exact 2020, 2021, 2022, 2023 and 2024
host binaries, each selected by its own Autodesk component requirements.

## Source layout

Before staging, populate all **five** directories from independently built
Release binaries on the same intended source SHA:

```text
verified-hosts/
  ACAD2020_NET47/
    IMSATVolume.ACAD2020_NET47.dll
    IMSATVolume.Cad.ACAD2020_NET47.dll
    MiningVolume.Core.dll
    MiningVolume.Surface.dll
    MiningVolume.Cdt.Native.dll
    LICENSE-CDT.txt
    NOTICE.txt
    BUILD_IDENTITY.txt
  ACAD2021_NET48/ ...
  ACAD2022_NET48/ ...
  ACAD2023_NET48/
    MiningVolume2023.dll
    MiningVolume.Cad2023.dll
    (same other files)
  ACAD2024_NET48/ ...
```

`BUILD_IDENTITY.txt` in every host folder must contain:

```text
SOURCE_SHA=<real 40-hex Git commit SHA>
<sha256 64 hex>  <dll filename>
```

Include **every** DLL exactly once. Source SHA alone is not a build guarantee;
the script checks actual per-file SHA-256. It rejects missing files, unknown
DLLs, host assemblies and symlinks, and never overwrites an existing output.

## Generate QA bundle (not the Setup)

```powershell
python installer/namvecta_bundle.py `
    --stage D:\build\verified-hosts `
    --output D:\build\qa\NAMVECTAVolume.bundle `
    --source-sha <commit-sha>
python -m unittest discover -s installer -p "test_namvecta_bundle.py" -v
```

The generated `PackageContents.xml` has five **independent** `Components`
sections, one per release `R23.1`, `R24.0`, `R24.1`, `R24.2` and
`R24.3`. Binary file names remain technical internal identifiers until a
separately regression-tested transition is possible. Visible brand names use
NAMVECTA.

## Hard release gates (not satisfied by this script)

1. Integrate approved PA3 **production vector/icon assets**, verified against
   the Word reference. The Word's raster images are not approved ICO/SVG.
2. Produce a Windows wizard with detected AutoCAD host choices, accurate
   install summary, progress, rollback, uninstall and data preservation.
3. Verify actual 64-bit host loading and full `MVSELFTEST` on each of
   AutoCAD 2020–2024. A successful compiler build or ZIP assembly is insufficient.
4. Test all five Pass levels, offline trial, upgrades and time rollback on
   genuine Windows user profiles (no MachineId/online service).
5. Prove end-to-end TIN, sections, grids, CAD/Excel and performance on real
   engineering drawings, including the selected reference fixtures.
6. Retain license and third-party notices. Do not label any unaccepted
   generated artifact a commercial release.

Autodesk package structure:
https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-BC76355D-682B-46ED-B9B7-66C95EEF2BD0.htm
