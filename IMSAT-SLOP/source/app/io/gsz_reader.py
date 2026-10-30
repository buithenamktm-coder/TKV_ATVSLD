from __future__ import annotations

from pathlib import Path
from tempfile import TemporaryDirectory
import csv
import math
import zipfile
import xml.etree.ElementTree as ET

from app.core.models import (
    GeoRegion, GeoStudioModel, LambdaPoint, Material, SearchRange,
    SliceData, SlipSurface,
)
from app.core.geometry import clean_ring, upper_envelope


def _f(value, default=0.0):
    try: return float(value)
    except (TypeError, ValueError): return default


def _i(value, default=0):
    try: return int(float(value))
    except (TypeError, ValueError): return default


def _color(value, default="#B4B4B4"):
    text=(value or "").strip()
    if text.startswith("RGB=(") and text.endswith(")"):
        try:
            r,g,b=[int(x.strip()) for x in text[5:-1].split(",")]
            return f"#{r:02X}{g:02X}{b:02X}"
        except Exception: return default
    if text.startswith("#") and len(text)>=7:return text[:7].upper()
    return default


def _root_xml(base:Path)->Path:
    candidates=list(base.rglob("*.xml"))
    if not candidates:raise ValueError("Không tìm thấy XML trong GSZ.")
    return max(candidates,key=lambda p:p.stat().st_size)


def _analysis(root:ET.Element)->ET.Element|None:
    nodes=root.findall(".//Analyses/Analysis")
    return nodes[0] if nodes else None


def _geometry(root,analysis):
    gid=_i(analysis.findtext("GeometryId") if analysis is not None else None)
    nodes=root.findall(".//Geometries/Geometry") or root.findall(".//Geometry")
    if gid:
        for node in nodes:
            if _i(node.findtext("ID") or node.attrib.get("ID"))==gid:return node
    return nodes[0] if nodes else None


def _points(root,geometry):
    nodes=geometry.findall("./Points/Point") if geometry is not None else []
    if not nodes:nodes=root.findall(".//GeometryItems/Points/Point")
    if not nodes:nodes=root.findall(".//Geometry/Points/Point")
    output={}
    for node in nodes:
        pid=_i(node.attrib.get("ID") or node.findtext("ID"))
        if pid:
            output[pid]=(_f(node.attrib.get("X") or node.findtext("X")),
                         _f(node.attrib.get("Y") or node.findtext("Y")))
    return output


def _region_materials(root):
    output={}
    for node in root.findall(".//RegionUsesMaterial"):
        rid=_i(node.attrib.get("ID"));mid=_i(node.attrib.get("UsesID") or node.attrib.get("Entry"))
        if rid and mid:output[rid]=mid
    for node in root.findall(".//GeometryUsesMaterial"):
        rid=_i((node.attrib.get("ID") or "").split("-")[-1]);mid=_i(node.attrib.get("Entry") or node.attrib.get("UsesID"))
        if rid and mid:output[rid]=mid
    return output


def _regions(root,geometry,point_map):
    nodes=geometry.findall("./Regions/Region") if geometry is not None else []
    if not nodes:nodes=root.findall(".//GeometryItems/Regions/Region")
    if not nodes:nodes=root.findall(".//Geometry/Regions/Region")
    mapping=_region_materials(root);output=[]
    for node in nodes:
        rid=_i(node.findtext("ID") or node.attrib.get("ID"))
        raw=(node.findtext("PointIDs") or "").replace(";",",")
        ids=[_i(x) for x in raw.split(",") if x.strip()]
        ring=clean_ring([point_map[x] for x in ids if x in point_map])
        if rid and len(ring)>=3:
            output.append(GeoRegion(source_id=rid,polygon=ring,material_source_id=mapping.get(rid),
                                    name=node.findtext("Name") or f"Region {rid}",source_type="GeoStudio"))
    return output


def _materials(root):
    output=[]
    for node in root.findall(".//Materials/Material"):
        stress=node.find("StressStrain")
        unit=_f(stress.findtext("UnitWeight") if stress is not None else None,20.0)
        sat=_f(stress.findtext("SaturatedUnitWeight") if stress is not None else None,unit)
        output.append(Material(
            name=node.findtext("Name") or "Material",
            unit_weight=unit,saturated_unit_weight=sat,
            cohesion=_f(stress.findtext("CohesionPrime") if stress is not None else None,0.0),
            phi=_f(stress.findtext("PhiPrime") if stress is not None else None,30.0),
            source_id=_i(node.findtext("ID") or node.attrib.get("ID")),
            slope_model=node.findtext("SlopeModel") or "Mohr-Coulomb",
            color=_color(node.findtext("Color"))))
    return output


