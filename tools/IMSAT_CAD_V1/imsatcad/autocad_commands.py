"""Testable AutoCAD-style command aliases and geometry subset for IMSAT CAD.

WARNING: command/name compatibility is not full AutoCAD behavioral compatibility.
Unsupported entity types or invalid geometry raise CommandError (never silently
ignore an operation). Edits must be snapshot by the calling document controller.
"""
from __future__ import annotations

from dataclasses import dataclass
import math
import re
import shlex
from ezdxf.math import Matrix44, Vec3

ALIASES = {
    'L': 'LINE', 'LINE': 'LINE',
    'PL': 'PLINE', 'PLINE': 'PLINE', 'POLYLINE': 'PLINE',
    'C': 'CIRCLE', 'CIRCLE': 'CIRCLE',
    'REC': 'RECTANG', 'RECTANG': 'RECTANG', 'RECTANGLE': 'RECTANG',
    'A': 'ARC', 'ARC': 'ARC',
    'E': 'ERASE', 'ERASE': 'ERASE', 'DELETE': 'ERASE',
    'CO': 'COPY', 'CP': 'COPY', 'COPY': 'COPY',
    'M': 'MOVE', 'MOVE': 'MOVE',
    'RO': 'ROTATE', 'ROTATE': 'ROTATE',
    'SC': 'SCALE', 'SCALE': 'SCALE',
    'MI': 'MIRROR', 'MIRROR': 'MIRROR',
    'O': 'OFFSET', 'OFFSET': 'OFFSET',
    'TR': 'TRIM', 'TRIM': 'TRIM',
    'EX': 'EXTEND', 'EXTEND': 'EXTEND',
    'DI': 'DIST', 'DIST': 'DIST', 'DISTANCE': 'DIST',
    'ID': 'ID',
    'U': 'UNDO', 'UNDO': 'UNDO', 'REDO': 'REDO',
    'Z': 'ZOOM', 'ZOOM': 'ZOOM',
    'PAN': 'PAN', 'P': 'PAN',
    'LA': 'LAYER', 'LAYER': 'LAYER',
    'OS': 'OSNAP', 'OSNAP': 'OSNAP',
    'F8': 'ORTHO', 'ORTHO': 'ORTHO',
    'F7': 'GRID', 'GRID': 'GRID',
    'QSAVE': 'QSAVE', '_QSAVE': 'QSAVE',
    'SAVEAS': 'SAVEAS', '_SAVEAS': 'SAVEAS',
    'OPEN': 'OPEN', '_OPEN': 'OPEN',
    'NEW': 'NEW', '_NEW': 'NEW',
}

DRAW_COMMANDS = {'LINE', 'PLINE', 'CIRCLE', 'RECTANG', 'ARC'}
EDIT_COMMANDS = {'ERASE', 'COPY', 'MOVE', 'ROTATE', 'SCALE', 'MIRROR', 'OFFSET', 'TRIM', 'EXTEND'}


class CommandError(ValueError):
    pass


@dataclass(frozen=True)
class Result:
    name: str
    changed: bool = False
    message: str = ''
    created_handle: str | None = None


def resolve_alias(value: str) -> str:
    v = value.upper().strip().lstrip('_.')
    return ALIASES.get(v, v)


def point(value: str, origin: Vec3 | tuple[float, float, float] | None = None) -> Vec3:
    """Read X,Y[,Z], relative @X,Y, or relative polar @length<degrees."""
    if not value:
        raise CommandError('Coordinate is missing')
    relative = value.startswith('@')
    text = value[1:] if relative else value.lstrip('#')
    if '<' in text:
        if not relative or origin is None:
            raise CommandError('Polar point requires an @ and reference point')
        try:
            length, degrees = (float(x) for x in text.split('<', 1))
        except ValueError as e:
            raise CommandError('Polar syntax is @distance<angle') from e
        numbers = (length*math.cos(math.radians(degrees)), length*math.sin(math.radians(degrees)), 0.0)
    else:
        try:
            parts = text.split(',')
            if len(parts) not in (2, 3):
                raise ValueError()
            numbers = tuple(float(x) for x in parts)
            if len(numbers)==2:
                numbers += (0.0,)
        except ValueError as e:
            raise CommandError('Point syntax is X,Y or X,Y,Z') from e
    if not all(math.isfinite(x) for x in numbers):
        raise CommandError('Coordinates must be finite')
    if relative:
        if origin is None:
            raise CommandError('Relative coordinates require a reference point')
        return Vec3(origin) + Vec3(numbers)
    return Vec3(numbers)


