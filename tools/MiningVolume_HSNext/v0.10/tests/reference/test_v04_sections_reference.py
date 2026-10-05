import math


def point_on_segment(p,a,b,tol=1e-9):
    cross=abs((b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0]))
    ln=math.hypot(b[0]-a[0], b[1]-a[1])
    if ln<=tol: return math.hypot(p[0]-a[0],p[1]-a[1])<=tol
    if cross/ln>tol: return False
    dot=(p[0]-a[0])*(p[0]-b[0])+(p[1]-a[1])*(p[1]-b[1])
    return dot<=tol*tol


def pip(p,poly,tol=1e-9):
    inside=False
    j=len(poly)-1
    for i,a in enumerate(poly):
        b=poly[j]
        if point_on_segment(p,b,a,tol): return True
        hit=((b[1]>p[1])!=(a[1]>p[1])) and (p[0] < (a[0]-b[0])*(p[1]-b[1])/((a[1]-b[1]) or 1e-300)+b[0])
        if hit: inside=not inside
        j=i
    return inside


def line_spans(poly, direction, offset, tol=1e-7):
    l=math.hypot(*direction); d=(direction[0]/l,direction[1]/l); n=(-d[1],d[0])
    dot=lambda p,v:p[0]*v[0]+p[1]*v[1]
    ts=[]
    for i,a in enumerate(poly):
        b=poly[(i+1)%len(poly)]
        na=dot(a,n)-offset; nb=dot(b,n)-offset
        if abs(na)<=tol and abs(nb)<=tol:
            ts += [dot(a,d),dot(b,d)]; continue
        if (na>tol and nb>tol) or (na<-tol and nb<-tol): continue
        den=na-nb
        if abs(den)<=tol: continue
        u=max(0,min(1,na/den))
        p=(a[0]+(b[0]-a[0])*u,a[1]+(b[1]-a[1])*u)
        ts.append(dot(p,d))
    ts=sorted(ts)
    uq=[]
    for t in ts:
        if not uq or abs(t-uq[-1])>tol*10: uq.append(t)
    spans=[]
    for a,b in zip(uq,uq[1:]):
        if b-a<=tol: continue
        tm=(a+b)/2; pm=(d[0]*tm+n[0]*offset,d[1]*tm+n[1]*offset)
        if pip(pm,poly,tol*10): spans.append((a,b))
    return spans


def area(length,d0,d1):
    cut=fill=0.0
    if d0>=0 and d1>=0: cut=.5*(d0+d1)*length
    elif d0<=0 and d1<=0: fill=.5*(-d0-d1)*length
    else:
        a,b=abs(d0),abs(d1); l1=length*a/(a+b); l2=length-l1
        A=.5*a*l1; B=.5*b*l2
        if d0>0: cut,fill=A,B
        else: fill,cut=A,B
    return cut,fill


def test_rectangle_section_spans_are_full_width():
    poly=[(0,0),(100,0),(100,50),(0,50)]
    for y in (0,10,20,30,40,50):
        spans=line_spans(poly,(1,0),y)
        assert spans == [(0,100)]


def test_concave_boundary_preserves_multiple_inside_spans():
    # U shape; at y=40 a horizontal section crosses two separate arms.
    poly=[(0,0),(100,0),(100,100),(70,100),(70,30),(30,30),(30,100),(0,100)]
    spans=line_spans(poly,(1,0),40)
    assert spans == [(0,30),(70,100)]


def test_cut_area_constant_difference():
    cut,fill=area(100,10,10)
    assert cut==1000 and fill==0


def test_fill_area_constant_difference():
    cut,fill=area(100,-4,-4)
    assert cut==0 and fill==400


def test_sign_change_is_split_exactly():
    cut,fill=area(10,2,-2)
    assert math.isclose(cut,5.0)
    assert math.isclose(fill,5.0)


def test_asymmetric_sign_change():
    cut,fill=area(12,3,-1)
    # zero at 9 m: cut triangular area 13.5; fill triangle 1.5
    assert math.isclose(cut,13.5)
    assert math.isclose(fill,1.5)