def _analysis_directory(base:Path,analysis_name:str)->Path|None:
    target=(analysis_name or "").strip().lower()
    candidates=[p for p in base.rglob("*") if p.is_dir() and p.name.strip().lower()==target]
    return candidates[0] if candidates else None


def _result_file(base:Path,analysis_name:str,filename:str)->Path|None:
    directory=_analysis_directory(base,analysis_name)
    if directory:
        exact=list(directory.rglob(filename))
        if exact:return exact[0]
    files=list(base.rglob(filename))
    return files[0] if files else None


def _surfaces(path:Path|None)->list[SlipSurface]:
    if path is None:return []
    output=[]
    with path.open(encoding="utf-8-sig",newline="") as stream:
        for row in csv.DictReader(stream):
            output.append(SlipSurface(
                number=_i(row.get("SlipNum")),fs=_f(row.get("SlipFOS"),998.0),
                center_x=_f(row.get("SlipCenterX")),center_y=_f(row.get("SlipCenterY")),
                radius=_f(row.get("SlipRadius")),volume=_f(row.get("SlipVolume")),
                weight=_f(row.get("SlipWeight")),resisting_force=_f(row.get("ResistingForce")),
                activating_force=_f(row.get("ActivatingForce")),resisting_moment=_f(row.get("ResistingMoment")),
                activating_moment=_f(row.get("ActivatingMoment")),method="Morgenstern-Price",
                source="GeoStudio reference"))
    return output


def _slice_rows(base:Path,analysis_name:str,number:int):
    path=_result_file(base,analysis_name,f"slice_{number}.csv");column=False
    if path is None:
        path=_result_file(base,analysis_name,f"column_{number}.csv");column=path is not None
    if path is None:return [],[],False,None
    output=[];path_points=[]
    with path.open(encoding="utf-8-sig",newline="") as stream:
        rows=list(csv.DictReader(stream))
    for row in rows:
        if column:
            x1=_f(row.get("XLeft"));x2=_f(row.get("XRight"));yb1=_f(row.get("YBackBotLeft"));yb2=_f(row.get("YBackBotRight"))
            yt1=_f(row.get("YBackTopLeft"));yt2=_f(row.get("YBackTopRight"));length=math.hypot(x2-x1,yb2-yb1)
            normal=_f(row.get("NormalEffectiveStress"));friction=_f(row.get("FrictionalStrength"))
            phi=math.degrees(math.atan2(friction,normal))
            data=SliceData(number=_i(row.get("ColumnNum")),x_left=x1,x_right=x2,
                           y_bot_left=yb1,y_bot_right=yb2,y_top_left=yt1,y_top_right=yt2,
                           weight=_f(row.get("ColumnWeight")),pore_pressure=_f(row.get("PoreWaterPressure")),
                           base_length=length,base_angle=_f(row.get("BotAlphaX")),
                           cohesion=_f(row.get("CohesiveStrength")),phi=phi,
                           surcharge_vertical=_f(row.get("SurchargeForceY"))+_f(row.get("PointForceY"))+_f(row.get("ReinforcementForceY")),
                           surcharge_horizontal=_f(row.get("SurchargeForceX"))+_f(row.get("PointForceX"))+_f(row.get("ReinforcementForceX")),
                           seismic_horizontal=_f(row.get("SeismicForceX")))
        else:
            data=SliceData(
                number=_i(row.get("SliceNum")),x_left=_f(row.get("XLeft")),x_right=_f(row.get("XRight")),
                y_bot_left=_f(row.get("YBotLeft")),y_bot_right=_f(row.get("YBotRight")),
                y_top_left=_f(row.get("YTopLeft")),y_top_right=_f(row.get("YTopRight")),
                weight=_f(row.get("SliceWeight")),pore_pressure=_f(row.get("PoreWaterPressure")),
                base_length=_f(row.get("BaseLength")),base_angle=_f(row.get("RelativeBaseAngle")),
                cohesion=_f(row.get("CohesiveStrength")),phi=math.degrees(_f(row.get("BasePhi"))),
                surcharge_vertical=_f(row.get("YSurchargeForce"))+_f(row.get("YPointForce"))+_f(row.get("YReinfForce")),
                surcharge_horizontal=_f(row.get("XSurchargeForce"))+_f(row.get("XPointForce"))+_f(row.get("XReinfForce")),
                seismic_horizontal=_f(row.get("XSeismicForce")))
        output.append(data)
        if not path_points:path_points.append((data.x_left,data.y_bot_left))
        path_points.append((data.x_right,data.y_bot_right))
    return output,path_points,column,rows


