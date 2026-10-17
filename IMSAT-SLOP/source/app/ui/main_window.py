from __future__ import annotations

import json
import math
import os
import sys
from pathlib import Path

from PySide6.QtCore import QProcess, QStandardPaths, Qt
from PySide6.QtGui import QAction, QActionGroup, QColor, QIcon, QPixmap
from PySide6.QtWidgets import (
    QAbstractItemView, QApplication, QDialog, QDockWidget, QFileDialog, QHBoxLayout,
    QLabel, QListWidget, QListWidgetItem, QMainWindow, QMessageBox, QProgressBar,
    QPushButton, QTableWidget, QTableWidgetItem, QTreeWidget, QTreeWidgetItem,
    QVBoxLayout, QWidget,
)

from app.core.geometry import assign_material, upper_envelope, validate_model
from app.algorithms.solver import preflight_analysis
from app.core.models import GeoRegion, GeoStudioModel, SearchRange
from app.io import cad_to_model, load_project, read_cad, read_gsz, save_project
from app.reports import export_html_report, export_results_csv, export_slices_csv
from .cad_import_dialog import CadImportDialog
from .engineering_canvas import EngineeringCanvas, ViewMode
from .materials_dialog import MaterialsDialog
from .region_dialogs import RegionCoordinateDialog
from .settings_dialogs import AnalysisDialog, SearchDialog, WaterLoadDialog
from .styles import APP_STYLE


