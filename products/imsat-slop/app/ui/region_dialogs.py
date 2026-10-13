from __future__ import annotations

from PySide6.QtWidgets import QDialog, QDialogButtonBox, QFormLayout, QLabel, QLineEdit, QMessageBox, QPlainTextEdit, QVBoxLayout

from app.core.geometry import clean_ring


class RegionCoordinateDialog(QDialog):
    def __init__(self, name="Region mới", points=None, parent=None):
        super().__init__(parent)
        self.setWindowTitle("Nhập tọa độ Region")
        self.resize(520, 560)
        layout = QVBoxLayout(self)
        form = QFormLayout()
        self.name = QLineEdit(name)
        form.addRow("Tên Region:", self.name)
        layout.addLayout(form)
        note = QLabel("Mỗi dòng nhập X, Y. Region sẽ tự đóng từ điểm cuối về điểm đầu. Tối thiểu 3 điểm.")
        note.setWordWrap(True)
        layout.addWidget(note)
        self.coordinates = QPlainTextEdit()
        if points:
            self.coordinates.setPlainText("\n".join(f"{x:.6f}, {y:.6f}" for x, y in points))
        layout.addWidget(self.coordinates)
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        layout.addWidget(buttons)
        self.points = []

    def accept(self):
        parsed = []
        try:
            for line_number, raw in enumerate(self.coordinates.toPlainText().splitlines(), 1):
                line = raw.strip()
                if not line:
                    continue
                if ";" in line:
                    fields = [item.strip().replace(",", ".") for item in line.split(";")]
                elif "\t" in line or ("," not in line and len(line.split()) == 2):
                    fields = [item.strip().replace(",", ".") for item in line.split()]
                else:
                    fields = [item.strip() for item in line.split(",")]
                if len(fields) != 2:
                    raise ValueError(f"Dòng {line_number} không có đúng hai tọa độ.")
                parsed.append((float(fields[0]), float(fields[1])))
            parsed = clean_ring(parsed)
            if len(parsed) < 3:
                raise ValueError("Region cần ít nhất 3 điểm khác nhau.")
        except ValueError as exc:
            QMessageBox.warning(self, "Tọa độ chưa hợp lệ", str(exc))
            return
        self.points = parsed
        super().accept()
