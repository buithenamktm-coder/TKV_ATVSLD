#!/usr/bin/env python3
"""Stage an AutoCAD 2020–2024 NAMVECTA bundle from verified release DLLs.

The result is a QA candidate, NOT a Windows Setup and NOT runtime acceptance.
Does not rename DLLs or claim approval of the PA3 production vector/icons.
"""
import argparse
import hashlib
import json
import re
import shutil
import tempfile
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

VERSIONS = {
    2020: ("R23.1", "ACAD2020_NET47"),
    2021: ("R24.0", "ACAD2021_NET48"),
    2022: ("R24.1", "ACAD2022_NET48"),
    2023: ("R24.2", "ACAD2023_NET48"),
    2024: ("R24.3", "ACAD2024_NET48"),
}
SHARED = {"MiningVolume.Core.dll", "MiningVolume.Surface.dll",
          "MiningVolume.Cdt.Native.dll", "NOTICE.txt",
          "LICENSE-CDT.txt", "BUILD_IDENTITY.txt"}
AUTODESK_HOST_DLLS = {"acmgd.dll", "acdbmgd.dll", "accoremgd.dll",
                     "acwindows.dll", "adwindows.dll"}

def expected_files(year):
    flavor = VERSIONS[year][1]
    main = "MiningVolume2023.dll" if year == 2023 else "IMSATVolume." + flavor + ".dll"
    adapter = "MiningVolume.Cad2023.dll" if year == 2023 else "IMSATVolume.Cad." + flavor + ".dll"
    return SHARED | {main, adapter}, main

def digest(file):
    h = hashlib.sha256()
    with file.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()

def verify_input(directory, year, sha):
    if not directory.is_dir() or directory.is_symlink():
        raise ValueError("Missing/unsafe stage directory for AutoCAD " + str(year))
    required, main = expected_files(year)
    present = {p.name for p in directory.iterdir()}
    if present != required:
        raise ValueError("Stage %s has missing/foreign files: %s" %
                         (year, sorted(present ^ required)))
    for name in required:
        item = directory / name
        if item.is_symlink() or not item.is_file() or item.stat().st_size == 0:
            raise ValueError("Empty, link or non-file in stage: " + str(item))
    if any(x.lower() in AUTODESK_HOST_DLLS for x in present):
        raise ValueError("Autodesk runtime assemblies must never be distributed")
    identity = (directory / "BUILD_IDENTITY.txt").read_text(encoding="utf-8-sig")
    line_list = identity.replace("\r", "").split("\n")
    if not line_list or line_list[0] != "SOURCE_SHA=" + sha:
        raise ValueError("Incorrect source SHA for AutoCAD " + str(year))
    actual = {}
    for line in line_list[1:]:
        if not line.strip():
            continue
        match = re.fullmatch(r"([a-fA-F0-9]{64})  ([A-Za-z0-9._-]+\.dll)", line)
        if not match or match.group(2) in actual:
            raise ValueError("Invalid build identity line for " + str(year))
        actual[match.group(2)] = match.group(1).lower()
    expected_dlls = {n for n in required if n.lower().endswith(".dll")}
    if set(actual) != expected_dlls:
        raise ValueError("DLL inventory does not match hashes for " + str(year))
    for name in expected_dlls:
        if digest(directory / name) != actual[name]:
            raise ValueError("DLL hash mismatch: " + str(year) + " / " + name)
    return main

def make_package(root, sha):
    pkg = ET.Element("ApplicationPackage", {
        "SchemaVersion": "1.0", "AutodeskProduct": "AutoCAD",
        "ProductType": "Application", "Name": "NAMVECTA VOLUME",
        "AppVersion": "2.0.0",
        "ProductCode": "{" + str(uuid.uuid5(uuid.NAMESPACE_URL,
                                  "namvecta.volume.autocad.2020-2024")) + "}",
        "Description": "NAMVECTA VOLUME 2.0 - terrain and mining volumes",
        "Author": "Bùi Thế Nam",
    })
    ET.SubElement(pkg, "RuntimeRequirements", {"OS": "Win64",
        "Platform": "AutoCAD*", "SeriesMin": "R23.1", "SeriesMax": "R24.3"})
    for year, (series, flavor) in VERSIONS.items():
        _, dll = expected_files(year)
        # AutoCAD schema uses one Components section PER host family,
        # each with its own RuntimeRequirements + ComponentEntry.
        group = ET.SubElement(pkg, "Components")
        ET.SubElement(group, "RuntimeRequirements", {
            "OS": "Win64", "Platform": "AutoCAD*",
            "SeriesMin": series, "SeriesMax": series
        })
        ET.SubElement(group, "ComponentEntry", {
            "AppName": "NAMVECTA VOLUME " + str(year),
            "Version": "2.0.0",
            "ModuleName": "./Contents/Windows/" + flavor + "/" + dll,
            "LoadOnAutoCADStartup": "True"
        })
    # ElementTree escapes all untrusted values; avoid fragile text substitutions.
    tree = ET.ElementTree(pkg)
    ET.indent(tree, space="  ")
    tree.write(root / "PackageContents.xml", encoding="utf-8", xml_declaration=True)
    files = []
    for item in sorted(root.rglob("*")):
        if item.is_file():
            files.append({"path": item.relative_to(root).as_posix(),
                          "sha256": digest(item), "bytes": item.stat().st_size})
    manifest = {
        "product": "NAMVECTA VOLUME 2.0",
        "status": "QA_CANDIDATE_NOT_RELEASE",
        "source_sha": sha,
        "autocad_years": list(VERSIONS),
        "host_runtime": "NOT_TESTED",
        "logo_pa3_production": "NOT_APPROVED",
        "setup_wizard": "NOT_BUILT",
        "files": files,
    }
    (root / "NAMVECTA_BUNDLE_QA_MANIFEST.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return manifest

def build(stage, output, sha):
    if not re.fullmatch(r"[0-9a-fA-F]{40}", sha):
        raise ValueError("SOURCE_SHA must be an existing 40-digit commit identifier")
    stage, output = Path(stage).resolve(), Path(output).absolute()
    if output.exists() or output.is_symlink():
        raise ValueError("Refusing to overwrite an existing bundle: " + str(output))
    if not stage.is_dir():
        raise ValueError("Stage root is missing")
    for year, (_, flavor) in VERSIONS.items():
        verify_input(stage / flavor, year, sha)
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".namvecta-qa-", dir=output.parent) as tmp:
        root = Path(tmp) / "NAMVECTAVolume.bundle"
        for year, (_, flavor) in VERSIONS.items():
            dest = root / "Contents" / "Windows" / flavor
            shutil.copytree(stage / flavor, dest, symlinks=False)
        manifest = make_package(root, sha)
        root.rename(output)
    return manifest

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage", required=True, help="Directory containing five verified flavor subdirectories")
    parser.add_argument("--output", required=True, help="New NAMVECTAVolume.bundle destination")
    parser.add_argument("--source-sha", required=True)
    a = parser.parse_args()
    m = build(a.stage, a.output, a.source_sha)
    print("QA BUNDLE STAGED (NOT RELEASE): " + str(a.output))
    print("AutoCAD releases: " + ", ".join(map(str, m["autocad_years"])))
    print("Host runtime, logo asset and Setup acceptance: NOT VERIFIED")

if __name__ == "__main__":
    main()
