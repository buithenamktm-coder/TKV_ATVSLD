from __future__ import annotations

import math
from enum import Enum
from typing import Optional

from PySide6.QtCore import QPointF, QRectF, Qt, Signal, QTimer
from PySide6.QtGui import QBrush, QColor, QFont, QPainter, QPen, QPolygonF
from PySide6.QtWidgets import QGraphicsItem, QGraphicsPolygonItem, QGraphicsScene, QGraphicsView

from app.algorithms.solver import interp_y
from app.core.geometry import model_bounds
from app.core.models import GeoStudioModel, SlipSurface


class ViewMode(str, Enum):
    DEFINE="define"
    ASSIGN="assign"
    DRAW_ENTRY="draw_entry"
    DRAW_EXIT="draw_exit"
    RESULTS="results"


class RegionItem(QGraphicsPolygonItem):
    def __init__(self, region_id: int, polygon: QPolygonF, pen: QPen, brush: QBrush):
        super().__init__(polygon)
        self.region_id = region_id
        self.normal_pen = pen
        self.setPen(pen)
        self.setBrush(brush)
        self.setZValue(10)
        self.setAcceptHoverEvents(True)
        self.setFlag(QGraphicsItem.GraphicsItemFlag.ItemIsSelectable, True)
        self.setCursor(Qt.CursorShape.PointingHandCursor)

    def hoverEnterEvent(self, event):
        pen = QPen(QColor("#E64A19"), 0)
        pen.setWidthF(2.6)
        self.setPen(pen)
        super().hoverEnterEvent(event)

    def hoverLeaveEvent(self, event):
        pen = QPen(QColor("#E64A19"), 0) if self.isSelected() else self.normal_pen
        self.setPen(pen)
        super().hoverLeaveEvent(event)

    def itemChange(self, change, value):
        if change == QGraphicsItem.GraphicsItemChange.ItemSelectedHasChanged:
            pen = QPen(QColor("#E64A19"), 0) if bool(value) else self.normal_pen
            pen.setWidthF(2.4 if bool(value) else 1.2)
            self.setPen(pen)
        return super().itemChange(change, value)


