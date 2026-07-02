from PySide6.QtWidgets import QToolBar
from PySide6.QtGui import QAction


def create_toolbar(window):

    toolbar = QToolBar("Thanh công cụ")

    window.addToolBar(toolbar)

    toolbar.addAction(QAction("Thêm", window))
    toolbar.addAction(QAction("Sửa", window))
    toolbar.addAction(QAction("Xóa", window))
    toolbar.addSeparator()

    toolbar.addAction(QAction("Lưu", window))
    toolbar.addAction(QAction("Làm mới", window))
    toolbar.addSeparator()

    toolbar.addAction(QAction("Excel", window))
    toolbar.addAction(QAction("PDF", window))
    toolbar.addAction(QAction("In", window))

    return toolbar