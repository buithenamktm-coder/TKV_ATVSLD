"""Independent GEOS overlay + NumPy plane fit + centroid integration oracle.
Inputs are private DXF geometry JSON and the production --grid-fixture JSONL.
Output contains aggregate quantities/handles only, no survey coordinates.
This does not certify AutoCAD loading, rendering, or the original survey.
"""
import argparse, json, math
from pathlib import Path
import numpy as np
from shapely.geometry import Polygon, LineString
from shapely.ops import split, unary_union
from shapely.strtree import STRtree


def polygons(shape):
    if shape.geom_type == 'Polygon':
        if shape.area > 0: yield shape
    elif hasattr(shape, 'geoms'):
        for child in shape.geoms: yield from polygons(child)


def main():
    ap=argparse.ArgumentParser();ap.add_argument('fixture');ap.add_argument('production')
    a=ap.parse_args(); data=json.loads(Path(a.fixture).read_text())
    origin=np.array(data['existing'][0][0][:2])
    def ring(points): return Polygon(np.array(points)-origin)
    def prepare(triangles):
        shapes=[]; planes=[]
        for tri in triangles:
            xyz=np.array(tri); xy=xyz[:,:2]-origin; poly=Polygon(xy)
            if poly.area == 0: continue  # zero XY measure, not a missing cell
            matrix=np.column_stack((xy,np.ones(3)))
            planes.append(np.linalg.solve(matrix,xyz[:,2]));shapes.append(poly)
        return shapes,planes,STRtree(shapes)
    ht,hz,hi=prepare(data['existing']);tk,tz,ti=prepare(data['design'])
    production={}
    for line in Path(a.production).read_text().splitlines():
        if line.startswith('{"gridFixture":'):
            r=json.loads(line);production[r['region'],r['id']]=r
    failed=0; tested=0
    for region in data['regions']:
        boundary=ring(region['polygon'])
        if not boundary.is_valid: raise ValueError('Invalid source boundary')
        for cell in data['cells']:
            target=ring(cell['polygon']).intersection(boundary)
            hidx=hi.query(target); tidx=ti.query(target)
            hp={int(i):ht[i].intersection(target) for i in hidx}
            tp={int(i):tk[i].intersection(target) for i in tidx}
            overlap=max([sum(p.area for p in parts.values())-unary_union(list(parts.values())).area for parts in (hp,tp)])
            cut=fill=covered=0.0
            for i,ph in hp.items():
                if ph.is_empty: continue
                for j in ti.query(ph):
                    shape=ph.intersection(tk[j]); coeff=hz[i]-tz[j]
                    grad=math.hypot(coeff[0],coeff[1])
                    for piece in polygons(shape):
                        covered+=piece.area
                        pieces=[piece]
                        if grad > 0:
                            normal=coeff[:2]/grad
                            centre=np.array([piece.centroid.x,piece.centroid.y])
                            point=centre-normal*((coeff[:2]@centre+coeff[2])/grad)
                            tangent=np.array([-normal[1],normal[0]])
                            radius=2*(math.hypot(piece.bounds[2]-piece.bounds[0],piece.bounds[3]-piece.bounds[1])+np.linalg.norm(point-centre)+1)
                            pieces=polygons(split(piece,LineString([point-radius*tangent,point+radius*tangent])))
                        for part in pieces:
                            z=float(coeff[0]*part.centroid.x+coeff[1]*part.centroid.y+coeff[2])
                            if z>=0: cut+=part.area*z
                            else: fill-=part.area*z
            p=production.get((region['id'],cell['id']))
            # Absolute area 1e-5 m² and volume 1e-4 m³ tolerances allow
            # independent translated GEOS vs mine-coordinate CAD double math.
            errors={} if not p or p['status']!='CALCULATED' else {
                'area':abs(p['area']-target.area),'covered':abs(p['covered']-covered),
                'cut':abs(p['cut']-cut),'fill':abs(p['fill']-fill)}
            if overlap>1e-5:
                ok=bool(p and p['status']=='REJECTED')
            else:
                ok=bool(errors and errors['area']<1e-5 and errors['covered']<1e-5 and errors['cut']<1e-4 and errors['fill']<1e-4)
            tested+=1;failed+=not ok
            print(json.dumps(dict(region=region['id'],cell=cell['id'],status='PASS' if ok else 'FAIL',
                oracle_area=target.area,oracle_covered=covered,oracle_cut=cut,oracle_fill=fill,
                overlap_area=overlap,absolute_errors=errors)),flush=True)
    print(json.dumps(dict(tested=tested,failed=failed,source_sha256=data['source_sha256'],host='NOT_TESTED')))
    if failed: raise SystemExit(1)


if __name__=='__main__': main()