def _lambda(base,analysis_name,number):
    path=_result_file(base,analysis_name,f"lambdafos_{number}.csv")
    if path is None:return []
    output=[]
    with path.open(encoding="utf-8-sig",newline="") as stream:
        for row in csv.DictReader(stream):
            def optional(key):
                value=(row.get(key) or "").strip()
                return float(value) if value else None
            output.append(LambdaPoint(_f(row.get("Lambda")),optional("FOSByForce"),optional("FOSByMoment")))
    return output


def read_gsz(path:str|Path)->GeoStudioModel:
    path=Path(path)
    if not path.exists():raise FileNotFoundError(path)
    with TemporaryDirectory() as directory:
        base=Path(directory)
        with zipfile.ZipFile(path) as archive:archive.extractall(base)
        root=ET.parse(_root_xml(base)).getroot();analysis=_analysis(root)
        model=GeoStudioModel(source_file=str(path),name=path.stem)
        if analysis is not None:
            model.analysis_name=analysis.findtext("Name") or model.analysis_name
            model.method=analysis.findtext("Method") or model.method
            model.slip_direction=analysis.findtext("SlipDirection") or model.slip_direction
        geometry=_geometry(root,analysis);point_map=_points(root,geometry)
        model.points=[point_map[k] for k in sorted(point_map)]
        model.regions=_regions(root,geometry,point_map)
        model.terrain=upper_envelope(model.regions)
        model.materials=_materials(root) or model.materials
        entry_exit=root.find(".//SlopeItems/SlopeItem/Entry/SlipEntryExit")
        if entry_exit is not None:
            def point(tag):
                node=entry_exit.find(tag)
                return (_f(node.attrib.get("X")),_f(node.attrib.get("Y"))) if node is not None else None
            model.search_range=SearchRange(point("LeftSideLeftPt"),point("LeftSideRightPt"),
                                           point("RightSideLeftPt"),point("RightSideRightPt"))
            if model.search_range.entry_left and model.search_range.entry_right:
                model.search.entry_x_min,model.search.entry_x_max=sorted([model.search_range.entry_left[0],model.search_range.entry_right[0]])
            if model.search_range.exit_left and model.search_range.exit_right:
                model.search.exit_x_min,model.search.exit_x_max=sorted([model.search_range.exit_left[0],model.search_range.exit_right[0]])
        model.reference_surfaces=_surfaces(_result_file(base,model.analysis_name,"slip_surface.csv"))
        valid=[surface for surface in model.reference_surfaces if 0<surface.fs<100 and math.isfinite(surface.fs)]
        model.reference_critical=min(valid,key=lambda surface:surface.fs) if valid else None
        if model.reference_critical:
            slices,path_points,_,_= _slice_rows(base,model.analysis_name,model.reference_critical.number)
            model.reference_critical.slices_data=slices
            model.reference_critical.path=path_points
            model.lambda_curve=_lambda(base,model.analysis_name,model.reference_critical.number)
        # On import, show reference results until IMSAT analysis is run.
        model.critical=model.reference_critical
        return model


def read_critical_slice_rows(path:str|Path):
    """Return reference Fs, raw critical CSV rows and whether it is a column table."""
    path=Path(path)
    with TemporaryDirectory() as directory:
        base=Path(directory)
        with zipfile.ZipFile(path) as archive:archive.extractall(base)
        root=ET.parse(_root_xml(base)).getroot();analysis=_analysis(root)
        name=analysis.findtext("Name") if analysis is not None else ""
        surfaces=_surfaces(_result_file(base,name,"slip_surface.csv"))
        valid=[s for s in surfaces if 0<s.fs<100 and math.isfinite(s.fs)]
        if not valid:raise ValueError("GSZ không có mặt trượt tham chiếu hợp lệ.")
        critical=min(valid,key=lambda s:s.fs)
        slices,path,column,rows=_slice_rows(base,name,critical.number)
        return critical.fs,rows,column,critical.number