class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.model = GeoStudioModel()
        self.active_material_id: int | None = self.model.materials[0].source_id
        self.project_path: Path | None = None
        self.process: QProcess | None = None
        self._solver_input: Path | None = None
        self._solver_output: Path | None = None
        self._solver_log: Path | None = None
        self._stdout_buffer = ""
        self._solver_error_details: list[str] = []
        self.display_reference = False

        self.setWindowTitle("IMSAT Slope Stability Professional V6.2")
        self.resize(1540, 930)
        self.setStyleSheet(APP_STYLE)
        logo = Path(__file__).resolve().parents[2] / "assets" / "logo_imsat.png"
        self.setWindowIcon(QIcon(str(logo)))

        self.canvas = EngineeringCanvas()
        self.setCentralWidget(self.canvas)
        self.canvas.region_clicked.connect(self._region_clicked)
        self.canvas.cursor_moved.connect(self._cursor_moved)
        self.canvas.search_range_changed.connect(self._range_changed)

        self._create_actions()
        self._create_menus()
        self._create_toolbar()
        self._create_explorer_dock()
        self._create_material_dock()
        self._create_results_dock()
        self._create_status_widgets()
        self.set_model(self.model, fit=True)
        self.statusBar().showMessage("Sẵn sàng — mở GSZ, DWG/DXF hoặc dự án IMSAT để bắt đầu.")

    # ---------- UI construction ----------
    def _action(self, text, slot=None, shortcut=None, checkable=False):
        action = QAction(text, self)
        action.setCheckable(checkable)
        if shortcut:
            action.setShortcut(shortcut)
        if slot:
            action.triggered.connect(slot)
        return action

    def _create_actions(self):
        self.a_new = self._action("Dự án mới", self.new_project, "Ctrl+N")
        self.a_open_gsz = self._action("Mở mô hình GeoStudio…", self.open_gsz, "Ctrl+G")
        self.a_open_project = self._action("Mở dự án IMSAT…", self.open_project, "Ctrl+O")
        self.a_import_cad = self._action("Nhập DWG/DXF…", self.import_cad, "Ctrl+I")
        self.a_save = self._action("Lưu dự án", self.save_current, "Ctrl+S")
        self.a_save_as = self._action("Lưu dự án thành…", self.save_as, "Ctrl+Shift+S")
        self.a_exit = self._action("Thoát", self.close)

        self.a_materials = self._action("Khai báo vật liệu…", self.define_materials)
        self.a_analysis = self._action("Phương pháp phân tích…", self.define_analysis)
        self.a_search = self._action("Tìm kiếm mặt trượt…", self.define_search)
        self.a_water = self._action("Nước, tải trọng và động đất…", self.define_water_loads)

        self.a_add_region = self._action("Tạo Region bằng tọa độ…", self.add_region)
        self.a_edit_region = self._action("Sửa Region đang chọn…", self.edit_region)
        self.a_delete_region = self._action("Xóa Region đang chọn", self.delete_region)

        self.mode_group = QActionGroup(self)
        self.mode_group.setExclusive(True)
        self.a_define = self._action("Geometry", lambda: self.set_mode(ViewMode.DEFINE), checkable=True)
        self.a_assign = self._action("Gán vật liệu", lambda: self.set_mode(ViewMode.ASSIGN), checkable=True)
        self.a_draw_entry = self._action("Vẽ vùng Entry", lambda: self.set_mode(ViewMode.DRAW_ENTRY), checkable=True)
        self.a_draw_exit = self._action("Vẽ vùng Exit", lambda: self.set_mode(ViewMode.DRAW_EXIT), checkable=True)
        self.a_results = self._action("Kết quả", lambda: self.set_mode(ViewMode.RESULTS), checkable=True)
        for action in (self.a_define, self.a_assign, self.a_draw_entry, self.a_draw_exit, self.a_results):
            self.mode_group.addAction(action)
        self.a_define.setChecked(True)

        self.a_validate = self._action("Kiểm tra mô hình", self.validate_current, "F7")
        self.a_run = self._action("Chạy phân tích", self.run_analysis, "F5")
        self.a_cancel = self._action("Dừng phân tích", self.cancel_analysis)
        self.a_cancel.setEnabled(False)

        self.a_fit = self._action("Hiển thị toàn bộ", self.canvas.fit_all, "F")
        self.a_vertices = self._action("Hiện điểm nút", lambda checked: self._toggle_canvas("show_vertices", checked), checkable=True)
        self.a_labels = self._action("Hiện tên Region", lambda checked: self._toggle_canvas("show_labels", checked), checkable=True)
        self.a_slices = self._action("Hiện lát tính", lambda checked: self._toggle_canvas("show_slices", checked), checkable=True)
        self.a_slices.setChecked(True)
        self.a_water_view = self._action("Hiện mực nước", lambda checked: self._toggle_canvas("show_water", checked), checkable=True)
        self.a_water_view.setChecked(True)
        self.a_loads_view = self._action("Hiện tải trọng", lambda checked: self._toggle_canvas("show_loads", checked), checkable=True)
        self.a_loads_view.setChecked(True)

        self.a_export_html = self._action("Xuất báo cáo HTML…", self.export_html)
        self.a_export_results = self._action("Xuất danh sách mặt trượt CSV…", self.export_results)
        self.a_export_slices = self._action("Xuất lát tới hạn CSV…", self.export_slices)
        self.a_about = self._action("Giới thiệu", self.about)

    def _create_menus(self):
        menu = self.menuBar().addMenu("Tệp")
        menu.addActions([self.a_new, self.a_open_project, self.a_open_gsz, self.a_import_cad])
        menu.addSeparator(); menu.addActions([self.a_save, self.a_save_as]); menu.addSeparator(); menu.addAction(self.a_exit)
        menu = self.menuBar().addMenu("Khai báo")
        menu.addActions([self.a_materials, self.a_analysis, self.a_search, self.a_water])
        menu = self.menuBar().addMenu("Vẽ / Gán")
        menu.addActions([self.a_define, self.a_add_region, self.a_edit_region, self.a_delete_region])
        menu.addSeparator(); menu.addActions([self.a_assign, self.a_draw_entry, self.a_draw_exit])
        menu = self.menuBar().addMenu("Hình học")
        menu.addActions([self.a_validate, self.a_fit, self.a_vertices, self.a_labels])
        menu = self.menuBar().addMenu("Phân tích")
        menu.addActions([self.a_analysis, self.a_search, self.a_run, self.a_cancel])
        menu = self.menuBar().addMenu("Kết quả")
        menu.addActions([self.a_results, self.a_slices]); menu.addSeparator(); menu.addActions([self.a_export_html, self.a_export_results, self.a_export_slices])
        menu = self.menuBar().addMenu("Hiển thị")
        menu.addActions([self.a_fit, self.a_water_view, self.a_loads_view])
        menu = self.menuBar().addMenu("Trợ giúp")
        menu.addAction(self.a_about)

    def _create_toolbar(self):
        bar = self.addToolBar("Nghiệp vụ")
        bar.setMovable(False)
        bar.addActions([self.a_open_gsz, self.a_import_cad, self.a_materials, self.a_define, self.a_assign, self.a_draw_entry, self.a_draw_exit])
        bar.addSeparator(); bar.addActions([self.a_validate, self.a_run, self.a_cancel, self.a_results, self.a_fit])

    def _create_explorer_dock(self):
        dock = QDockWidget("Analysis Explorer", self)
        self.explorer = QTreeWidget(); self.explorer.setHeaderHidden(True)
        self.explorer.itemClicked.connect(self._explorer_clicked)
        dock.setWidget(self.explorer)
        self.addDockWidget(Qt.DockWidgetArea.LeftDockWidgetArea, dock)
        self.explorer_dock = dock

    def _create_material_dock(self):
        dock = QDockWidget("Vật liệu / Gán", self)
        panel = QWidget(); layout = QVBoxLayout(panel)
        self.active_material_label = QLabel("Vật liệu đang cầm: —")
        self.active_material_label.setWordWrap(True)
        layout.addWidget(self.active_material_label)
        self.material_list = QListWidget()
        self.material_list.itemClicked.connect(self._material_selected)
        layout.addWidget(self.material_list)
        buttons = QHBoxLayout()
        define = QPushButton("Khai báo…"); define.clicked.connect(self.define_materials)
        assign = QPushButton("Bật Gán"); assign.setObjectName("primary"); assign.clicked.connect(lambda: self.set_mode(ViewMode.ASSIGN))
        buttons.addWidget(define); buttons.addWidget(assign); layout.addLayout(buttons)
        hint = QLabel("Cách gán: chọn vật liệu trong danh sách rồi nhấp trực tiếp vào Region trên mặt cắt.")
        hint.setWordWrap(True); layout.addWidget(hint)
        dock.setWidget(panel)
        self.addDockWidget(Qt.DockWidgetArea.RightDockWidgetArea, dock)
        self.material_dock = dock

    def _create_results_dock(self):
        dock = QDockWidget("Slip Surfaces", self)
        panel = QWidget(); layout = QVBoxLayout(panel)
        self.result_source = QLabel("Chưa có kết quả IMSAT")
        layout.addWidget(self.result_source)
        self.results_table = QTableWidget(0, 6)
        self.results_table.setHorizontalHeaderLabels(["STT", "Fs", "Phương pháp", "Hội tụ", "Vòng lặp", "Lambda"])
        self.results_table.setSelectionBehavior(QAbstractItemView.SelectionBehavior.SelectRows)
        self.results_table.setSelectionMode(QAbstractItemView.SelectionMode.SingleSelection)
        self.results_table.cellClicked.connect(self._result_selected)
        layout.addWidget(self.results_table)
        ref_button = QPushButton("Hiện kết quả tham chiếu GSZ")
        ref_button.clicked.connect(self.show_reference_results)
        layout.addWidget(ref_button)
        dock.setWidget(panel)
        self.addDockWidget(Qt.DockWidgetArea.LeftDockWidgetArea, dock)
        self.results_dock = dock

    def _create_status_widgets(self):
        self.progress = QProgressBar(); self.progress.setRange(0, 100); self.progress.setValue(0); self.progress.setFixedWidth(230); self.progress.hide()
        self.statusBar().addPermanentWidget(self.progress)
        self.coord_label = QLabel("X=— | Y=—")
        self.statusBar().addPermanentWidget(self.coord_label)

    # ---------- model / views ----------
    def set_model(self, model: GeoStudioModel, fit=True):
        self.model = model
        self.display_reference = False
        if not self.model.terrain and self.model.regions:
            self.model.terrain = upper_envelope(self.model.regions)
        if self.model.materials and self.model.material_by_id(self.active_material_id) is None:
            self.active_material_id = self.model.materials[0].source_id
        self.canvas.set_model(self.model, fit=fit)
        self._refresh_all()

    def _refresh_all(self):
        self._refresh_explorer()
        self._refresh_materials()
        self._refresh_results()
        self.canvas.redraw()
        self._update_title()

    def _update_title(self):
        name = self.model.name if self.model else "Dự án mới"
        dirty_path = f" — {self.project_path.name}" if self.project_path else ""
        self.setWindowTitle(f"IMSAT Slope Stability Professional V6.2 — {name}{dirty_path}")

    def _refresh_explorer(self):
        self.explorer.clear()
        root = QTreeWidgetItem([self.model.name]); root.setData(0, Qt.ItemDataRole.UserRole, ("project", None))
        analysis = QTreeWidgetItem([f"Phân tích: {self.model.analysis.method}"]); analysis.setData(0, Qt.ItemDataRole.UserRole, ("analysis", None)); root.addChild(analysis)
        geometry = QTreeWidgetItem([f"Geometry ({len(self.model.regions)} Region)"]); geometry.setData(0, Qt.ItemDataRole.UserRole, ("geometry", None)); root.addChild(geometry)
        for region in self.model.regions:
            child = QTreeWidgetItem([f"{region.source_id} — {region.name}"]); child.setData(0, Qt.ItemDataRole.UserRole, ("region", region.source_id)); geometry.addChild(child)
        materials = QTreeWidgetItem([f"Vật liệu ({len(self.model.materials)})"]); materials.setData(0, Qt.ItemDataRole.UserRole, ("materials", None)); root.addChild(materials)
        results = QTreeWidgetItem([f"Kết quả IMSAT ({len(self.model.results)})"]); results.setData(0, Qt.ItemDataRole.UserRole, ("results", None)); root.addChild(results)
        reference = QTreeWidgetItem([f"Tham chiếu GSZ ({len(self.model.reference_surfaces)})"]); reference.setData(0, Qt.ItemDataRole.UserRole, ("reference", None)); root.addChild(reference)
        self.explorer.addTopLevelItem(root); root.setExpanded(True); geometry.setExpanded(True)

    @staticmethod
    def _color_icon(color: str):
        pixmap = QPixmap(28, 18); pixmap.fill(QColor(color)); return QIcon(pixmap)

    def _refresh_materials(self):
        self.material_list.blockSignals(True); self.material_list.clear()
        current_row = 0
        for row, material in enumerate(self.model.materials):
            item = QListWidgetItem(self._color_icon(material.color), f"{material.name}\nγ={material.unit_weight:g}; C={material.cohesion:g}; φ={material.phi:g}°")
            item.setData(Qt.ItemDataRole.UserRole, material.source_id)
            self.material_list.addItem(item)
            if material.source_id == self.active_material_id:
                current_row = row
        if self.material_list.count(): self.material_list.setCurrentRow(current_row)
        self.material_list.blockSignals(False)
        material = self.model.material_by_id(self.active_material_id)
        self.active_material_label.setText(f"Vật liệu đang cầm: {material.name if material else '—'}")

    def _displayed_results(self):
        if self.display_reference and self.model.reference_surfaces:
            return self.model.reference_surfaces
        return self.model.results if self.model.results else self.model.reference_surfaces

    def _refresh_results(self):
        results = self._displayed_results()
        source = "GeoStudio tham chiếu" if self.display_reference and self.model.reference_surfaces else ("IMSAT" if self.model.results else ("GeoStudio tham chiếu" if self.model.reference_surfaces else "Chưa có kết quả"))
        self.result_source.setText(f"Nguồn: {source} | {len(results)} mặt trượt")
        self.results_table.setRowCount(len(results))
        for row, result in enumerate(results):
            values = [result.number, f"{result.fs:.5f}" if math.isfinite(result.fs) else "—", result.method, "Có" if result.converged else "Không", result.iterations, "—" if result.lambda_value is None else f"{result.lambda_value:.5f}"]
            for column, value in enumerate(values):
                item = QTableWidgetItem(str(value)); item.setData(Qt.ItemDataRole.UserRole, row); self.results_table.setItem(row, column, item)
        self.results_table.resizeColumnsToContents()
        selected = self.model.reference_critical if self.display_reference else (self.model.critical or self.model.reference_critical)
        self.canvas.set_surface(selected)

    def set_mode(self, mode: ViewMode):
        mode = ViewMode(mode)
        self.canvas.set_mode(mode)
        action = {ViewMode.DEFINE: self.a_define, ViewMode.ASSIGN: self.a_assign, ViewMode.DRAW_ENTRY: self.a_draw_entry, ViewMode.DRAW_EXIT: self.a_draw_exit, ViewMode.RESULTS: self.a_results}[mode]
        action.setChecked(True)
        if mode == ViewMode.ASSIGN:
            self.material_dock.show(); self.material_dock.raise_()
            self.statusBar().showMessage("Chọn vật liệu rồi nhấp trực tiếp vào Region.")
        elif mode in {ViewMode.DRAW_ENTRY, ViewMode.DRAW_EXIT}:
            self.statusBar().showMessage("Nhấp hai điểm trên địa hình để xác định khoảng tìm kiếm.")
        elif mode == ViewMode.RESULTS:
            self.results_dock.show(); self.results_dock.raise_()
        else:
            self.statusBar().showMessage("Chế độ Geometry — chỉ hiển thị biên Region thật, không hiển thị lát tính.")

    # ---------- file operations ----------
    def new_project(self):
        model = GeoStudioModel(name="Dự án mới")
        self.project_path = None
        self.active_material_id = model.materials[0].source_id
        self.set_model(model, fit=True)

    def open_gsz(self):
        path, _ = QFileDialog.getOpenFileName(self, "Mở mô hình GeoStudio", "", "GeoStudio (*.gsz);;Tất cả (*.*)")
        if path: self.load_model(path)

    def open_project(self):
        path, _ = QFileDialog.getOpenFileName(self, "Mở dự án IMSAT", "", "IMSAT V6 (*.issv6);;Tất cả (*.*)")
        if not path: return
        try:
            self.project_path = Path(path)
            self.set_model(load_project(path), fit=True)
        except Exception as exc:
            QMessageBox.critical(self, "Không mở được dự án", str(exc))

    def load_model(self, path):
        try:
            source = Path(path)
            model = load_project(source) if source.suffix.lower() == ".issv6" else read_gsz(source)
            self.project_path = source if source.suffix.lower() == ".issv6" else None
            self.active_material_id = model.materials[0].source_id if model.materials else None
            self.set_model(model, fit=True)
            self.statusBar().showMessage(f"Đã mở {source.name}: {len(model.regions)} Region, {len(model.materials)} vật liệu.")
        except Exception as exc:
            QMessageBox.critical(self, "Không đọc được mô hình", str(exc))

    def import_cad(self):
        path, _ = QFileDialog.getOpenFileName(self, "Nhập DWG/DXF", "", "AutoCAD (*.dwg *.dxf);;Tất cả (*.*)")
        if not path: return
        try:
            cad = read_cad(path)
            dialog = CadImportDialog(cad, self)
            if dialog.exec() != QDialog.DialogCode.Accepted: return
            model = cad_to_model(cad, dialog.mapping())
            self.project_path = None
            self.active_material_id = model.materials[0].source_id if model.materials else None
            self.set_model(model, fit=True)
            issues = validate_model(model)
            if not model.regions:
                QMessageBox.warning(
                    self,
                    "CAD chưa tạo được Region",
                    f"Đã đọc {len(cad.entities)} đối tượng nhưng không nhận diện được Region kín.\n\n"
                    "Mô hình chưa thể tính Fs. Hãy kiểm tra layer có polyline kín hoặc chọn vai trò "
                    "‘Mặt cắt kín (Region + địa hình)’ khi nhập lại.",
                )
                return
            note = (
                f"Đã đọc {len(cad.entities)} đối tượng, tạo {len(model.regions)} Region.\n"
                "Các Region đã được gán tạm Vật liệu 1 để có thể kiểm tra và chạy tính; "
                "hãy đổi vật liệu nếu mặt cắt có nhiều lớp."
            )
            if any(i.severity == 'error' for i in issues):
                QMessageBox.warning(self, "Đã nhập CAD nhưng còn lỗi", note + "\n\nHãy chạy ‘Kiểm tra mô hình’ để xem chi tiết.")
            else:
                QMessageBox.information(self, "Đã nhập CAD", note + "\nHình học đã sẵn sàng cho kiểm tra tính toán.")
        except Exception as exc:
            QMessageBox.critical(self, "Không nhập được CAD", str(exc))

    def save_current(self):
        if not self.project_path: return self.save_as()
        try:
            self.project_path = save_project(self.model, self.project_path)
            self.statusBar().showMessage(f"Đã lưu {self.project_path}")
        except Exception as exc:
            QMessageBox.critical(self, "Không lưu được dự án", str(exc))

    def save_as(self):
        path, _ = QFileDialog.getSaveFileName(self, "Lưu dự án IMSAT", str(self.project_path or f"{self.model.name}.issv6"), "IMSAT V6 (*.issv6)")
        if not path: return
        try:
            self.project_path = save_project(self.model, path)
            self._update_title(); self.statusBar().showMessage(f"Đã lưu {self.project_path}")
        except Exception as exc:
            QMessageBox.critical(self, "Không lưu được dự án", str(exc))

    # ---------- definitions / geometry ----------
    def define_materials(self):
        dialog = MaterialsDialog(self.model, self)
        dialog.materials_changed.connect(self._refresh_all)
        dialog.exec(); self._refresh_all()

    def define_analysis(self):
        if AnalysisDialog(self.model, self).exec(): self._refresh_all()

    def define_search(self):
        if SearchDialog(self.model, self).exec(): self.canvas.redraw()

    def define_water_loads(self):
        if WaterLoadDialog(self.model, self).exec(): self.canvas.redraw()

    def add_region(self):
        dialog = RegionCoordinateDialog(parent=self)
        if not dialog.exec(): return
        source_id = max([r.source_id for r in self.model.regions] or [0]) + 1
        self.model.regions.append(GeoRegion(source_id=source_id, polygon=dialog.points, name=dialog.name.text().strip() or f"Region {source_id}"))
        self.model.terrain = upper_envelope(self.model.regions)
        self.canvas.set_model(self.model, fit=True); self._refresh_all()

    def _selected_region_id(self):
        items = self.explorer.selectedItems()
        if items:
            data = items[0].data(0, Qt.ItemDataRole.UserRole)
            if data and data[0] == "region": return int(data[1])
        selected = [item for item in self.canvas.scene().selectedItems() if hasattr(item, "region_id")]
        return selected[0].region_id if selected else None

    def edit_region(self):
        region_id = self._selected_region_id()
        region = self.model.region_by_id(region_id) if region_id is not None else None
        if not region:
            QMessageBox.information(self, "Chưa chọn Region", "Chọn Region trên cây mô hình hoặc trên mặt cắt trước."); return
        dialog = RegionCoordinateDialog(region.name, region.polygon, self)
        if not dialog.exec(): return
        region.name = dialog.name.text().strip() or region.name; region.polygon = dialog.points
        self.model.terrain = upper_envelope(self.model.regions); self.canvas.set_model(self.model, fit=False); self._refresh_all()

    def delete_region(self):
        region_id = self._selected_region_id()
        if region_id is None: return
        if QMessageBox.question(self, "Xóa Region", f"Xóa Region {region_id}?") != QMessageBox.StandardButton.Yes: return
        self.model.regions = [region for region in self.model.regions if region.source_id != region_id]
        self.model.terrain = upper_envelope(self.model.regions); self.canvas.set_model(self.model, fit=True); self._refresh_all()

    # ---------- interactions ----------
    def _material_selected(self, item):
        self.active_material_id = int(item.data(Qt.ItemDataRole.UserRole))
        material = self.model.material_by_id(self.active_material_id)
        self.active_material_label.setText(f"Vật liệu đang cầm: {material.name if material else '—'}")

    def _region_clicked(self, region_id):
        if self.canvas.mode == ViewMode.ASSIGN:
            if self.active_material_id is None:
                QMessageBox.warning(self, "Chưa chọn vật liệu", "Hãy chọn một vật liệu trong cửa sổ Vật liệu / Gán."); return
            try:
                assign_material(self.model, region_id, self.active_material_id)
                self.canvas.redraw(); self._refresh_explorer()
                material = self.model.material_by_id(self.active_material_id)
                self.statusBar().showMessage(f"Đã gán {material.name if material else self.active_material_id} cho Region {region_id}.")
            except Exception as exc:
                QMessageBox.warning(self, "Không gán được vật liệu", str(exc))

    def _range_changed(self, kind, left, right):
        y_left = self._terrain_y(left); y_right = self._terrain_y(right)
        if kind == "entry":
            self.model.search_range.entry_left = (left, y_left); self.model.search_range.entry_right = (right, y_right)
            self.model.search.entry_x_min, self.model.search.entry_x_max = left, right
        else:
            self.model.search_range.exit_left = (left, y_left); self.model.search_range.exit_right = (right, y_right)
            self.model.search.exit_x_min, self.model.search.exit_x_max = left, right
        self.canvas.redraw(); self.statusBar().showMessage(f"Đã vẽ vùng {kind.upper()}: X={left:.3f} đến {right:.3f} m.")

    def _terrain_y(self, x):
        from app.algorithms.solver import interp_y
        value = interp_y(self.model.terrain, x, upper=True)
        return float(value or 0.0)

    def _cursor_moved(self, x, y):
        self.coord_label.setText(f"X={x:.3f} m | Y={y:.3f} m")

    def _explorer_clicked(self, item, column):
        data = item.data(0, Qt.ItemDataRole.UserRole)
        if not data: return
        kind, identifier = data
        if kind == "region": self.set_mode(ViewMode.DEFINE)
        elif kind == "analysis": self.define_analysis()
        elif kind == "materials": self.define_materials()
        elif kind == "results": self.set_mode(ViewMode.RESULTS)
        elif kind == "reference": self.show_reference_results()

    def _result_selected(self, row, column):
        results = self._displayed_results()
        if 0 <= row < len(results):
            self.canvas.set_surface(results[row]); self.set_mode(ViewMode.RESULTS)

    def show_reference_results(self):
        if not self.model.reference_surfaces:
            QMessageBox.information(self, "Không có tham chiếu", "Dự án không có kết quả GeoStudio tham chiếu."); return
        self.display_reference = True
        self._refresh_results(); self.set_mode(ViewMode.RESULTS)

    def _toggle_canvas(self, name, value):
        setattr(self.canvas, name, bool(value)); self.canvas.redraw()

    # ---------- validation and solver process ----------
    def validate_current(self):
        issues = validate_model(self.model)
        errors = [issue for issue in issues if issue.severity == "error"]
        warnings = [issue for issue in issues if issue.severity != "error"]
        lines = [f"Lỗi hình học: {len(errors)} | Cảnh báo: {len(warnings)}"]
        lines.extend(f"• {issue.message}" for issue in issues)
        if not issues:
            lines.append("• Geometry và quan hệ vật liệu hợp lệ.")

        preflight = {"status": "SKIPPED", "generated": 0, "slice_valid": 0, "fallback_slice_valid": 0, "reasons": {}}
        if not errors and self.model.regions and all(region.material_source_id is not None for region in self.model.regions):
            QApplication.setOverrideCursor(Qt.CursorShape.WaitCursor)
            try:
                preflight = preflight_analysis(self.model)
            finally:
                QApplication.restoreOverrideCursor()
            valid = int(preflight.get("slice_valid", 0))
            fallback_valid = int(preflight.get("fallback_slice_valid", 0))
            lines.append(f"• Kiểm tra tính toán: sinh {preflight.get('generated', 0)} mặt trượt; mẫu kiểm tra có {valid} mặt tạo được lát hợp lệ.")
            if valid == 0 and fallback_valid > 0:
                lines.append(f"• Vùng Entry–Exit hiện tại chưa tốt; tìm kiếm thích nghi toàn mô hình có {fallback_valid} mặt hợp lệ và sẽ được dùng khi chạy.")
            elif valid == 0:
                top_reasons = list(preflight.get("reasons", {}).items())[:5]
                lines.append("• Không tạo được lát tính từ Entry–Exit hiện tại.")
                lines.extend(f"  - {code}: {count}" for code, count in top_reasons)

        calculation_ready = bool(self.model.regions) and all(
            region.material_source_id is not None for region in self.model.regions
        )
        calculation_ok = calculation_ready and preflight.get("status") == "PASS"
        if not calculation_ready:
            lines.append("• Chưa đủ Region/vật liệu để chạy bộ giải.")
        title = "Kiểm tra mô hình và khả năng tính toán"
        message = "\n".join(lines)
        if errors or not calculation_ok:
            QMessageBox.warning(self, title, message)
        else:
            QMessageBox.information(self, title, message)
        return not errors and calculation_ok

    def _app_data_dir(self):
        base = QStandardPaths.writableLocation(QStandardPaths.StandardLocation.AppLocalDataLocation)
        path = Path(base or (Path.home() / ".imsat_slope_v6")); path.mkdir(parents=True, exist_ok=True)
        (path / "logs").mkdir(exist_ok=True); (path / "jobs").mkdir(exist_ok=True)
        return path

    def run_analysis(self):
        if self.process and self.process.state() != QProcess.ProcessState.NotRunning:
            QMessageBox.information(self, "Đang phân tích", "Một bộ giải đang chạy."); return
        if not self.validate_current(): return
        if any(region.material_source_id is None for region in self.model.regions):
            QMessageBox.warning(self, "Chưa gán đủ vật liệu", "Mỗi Region phải được gán vật liệu trước khi chạy."); return
        app_dir = self._app_data_dir(); stamp = __import__("datetime").datetime.now().strftime("%Y%m%d_%H%M%S_%f")
        self._solver_input = app_dir / "jobs" / f"input_{stamp}.issv6"
        self._solver_output = app_dir / "jobs" / f"output_{stamp}.issv6"
        self._solver_log = app_dir / "logs" / f"solver_{stamp}.log"
        self.model.save(self._solver_input)
        # Recovery snapshot is always overwritten with the latest valid pre-solve model.
        self.model.save(app_dir / "recovery_latest.issv6")

        self.process = QProcess(self); self._stdout_buffer = ""; self._solver_error_details = []
        self.process.readyReadStandardOutput.connect(self._solver_stdout)
        self.process.readyReadStandardError.connect(self._solver_stderr)
        self.process.finished.connect(self._solver_finished)
        self.process.errorOccurred.connect(self._solver_process_error)
        if getattr(sys, "frozen", False):
            program = sys.executable; arguments = ["--solver-job", str(self._solver_input), str(self._solver_output)]
        else:
            program = sys.executable; arguments = [str(Path(__file__).resolve().parents[2] / "main.py"), "--solver-job", str(self._solver_input), str(self._solver_output)]
        # A windowed PyInstaller worker may not expose stdout.  Start with an
        # indeterminate busy bar; switch to percentage mode if progress JSON arrives.
        self.progress.setRange(0, 0); self.progress.show()
        self.a_run.setEnabled(False); self.a_cancel.setEnabled(True)
        self.statusBar().showMessage("Đang phân tích trong tiến trình riêng — giao diện vẫn hoạt động.")
        self.process.start(program, arguments)

    def _append_log(self, text):
        if self._solver_log:
            with self._solver_log.open("a", encoding="utf-8") as stream: stream.write(text)

    def _solver_stdout(self):
        if not self.process: return
        text = bytes(self.process.readAllStandardOutput()).decode("utf-8", errors="replace")
        self._append_log(text); self._stdout_buffer += text
        while "\n" in self._stdout_buffer:
            line, self._stdout_buffer = self._stdout_buffer.split("\n", 1)
            try: payload = json.loads(line)
            except json.JSONDecodeError: continue
            kind = payload.get("type")
            if kind == "progress":
                total = max(1, int(payload.get("total", 1))); done = int(payload.get("done", 0))
                if self.progress.maximum() == 0:
                    self.progress.setRange(0, 100)
                self.progress.setValue(round(100 * done / total))
                fs = payload.get("fs"); self.statusBar().showMessage(f"Đang phân tích {done}/{total}" + (f" | Fs thử={fs:.4f}" if isinstance(fs, (int, float)) else ""))
            elif kind in {"error", "exception"}:
                self._solver_error_details = [str(item) for item in payload.get("details", [])]
                self.statusBar().showMessage(payload.get("message", "Bộ giải báo lỗi."))

    def _solver_stderr(self):
        if self.process: self._append_log(bytes(self.process.readAllStandardError()).decode("utf-8", errors="replace"))

    def _solver_finished(self, exit_code, exit_status):
        self.progress.hide(); self.a_run.setEnabled(True); self.a_cancel.setEnabled(False)
        if exit_code == 0 and self._solver_output and self._solver_output.exists():
            try:
                solved = GeoStudioModel.load(self._solver_output)
                self.model.results = solved.results
                self.model.critical = solved.critical
                self.model.analysis_diagnostics = solved.analysis_diagnostics
                self.display_reference = False
                self._refresh_all(); self.set_mode(ViewMode.RESULTS)
                fs = self.model.critical.fs if self.model.critical else math.nan
                QMessageBox.information(self, "Phân tích hoàn thành", f"Đã tìm được {len(self.model.results)} mặt trượt hợp lệ.\nFs tới hạn = {fs:.5f}\nPhương pháp: {self.model.analysis.method}")
                self.statusBar().showMessage("Phân tích hoàn thành.")
                return
            except Exception as exc:
                self._append_log(f"\nLOAD OUTPUT ERROR: {exc}\n")
        # In a windowed/frozen child process stdout can be unavailable.  Read
        # the persisted terminal diagnostics before falling back to a generic message.
        if not self._solver_error_details and self._solver_output:
            status_path = Path(str(self._solver_output) + ".diagnostics.json")
            if status_path.exists():
                try:
                    payload = json.loads(status_path.read_text(encoding="utf-8"))
                    self._solver_error_details = [str(item) for item in payload.get("details", [])]
                    if not self._solver_error_details and payload.get("message"):
                        self._solver_error_details = [str(payload.get("message"))]
                except Exception as exc:
                    self._append_log(f"\nREAD DIAGNOSTICS ERROR: {exc}\n")
        details = "\n".join(f"• {item}" for item in self._solver_error_details[:8])
        message = "Bộ giải không tạo được kết quả hội tụ."
        if details:
            message += "\n\nNguyên nhân chính:\n" + details
        message += f"\n\nNhật ký: {self._solver_log}"
        QMessageBox.warning(self, "Không có kết quả hợp lệ", message)
        self.statusBar().showMessage("Phân tích chưa tạo được kết quả hợp lệ.")

    def _solver_process_error(self, error):
        self._append_log(f"\nQProcess error: {error}\n")

    def cancel_analysis(self):
        if self.process and self.process.state() != QProcess.ProcessState.NotRunning:
            self.process.kill(); self.process.waitForFinished(3000)
        self.a_run.setEnabled(True); self.a_cancel.setEnabled(False); self.progress.hide(); self.statusBar().showMessage("Đã dừng phân tích.")

    # ---------- exports / help ----------
    def export_html(self):
        path, _ = QFileDialog.getSaveFileName(self, "Xuất báo cáo HTML", f"{self.model.name}_report.html", "HTML (*.html)")
        if path: export_html_report(self.model, path); self.statusBar().showMessage(f"Đã xuất {path}")

    def export_results(self):
        path, _ = QFileDialog.getSaveFileName(self, "Xuất danh sách mặt trượt", f"{self.model.name}_surfaces.csv", "CSV (*.csv)")
        if path: export_results_csv(self.model, path); self.statusBar().showMessage(f"Đã xuất {path}")

    def export_slices(self):
        path, _ = QFileDialog.getSaveFileName(self, "Xuất lát tới hạn", f"{self.model.name}_critical_slices.csv", "CSV (*.csv)")
        if path: export_slices_csv(self.model, path); self.statusBar().showMessage(f"Đã xuất {path}")

    def about(self):
        QMessageBox.about(self, "IMSAT Slope Stability Professional V6.2", "Phần mềm phân tích ổn định bờ mỏ bằng Fellenius, Bishop Simplified, Janbu Simplified và Morgenstern–Price.\n\nPhát triển cho Viện Khoa học Công nghệ Mỏ – Vinacomin.\n\nKết quả phải được kỹ sư có năng lực kiểm tra trước khi sử dụng trong thiết kế.")

    def closeEvent(self, event):
        if self.process and self.process.state() != QProcess.ProcessState.NotRunning:
            answer = QMessageBox.question(self, "Đang phân tích", "Bộ giải đang chạy. Dừng và thoát?")
            if answer != QMessageBox.StandardButton.Yes:
                event.ignore(); return
            self.cancel_analysis()
        event.accept()
