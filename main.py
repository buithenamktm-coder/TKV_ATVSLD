import sys
from database.db import create_tables
from PySide6.QtWidgets import QApplication

from ui.main_window import MainWindow

app = QApplication(sys.argv)

create_tables()          # Đưa lên đây

window = MainWindow()

window.show()

sys.exit(app.exec())