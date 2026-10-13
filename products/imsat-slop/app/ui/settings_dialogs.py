from __future__ import annotations

from PySide6.QtCore import Qt
from PySide6.QtWidgets import (
    QCheckBox, QComboBox, QDialog, QDialogButtonBox, QDoubleSpinBox,
    QFormLayout, QHBoxLayout, QLabel, QPlainTextEdit, QPushButton,
    QSpinBox, QTableWidget, QTableWidgetItem, QTabWidget, QVBoxLayout, QWidget,
)

from app.core.models import DistributedLoad, GeoStudioModel


def _double(value=0.0, minimum=-1e9, maximum=1e9, decimals=5):
    box = QDoubleSpinBox(); box.setRange(minimum, maximum); box.setDecimals(decimals); box.setValue(float(value)); box.setKeyboardTracking(False); return box


def _integer(value=1, minimum=1, maximum=100000):
    box = QSpinBox(); box.setRange(minimum, maximum); box.setValue(int(value)); box.setKeyboardTracking(False); return box


class AnalysisDialog(QDialog):
    def __init__(self, model: GeoStudioModel, parent=None):
        super().__init__(parent); self.model = model; self.setWindowTitle("Khai báo phương pháp phân tích"); self.resize(490, 350)
        layout = QVBoxLayout(self); form = QFormLayout()
        self.method = QComboBox(); self.method.addItems(["Morgenstern-Price", "Bishop Simplified", "Janbu Simplified", "Fellenius"]); self.method.setCurrentText(model.analysis.method)
        self.direction = QComboBox(); self.direction.addItems(["Auto", "LeftToRight", "RightToLeft"]); self.direction.setCurrentText(model.analysis.slip_direction)
        self.side = QComboBox(); self.side.addItems(["Half-Sine", "Constant"]); self.side.setCurrentText(model.analysis.side_function)
        self.slices = _integer(model.analysis.slices, 5, 500)
        self.tol = _double(model.analysis.tolerance, 1e-12, 0.1, 10)
        self.iterations = _integer(model.analysis.max_iterations, 10, 2000)
        form.addRow("Phương pháp:", self.method); form.addRow("Hướng trượt:", self.direction); form.addRow("Hàm liên lát:", self.side); form.addRow("Số lát:", self.slices); form.addRow("Sai số hội tụ:", self.tol); form.addRow("Số vòng lặp tối đa:", self.iterations)
        layout.addLayout(form)
        note = QLabel("Morgenstern–Price sử dụng bộ giải độc lập Zhu–Lee–Qian–Chen. Đường chia lát chỉ được tạo sau khi chạy giải."); note.setWordWrap(True); layout.addWidget(note)
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel); buttons.accepted.connect(self.accept); buttons.rejected.connect(self.reject); layout.addWidget(buttons)

    def accept(self):
        analysis = self.model.analysis
        analysis.method = self.method.currentText(); analysis.slip_direction = self.direction.currentText(); analysis.side_function = self.side.currentText(); analysis.slices = self.slices.value(); analysis.tolerance = self.tol.value(); analysis.max_iterations = self.iterations.value()
        super().accept()