def number(value: str, name: str = 'Value') -> float:
    try:
        n = float(value)
    except ValueError as e:
        raise CommandError(f'{name} must be a number') from e
    if not math.isfinite(n):
        raise CommandError(f'{name} must be finite')
    return n


def _entity(document, selected_handle: str | None):
    if not selected_handle:
        raise CommandError('Select an entity before running this command')
    e = document.entitydb.get(selected_handle)
    if e is None or not e.is_alive or e.get_layout() != document.modelspace():
        raise CommandError('The selected entity is missing or not in modelspace')
    return e


def _ensure_args(args, n_min, n_max=None):
    if n_max is None: n_max=n_min
    if not n_min <= len(args) <= n_max:
        raise CommandError(f'Expected {n_min}' + (f'–{n_max}' if n_min!=n_max else '') + f' arguments; received {len(args)}')


def _intersection(a0, a1, b0, b1):
    """Unbounded XY line intersection, with line parameters (ta, tb)."""
    ax, ay = a1.x-a0.x, a1.y-a0.y
    bx, by = b1.x-b0.x, b1.y-b0.y
    det=ax*by-ay*bx
    scale=max(math.hypot(ax, ay)*math.hypot(bx, by), 1.0)
    if abs(det)<1e-12*scale:
        raise CommandError('Lines are parallel or degenerate')
    dx,dy=b0.x-a0.x,b0.y-a0.y
    t=(dx*by-dy*bx)/det
    u=(dx*ay-dy*ax)/det
    z=a0.z+t*(a1.z-a0.z)
    return Vec3(a0.x+t*ax,a0.y+t*ay,z),t,u


def _line_edge(doc, handle):
    e = doc.entitydb.get(handle)
    if e is None or not e.is_alive or e.dxftype()!='LINE' or e.get_layout()!=doc.modelspace():
        raise CommandError('Boundary must be an existing LINE in modelspace')
    return e


