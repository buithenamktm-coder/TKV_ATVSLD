from PySide6.QtWidgets import QStackedWidget

from modules.cong_doan import CongDoanWidget


class Workspace(QStackedWidget):

    def __init__(self):
        super().__init__()

        # Màn hình Công đoạn
        self.cong_doan = CongDoanWidget()

        # Thêm vào Stack
        self.addWidget(self.cong_doan)

    def show_cong_doan(self):
        self.setCurrentWidget(self.cong_doan)