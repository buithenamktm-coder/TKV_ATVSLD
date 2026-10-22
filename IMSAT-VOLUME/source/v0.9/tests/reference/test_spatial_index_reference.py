import math

def test_uniform_grid_candidate_reduction_on_regular_contours():
    # Reference shape only: 200 x 200 sites and 20k short breakline segments.
    nx=200; ny=200
    points=[(float(x),float(y)) for y in range(ny) for x in range(nx)]
    segs=[]
    # 100 horizontal rows, each row 199 short segments => 19,900 segments
    for y in range(0,200,2):
        for x in range(199): segs.append(((x,y),(x+1,y)))

    minx=min(p[0] for p in points); maxx=max(p[0] for p in points)
    miny=min(p[1] for p in points); maxy=max(p[1] for p in points)
    span=max(maxx-minx,maxy-miny)
    cell=span/max(8.0, math.sqrt(len(points)))
    grid={}
    def key(x,y): return (int(math.floor((x-minx)/cell)),int(math.floor((y-miny)/cell)))
    for i,p in enumerate(points): grid.setdefault(key(*p),[]).append(i)

    candidate_total=0
    for a,b in segs:
        ix0,iy0=key(min(a[0],b[0]),min(a[1],b[1]))
        ix1,iy1=key(max(a[0],b[0]),max(a[1],b[1]))
        seen=set()
        for ix in range(ix0,ix1+1):
            for iy in range(iy0,iy1+1): seen.update(grid.get((ix,iy),()))
        candidate_total += len(seen)

    brute=len(points)*len(segs)
    # The grid should remove >99% of site-segment candidate checks in this regular case.
    assert candidate_total < brute * 0.01