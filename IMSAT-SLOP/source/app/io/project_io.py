from __future__ import annotations

from pathlib import Path

from app.core.models import GeoStudioModel

FORMAT_EXTENSION = ".issv6"


def save_project(model: GeoStudioModel, path: str | Path) -> Path:
    target = Path(path)
    if target.suffix.lower() != FORMAT_EXTENSION:
        target = target.with_suffix(FORMAT_EXTENSION)
    model.save(target)
    return target


def load_project(path: str | Path) -> GeoStudioModel:
    return GeoStudioModel.load(path)
