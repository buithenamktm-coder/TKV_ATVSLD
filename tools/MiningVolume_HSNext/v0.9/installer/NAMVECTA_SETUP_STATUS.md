# NAMVECTA VOLUME 2.0 — Setup source checkpoint

- Status: **QA SOURCE ONLY / no signed EXE or Autodesk host acceptance**.
- Inno Setup script: `installer/NAMVECTA_Volume_V2.iss`.
- Language overlay: `installer/lang/NAMVECTA_Vietnamese.isl` (English fallback for untranslated default messages).
- Staging: `installer/namvecta_host_bundles.py`. This emits **one separate bundle per supported AutoCAD year**, so that the checkbox selection controls installed files rather than only the UI.
- The old IMSAT Go `main.go` and `build_v2_qa.ps1` are intentionally not renamed as NAMVECTA. They still install only R24.2 and depend on legacy RSA packaging.
- Logo input must match the user-provided PA3 raster reference; the raster-derived icon is provisional until production artwork is approved.
- Installation checks copied manifest and main DLL; cannot prove actual AutoCAD start or MVSELFTEST.
- A successful file-copy wizard must not be described as a fully accepted AutoCAD host.
- No uninstall operation deletes drawings, reports, or old IMSAT bundles. Legacy installer migration still requires an audited compatibility policy.
- Before release: verify copyright/publisher wording, signature, all DLL hashes, Windows x64 installer execution, host loading on all 2020–2024 releases, Excel and T01–T22.

Build example from the project root:

```powershell
python installer/namvecta_host_bundles.py --stage D:\verified-flavors --output D:\NAMVECTA\Bundles --source-sha <real-sha>
# Add a verified PA3-derived Branding/welcome.bmp and Branding/namvecta-setup.ico.
ISCC /DStageDir="D:\NAMVECTA" installer\NAMVECTA_Volume_V2.iss
```

This build produces `NAMVECTA-VOLUME-2.0-Setup-QA-NOT-RELEASE.exe`.
The final filename `NAMVECTA-VOLUME-2.0-Setup.exe` is reserved for a future release passing all gates.
