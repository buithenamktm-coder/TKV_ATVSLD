"""Create traceable QA tables from an exact-HEAD GitHub jobs response.
Does not promote a successful compile to host or functional acceptance.
"""
import argparse, csv, json
from pathlib import Path
import xml.etree.ElementTree as ET


def build(root, evidence):
    sha=evidence['source_sha']
    if len(sha)!=40 or any(c not in '0123456789abcdef' for c in sha):
        raise ValueError('Expected an exact commit SHA')
    targets=json.loads((root/'compat/target_matrix.json').read_text())['targets']
    props=ET.parse(root/'compat/IMSAT.CadCompatibility.props').getroot()
    jobs=evidence['jobs']; rows=[]
    for t in targets:
        flavor=t['flavor']
        name='build' if t['year']==2023 else 'compatibility / compatibility ('+str(flavor)+')'
        matched=[j for j in jobs if j['name']==name]
        job=matched[0] if len(matched)==1 else None
        status='COMPILE_PASS' if job and job['status']=='completed' and job['conclusion']=='success' else 'FAIL' if job and job['conclusion']=='failure' else 'NOT_TESTED'
        sdk=next((n.text for n in props.iter('IMSATCadPackageVersion') if flavor and "'"+flavor+"'" in n.get('Condition','')),'SDK_REQUIRED')
        rows.append(dict(year=t['year'],runtime=t['runtime'],update=t.get('update',''),series=t['series'],
            api=sdk,flavor=flavor or 'ADAPTER_MISSING',dll=('MiningVolume2023.dll' if t['year']==2023 else 'IMSATVolume.'+flavor+'.dll') if flavor else '',
            source_sha=sha,compile=status,host='NOT_TESTED',tin='NOT_TESTED',sections='NOT_TESTED',grid='NOT_TESTED',
            cad='NOT_TESTED',excel='NOT_TESTED',installer='NOT_TESTED',functional='BLOCKED',
            evidence=('https://github.com/buithenamktm-coder/TKV_ATVSLD/actions/runs/'+str(evidence['run_id'])+'/job/'+str(job['id'])) if job else '',
            issue='MC-008' if flavor else 'MC-006'))
    return dict(source_sha=sha,run_id=evidence['run_id'],release='BLOCKED',matrix=rows)


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--jobs',required=True);ap.add_argument('--output',required=True)
    args=ap.parse_args();root=Path(__file__).resolve().parents[1]
    result=build(root,json.loads(Path(args.jobs).read_text()))
    out=Path(args.output);out.mkdir(parents=True,exist_ok=True)
    (out/'qa-data.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    with (out/'IMSAT_V2_RELEASE_MATRIX.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=list(result['matrix'][0]));writer.writeheader();writer.writerows(result['matrix'])
    print(json.dumps(dict(source_sha=result['source_sha'],rows=len(result['matrix']),release=result['release'])))


if __name__=='__main__':main()
