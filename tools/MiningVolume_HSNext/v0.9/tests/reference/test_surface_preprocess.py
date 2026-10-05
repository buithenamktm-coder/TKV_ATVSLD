import math

TOL=1e-6

def proper_cross(a,b,c,d,tol=TOL):
    def cross(a,b,c): return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    def same(p,q): return abs(p[0]-q[0])<=tol and abs(p[1]-q[1])<=tol
    if same(a,c) or same(a,d) or same(b,c) or same(b,d): return False
    x1,x2,x3,x4=cross(a,b,c),cross(a,b,d),cross(c,d,a),cross(c,d,b)
    return ((x1>tol and x2<-tol) or (x1<-tol and x2>tol)) and ((x3>tol and x4<-tol) or (x3<-tol and x4>tol))

def segments_for_vertices(enabled, closed=False):
    out=[]
    n=len(enabled)
    for i in range(n-1):
        if enabled[i] and enabled[i+1]: out.append((i,i+1))
    if closed and n>2 and enabled[-1] and enabled[0]: out.append((n-1,0))
    return out

def interp_z(p,a,b):
    dx=b[0]-a[0]; dy=b[1]-a[1]
    den=dx*dx+dy*dy
    t=((p[0]-a[0])*dx+(p[1]-a[1])*dy)/den
    return a[2]+max(0,min(1,t))*(b[2]-a[2])

def test_removed_middle_vertex_does_not_create_shortcut():
    assert segments_for_vertices([True,False,True]) == []

def test_closed_polyline_preserves_only_original_adjacency():
    assert segments_for_vertices([True,False,True,True], closed=True) == [(2,3),(3,0)]

def test_shared_endpoint_not_reported_as_crossing():
    assert not proper_cross((0,0),(1,1),(1,1),(2,0))

def test_true_breakline_crossing_is_reported():
    assert proper_cross((0,0),(2,2),(0,2),(2,0))

def test_steiner_z_is_linear_on_breakline():
    assert math.isclose(interp_z((5,0),(0,0,100),(10,0,110)),105.0)

def collinear_overlap(a,b,c,d,tol=TOL):
    def cross(a,b,c): return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    la=math.dist(a,b)
    if la<=tol: return False
    if abs(cross(a,b,c))/la>tol or abs(cross(a,b,d))/la>tol: return False
    use_x=abs(b[0]-a[0])>=abs(b[1]-a[1])
    a0,a1=(a[0],b[0]) if use_x else (a[1],b[1])
    b0,b1=(c[0],d[0]) if use_x else (c[1],d[1])
    a0,a1=sorted((a0,a1)); b0,b1=sorted((b0,b1))
    return min(a1,b1)-max(a0,b0)>tol

def test_collinear_overlap_is_rejected():
    assert collinear_overlap((0,0),(10,0),(5,0),(15,0))

def test_touching_collinear_segments_are_not_overlap():
    assert not collinear_overlap((0,0),(10,0),(10,0),(15,0))