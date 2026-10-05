import math


def clip(poly, inside, intersect):
    if not poly: return []
    out=[]; prev=poly[-1]; pin=inside(prev)
    for cur in poly:
        cin=inside(cur)
        if cin:
            if not pin: out.append(intersect(prev,cur))
            out.append(cur)
        elif pin:
            out.append(intersect(prev,cur))
        prev=cur; pin=cin
    return out


def ih(a,b,z):
    dz=b[1]-a[1]
    if abs(dz)<1e-30: return (a[0],z)
    u=(z-a[1])/dz
    return (a[0]+(b[0]-a[0])*u,z)


def poly_area(poly):
    if len(poly)<3: return 0.0
    return abs(sum(p[0]*poly[(i+1)%len(poly)][1]-poly[(i+1)%len(poly)][0]*p[1] for i,p in enumerate(poly)))/2


def band_area_segment(s0,s1,e0,e1,d0,d1,lo,hi):
    # caller supplies same-sign piece
    poly=[(s0,e0),(s1,e1),(s1,d1),(s0,d0)]
    poly=clip(poly,lambda p:p[1]>=lo-1e-9,lambda a,b:ih(a,b,lo))
    poly=clip(poly,lambda p:p[1]<=hi+1e-9,lambda a,b:ih(a,b,hi))
    return poly_area(poly)


def volume(a1,am,a2,L,prism=True):
    if prism and am is not None:
        return L*(a1+4*am+a2)/6
    return L*(a1+a2)/2


def build_bands(fr,to,step):
    sign=1 if to>fr else -1
    cur=fr; out=[]
    while (cur < to-1e-9 if sign>0 else cur > to+1e-9):
        nxt=cur+sign*step
        if sign>0 and nxt>to: nxt=to
        if sign<0 and nxt<to: nxt=to
        out.append((cur,nxt))
        cur=nxt
    return out


def test_build_bands_descending_and_partial_last_band():
    assert build_bands(150,117,10)==[(150,140),(140,130),(130,120),(120,117)]


def test_horizontal_cut_splits_equally_between_two_5m_levels():
    # existing z=10, design z=0, section length=10 => 100 m2 cut
    a0=band_area_segment(0,10,10,10,0,0,0,5)
    a1=band_area_segment(0,10,10,10,0,0,5,10)
    assert math.isclose(a0,50.0) and math.isclose(a1,50.0)
    assert math.isclose(a0+a1,100.0)


def test_sloping_cut_is_clipped_exactly_by_elevation_band():
    # existing rises linearly 0->10, design=0 over length 10; total triangle area=50
    lower=band_area_segment(0,10,0,10,0,0,0,5)
    upper=band_area_segment(0,10,0,10,0,0,5,10)
    assert math.isclose(lower,37.5)
    assert math.isclose(upper,12.5)
    assert math.isclose(lower+upper,50.0)


def test_sign_change_can_be_split_into_cut_and_fill_pieces():
    # e: +5 -> -5, d=0, zero crossing at s=5
    cut=band_area_segment(0,5,5,0,0,0,0,5)
    fill=band_area_segment(5,10,0,-5,0,0,-5,0)
    assert math.isclose(cut,12.5)
    assert math.isclose(fill,12.5)


def test_prismoid_uses_real_mid_area():
    assert math.isclose(volume(10,20,30,6,True),120.0)


def test_average_end_area_fallback():
    assert math.isclose(volume(10,None,30,6,False),120.0)


def test_sum_of_band_prismoids_equals_prismoid_of_summed_band_areas():
    # linearity is the invariant guaranteeing detail total == level total before display rounding
    bands=[(10,14,18),(5,7,9),(2,3,4)]
    L=25
    by_band=sum(volume(a,m,b,L,True) for a,m,b in bands)
    summed=volume(sum(x[0] for x in bands),sum(x[1] for x in bands),sum(x[2] for x in bands),L,True)
    assert math.isclose(by_band,summed,rel_tol=1e-12,abs_tol=1e-12)