class SearchDialog(QDialog):
    def __init__(self, model: GeoStudioModel, parent=None):
        super().__init__(parent); self.model = model; self.setWindowTitle("Khai báo tìm kiếm mặt trượt"); self.resize(620, 650)
        layout = QVBoxLayout(self); tabs = QTabWidget(); layout.addWidget(tabs)

        entry = QWidget(); form = QFormLayout(entry)
        self.option = QComboBox(); self.option.addItems(["Entry and Exit", "Grid and Radius", "Auto-Locate", "Cuckoo Search", "Fully Specified"]); self.option.setCurrentText(model.search.option)
        self.entry_min = _double(model.search.entry_x_min); self.entry_max = _double(model.search.entry_x_max); self.exit_min = _double(model.search.exit_x_min); self.exit_max = _double(model.search.exit_x_max)
        self.entry_steps = _integer(model.search.entry_steps, 2, 100); self.exit_steps = _integer(model.search.exit_steps, 2, 100)
        self.depth_min = _double(model.search.min_depth, 0.01, 1e6); self.depth_max = _double(model.search.max_depth, 0.01, 1e6); self.depth_steps = _integer(model.search.depth_steps, 3, 200)
        self.random_trials = _integer(model.search.random_trials, 0, 100000); self.store = _integer(model.search.critical_surfaces_to_store, 1, 10000)
        form.addRow("Kiểu tìm kiếm:", self.option); form.addRow("Entry X nhỏ nhất:", self.entry_min); form.addRow("Entry X lớn nhất:", self.entry_max); form.addRow("Số bước Entry:", self.entry_steps); form.addRow("Exit X nhỏ nhất:", self.exit_min); form.addRow("Exit X lớn nhất:", self.exit_max); form.addRow("Số bước Exit:", self.exit_steps); form.addRow("Độ võng nhỏ nhất:", self.depth_min); form.addRow("Độ võng lớn nhất:", self.depth_max); form.addRow("Số bước độ võng:", self.depth_steps); form.addRow("Số mẫu ngẫu nhiên:", self.random_trials); form.addRow("Số kết quả lưu:", self.store)
        tabs.addTab(entry, "Entry / Exit")

        grid = QWidget(); gf = QFormLayout(grid)
        self.grid_x_min = _double(model.search.grid_x_min); self.grid_x_max = _double(model.search.grid_x_max); self.grid_y_min = _double(model.search.grid_y_min); self.grid_y_max = _double(model.search.grid_y_max)
        self.grid_x_steps = _integer(model.search.grid_x_steps, 2, 100); self.grid_y_steps = _integer(model.search.grid_y_steps, 2, 100)
        self.radius_min = _double(model.search.radius_min, 0.01, 1e9); self.radius_max = _double(model.search.radius_max, 0.01, 1e9); self.radius_steps = _integer(model.search.radius_steps, 2, 200)
        gf.addRow("Tâm X nhỏ nhất:", self.grid_x_min); gf.addRow("Tâm X lớn nhất:", self.grid_x_max); gf.addRow("Số bước X:", self.grid_x_steps); gf.addRow("Tâm Y nhỏ nhất:", self.grid_y_min); gf.addRow("Tâm Y lớn nhất:", self.grid_y_max); gf.addRow("Số bước Y:", self.grid_y_steps); gf.addRow("Bán kính nhỏ nhất:", self.radius_min); gf.addRow("Bán kính lớn nhất:", self.radius_max); gf.addRow("Số bước bán kính:", self.radius_steps)
        tabs.addTab(grid, "Grid / Radius")

        specified = QWidget(); sv = QVBoxLayout(specified)
        label = QLabel("Nhập các điểm X,Y của mặt trượt xác định, mỗi dòng một điểm, từ trái sang phải."); label.setWordWrap(True); sv.addWidget(label)
        self.specified = QPlainTextEdit(); self.specified.setPlainText("\n".join(f"{x:.6f}, {y:.6f}" for x, y in model.search.fully_specified_points)); sv.addWidget(self.specified)
        tabs.addTab(specified, "Fully Specified")

        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel); buttons.accepted.connect(self.accept); buttons.rejected.connect(self.reject); layout.addWidget(buttons)

    def accept(self):
        search = self.model.search
        search.option = self.option.currentText(); search.entry_x_min = self.entry_min.value(); search.entry_x_max = self.entry_max.value(); search.exit_x_min = self.exit_min.value(); search.exit_x_max = self.exit_max.value(); search.entry_steps = self.entry_steps.value(); search.exit_steps = self.exit_steps.value(); search.min_depth = self.depth_min.value(); search.max_depth = self.depth_max.value(); search.depth_steps = self.depth_steps.value(); search.random_trials = self.random_trials.value(); search.critical_surfaces_to_store = self.store.value()
        search.grid_x_min = self.grid_x_min.value(); search.grid_x_max = self.grid_x_max.value(); search.grid_y_min = self.grid_y_min.value(); search.grid_y_max = self.grid_y_max.value(); search.grid_x_steps = self.grid_x_steps.value(); search.grid_y_steps = self.grid_y_steps.value(); search.radius_min = self.radius_min.value(); search.radius_max = self.radius_max.value(); search.radius_steps = self.radius_steps.value()
        points = []
        try:
            for raw in self.specified.toPlainText().splitlines():
                raw = raw.strip()
                if not raw: continue
                if ";" in raw:
                    parts = [part.strip().replace(",", ".") for part in raw.split(";")]
                elif "\t" in raw or ("," not in raw and len(raw.split()) == 2):
                    parts = [part.strip().replace(",", ".") for part in raw.split()]
                else:
                    parts = [part.strip() for part in raw.split(",")]
                if len(parts) != 2: raise ValueError
                points.append((float(parts[0]), float(parts[1])))
        except ValueError:
            points = []
        search.fully_specified_points = points
        super().accept()


