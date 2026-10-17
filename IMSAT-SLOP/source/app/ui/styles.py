APP_STYLE = r"""
QMainWindow, QWidget { font-family: Arial; font-size: 10pt; color: #173B5E; }
QMainWindow { background: #F3F6F9; }
QMenuBar { background: #FFFFFF; border-bottom: 1px solid #D6E0E8; }
QMenuBar::item:selected, QMenu::item:selected { background: #DCEEFF; color: #075CA8; }
QToolBar { background: #FFFFFF; border-bottom: 1px solid #D6E0E8; spacing: 5px; padding: 4px; }
QToolButton { padding: 5px 8px; border-radius: 4px; }
QToolButton:hover, QToolButton:checked { background: #DCEEFF; color: #075CA8; }
QDockWidget { color: #173B5E; font-weight: bold; }
QDockWidget::title { background: #EDF3F8; padding: 7px; border: 1px solid #D6E0E8; }
QTreeWidget, QTableWidget, QListWidget { background: white; border: 1px solid #C9D6E2; alternate-background-color: #F6F9FB; }
QHeaderView::section { background: #EAF1F7; border: 0; border-right: 1px solid #D6E0E8; padding: 6px; font-weight: bold; }
QPushButton { background: white; border: 1px solid #BFCEDB; border-radius: 5px; padding: 6px 10px; }
QPushButton:hover { border-color: #1682D4; background: #EDF7FF; }
QPushButton#primary { color: white; background: #0878C9; border-color: #0878C9; font-weight: bold; }
QPushButton#primary:hover { background: #0969AD; }
QGroupBox { border: 1px solid #C9D6E2; border-radius: 6px; margin-top: 10px; padding-top: 8px; font-weight: bold; }
QGroupBox::title { subcontrol-origin: margin; left: 10px; padding: 0 5px; }
QStatusBar { background: #FFFFFF; border-top: 1px solid #D6E0E8; }
"""
