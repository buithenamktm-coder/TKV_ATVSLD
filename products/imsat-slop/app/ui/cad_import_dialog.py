from __future__ import annotations

from PySide6.QtCore import Qt
from PySide6.QtWidgets import (
    QComboBox, QDialog, QDialogButtonBox, QLabel, QTableWidget,
    QTableWidgetItem, QVBoxLayout,
)

from app.io.cad_model_import import COMBINED_ROLE
from app.io.cad_reader import CadModel

ROLES = [
    'Chưa gán',
    COMBINED_ROLE,
    'Địa hình',
    'Region kín',
    'Ranh giới địa chất',
    'Mực nước',
    'Không nhập',
]


class CadImportDialog(QDialog):
    def __init__(self, cad: CadModel, parent=None):
        super().__init__(parent)
        self.cad = cad
        self.setWindowTitle('Ánh xạ layer DWG/DXF')
        self.resize(820, 540)
        layout = QVBoxLayout(self)
        label = QLabel(
            'Chọn vai trò cho từng layer. Với mặt cắt mỏ là một polyline kín, '
            'hãy dùng “Mặt cắt kín (Region + địa hình)”. Phần mềm tự chọn vai trò '
            'này khi nhận diện được polyline kín.'
        )
        label.setWordWrap(True)
        layout.addWidget(label)
        self.table = QTableWidget(len(cad.layers), 5)
        self.table.setHorizontalHeaderLabels([
            'Layer', 'Vai trò', 'Số đối tượng', 'Polyline kín', 'Tổng chiều dài'
        ])
        layout.addWidget(self.table)
        by_layer = {layer: [e for e in cad.entities if e.layer == layer] for layer in cad.layers}
        populated_layers = sum(1 for entities in by_layer.values() if entities)
        single_layer = populated_layers == 1
        for row, layer in enumerate(cad.layers):
            self.table.setItem(row, 0, QTableWidgetItem(layer))
            combo = QComboBox()
            combo.addItems(ROLES)
            lower = layer.lower()
            entities = by_layer[layer]
            closed_count = sum(1 for e in entities if e.closed and len(e.points) >= 3)
            if closed_count and (single_layer or not any(k in lower for k in ('water','muc nuoc','mực nước','phreatic'))):
                combo.setCurrentText(COMBINED_ROLE)
            elif any(k in lower for k in ('terrain','dia hinh','địa hình','mat dat','ground')):
                combo.setCurrentText('Địa hình')
            elif any(k in lower for k in ('region','vat lieu','material','lithology')):
                combo.setCurrentText('Region kín')
            elif any(k in lower for k in ('water','muc nuoc','mực nước','phreatic')):
                combo.setCurrentText('Mực nước')
            self.table.setCellWidget(row, 1, combo)
            self.table.setItem(row, 2, QTableWidgetItem(str(len(entities))))
            self.table.setItem(row, 3, QTableWidgetItem(str(closed_count)))
            length = sum(
                sum(((a[0]-b[0])**2 + (a[1]-b[1])**2)**0.5 for a, b in zip(e.points, e.points[1:]))
                for e in entities
            )
            self.table.setItem(row, 4, QTableWidgetItem(f'{length:.3f}'))
        self.table.resizeColumnsToContents()
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        layout.addWidget(buttons)

    def mapping(self):
        return {
            self.table.item(row, 0).text(): self.table.cellWidget(row, 1).currentText()
            for row in range(self.table.rowCount())
        }
