"""Read-only DXF adapter for the C# regression runner. Requires ezdxf.
Private drawing coordinates stay in the output directory; never commit fixtures.
Supports the same point/line/polyline source types as CadLayerSurfaceReader.
LWPOLYLINE arcs use a maximum 0.5 m arc-length step, in WCS.
"""
import argparse, collections, json, math, pathlib, struct
from ezdxf.addons import iterdxf
from ezdxf.math import bulge_to_arc

def vertices(e):
    kind = e.dxftype()
    if kind == 'POINT': return 0, False, [e.dxf.location]
    if kind == 'LINE': return 1, False, [e.dxf.start, e.dxf.end]
    if kind == 'POLYLINE':
        if not (e.is_2d_polyline or e.is_3d_polyline): return None
        p = list(e.points_in_wcs())
        typ = 4 if e.is_3d_polyline else 3
        if typ == 3 and p and all(abs(q.z-p[0].z) <= 1e-6 for q in p): typ = 5
        return typ, e.is_closed, p
    if kind != 'LWPOLYLINE': return None
    raw = list(e.get_points('xyb')); p = []
    ocs = e.ocs(); z = e.dxf.elevation
    if not raw: return 2, e.closed, p
    p.append(ocs.to_wcs((*raw[0][:2], z)))
    for i, (x,y,b) in enumerate(raw):
        j = (i+1) % len(raw)
        if j == 0 and not e.closed: break
        q = raw[j]
        if abs(b) < 1e-12:
            if j != 0: p.append(ocs.to_wcs((*q[:2],z)))
        else:
            center, start, end, r = bulge_to_arc((x,y),q[:2],b)
            angle = 4*math.atan(b)
            start = math.atan2(y-center.y,x-center.x)
            n = max(2,math.ceil(abs(angle)*r/0.5))
            for k in range(1,n+1):
                if j == 0 and k == n: continue
                a = start + angle*k/n
                p.append(ocs.to_wcs((center.x+r*math.cos(a),center.y+r*math.sin(a),z)))
    typ = 5 if p and all(abs(q.z-p[0].z)<=1e-6 for q in p) else 2
    return typ, e.closed, p

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('dxf'); ap.add_argument('output')
    ap.add_argument('--layers',nargs=2,required=True); ap.add_argument('--boundary')
    a=ap.parse_args(); out=pathlib.Path(a.output); out.mkdir(parents=True,exist_ok=True)
    streams={layer:open(out/(role+'.bin'),'wb') for layer,role in zip(a.layers,['existing','design'])}
    stats={layer:collections.Counter() for layer in a.layers}; boundaries=[]
    try:
        doc=iterdxf.opendxf(a.dxf)
        try:
            for e in doc.modelspace():
                layer=e.dxf.layer
                if a.boundary and layer == a.boundary and e.dxftype() in ('LWPOLYLINE','POLYLINE'):
                    rec=vertices(e)
                    if rec and rec[1]: boundaries.append([[v.x,v.y] for v in rec[2]])
                if layer not in streams: continue
                stats[layer][e.dxftype()]+=1
                rec=vertices(e)
                if rec is None:
                    stats[layer]['unsupported']+=1; continue
                typ,closed,p=rec; handle=e.dxf.handle.encode('ascii')
                f=streams[layer]; f.write(struct.pack('<BBII',typ,closed,len(handle),len(p))); f.write(handle)
                for v in p: f.write(struct.pack('<ddd',v.x,v.y,v.z))
                stats[layer]['vertices']+=len(p); stats[layer]['supported']+=1
        finally: doc.close()
    finally:
        for f in streams.values(): f.close()
    if a.boundary:
        if len(boundaries)!=1: raise ValueError(f'Expected one closed boundary, found {len(boundaries)}')
        with open(out/'boundary.bin','wb') as f:
            f.write(struct.pack('<I',len(boundaries[0])))
            for xy in boundaries[0]: f.write(struct.pack('<dd',*xy))
    report={'layers':stats,'boundary_vertices':len(boundaries[0]) if boundaries else 0,
            'adapter':'ezdxf WCS; LW arc step 0.5m; not an AutoCAD runtime test'}
    (out/'input-summary.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(json.dumps(report),flush=True)
if __name__=='__main__': main()
