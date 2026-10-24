#!/usr/bin/env python3
"""Stage five independent AutoCAD bundles for a host-selective Inno installer.

Input must be five separately-built, SHA256-verified host directories.
Output is a QA staging tree. This is not evidence of Autodesk host acceptance.
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
import namvecta_bundle as core

def package_xml(year, name, module):
    series, _ = core.VERSIONS[year]
    package = ET.Element("ApplicationPackage", {
        "SchemaVersion": "1.0", "AutodeskProduct": "AutoCAD",
        "ProductType": "Application", "Name": "NAMVECTA VOLUME",
        "AppVersion": "2.0.0",
        "ProductCode": "{" + str(uuid.uuid5(uuid.NAMESPACE_URL, "namvecta-volume-addon-" + str(year))) + "}",
        "Description": "NAMVECTA VOLUME 2.0 for AutoCAD " + str(year),
        "Author": "Bùi Thế Nam",
    })
    comp = ET.SubElement(package, "Components")
    ET.SubElement(comp, "RuntimeRequirements", {
        "OS": "Win64", "Platform": "AutoCAD*",
        "SeriesMin": series, "SeriesMax": series,
    })
    ET.SubElement(comp, "ComponentEntry", {
        "AppName": "NAMVECTA VOLUME " + str(year),
        "Version": "2.0.0",
        "ModuleName": "./Contents/Windows/" + module,
        "LoadOnAutoCADStartup": "True"
    })
    tree = ET.ElementTree(package)
    ET.indent(tree, space="  ")
    return tree

def stage(stage_root, destination, source_sha):
    if not re.fullmatch(r"[0-9a-fA-F]{40}", source_sha):
        raise ValueError("Invalid source commit SHA")
    src = Path(stage_root).resolve()
    dst = Path(destination).absolute()
    if dst.exists() or dst.is_symlink():
        raise ValueError("Output already exists; refusing to overwrite: " + str(dst))
    if not src.is_dir():
        raise ValueError("Missing source stage")
    for year, (_, flavor) in core.VERSIONS.items():
        core.verify_input(src / flavor, year, source_sha)
    dst.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".namvecta-hosts-", dir=dst.parent) as temp:
        pending = Path(temp) / "Bundles"
        pending.mkdir()
        manifests = {}
        for year, (_, flavor) in core.VERSIONS.items():
            required, module = core.expected_files(year)
            bundle = pending / ("NAMVECTAVolume" + str(year) + ".bundle")
            win = bundle / "Contents" / "Windows"
            win.mkdir(parents=True)
            for name in required:
                shutil.copy2(src / flavor / name, win / name, follow_symlinks=False)
            package_xml(year, flavor, module).write(
                bundle / "PackageContents.xml", encoding="utf-8", xml_declaration=True)
            parsed = ET.parse(bundle / "PackageContents.xml").getroot()
            comps = parsed.findall("Components")
            if len(comps) != 1:
                raise ValueError("Unsafe package structure")
            rt = comps[0].find("RuntimeRequirements")
            if rt is None or rt.attrib["SeriesMin"] != core.VERSIONS[year][0] or (
                    rt.attrib["SeriesMax"] != core.VERSIONS[year][0]):
                raise ValueError("Incorrect host series")
            files = {}
            for item in sorted(bundle.rglob("*")):
                if item.is_file():
                    files[item.relative_to(bundle).as_posix()] = core.digest(item)
            manifests[str(year)] = {"series": core.VERSIONS[year][0],
                                    "flavor": flavor, "main_module": module,
                                    "files_sha256": files}
        (pending / "STAGING_QA.json").write_text(json.dumps({
            "status": "NOT_RELEASE_HOST_RUNTIME_UNVERIFIED",
            "product": "NAMVECTA VOLUME 2.0",
            "source_sha": source_sha,
            "host_years": list(core.VERSIONS),
            "bundles": manifests,
        }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        pending.rename(dst)
    return dst

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--source-sha", required=True)
    args = parser.parse_args()
    print("QA bundles staged: " + str(stage(args.stage, args.output, args.source_sha)))
    print("Windows Setup and AutoCAD runtime: NOT TESTED")

if __name__ == "__main__":
    main()
