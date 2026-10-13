from __future__ import annotations

import faulthandler
import sys
import traceback
from datetime import datetime
from pathlib import Path

from PySide6.QtCore import QStandardPaths
from PySide6.QtWidgets import QMessageBox


def _log_dir() -> Path:
    base = QStandardPaths.writableLocation(QStandardPaths.StandardLocation.AppLocalDataLocation)
    path = Path(base or (Path.home() / ".imsat_slope_v6")) / "logs"
    path.mkdir(parents=True, exist_ok=True)
    return path


def install_exception_handler() -> Path:
    log_path = _log_dir() / f"session_{datetime.now():%Y%m%d_%H%M%S}.log"
    stream = log_path.open("a", encoding="utf-8")
    try:
        faulthandler.enable(stream)
    except Exception:
        pass

    def handler(exc_type, exc_value, exc_traceback):
        if issubclass(exc_type, KeyboardInterrupt):
            sys.__excepthook__(exc_type, exc_value, exc_traceback)
            return
        text = "".join(traceback.format_exception(exc_type, exc_value, exc_traceback))
        try:
            stream.write("\n" + text + "\n"); stream.flush()
        except Exception:
            pass
        try:
            QMessageBox.critical(None, "Phần mềm gặp lỗi", f"Đã ghi nhật ký lỗi tại:\n{log_path}\n\n{exc_value}")
        except Exception:
            sys.stderr.write(text)

    sys.excepthook = handler
    return log_path
