from __future__ import annotations
import json
from pathlib import Path
from app.io.gsz_reader import read_gsz
from app.core.geometry import model_bounds, upper_envelope, validate_model

EXPECTED={
'MC 20-20.gsz':(2,2),'MC B1-B2.gsz':(2,2),'MC C1-C2.gsz':(2,2),'MC D1-D2.gsz':(2,2),
'MC E1-E2.gsz':(2,2),'MC F1-F2.gsz':(2,2),'MC TIII.gsz':(12,4),'MC TV.gsz':(9,3)}

def main():
    base=Path(__file__).resolve().parents[1]/'examples'; report={'status':'PASS','models':[]}
    for name,(regions,materials) in EXPECTED.items():
        m=read_gsz(base/name); issues=validate_model(m); errors=[i for i in issues if i.severity=='error']
        assert len(m.regions)==regions,(name,len(m.regions)); assert len(m.materials)==materials,(name,len(m.materials))
        assert all(len(r.polygon)>=3 for r in m.regions); assert all(r.material_source_id is not None for r in m.regions)
        terrain=upper_envelope(m.regions); assert len(terrain)>=2; assert all(terrain[i][0] <= terrain[i+1][0]+1e-9 for i in range(len(terrain)-1))
        assert not errors,(name,[e.message for e in errors]); b=model_bounds(m); assert b[2]>b[0] and b[3]>b[1]
        report['models'].append({'file':name,'regions':len(m.regions),'materials':len(m.materials),'terrain_points':len(terrain),'surfaces':len(m.surfaces),'critical_fs':m.critical.fs if m.critical else None,'geometry_errors':len(errors)})
    out=Path(__file__).with_name('V6_GEOMETRY_VALIDATION.json'); out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8'); print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
