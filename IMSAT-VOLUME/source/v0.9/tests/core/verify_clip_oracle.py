"""Independent GEOS oracle on triangles emitted by the actual production C# builder.
Usage: python verify_clip_oracle.py output.jsonl
Input contains only synthetic fixtures; never publish coordinate-bearing mine data.
"""
import json
import sys
from shapely.geometry import Polygon, box
from shapely.ops import unary_union

count = 0
for line in open(sys.argv[1], encoding="utf-8"):
    if not line.startswith('{"oracleCase":'):
        continue
    data = json.loads(line)
    target = Polygon(data["boundary"]).intersection(box(0, 0, 10, 10))
    triangles = [Polygon([(v[0], v[1]) for v in t]) for t in data["triangles"]]
    union = unary_union(triangles)
    missing = target.difference(union).area
    outside = union.difference(target).area
    overlap = sum(t.area for t in triangles) - union.area
    plane_error = max(abs(v[2] - (2*v[0] - 3*v[1] + 7)) for t in data["triangles"] for v in t)
    passed = max(missing, outside, abs(overlap), plane_error) < 1e-8
    print(json.dumps(dict(case=data["oracleCase"], status="PASS" if passed else "FAIL",
        triangles=len(triangles), area=union.area, missing=missing, outside=outside,
        overlap=overlap, max_plane_z_error=plane_error)))
    assert passed, "Production clip differs from independent GEOS polygon overlay"
    count += 1
assert count == 4, "Missing production fixture output"