def execute(document, line: str, *, selected_handle: str | None = None, layer: str='0') -> Result:
    """Execute geometry-only commands; UI/global commands handled by controller.

    E.g. L 0,0 10,10; C 5,5 3; REC 0,0 100,50; CO 20,0;
    M 10,5; RO 45 0,0; MI 0,0 0,20; O 2;
    TR <boundary-handle> 10,0 and EX <boundary-handle> 10,0.
    """
    try:
        args = shlex.split(line)
    except ValueError as e:
        raise CommandError('Invalid quoted arguments') from e
    if not args:
        raise CommandError('Empty command')
    name=resolve_alias(args.pop(0))
    msp=document.modelspace()
    attrs={'layer': layer}
    if name=='LINE':
        _ensure_args(args,2)
        a=point(args[0]);b=point(args[1],a)
        if a.isclose(b): raise CommandError('Line endpoints must be different')
        e=msp.add_line(a,b,dxfattribs=attrs)
    elif name=='PLINE':
        if len(args)<2: raise CommandError('PLINE needs at least 2 points')
        points=[]
        for arg in args: points.append(point(arg, points[-1] if points else None))
        if any(a.isclose(b) for a,b in zip(points,points[1:])):
            raise CommandError('Consecutive polyline vertices must be distinct')
        if any(abs(p.z)>1e-9 for p in points):
            e=msp.add_polyline3d(points,dxfattribs=attrs)
        else:
            e=msp.add_lwpolyline(points,dxfattribs=attrs)
    elif name=='RECTANG':
        _ensure_args(args,2)
        a=point(args[0]);b=point(args[1],a)
        if a.x==b.x or a.y==b.y: raise CommandError('Rectangle must have nonzero width and height')
        if not math.isclose(a.z,b.z): raise CommandError('Rectangle corners must share Z')
        e=msp.add_lwpolyline([(a.x,a.y),(b.x,a.y),(b.x,b.y),(a.x,b.y)],close=True,dxfattribs={**attrs,'elevation':a.z})
    elif name=='CIRCLE':
        _ensure_args(args,2)
        c=point(args[0]);radius=number(args[1],'Radius')
        if radius<=0: raise CommandError('Circle radius must be positive')
        e=msp.add_circle(c,radius,dxfattribs=attrs)
    elif name=='ARC':
        _ensure_args(args,4)
        c=point(args[0]);radius=number(args[1],'Radius')
        if radius<=0: raise CommandError('Arc radius must be positive')
        e=msp.add_arc(c,radius,number(args[2],'Start angle'),number(args[3],'End angle'),dxfattribs=attrs)
    elif name=='DIST':
        _ensure_args(args,2)
        a=point(args[0]);b=point(args[1],a)
        return Result(name, message=f'Distance: {a.distance(b):.6f}')
    elif name=='ID':
        _ensure_args(args,1)
        p=point(args[0]);return Result(name,message=f'X={p.x:.6f}; Y={p.y:.6f}; Z={p.z:.6f}')
    elif name in EDIT_COMMANDS:
        e=_entity(document,selected_handle)
        if name=='ERASE':
            _ensure_args(args,0);msp.delete_entity(e)
            return Result(name,changed=True)
        if name in ('MOVE','COPY'):
            _ensure_args(args,1)
            delta=point(args[0],(0,0,0))
            if name=='COPY':
                clone=e.copy(); clone.translate(delta.x,delta.y,delta.z)
                msp.add_entity(clone)
                return Result(name,changed=True,created_handle=clone.dxf.handle)
            e.translate(delta.x,delta.y,delta.z)
        elif name=='ROTATE':
            _ensure_args(args,1,2)
            angle=math.radians(number(args[0],'Angle'))
            c=point(args[1]) if len(args)==2 else Vec3(0,0,0)
            try:
                e.transform(Matrix44.chain(Matrix44.translate(-c.x,-c.y,-c.z), Matrix44.z_rotate(angle), Matrix44.translate(c.x,c.y,c.z)))
            except Exception as exc:
                raise CommandError(f'Rotation unsupported for this entity: {exc}') from exc
        elif name=='SCALE':
            _ensure_args(args,1,2)
            factor=number(args[0],'Scale factor')
            if factor<=0: raise CommandError('Scale factor must be positive')
            c=point(args[1]) if len(args)==2 else Vec3(0,0,0)
            try:
                e.transform(Matrix44.chain(Matrix44.translate(-c.x,-c.y,-c.z), Matrix44.scale(factor,factor,factor), Matrix44.translate(c.x,c.y,c.z)))
            except Exception as exc:
                raise CommandError(f'Scale unsupported for this entity: {exc}') from exc
        elif name=='MIRROR':
            _ensure_args(args,2)
            a=point(args[0]);b=point(args[1]); angle=math.atan2(b.y-a.y,b.x-a.x)
            if math.hypot(b.x-a.x,b.y-a.y)<1e-10: raise CommandError('Mirror axis needs two different XY points')
            try:
                e.transform(Matrix44.chain(Matrix44.translate(-a.x,-a.y,0),Matrix44.z_rotate(-angle),Matrix44.scale(1,-1,1),Matrix44.z_rotate(angle),Matrix44.translate(a.x,a.y,0)))
            except Exception as exc:
                raise CommandError(f'Mirror unsupported for this entity: {exc}') from exc
        elif name=='OFFSET':
            _ensure_args(args,1)
            dist=number(args[0],'Offset')
            kind=e.dxftype()
            if kind=='LINE':
                a,b=Vec3(e.dxf.start),Vec3(e.dxf.end)
                dx,dy=b.x-a.x,b.y-a.y
                l=math.hypot(dx,dy)
                if l<=1e-10:raise CommandError('Degenerate line')
                o=Vec3(-dy*dist/l,dx*dist/l,0)
                copy=msp.add_line(a+o,b+o,dxfattribs={'layer':e.dxf.layer})
            elif kind=='CIRCLE':
                radius=e.dxf.radius+dist
                if radius<=0:raise CommandError('Offset would yield nonpositive radius')
                copy=msp.add_circle(e.dxf.center,radius,dxfattribs={'layer':e.dxf.layer})
            else:
                raise CommandError('OFFSET currently supports LINE and CIRCLE only')
            return Result(name,changed=True,created_handle=copy.dxf.handle)
        elif name in ('TRIM','EXTEND'):
            _ensure_args(args,2)
            if e.dxftype()!='LINE': raise CommandError('TRIM/EXTEND currently support LINE only')
            border=_line_edge(document,args[0]);target=point(args[1]);
            a0,a1=Vec3(e.dxf.start),Vec3(e.dxf.end)
            b0,b1=Vec3(border.dxf.start),Vec3(border.dxf.end)
            inter,t,u=_intersection(a0,a1,b0,b1)
            if not -1e-9 <= u <= 1+1e-9:
                raise CommandError('Intersection is outside boundary segment')
            choose_start=target.distance(a0)<=target.distance(a1)
            if name=='TRIM':
                if not 0<t<1: raise CommandError('TRIM requires intersection inside selected line')
                if choose_start: e.dxf.start=inter
                else: e.dxf.end=inter
            else:
                if choose_start and t<0: e.dxf.start=inter
                elif not choose_start and t>1: e.dxf.end=inter
                else: raise CommandError('Selected endpoint cannot be extended to that boundary')
        return Result(name,changed=True)
    else:
        raise CommandError(f'Unknown/unsupported command: {name}')
    return Result(name, changed=True, created_handle=e.dxf.handle)
