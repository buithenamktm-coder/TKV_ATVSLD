from __future__ import annotations

import csv
import html
import math
from pathlib import Path

from app.core.models import GeoStudioModel


def _num(value, digits=4):
    if value is None or not math.isfinite(float(value)):
        return "—"
    return f"{float(value):,.{digits}f}".replace(",", "X").replace(".", ",").replace("X", ".")


def export_results_csv(model: GeoStudioModel, path: str | Path) -> Path:
    target = Path(path)
    with target.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["STT", "Fs", "Phương pháp", "Hội tụ", "Số vòng lặp", "Lambda", "X tâm", "Y tâm", "Bán kính", "Thể tích", "Trọng lượng", "Nguồn"])
        for surface in model.results:
            writer.writerow([surface.number, surface.fs, surface.method, surface.converged, surface.iterations, surface.lambda_value, surface.center_x, surface.center_y, surface.radius, surface.volume, surface.weight, surface.source])
    return target


def export_slices_csv(model: GeoStudioModel, path: str | Path) -> Path:
    target = Path(path)
    surface = model.critical
    with target.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["Lát", "X trái", "X phải", "Y đáy trái", "Y đáy phải", "Y đỉnh trái", "Y đỉnh phải", "Trọng lượng", "U", "Góc đáy (rad)", "C", "Phi", "Vật liệu"])
        if surface:
            for s in surface.slices_data:
                writer.writerow([s.number, s.x_left, s.x_right, s.y_bot_left, s.y_bot_right, s.y_top_left, s.y_top_right, s.weight, s.pore_pressure, s.base_angle, s.cohesion, s.phi, s.material_id])
    return target


def export_html_report(model: GeoStudioModel, path: str | Path) -> Path:
    target = Path(path)
    critical = model.critical
    material_rows = "".join(
        f"<tr><td>{html.escape(m.name)}</td><td>{_num(m.unit_weight,3)}</td><td>{_num(m.cohesion,3)}</td><td>{_num(m.phi,3)}</td><td><span style='display:inline-block;width:22px;height:14px;background:{m.color};border:1px solid #555'></span></td></tr>"
        for m in model.materials
    )
    region_rows = "".join(
        f"<tr><td>{r.source_id}</td><td>{html.escape(r.name)}</td><td>{html.escape(model.material_by_id(r.material_source_id).name if model.material_by_id(r.material_source_id) else 'Chưa gán')}</td><td>{len(r.polygon)}</td></tr>"
        for r in model.regions
    )
    result_rows = "".join(
        f"<tr><td>{r.number}</td><td>{_num(r.fs,5)}</td><td>{html.escape(r.method)}</td><td>{'Có' if r.converged else 'Không'}</td><td>{r.iterations}</td><td>{_num(r.lambda_value,5)}</td></tr>"
        for r in model.results[:100]
    ) or "<tr><td colspan='6'>Chưa có kết quả IMSAT.</td></tr>"
    body = f"""<!doctype html><html lang='vi'><head><meta charset='utf-8'><title>Báo cáo ổn định bờ mỏ</title>
<style>body{{font-family:Arial;margin:34px;color:#173B5E}}h1{{color:#075CA8}}table{{border-collapse:collapse;width:100%;margin:12px 0 24px}}th,td{{border:1px solid #9fb2c2;padding:6px 8px}}th{{background:#eaf1f7}}.summary{{padding:14px;background:#eef7ff;border-left:5px solid #0878c9}}small{{color:#607487}}</style></head><body>
<h1>IMSAT Slope Stability Professional V6</h1><h2>{html.escape(model.name)}</h2>
<div class='summary'><b>Phương pháp:</b> {html.escape(model.analysis.method)}<br><b>Hướng trượt:</b> {html.escape(model.analysis.slip_direction)}<br><b>Số lát:</b> {model.analysis.slices}<br><b>Fs tới hạn:</b> {_num(critical.fs,5) if critical else '—'}<br><b>Lambda:</b> {_num(critical.lambda_value,5) if critical else '—'}<br><b>Trạng thái:</b> {'Hội tụ' if critical and critical.converged else 'Chưa có kết quả hội tụ'}</div>
<h3>Vật liệu</h3><table><tr><th>Tên</th><th>γ (kN/m³)</th><th>C (kPa)</th><th>φ (độ)</th><th>Màu</th></tr>{material_rows}</table>
<h3>Region</h3><table><tr><th>Mã</th><th>Tên</th><th>Vật liệu</th><th>Số điểm</th></tr>{region_rows}</table>
<h3>Kết quả mặt trượt</h3><table><tr><th>STT</th><th>Fs</th><th>Phương pháp</th><th>Hội tụ</th><th>Vòng lặp</th><th>Lambda</th></tr>{result_rows}</table>
<p><small>Kết quả cần được kiểm tra bởi kỹ sư địa kỹ thuật có năng lực trước khi sử dụng cho thiết kế hoặc quyết định an toàn.</small></p></body></html>"""
    target.write_text(body, encoding="utf-8")
    return target
