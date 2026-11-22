"""Static direct-damage comparison; equilateral hull, no AI/core redistribution/detachment."""
import contextlib
import io
import json
import math
import pathlib
import re
with contextlib.redirect_stdout(io.StringIO()):
    import triangle_blast_coverage as geometry

root=pathlib.Path(__file__).resolve().parents[1]
source=(root/'新しいフォルダー/sennkannTPS/Assets/Script/module/MvpWeaponCatalog.cs').read_text(encoding='utf-8')
weapons=re.findall(r'new MvpWeaponDefinition\(\d+, "([^"]+)", ([\d.]+), ([\d.]+),',source)
size,h=geometry.size,geometry.h
origin=(size/2,0)

def tiles(extent):
    # Enemy occupies y >= 0. Finite hull depth and width are explicit, not an infinite AP target.
    for i in range(-extent,extent+1):
        for j in range(24):
            x,y=size*(i+j/2),h*j
            a,b,c,d=(x,y),(x+size,y),(x+size/2,y+h),(x+size*1.5,y+h)
            yield (a,b,c)
            yield (b,d,c)

def entry(tri,direction):
    lo,hi=-math.inf,math.inf
    cosine=0
    for k in range(3):
        a,b=tri[k],tri[(k+1)%3]
        ex,ey=b[0]-a[0],b[1]-a[1]
        q=geometry.cross(a,b,origin)
        rate=ex*direction[1]-ey*direction[0]
        if abs(rate)<1e-10:
            if q < -1e-7: return None
        elif rate>0:
            bound=-q/rate
            if bound>lo:
                lo=bound; cosine=rate/math.hypot(ex,ey)
        else: hi=min(hi,-q/rate)
    if hi-max(lo,0)<=1e-7: return None
    return max(lo,0),cosine

def compare(extent):
    hull=list(tiles(extent)); rows=[]
    rays={}
    for degrees in (47,73,89,107,131):
        direction=(math.cos(math.radians(degrees)),math.sin(math.radians(degrees)))
        rays[degrees]=sorted(hit for tri in hull if (hit:=entry(tri,direction)) is not None)
    for name,power,penetration in weapons:
        power,penetration=float(power),float(penetration)
        he=sum(geometry.distance(origin,t)<=power*1.5+1e-8 for t in hull)
        for armor in (0,25,50,100,200,300):
            for degrees,hits in rays.items():
                remaining=penetration; count=0; furthest=0
                for dist,cosine in hits:
                    count+=1; furthest=dist
                    effective=armor/cosine if armor else 0
                    if power*remaining<=effective+1e-9:
                        remaining=0; break
                    remaining*=1-effective/(power*remaining)
                rows.append(dict(weapon=name,power=power,armor=armor,angleFromNormal=degrees-90,
                    apSections=count,heSections=he,apHP10=count*min(power,10),heHP10=he*min(power,10),
                    apLastHitDistance=furthest,apExitedHull=remaining>0))
    return rows

rows=compare(40)
assert rows==compare(50), 'Side boundary affected comparison'
# Exact entry into the first triangle is perpendicular at 90 degrees.
assert abs(entry(next(tiles(0)),(0,1))[1]-1)<1e-8
output=root/'Tools/AmmoBalanceResults/20261122/ap-he-triangle-comparison.json'
output.write_text(json.dumps(dict(side=size,rowsDeep=24,hullDepth=24*h,origin=origin,
    description='Hull y>=0, edge-midpoint impact; positive-length ray crossings; five non-vertex directions',rows=rows),ensure_ascii=False,indent=2),encoding='utf-8')
for name,_,_ in weapons:
    for armor in (0,25,50,100,200,300):
        group=[r for r in rows if r['weapon']==name and r['armor']==armor]
        print(name,armor,'AP',min(r['apSections'] for r in group),max(r['apSections'] for r in group),'HE',group[0]['heSections'])
