from PySide6.QtWidgets import (
    QDialog,
    QLabel,
    QLineEdit,
    QTextEdit,
    QPushButton,
    QFormLayout,
    QHBoxLayout,
    QVBoxLayout
)


class CongDoanForm(QDialog):

    def __init__(self, data=None):
        super().__init__()

        self.setWindowTitle("Công đoạn sản xuất")
        self.resize(500, 260)

        self.txt_ma = QLineEdit()
        self.txt_ten = QLineEdit()
        self.txt_mota = QTextEdit()

        if data:
            self.txt_ma.setText(data["ma"])
            self.txt_ten.setText(data["ten"])
            self.txt_mota.setPlainText(data["mo_ta"])

        form = QFormLayout()

        form.addRow(QLabel("Mã công đoạn"), self.txt_ma)
        form.addRow(QLabel("Tên công đoạn"), self.txt_ten)
        form.addRow(QLabel("Mô tả"), self.txt_mota)

        self.btn_luu = QPushButton("Lưu")
        self.btn_huy = QPushButton("Hủy")

        self.btn_luu.clicked.connect(self.accept)
        self.btn_huy.clicked.connect(self.reject)

        button = QHBoxLayout()
        button.addStretch()
        button.addWidget(self.btn_luu)
        button.addWidget(self.btn_huy)

        layout = QVBoxLayout()
        layout.addLayout(form)
        layout.addLayout(button)

        self.setLayout(layout)

    def get_data(self):

        return {
            "ma": self.txt_ma.text(),
            "ten": self.txt_ten.text(),
            "mo_ta": self.txt_mota.toPlainText()
        }