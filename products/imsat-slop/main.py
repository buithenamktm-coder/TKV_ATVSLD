from __future__ import annotations

import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) >= 2 and sys.argv[1] == "--solver-job":
        if len(sys.argv) != 4:
            print("Usage: main.py --solver-job input.issv6 output.issv6", file=sys.stderr)
            return 64
        from app.core.solver_job import run_solver_job
        return run_solver_job(sys.argv[2], sys.argv[3])

    from PySide6.QtWidgets import QApplication
    from app.core.crash_handler import install_exception_handler
    from app.ui.main_window import MainWindow

    app = QApplication(sys.argv)
    app.setApplicationName("IMSAT Slope Stability Professional V6.2")
    app.setOrganizationName("IMSAT")
    install_exception_handler()
    window = MainWindow()
    window.show()
    if len(sys.argv) > 1 and Path(sys.argv[1]).exists():
        window.load_model(sys.argv[1])
    return app.exec()


if __name__ == "__main__":
    raise SystemExit(main())
