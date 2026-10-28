"""Find growth constraints of the existing reference, without changing its geometry."""
import json
from pathlib import Path
from shapely.geometry import Polygon
from shapely import union_all
from shapely.affinity import scale
from collections import deque

data=json.loads((Path(__file__).parents[1]/'battleship-11/design.json').read_text())
vertices={v['id']:v['position'] for v in data['vertices']}
parts=[Polygon([vertices[v] for v in p['vertex_ids']]) for p in data['parts']]
groups=[]
used=set()
for i,p in enumerate(parts):
    if i in used:continue
    mirror=scale(p,xfact=-1,yfact=1,origin=(0,0))
    j=next(j for j,q in enumerate(parts) if mirror.symmetric_difference(q).area<1e-6)
    group={i,j};groups.append(group);used|=group
print('mirror groups',[[i+1 for i in g] for g in groups])
full=(1<<len(groups))-1
def indices(mask):return set().union(*(g for k,g in enumerate(groups) if mask>>k&1))
def geom(mask):return union_all([parts[i] for i in indices(mask)],grid_size=1e-8)
cache={m:geom(m) for m in range(1,full+1)}
convex={m for m,g in cache.items() if g.geom_type=='Polygon' and g.convex_hull.area-g.area<1e-6}
print('convex subsets',len(convex))
for start in [k for k,g in enumerate(groups) if len(g)==1]:
    queue=deque([1<<start]);parents={1<<start:None}
    while queue:
        state=queue.popleft()
        if state==full:break
        occupied=indices(state)
        for k,group in enumerate(groups):
            if state>>k&1:continue
            if not any(set(data['parts'][i]['neighbors'])&{data['parts'][j]['id'] for j in occupied} for i in group):continue
            trial=state|1<<k;hull=cache[trial].convex_hull
            filled=trial
            for n,other in enumerate(groups):
                if all(parts[i].difference(hull).area<1e-6 for i in other):filled|=1<<n
            if filled in convex and filled not in parents:
                parents[filled]=state;queue.append(filled)
    path=[];end=full if full in parents else max(parents,key=lambda m:len(indices(m)))
    while end is not None:path.append(sorted(i+1 for i in indices(end)));end=parents[end]
    print('start', [i+1 for i in groups[start]],'reachable',full in parents,'path',path[::-1])

for burst in (2,3,4):
    start=5 # P08, the square centered on the symmetry axis.
    queue=deque([1<<start]);parents={1<<start:None}
    while queue:
        state=queue.popleft()
        if state==full:break
        trials={state}
        for depth in range(burst):
            next_trials=set()
            for trial in trials:
                occupied=indices(trial)
                for k,group in enumerate(groups):
                    if trial>>k&1:continue
                    if not any(set(data['parts'][i]['neighbors'])&{data['parts'][j]['id'] for j in occupied} for i in group):continue
                    grown=trial|1<<k;next_trials.add(grown);hull=cache[grown].convex_hull;filled=grown
                    for n,other in enumerate(groups):
                        if all(parts[i].difference(hull).area<1e-6 for i in other):filled|=1<<n
                    if filled in convex and filled not in parents:
                        parents[filled]=(state,depth+1);queue.append(filled)
            trials=next_trials
    path=[];end=full if full in parents else max(parents,key=lambda m:len(indices(m)))
    while end is not None:
        link=parents[end];path.append((sorted(i+1 for i in indices(end)),link[1] if link else 0));end=link[0] if link else None
    print('burst',burst,'reachable',full in parents,'path',path[::-1])
