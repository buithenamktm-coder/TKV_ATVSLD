from PySide6.QtWidgets import (
    QWidget,
    QVBoxLayout,
    QHBoxLayout,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QMessageBox,
    QHeaderView,
)

from modules.cong_doan_form import CongDoanForm

from database.db import (
    get_all_cong_doan,
    insert_cong_doan,
    update_cong_doan,
    delete_cong_doan,
)


class CongDoanWidget(QWidget):

    def __init__(self):
        super().__init__()

        self.current_id = None

        layout = QVBoxLayout()

        # ==========================
        # Thanh công cụ
        # ==========================

        toolbar = QHBoxLayout()

        self.btn_them = QPushButton("➕ Thêm")
        self.btn_sua = QPushButton("✏️ Sửa")
        self.btn_xoa = QPushButton("❌ Xóa")
        self.btn_lam_moi = QPushButton("🔄 Làm mới")

        toolbar.addWidget(self.btn_them)
        toolbar.addWidget(self.btn_sua)
        toolbar.addWidget(self.btn_xoa)
        toolbar.addStretch()
        toolbar.addWidget(self.btn_lam_moi)

        layout.addLayout(toolbar)

        # ==========================
        # Bảng dữ liệu
        # ==========================

        self.table = QTableWidget()

        self.table.setColumnCount(4)

        self.table.setHorizontalHeaderLabels([
            "ID",
            "Mã công đoạn",
            "Tên công đoạn",
            "Mô tả"
        ])

        self.table.setColumnHidden(0, True)

        self.table.horizontalHeader().setStretchLastSection(True)
        self.table.horizontalHeader().setSectionResizeMode(
            QHeaderView.Stretch
        )

        layout.addWidget(self.table)

        self.setLayout(layout)

        # ==========================
        # Kết nối sự kiện
        # ==========================

        self.btn_them.clicked.connect(self.them_cong_doan)
        self.btn_sua.clicked.connect(self.sua_cong_doan)
        self.btn_xoa.clicked.connect(self.xoa_cong_doan)
        self.btn_lam_moi.clicked.connect(self.load_data)

        self.table.cellClicked.connect(self.chon_dong)

        self.load_data()

    # ==========================
    # Đọc dữ liệu
    # ==========================

    def load_data(self):

        rows = get_all_cong_doan()

        self.table.setRowCount(0)

        for row_index, row in enumerate(rows):

            self.table.insertRow(row_index)

            self.table.setItem(
                row_index,
                0,
                QTableWidgetItem(str(row["id"]))
            )

            self.table.setItem(
                row_index,
                1,
                QTableWidgetItem(row["ma"])
            )

            self.table.setItem(
                row_index,
                2,
                QTableWidgetItem(row["ten"])
            )

            self.table.setItem(
                row_index,
                3,
                QTableWidgetItem(row["mo_ta"])
            )
    # ==========================
    # Chọn dòng
    # ==========================

    def chon_dong(self, row, column):

        self.current_id = int(self.table.item(row, 0).text())

    # ==========================
    # Thêm công đoạn
    # ==========================

    def them_cong_doan(self):

        form = CongDoanForm()

        if form.exec() != form.Accepted:
            return

        data = form.get_data()

        if data["ma"].strip() == "":
            QMessageBox.warning(
                self,
                "Thông báo",
                "Chưa nhập mã công đoạn."
            )
            return

        if data["ten"].strip() == "":
            QMessageBox.warning(
                self,
                "Thông báo",
                "Chưa nhập tên công đoạn."
            )
            return

        try:

            insert_cong_doan(
                data["ma"],
                data["ten"],
                data["mo_ta"]
            )

            self.load_data()

            QMessageBox.information(
                self,
                "Thông báo",
                "Đã thêm công đoạn thành công."
            )

        except Exception as e:

            QMessageBox.critical(
                self,
                "Lỗi",
                str(e)
            )

    # ==========================
    # Sửa
    # ==========================

    def sua_cong_doan(self):

        if self.current_id is None:

            QMessageBox.warning(
                self,
                "Thông báo",
                "Chưa chọn công đoạn."
            )

            return

        row = self.table.currentRow()

        data = {
            "ma": self.table.item(row, 1).text(),
            "ten": self.table.item(row, 2).text(),
            "mo_ta": self.table.item(row, 3).text()
        }

        form = CongDoanForm(data)

        if form.exec() != form.Accepted:
            return

        data = form.get_data()

        update_cong_doan(
            self.current_id,
            data["ma"],
            data["ten"],
            data["mo_ta"]
        )

        self.load_data()

    # ==========================
    # Xóa
    # ==========================

    def xoa_cong_doan(self):

        if self.current_id is None:

            QMessageBox.warning(
                self,
                "Thông báo",
                "Chưa chọn công đoạn."
            )

            return

        reply = QMessageBox.question(
            self,
            "Xác nhận",
            "Bạn có chắc muốn xóa công đoạn này?"
        )

        if reply != QMessageBox.Yes:
            return

        delete_cong_doan(self.current_id)

        self.current_id = None

        self.load_data()

        QMessageBox.information(
            self,
            "Thông báo",
            "Đã xóa thành công."
        )