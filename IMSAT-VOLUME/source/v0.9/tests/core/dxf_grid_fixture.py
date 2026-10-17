"""Read existing DXF 3DFACEs and exported cells without changing the drawing.
Private geometry is written ONLY to the supplied local output; never commit it.
Explicit layer/region selection avoids treating drawing handles as app constants.
"""
import argparse, hashlib, json
from pathlib import Path
from ezdxf.addons import iterdxf
from dxf_fixture import vertices


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('dxf'); ap.add_argument('output')
    ap.add_argument('--layers', nargs=2, required=True)
    ap.add_argument('--regions', nargs='+', required=True)
    ap.add_argument('--cell-layers', nargs='+', required=True)
    args = ap.parse_args()
    data = dict(existing=[], design=[], cells=[], regions=[])
    doc = iterdxf.opendxf(args.dxf)
    try:
        for e in doc.modelspace():
            if e.dxftype() == '3DFACE' and e.dxf.layer in args.layers:
                role = 'existing' if e.dxf.layer == args.layers[0] else 'design'
                pts = [list(e.dxf.get('vtx'+str(i))) for i in range(4)]
                data[role].append(pts[:3])
                if pts[3] != pts[2]: data[role].append([pts[0], pts[2], pts[3]])
            elif e.dxftype() in ('LWPOLYLINE', 'POLYLINE'):
                kind = 'regions' if e.dxf.handle in args.regions else 'cells' if e.dxf.layer in args.cell_layers else None
                if kind:
                    record = vertices(e)
                    if not record or not record[1]: raise ValueError('Expected a closed ring: '+e.dxf.handle)
                    data[kind].append(dict(id=e.dxf.handle, polygon=[[p.x,p.y] for p in record[2]]))
    finally:
        doc.close()
    if set(r['id'] for r in data['regions']) != set(args.regions): raise ValueError('Missing requested region')
    if not all(data.values()): raise ValueError('Empty surface, region or cell collection')
    data['source_sha256'] = hashlib.sha256(Path(args.dxf).read_bytes()).hexdigest()
    Path(args.output).write_text(json.dumps(data), encoding='utf-8')
    print(json.dumps(dict(source_sha256=data['source_sha256'], counts={k:len(data[k]) for k in ('existing','design','cells','regions')}, host='NOT_TESTED')))


if __name__ == '__main__': main()
