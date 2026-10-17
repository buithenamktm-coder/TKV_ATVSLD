"""Recreate user-provided IMSAT emblem raster from checked-in base64 source."""
from pathlib import Path
import base64
from PIL import Image

p=Path(__file__).resolve().parent/"assets"
(p/"imsat_icon.png").write_bytes(base64.b64decode((p/"imsat_icon.b64").read_text(encoding="ascii")))
Image.open(p/"imsat_icon.png").convert("RGBA").save(p/"imsatcad.ico",sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(96,96)])