class WaterLoadDialog(QDialog):
    def __init__(self, model: GeoStudioModel, parent=None):
        super().__init__(parent); self.model = model; self.setWindowTitle("Nước, tải trọng và động đất"); self.resize(760, 560)
        layout = QVBoxLayout(self); tabs = QTabWidget(); layout.addWidget(tabs)
        water = QWidget(); wf = QFormLayout(water); self.water_enabled = QCheckBox("Sử dụng đường/mực nước"); self.water_enabled.setChecked(model.water.enabled); self.water_level = _double(model.water.level); self.gamma_water = _double(model.water.gamma_water, 0.1, 100); wf.addRow(self.water_enabled); wf.addRow("Cao độ mực nước:", self.water_level); wf.addRow("Dung trọng nước:", self.gamma_water); tabs.addTab(water, "Nước")
        seismic = QWidget(); sf = QFormLayout(seismic); self.seis_enabled = QCheckBox("Phân tích giả tĩnh"); self.seis_enabled.setChecked(model.seismic.enabled); self.kh = _double(model.seismic.kh, -2, 2); self.kv = _double(model.seismic.kv, -2, 2); sf.addRow(self.seis_enabled); sf.addRow("Kh:", self.kh); sf.addRow("Kv:", self.kv); tabs.addTab(seismic, "Động đất")
        loads = QWidget(); lv = QVBoxLayout(loads); self.load_table = QTableWidget(max(1, len(model.loads)), 6); self.load_table.setHorizontalHeaderLabels(["Dùng", "Mã", "X đầu", "X cuối", "Tải (kN/m)", "Góc từ đứng"])
        items = model.loads or [DistributedLoad()]
        for row, load in enumerate(items): self._fill_load_row(row, load)
        lv.addWidget(self.load_table)
        controls = QHBoxLayout(); add = QPushButton("Thêm dòng tải"); delete = QPushButton("Xóa dòng cuối"); add.clicked.connect(self._add_row); delete.clicked.connect(self._delete_row); controls.addWidget(add); controls.addWidget(delete); controls.addStretch(); lv.addLayout(controls)
        tabs.addTab(loads, "Tải phân bố")
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel); buttons.accepted.connect(self.accept); buttons.rejected.connect(self.reject); layout.addWidget(buttons)

    def _fill_load_row(self, row, load):
        check = QTableWidgetItem(); check.setFlags(check.flags() | Qt.ItemFlag.ItemIsUserCheckable); check.setCheckState(Qt.CheckState.Checked if load.enabled else Qt.CheckState.Unchecked); self.load_table.setItem(row, 0, check)
        for column, value in enumerate([load.load_id, load.x_start, load.x_end, load.magnitude, load.angle_from_vertical_deg], 1): self.load_table.setItem(row, column, QTableWidgetItem(str(value)))

    def _add_row(self):
        row = self.load_table.rowCount(); self.load_table.insertRow(row); self._fill_load_row(row, DistributedLoad(load_id=f"LOAD-{row+1:02d}"))

    def _delete_row(self):
        if self.load_table.rowCount() > 1: self.load_table.removeRow(self.load_table.rowCount()-1)

    def accept(self):
        self.model.water.enabled = self.water_enabled.isChecked(); self.model.water.level = self.water_level.value(); self.model.water.gamma_water = self.gamma_water.value(); self.model.seismic.enabled = self.seis_enabled.isChecked(); self.model.seismic.kh = self.kh.value(); self.model.seismic.kv = self.kv.value(); loads = []
        for row in range(self.load_table.rowCount()):
            def text(column, default="0"):
                item = self.load_table.item(row, column); return item.text() if item else default
            try:
                check = self.load_table.item(row, 0)
                loads.append(DistributedLoad(load_id=text(1, f"LOAD-{row+1:02d}"), enabled=bool(check and check.checkState() == Qt.CheckState.Checked), x_start=float(text(2).replace(",", ".")), x_end=float(text(3).replace(",", ".")), magnitude=float(text(4).replace(",", ".")), angle_from_vertical_deg=float(text(5).replace(",", "."))))
            except ValueError: continue
        self.model.loads = loads; super().accept()
