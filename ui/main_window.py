from PySide6.QtWidgets import (
    QMainWindow,
    QWidget,
    QVBoxLayout,
    QSplitter,
    QStackedWidget,
)

from ui.toolbar import create_toolbar
from ui.tree_menu import TreeMenu
from modules.cong_doan import CongDoanWidget


class MainWindow(QMainWindow):

    def __init__(self):
        super().__init__()

        self.setWindowTitle("PHẦN MỀM QUẢN LÝ ATVSLĐ TKV")
        self.resize(1400, 800)

        self.create_menu()
        create_toolbar(self)
        self.create_layout()

        self.statusBar().showMessage("Sẵn sàng")

    def create_menu(self):

        menu = self.menuBar()

        menu.addMenu("File")
        menu.addMenu("Danh mục")
        menu.addMenu("Nghiệp vụ")
        menu.addMenu("Báo cáo")
        menu.addMenu("Trợ giúp")

    def create_layout(self):

        central = QWidget()
        self.setCentralWidget(central)

        layout = QVBoxLayout(central)

        splitter = QSplitter()
        layout.addWidget(splitter)

        # Menu bên trái
        self.tree = TreeMenu()
        splitter.addWidget(self.tree)

        # Khu vực làm việc
        self.stack = QStackedWidget()
        splitter.addWidget(self.stack)

        splitter.setStretchFactor(0, 1)
        splitter.setStretchFactor(1, 5)

        # Trang Công đoạn
        self.page_cong_doan = CongDoanWidget()
        self.stack.addWidget(self.page_cong_doan)

        self.stack.setCurrentWidget(self.page_cong_doan)

        # Sự kiện chọn menu
        self.tree.itemClicked.connect(self.menu_clicked)

    def menu_clicked(self, item, column):

        text = item.text(0)

        if text == "Công đoạn":
            self.stack.setCurrentWidget(self.page_cong_doan)
            self.statusBar().showMessage("Quản lý Công đoạn")

        else:
            self.statusBar().showMessage(f"Module '{text}' đang phát triển")