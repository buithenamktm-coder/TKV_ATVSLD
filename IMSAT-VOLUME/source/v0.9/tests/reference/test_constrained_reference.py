# Reference-only geometric check using Shapely/GEOS available in the build sandbox.
# It is not shipped as runtime code. Purpose: verify the intended geometry behavior.
from shapely import LineString, MultiLineString
from shapely.ops import unary_union

def test_crossing_breaklines_are_noded_by_reference_engine():
    lines=MultiLineString([[(0,0),(10,10)],[(0,10),(10,0)]])
    noded=unary_union(lines)
    # GEOS nodes the crossing into four segments; MiningVolume pre-validator deliberately
    # rejects the un-noded input so the user can control the shared Z at the intersection.
    assert len(list(noded.geoms)) == 4