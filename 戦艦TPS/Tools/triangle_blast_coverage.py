"""Count circle/triangle overlaps in a fully occupied equilateral lattice (no ship combat)."""
import json
import math
import pathlib
import re

root = pathlib.Path(__file__).resolve().parents[1]
src = root / '新しいフォルダー/sennkannTPS/Assets/Script/module'
size = float(re.search(r'sectionSize = ([0-9.]+)', (src/'ShipBuildData.cs').read_text(encoding='utf-8')).group(1))
weapons = re.findall(r'new MvpWeaponDefinition\(\d+, "([^"]+)", ([\d.]+),', (src/'MvpWeaponCatalog.cs').read_text(encoding='utf-8'))
h = size * math.sqrt(3) / 2
origins = {'区画中心': (size/2, h/3), '辺の中央': (size/2, 0), '頂点': (0, 0)}

def cross(a, b, p):
    return (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])

def distance(p, tri):
    signs = [cross(tri[k], tri[(k+1)%3], p) for k in range(3)]
    if min(signs) >= -1e-9 or max(signs) <= 1e-9:
        return 0
    result = math.inf
    for k in range(3):
        a, b = tri[k], tri[(k+1)%3]
        dx, dy = b[0]-a[0], b[1]-a[1]
        t = max(0, min(1, ((p[0]-a[0])*dx+(p[1]-a[1])*dy)/(dx*dx+dy*dy)))
        result = min(result, math.hypot(p[0]-a[0]-t*dx, p[1]-a[1]-t*dy))
    return result

def count(origin, radius, extent):
    total = 0
    for i in range(-extent, extent+1):
        for j in range(-extent, extent+1):
            x, y = size*(i+j/2), h*j
            a, b, c, d = (x,y), (x+size,y), (x+size/2,y+h), (x+size*1.5,y+h)
            total += sum(distance(origin, tri) <= radius+1e-8 for tri in ((a,b,c),(b,d,c)))
    return total

rows=[]
for name, power in weapons:
    power=float(power); radius=power*1.5
    extent=math.ceil(2*radius/size)+5
    counts={key:count(origin,radius,extent) for key,origin in origins.items()}
    assert counts == {key:count(origin,radius,extent+3) for key,origin in origins.items()}, 'Lattice truncated'
    rows.append(dict(weapon=name,power=power,radius=radius,counts=counts))
assert count(origins['区画中心'],1,3)==1
assert count(origins['辺の中央'],1,3)==2
assert count(origins['頂点'],1,3)==6
output=root/'Tools/AmmoBalanceResults/20261122/triangle-coverage-no-falloff.json'
output.write_text(json.dumps(dict(side=size,triangleArea=size*h/2,tangentIncluded=True,rows=rows),ensure_ascii=False,indent=2),encoding='utf-8')
print(output.read_text(encoding='utf-8'))
