from PySide6.QtWidgets import QTreeWidget, QTreeWidgetItem


class TreeMenu(QTreeWidget):

    def __init__(self):
        super().__init__()

        self.setHeaderLabel("DANH MỤC")

        self.cd = QTreeWidgetItem(["Công đoạn"])
        self.cv = QTreeWidgetItem(["Công việc"])
        self.tb = QTreeWidgetItem(["Thiết bị"])
        self.nl = QTreeWidgetItem(["Người lao động"])
        self.mn = QTreeWidgetItem(["Mối nguy"])
        self.rr = QTreeWidgetItem(["Đánh giá rủi ro"])
        self.bp = QTreeWidgetItem(["Biện pháp"])

        self.addTopLevelItems([
            self.cd,
            self.cv,
            self.tb,
            self.nl,
            self.mn,
            self.rr,
            self.bp
        ])