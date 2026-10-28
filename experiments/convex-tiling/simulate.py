"""Geometry-only experiment; no Unity assets or gameplay are modified."""
import json
import math
import random
from pathlib import Path
from collections import Counter
from shapely.geometry import Polygon
from shapely import union_all
from shapely.affinity import scale

OUT = Path(__file__).parent
EPS = 1e-6

def keypoint(p):
    return tuple(round(v, 7) for v in p)

def edgekey(a, b):
    return tuple(sorted((keypoint(a), keypoint(b))))

def points(p):
    return list(p.exterior.coords)[:-1]

def union(parts):
    return union_all(parts, grid_size=1e-8)

def frontier(parts):
    edges = [[(a,b) for a,b in zip(points(p), points(p)[1:]+points(p)[:1])] for p in parts]
    counts = Counter(edgekey(a,b) for es in edges for a,b in es)
    return [(i, [(a,b) for a,b in es if counts[edgekey(a,b)] == 1]) for i,es in enumerate(edges) if any(counts[edgekey(a,b)] == 1 for a,b in es)]

def candidate(a, b, triangle):
    # Stored polygons are CCW: the right normal points out of the tile.
    dx,dy = b[0]-a[0], b[1]-a[1]
    nx,ny = dy,-dx
    if triangle:
        c=((a[0]+b[0])/2+nx*math.sqrt(3)/2,(a[1]+b[1])/2+ny*math.sqrt(3)/2)
        return Polygon([a,c,b])
    return Polygon([a,(a[0]+nx,a[1]+ny),(b[0]+nx,b[1]+ny),b])

def pair(p):
    q=Polygon(list(reversed(points(scale(p, xfact=-1, yfact=1, origin=(0,0))))))
    return [p] if p.symmetric_difference(q).area < EPS else [p,q]

def fits(new, occupied):
    if any(p.intersection(occupied).area > EPS for p in new):
        return False
    return len(new)==1 or new[0].intersection(new[1]).area < EPS

def snapshot(parts, label, added=0):
    u=union(parts)
    gap=u.convex_hull.area-u.area
    return dict(label=label, parts=[[[round(x,7),round(y,7)] for x,y in points(p)] for p in parts], added=added, gap=round(gap,7), convex=gap<EPS, hull=[[round(x,7),round(y,7)] for x,y in points(u.convex_hull)])

def complete(parts, cap=64):
    """Greedy fill inside the fixed hull; failure is not proof of impossibility."""
    parts=list(parts)
    hull=union(parts).convex_hull
    frames=[]
    while len(parts)<=cap:
        u=union(parts)
        if hull.area-u.area<EPS:
            return parts,frames
        options=[]
        for _,edges in frontier(parts):
            for a,b in edges:
                for tri in (True,False):
                    new=pair(candidate(a,b,tri))
                    if len(parts)+len(new)>cap or not fits(new,u):
                        continue
                    if any(p.difference(hull).area>EPS for p in new):
                        continue
                    options.append((sum(p.area for p in new),new))
        if not options:
            return None,[]
        new=max(options,key=lambda x:x[0])[1]
        parts+=new
        frames.append(snapshot(parts,'凹みの補完',len(new)))
    return None,[]

def validate(parts):
    u=union(parts)
    assert u.geom_type=='Polygon' and not u.interiors
    assert abs(sum(p.area for p in parts)-u.area)<EPS
    assert u.convex_hull.area-u.area<EPS
    assert u.symmetric_difference(scale(u,xfact=-1,yfact=1,origin=(0,0))).area<EPS
    for p in parts:
        ps=points(p)
        assert all(abs(math.dist(a,b)-1)<EPS for a,b in zip(ps,ps[1:]+ps[:1]))
    return True

def run(seed,target=24,attempts=160):
    rng=random.Random(seed)
    parts=[Polygon([(-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5)])]
    frames=[snapshot(parts,'初期区画')]
    rejects=Counter()
    accepted=0
    for attempt in range(1,attempts+1):
        _,edges=rng.choice(frontier(parts)) # Uniform tile, then uniform edge.
        a,b=rng.choice(edges)
        new=pair(candidate(a,b,rng.choice((True,False))))
        if not fits(new,union(parts)):
            rejects['overlap']+=1
            continue
        initial=parts+new
        completed,fillframes=complete(initial)
        if completed is None:
            rejects['fill_failed']+=1
            continue
        frames.append(snapshot(initial,'抽選による増設',len(new)))
        frames+=fillframes
        parts=completed
        validate(parts)
        accepted+=1
        if len(parts)>=target:
            break
    return dict(seed=seed,target=target,count=len(parts),attempts=attempt,accepted=accepted,rejects=dict(rejects),validated=validate(parts),reached=len(parts)>=target,frames=frames)

if __name__=='__main__':
    results=[run(seed) for seed in range(1,7)]
    (OUT/'results.json').write_text(json.dumps(results,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    print(json.dumps([{k:v for k,v in r.items() if k!='frames'} for r in results],ensure_ascii=False,indent=2))
