#!/usr/bin/env python3
"""Lossless-source PA3 raster adaptation to Windows setup icon/bitmap (QA only).

Exact approved raster bytes are verified before conversion. Geometry and colors
are not redesigned. These derivatives are NOT approved production vectors.
Requires Pillow: python -m pip install Pillow
"""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image

def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def contained(original, size, pad):
    image = original.convert("RGBA")
    maximum = (size[0] - 2 * pad, size[1] - 2 * pad)
    image.thumbnail(maximum, Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", size, "#FFFFFF")
    x = (size[0] - image.width) // 2
    y = (size[1] - image.height) // 2
    canvas.paste(image, (x, y), image)
    return canvas

def prepare(reference_dir, output_dir):
    root = Path(reference_dir)
    destination = Path(output_dir)
    manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
    if manifest.get("status", "").startswith("REFERENCE_ONLY") is False:
        raise ValueError("PA3 source must remain a reference asset")
    expected = {x["file"]: x["sha256"].lower() for x in manifest["assets"]}
    for name in ("image1.png", "image2.png", "image3.jpeg"):
        file = root / name
        if name not in expected or sha256(file) != expected[name]:
            raise ValueError("PA3 reference missing or hash mismatch: " + name)
    if destination.exists():
        raise ValueError("Refusing to overwrite a branding stage")
    destination.mkdir(parents=True)
    with Image.open(root / "image1.png") as src:
        contained(src, (164, 314), 10).save(destination / "welcome.bmp")
    with Image.open(root / "image2.png") as src:
        icon = contained(src, (256, 256), 10)
        icon.save(destination / "namvecta-setup.ico",
                  format="ICO", sizes=[(16, 16), (24, 24), (32, 32),
                                       (48, 48), (64, 64), (128, 128),
                                       (256, 256)])
    receipt = {
        "status": "PA3_REFERENCE_RASTER_ADAPTATION_ONLY",
        "approval": "PENDING_PRODUCTION_VECTOR_ICON_APPROVAL",
        "source_manifest": manifest.get("source_document"),
        "source_sha256": expected,
        "welcome_bmp_sha256": sha256(destination / "welcome.bmp"),
        "setup_ico_sha256": sha256(destination / "namvecta-setup.ico"),
    }
    (destination / "BRANDING_QA.json").write_text(
        json.dumps(receipt, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return receipt

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", default="docs/branding/namvecta-pa3-reference")
    parser.add_argument("--output", required=True)
    a = parser.parse_args()
    m = prepare(a.reference, a.output)
    print("PA3 brand rasters staged, SHA256:", m["setup_ico_sha256"])
    print("This is NOT production logo approval.")
