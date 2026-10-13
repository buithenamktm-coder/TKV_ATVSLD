from __future__ import annotations
from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QColor, QIcon, QPixmap
from PySide6.QtWidgets import (QColorDialog,QDialog,QDialogButtonBox,QDoubleSpinBox,QFormLayout,QHBoxLayout,QHeaderView,QLabel,QLineEdit,QListWidget,QListWidgetItem,QPushButton,QSplitter,QVBoxLayout,QWidget)
from app.core.models import Material

PALETTE=['#F5E66F','#8A8F00','#B100C0','#0E2DBB','#4CAF50','#D77A00','#8D6E63','#607D8B']

def spin(value=0.0):
    w=QDoubleSpinBox(); w.setRange(-1e9,1e9); w.setDecimals(4); w.setValue(value); return w

def color_icon(color):
    p=QPixmap(24,18); p.fill(QColor(color)); return QIcon(p)

class MaterialsDialog(QDialog):
    materials_changed=Signal()
    def __init__(self,model,parent=None):
        super().__init__(parent); self.model=model; self.setWindowTitle('Khai báo vật liệu'); self.resize(820,480); self.current_color='#B4B4B4'
        lay=QVBoxLayout(self); split=QSplitter(Qt.Orientation.Horizontal)
        left=QWidget(); ll=QVBoxLayout(left); self.list=QListWidget(); self.list.currentRowChanged.connect(self.load_row); ll.addWidget(self.list)
        row=QHBoxLayout(); add=QPushButton('Thêm'); delete=QPushButton('Xóa'); add.clicked.connect(self.add); delete.clicked.connect(self.delete); row.addWidget(add); row.addWidget(delete); ll.addLayout(row); split.addWidget(left)
        right=QWidget(); form=QFormLayout(right); self.name=QLineEdit(); self.gamma=spin(); self.gamma_sat=spin(); self.c=spin(); self.phi=spin(); self.color=QPushButton('Chọn màu'); self.color.clicked.connect(self.pick_color)
        form.addRow('Tên vật liệu',self.name); form.addRow('Màu',self.color); form.addRow('Mô hình',QLabel('Mohr–Coulomb')); form.addRow('Dung trọng γ (kN/m³)',self.gamma); form.addRow('Dung trọng bão hòa γsat (kN/m³)',self.gamma_sat); form.addRow('Lực dính C (kPa)',self.c); form.addRow('Góc ma sát φ (độ)',self.phi)
        save=QPushButton('Cập nhật vật liệu'); save.setObjectName('primary'); save.clicked.connect(self.save); form.addRow(save); split.addWidget(right); split.setSizes([300,500]); lay.addWidget(split)
        buttons=QDialogButtonBox(QDialogButtonBox.StandardButton.Close); buttons.rejected.connect(self.reject); lay.addWidget(buttons); self.refresh()
    def refresh(self):
        row=self.list.currentRow(); self.list.clear()
        for m in self.model.materials:
            i=QListWidgetItem(color_icon(m.color),f'{m.source_id} — {m.name}'); i.setData(Qt.ItemDataRole.UserRole,m.source_id); self.list.addItem(i)
        self.list.setCurrentRow(max(0,min(row,self.list.count()-1)))
    def current(self):
        i=self.list.currentItem(); return self.model.material_by_id(i.data(Qt.ItemDataRole.UserRole)) if i else None
    def load_row(self,row):
        m=self.current()
        if not m:return
        self.name.setText(m.name); self.gamma.setValue(m.unit_weight); self.gamma_sat.setValue(m.saturated_unit_weight); self.c.setValue(m.cohesion); self.phi.setValue(m.phi); self.current_color=m.color; self.color.setIcon(color_icon(m.color))
    def pick_color(self):
        c=QColorDialog.getColor(QColor(self.current_color),self)
        if c.isValid(): self.current_color=c.name().upper(); self.color.setIcon(color_icon(self.current_color))
    def save(self):
        m=self.current()
        if not m:return
        m.name=self.name.text().strip() or m.name; m.unit_weight=self.gamma.value(); m.saturated_unit_weight=self.gamma_sat.value(); m.cohesion=self.c.value(); m.phi=self.phi.value(); m.color=self.current_color; self.refresh(); self.materials_changed.emit()
    def add(self):
        nid=max([m.source_id for m in self.model.materials] or [0])+1; color=PALETTE[len(self.model.materials)%len(PALETTE)]; self.model.materials.append(Material(name=f'Vật liệu {nid}', unit_weight=20.0, saturated_unit_weight=21.0, cohesion=0.0, phi=30.0, source_id=nid, slope_model='Mohr-Coulomb', color=color)); self.refresh(); self.list.setCurrentRow(self.list.count()-1); self.materials_changed.emit()
    def delete(self):
        m=self.current()
        if not m:return
        if any(r.material_source_id==m.source_id for r in self.model.regions): return
        self.model.materials=[x for x in self.model.materials if x.source_id!=m.source_id]; self.refresh(); self.materials_changed.emit()
