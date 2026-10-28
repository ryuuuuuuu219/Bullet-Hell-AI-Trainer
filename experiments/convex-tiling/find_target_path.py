"""Check whether greedy completion admits a reference-preserving path."""
import json
from pathlib import Path
from shapely.geometry import Polygon
from shapely.affinity import translate
from simulate import frontier,candidate,pair,fits,union,complete

d=json.loads((Path(__file__).parents[1]/'battleship-11/design.json').read_text())
v={v['id']:v['position'] for v in d['vertices']}
target=[translate(Polygon([v[k] for k in p['vertex_ids']]),yoff=-(1+3**.5)/2) for p in d['parts']]
def idx(p):return next((i for i,q in enumerate(target) if p.symmetric_difference(q).area<1e-6),None)
def mask(parts):return tuple(sorted(idx(p) for p in parts))
seen=set()
def grow(parts,path):
    state=mask(parts)
    if state in seen:return None
    seen.add(state)
    if len(parts)==20:return path
    def attempt(trial,draws,depth):
        for fi,(_,edges) in enumerate(frontier(trial)):
            for ei,(a,b) in enumerate(edges):
                for shape in (0,1):
                    new=pair(candidate(a,b,shape==0))
                    if any(idx(p) is None for p in new) or not fits(new,union(trial)):continue
                    extended=trial+new
                    done,_=complete(extended)
                    decisions=draws+[fi,ei,shape]
                    if done is not None:
                        if all(idx(p) is not None for p in done):
                            result=grow(done,path+[decisions])
                            if result:return result
                    elif depth<4:
                        result=attempt(extended,decisions,depth+1)
                        if result:return result
        return None
    return attempt(parts,[],1)
initial=[Polygon([(-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5)])]
print(grow(initial,[]),flush=True)
print('states',seen)