class EngineeringCanvas(QGraphicsView):
    region_clicked = Signal(int)
    cursor_moved = Signal(float, float)
    search_range_changed = Signal(str, float, float)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setScene(QGraphicsScene(self))
        self.setRenderHint(QPainter.RenderHint.Antialiasing)
        self.setDragMode(QGraphicsView.DragMode.ScrollHandDrag)
        self.setTransformationAnchor(QGraphicsView.ViewportAnchor.AnchorUnderMouse)
        self.setResizeAnchor(QGraphicsView.ViewportAnchor.AnchorViewCenter)
        self.setMouseTracking(True)
        self.model: Optional[GeoStudioModel] = None
        self.mode = ViewMode.DEFINE
        self.selected_surface: Optional[SlipSurface] = None
        self.show_vertices = False
        self.show_labels = False
        self.show_search = True
        self.show_slices = True
        self.show_water = True
        self.show_loads = True
        self._bounds = (0.0, 0.0, 100.0, 100.0)
        self._cursor = None
        self._range_start_x: Optional[float] = None
        self._hover_snap: Optional[tuple[float, float]] = None
        self._fit_pending = False

    def set_model(self, model, fit=True):
        self.model = model
        self.selected_surface = model.critical if model else None
        if model:
            self._bounds = model_bounds(model, 0.05)
        self.redraw()
        self._fit_pending = bool(fit)
        self._apply_bounds(fit)
        if fit:
            # fitInView before the window is laid out can use a tiny viewport.
            # Repeat once after Qt completes the current layout pass.
            QTimer.singleShot(0, self.fit_all)

    def set_mode(self, mode):
        self.mode = ViewMode(mode)
        self._range_start_x = None
        self.setDragMode(
            QGraphicsView.DragMode.NoDrag
            if self.mode in {ViewMode.DRAW_ENTRY, ViewMode.DRAW_EXIT}
            else QGraphicsView.DragMode.ScrollHandDrag
        )
        self.redraw()

    def set_surface(self, surface):
        self.selected_surface = surface
        self.redraw()

    def _apply_bounds(self, fit=False):
        xmin, ymin, xmax, ymax = self._bounds
        rect = QRectF(xmin, -ymax, xmax - xmin, ymax - ymin)
        self.scene().setSceneRect(rect)
        if fit and not rect.isNull():
            self.fitInView(rect, Qt.AspectRatioMode.KeepAspectRatio)
        self.viewport().update()

    def fit_all(self):
        self._apply_bounds(True)

    def resizeEvent(self, event):
        super().resizeEvent(event)
        if self._fit_pending and self.model:
            self._fit_pending = False
            QTimer.singleShot(0, self.fit_all)


    def wheelEvent(self, event):
        factor = 1.15 if event.angleDelta().y() > 0 else 1 / 1.15
        self.scale(factor, factor)

    def mouseMoveEvent(self, event):
        point = self.mapToScene(event.position().toPoint())
        self._cursor = (point.x(), -point.y())
        self._hover_snap = None
        if self.model and self.mode in {ViewMode.DRAW_ENTRY, ViewMode.DRAW_EXIT} and len(self.model.terrain) >= 2:
            xmin, xmax = self.model.terrain[0][0], self.model.terrain[-1][0]
            x = min(max(point.x(), xmin), xmax)
            y = interp_y(self.model.terrain, x, upper=True)
            if y is not None:
                self._hover_snap = (x, y)
                self._cursor = self._hover_snap
        self.cursor_moved.emit(*self._cursor)
        self.viewport().update()
        super().mouseMoveEvent(event)

    def mousePressEvent(self, event):
        if event.button() == Qt.MouseButton.LeftButton:
            if self.mode in {ViewMode.DRAW_ENTRY, ViewMode.DRAW_EXIT} and self.model:
                scene_point = self.mapToScene(event.position().toPoint())
                xmin, xmax = self.model.terrain[0][0], self.model.terrain[-1][0]
                x = min(max(scene_point.x(), xmin), xmax)
                y = interp_y(self.model.terrain, x, upper=True)
                if y is None:
                    event.accept()
                    return
                if self._range_start_x is None:
                    self._range_start_x = x
                else:
                    left, right = sorted([self._range_start_x, x])
                    label = "entry" if self.mode == ViewMode.DRAW_ENTRY else "exit"
                    self.search_range_changed.emit(label, left, right)
                    self._range_start_x = None
                event.accept()
                return
            for item in self.items(event.position().toPoint()):
                if isinstance(item, RegionItem):
                    self.scene().clearSelection()
                    item.setSelected(True)
                    self.region_clicked.emit(item.region_id)
                    event.accept()
                    return
        super().mousePressEvent(event)

    def redraw(self):
        self.scene().clear()
        model = self.model
        if not model:
            return
        # CAD/geological reference linework is visual context only; it never becomes slice boundaries.
        reference_pen = QPen(QColor("#8A98A6"), 0, Qt.PenStyle.DashLine)
        reference_pen.setWidthF(1.0)
        for polyline in model.reference_geometry:
            for start, end in zip(polyline, polyline[1:]):
                self.scene().addLine(start[0], -start[1], end[0], -end[1], reference_pen).setZValue(2)
        materials = {material.source_id: material for material in model.materials}
        for region in model.regions:
            if len(region.polygon) < 3:
                continue
            material = materials.get(region.material_source_id)
            color = QColor(material.color if material else "#E6E9EC")
            color.setAlpha(220)
            polygon = QPolygonF([QPointF(x, -y) for x, y in region.polygon])
            pen = QPen(QColor("#243B53"), 0)
            pen.setWidthF(1.2)
            item = RegionItem(region.source_id, polygon, pen, QBrush(color))
            self.scene().addItem(item)
            if self.show_labels:
                cx = sum(x for x, _ in region.polygon) / len(region.polygon)
                cy = sum(y for _, y in region.polygon) / len(region.polygon)
                label = self.scene().addText(region.name or f"Region {region.source_id}", QFont("Arial", 8))
                label.setDefaultTextColor(QColor("#173B5E"))
                label.setPos(cx, -cy)
                label.setZValue(30)
            if self.show_vertices:
                for x, y in region.polygon:
                    dot = self.scene().addEllipse(
                        x - 0.45, -y - 0.45, 0.9, 0.9,
                        QPen(QColor("#111111"), 0), QBrush(QColor("#111111")),
                    )
                    dot.setZValue(40)
        terrain_pen = QPen(QColor("#12813A"), 0)
        terrain_pen.setWidthF(2.4)
        for start, end in zip(model.terrain, model.terrain[1:]):
            line = self.scene().addLine(start[0], -start[1], end[0], -end[1], terrain_pen)
            line.setZValue(25)
        if self.show_water:
            self._draw_water()
        if self.show_loads:
            self._draw_loads()
        if self.show_search:
            self._draw_search()
        # Slice boundaries are results only; Geometry/Assign never draw them.
        if self.mode == ViewMode.RESULTS:
            self._draw_results()

    def _draw_water(self):
        model = self.model
        if not model or not model.water.enabled:
            return
        line = model.water.phreatic_line
        if len(line) < 2:
            xmin, _, xmax, _ = self._bounds
            line = [(xmin, model.water.level), (xmax, model.water.level)]
        pen = QPen(QColor("#1479D1"), 0, Qt.PenStyle.DashLine)
        pen.setWidthF(2.0)
        for start, end in zip(line, line[1:]):
            self.scene().addLine(start[0], -start[1], end[0], -end[1], pen).setZValue(35)

    def _draw_loads(self):
        model = self.model
        if not model:
            return
        for load in model.loads:
            if not load.enabled:
                continue
            pen = QPen(QColor("#7B1FA2"), 0)
            pen.setWidthF(1.8)
            for x in (load.x_start, load.x_end):
                y = interp_y(model.terrain, x, upper=True)
                if y is None:
                    continue
                length = max(5.0, abs(load.magnitude) * 0.08)
                self.scene().addLine(x, -y, x, -(y + length), pen).setZValue(45)

    def _draw_search(self):
        model = self.model
        if not model or len(model.terrain) < 2:
            return
        specs = [
            (model.search_range.entry_left, model.search_range.entry_right, QColor("#00A65A"), "ENTRY"),
            (model.search_range.exit_left, model.search_range.exit_right, QColor("#F57C00"), "EXIT"),
        ]
        terrain = model.terrain
        for left_point, right_point, color, label in specs:
            if not left_point or not right_point:
                continue
            x1, x2 = sorted([left_point[0], right_point[0]])
            # GeoSlope-like range: highlight only the actual terrain segment,
            # instead of a full-height rectangle that is difficult to position.
            points = [(x1, interp_y(terrain, x1, upper=True))]
            points.extend((x, y) for x, y in terrain if x1 < x < x2)
            points.append((x2, interp_y(terrain, x2, upper=True)))
            points = [(x, y) for x, y in points if y is not None]
            halo = QColor(color); halo.setAlpha(70)
            halo_pen = QPen(halo, 0); halo_pen.setWidthF(9.0)
            line_pen = QPen(color, 0); line_pen.setWidthF(2.4)
            for start, finish in zip(points, points[1:]):
                self.scene().addLine(start[0], -start[1], finish[0], -finish[1], halo_pen).setZValue(46)
                self.scene().addLine(start[0], -start[1], finish[0], -finish[1], line_pen).setZValue(47)
            for x in (x1, x2):
                y = interp_y(terrain, x, upper=True)
                if y is not None:
                    self.scene().addEllipse(x-2.5, -y-2.5, 5, 5, QPen(color, 0), QBrush(QColor("#FFFFFF"))).setZValue(55)
            y_label = interp_y(terrain, x1, upper=True) or 0.0
            text = self.scene().addText(label, QFont("Arial", 8, QFont.Weight.Bold))
            text.setDefaultTextColor(color); text.setPos(x1, -y_label-20); text.setZValue(56)

        if self.mode in {ViewMode.DRAW_ENTRY, ViewMode.DRAW_EXIT}:
            color = QColor("#00A65A" if self.mode == ViewMode.DRAW_ENTRY else "#F57C00")
            if self._range_start_x is not None:
                start_y = interp_y(terrain, self._range_start_x, upper=True)
                if start_y is not None:
                    self.scene().addEllipse(self._range_start_x-3, -start_y-3, 6, 6, QPen(color, 0), QBrush(color)).setZValue(60)
                if self._hover_snap is not None:
                    x1, x2 = sorted((self._range_start_x, self._hover_snap[0]))
                    preview = [(x1, interp_y(terrain, x1, upper=True))]
                    preview.extend((x, y) for x, y in terrain if x1 < x < x2)
                    preview.append((x2, interp_y(terrain, x2, upper=True)))
                    preview_pen = QPen(color, 0, Qt.PenStyle.DashLine); preview_pen.setWidthF(3.0)
                    for a, b in zip(preview, preview[1:]):
                        if a[1] is not None and b[1] is not None:
                            self.scene().addLine(a[0], -a[1], b[0], -b[1], preview_pen).setZValue(59)
            if self._hover_snap is not None:
                x, y = self._hover_snap
                self.scene().addEllipse(x-3, -y-3, 6, 6, QPen(color, 0), QBrush(QColor("#FFFFFF"))).setZValue(61)

    def keyPressEvent(self, event):
        if event.key() == Qt.Key.Key_Escape and self.mode in {ViewMode.DRAW_ENTRY, ViewMode.DRAW_EXIT}:
            self._range_start_x = None
            self._hover_snap = None
            self.redraw()
            event.accept()
            return
        super().keyPressEvent(event)

    def _surface_path(self, surface):
        if surface.path:
            return surface.path
        if surface.radius <= 0:
            return []
        xmin, _, xmax, _ = self._bounds
        left = max(xmin, surface.center_x - surface.radius)
        right = min(xmax, surface.center_x + surface.radius)
        output = []
        for index in range(180):
            x = left + (right - left) * index / 179
            value = surface.radius * surface.radius - (x - surface.center_x) ** 2
            if value >= 0:
                output.append((x, surface.center_y - math.sqrt(value)))
        return output

    def _draw_results(self):
        model = self.model
        surface = self.selected_surface or (model.critical if model else None)
        if not surface:
            return
        path = self._surface_path(surface)
        pen = QPen(QColor("#E91E63"), 0)
        pen.setWidthF(3.0)
        for start, end in zip(path, path[1:]):
            self.scene().addLine(start[0], -start[1], end[0], -end[1], pen).setZValue(80)
        if self.show_slices and surface.slices_data:
            slice_pen = QPen(QColor("#00C900"), 0)
            slice_pen.setWidthF(1.0)
            for slice_data in surface.slices_data:
                self.scene().addLine(
                    slice_data.x_left, -slice_data.y_bot_left,
                    slice_data.x_left, -slice_data.y_top_left, slice_pen,
                ).setZValue(70)
            last = surface.slices_data[-1]
            self.scene().addLine(
                last.x_right, -last.y_bot_right, last.x_right, -last.y_top_right, slice_pen,
            ).setZValue(70)
        if path:
            x = sum(px for px, _ in path) / len(path)
            y = max(py for _, py in path)
        else:
            x, y = surface.center_x, surface.center_y
        fs_text = "—" if not math.isfinite(surface.fs) else f"{surface.fs:.3f}".replace(".", ",")
        label = self.scene().addText(f"Fs = {fs_text}", QFont("Arial", 18, QFont.Weight.Bold))
        label.setDefaultTextColor(QColor("#B00020"))
        label.setPos(x, -y - 30)
        label.setZValue(100)

    def drawBackground(self, painter, rect):
        painter.fillRect(rect, QColor("#FFFFFF"))
        xmin, ymin, xmax, ymax = self._bounds

        def step(span):
            raw = max(span / 10, 1e-9)
            exponent = math.floor(math.log10(raw))
            fraction = raw / 10 ** exponent
            nice = 1 if fraction <= 1 else 2 if fraction <= 2 else 5 if fraction <= 5 else 10
            return nice * 10 ** exponent

        x_step, y_step = step(xmax - xmin), step(ymax - ymin)
        painter.setPen(QPen(QColor("#E8EEF4"), 0))
        x = math.ceil(xmin / x_step) * x_step
        while x <= xmax:
            painter.drawLine(QPointF(x, -ymax), QPointF(x, -ymin))
            x += x_step
        y = math.ceil(ymin / y_step) * y_step
        while y <= ymax:
            painter.drawLine(QPointF(xmin, -y), QPointF(xmax, -y))
            y += y_step

    def drawForeground(self, painter, rect):
        xmin, ymin, xmax, ymax = self._bounds
        top_left = self.mapFromScene(QPointF(xmin, -ymax))
        bottom_right = self.mapFromScene(QPointF(xmax, -ymin))
        left, right = min(top_left.x(), bottom_right.x()), max(top_left.x(), bottom_right.x())
        top, bottom = min(top_left.y(), bottom_right.y()), max(top_left.y(), bottom_right.y())
        painter.save()
        painter.resetTransform()
        painter.setPen(QPen(QColor("#607487"), 1))
        painter.drawRect(left, top, max(1, right - left), max(1, bottom - top))
        # Numeric axis ticks keep the engineering scale visible at every zoom level.
        def nice_step(span):
            raw=max(span/10.0,1e-9); exponent=math.floor(math.log10(raw)); fraction=raw/10**exponent
            return (1 if fraction<=1 else 2 if fraction<=2 else 5 if fraction<=5 else 10)*10**exponent
        painter.setFont(QFont("Arial", 7))
        x_step=nice_step(xmax-xmin); x=math.ceil(xmin/x_step)*x_step
        while x<=xmax+1e-9:
            screen=self.mapFromScene(QPointF(x,-ymin)); sx=screen.x()
            painter.drawLine(sx,bottom,sx,bottom+5)
            painter.drawText(sx-32,bottom+6,64,16,Qt.AlignmentFlag.AlignHCenter,f"{x:g}")
            x+=x_step
        y_step=nice_step(ymax-ymin); y=math.ceil(ymin/y_step)*y_step
        while y<=ymax+1e-9:
            screen=self.mapFromScene(QPointF(xmin,-y)); sy=screen.y()
            painter.drawLine(left-5,sy,left,sy)
            painter.drawText(max(0,left-58),sy-8,50,16,Qt.AlignmentFlag.AlignRight,f"{y:g}")
            y+=y_step
        painter.setFont(QFont("Arial", 9, QFont.Weight.Bold))
        painter.drawText(left, bottom + 24, max(1, right - left), 20, Qt.AlignmentFlag.AlignHCenter, "CHIỀU DÀI X (m)")
        painter.save()
        painter.translate(max(10, left - 54), (top + bottom) / 2)
        painter.rotate(-90)
        painter.drawText(-90, -18, 180, 20, Qt.AlignmentFlag.AlignHCenter, "CAO ĐỘ Y (m)")
        painter.restore()
        if self._cursor:
            x, y = self._cursor
            painter.setFont(QFont("Arial", 8))
            painter.drawText(max(left, right - 260), top + 6, 250, 18, Qt.AlignmentFlag.AlignRight,
                             f"X={x:.3f} m | Y={y:.3f} m")
        painter.restore()
