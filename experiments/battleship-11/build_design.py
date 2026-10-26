"""Reconstruct the supplied image under an equal-edge square/triangle assumption."""
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).parent
R = math.sqrt(3) / 2
EPS = 1e-8
parts = []

def add(a, b):
    return (a[0] + b[0], a[1] + b[1])

def cross(a, b, c):
    return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])

def area(p):
    return sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(p,p[1:]+p[:1]))/2

def put(kind, p):
    if area(p) < 0:
        p = list(reversed(p))
    parts.append({'id': f'P{len(parts)+1:02}', 'shape':kind, 'points':p})

def square(a,b):
    n=(b[1]-a[1],a[0]-b[0])
    put('square',[a,add(a,n),add(b,n),b])
    return n

v=[(1,0),(.5,R),(-.5,R),(-1,0),(-.5,-R),(.5,-R)]
for i in range(6):
    put('equilateral_triangle',[(0,0),v[i],v[(i+1)%6]])
normals={i:square(v[i],v[(i+1)%6]) for i in (0,1,2,3,5)}
for i in (0,1,2,3):
    put('equilateral_triangle',[v[i],add(v[i],normals[(i-1)%6]),add(v[i],normals[i])])
tip=(0,-2*R)
put('equilateral_triangle',[v[4],tip,v[5]])
nl=square(v[4],tip)
nr=square(tip,v[5])
bottom=(0,-2*R-1)
put('equilateral_triangle',[tip,add(tip,nl),bottom])
put('equilateral_triangle',[tip,bottom,add(tip,nr)])

def key(p):
    return tuple(round(x,10) for x in p)

vertices={}
edges={}
for part in parts:
    p=part['points']
    for a,b in zip(p,p[1:]+p[:1]):
        assert abs(math.dist(a,b)-1)<EPS
        vertices[key(a)]=a
        vertices[key(b)]=b
        edges.setdefault(tuple(sorted((key(a),key(b)))),[]).append(part['id'])
assert all(len(owners) in (1,2) for owners in edges.values())
boundary_neighbors={}
for (a,b),owners in edges.items():
    if len(owners)==1:
        boundary_neighbors.setdefault(a,[]).append(b)
        boundary_neighbors.setdefault(b,[]).append(a)
assert all(len(x)==2 for x in boundary_neighbors.values())
start=key((-.5,R+1))
boundary=[start]
prev=None
current=start
while True:
    nxt=next(p for p in boundary_neighbors[current] if p!=prev)
    if nxt==start:
        break
    boundary.append(nxt)
    prev,current=current,nxt
assert len(boundary)==len(boundary_neighbors)
boundary=[vertices[p] for p in boundary]
if area(boundary)<0:
    boundary=[boundary[0]]+list(reversed(boundary[1:]))
boundary=[p for i,p in enumerate(boundary) if abs(cross(boundary[i-1],p,boundary[(i+1)%len(boundary)]))>EPS]
assert len(boundary)==11

def intersection(poly, clip):
    for a,b in zip(clip,clip[1:]+clip[:1]):
        out=[]
        if not poly:
            break
        for p,q in zip(poly,poly[1:]+poly[:1]):
            cp,cq=cross(a,b,p),cross(a,b,q)
            if cp>=-EPS:
                out.append(p)
            if (cp>EPS and cq < -EPS) or (cp < -EPS and cq > EPS):
                t=cp/(cp-cq)
                out.append((p[0]+t*(q[0]-p[0]),p[1]+t*(q[1]-p[1])))
        poly=out
    return poly

overlap=max(abs(area(intersection(a['points'],b['points']))) for i,a in enumerate(parts) for b in parts[i+1:])
assert overlap<EPS
assert abs(sum(area(p['points']) for p in parts)-area(boundary))<EPS

vertex_ids={p:f'V{i+1:02}' for i,p in enumerate(sorted(vertices))}
def clean(p):
    return [round(x,10) for x in p]
def angle(a,b):
    return round(math.degrees(math.atan2(b[1]-a[1],b[0]-a[0]))%360,8)

outline=[]
for i,p in enumerate(boundary):
    before,after=boundary[i-1],boundary[(i+1)%len(boundary)]
    ux,uy=before[0]-p[0],before[1]-p[1]
    vx,vy=after[0]-p[0],after[1]-p[1]
    interior=math.degrees(math.acos(max(-1,min(1,(ux*vx+uy*vy)/(math.hypot(ux,uy)*math.hypot(vx,vy))))))
    outline.append({'id':f'B{i+1:02}','vertex_id':vertex_ids[key(p)],'position':clean(p),'interior_angle_deg':round(interior,8),'next_edge_length':round(math.dist(p,after),10),'next_edge_heading_deg':angle(p,after)})
assert abs(sum(x['interior_angle_deg'] for x in outline)-1620)<EPS

for part in parts:
    p=part.pop('points')
    part['vertex_ids']=[vertex_ids[key(a)] for a in p]
    part['centroid']=clean((sum(a[0] for a in p)/len(p),sum(a[1] for a in p)/len(p)))
    part['interior_angles_deg']=[90 if len(p)==4 else 60]*len(p)
    part['edge_headings_deg']=[angle(a,b) for a,b in zip(p,p[1:]+p[:1])]
    part['area']=round(area(p),10)
    part['neighbors']=sorted({other for a,b in zip(p,p[1:]+p[:1]) for other in edges[tuple(sorted((key(a),key(b))))] if other!=part['id']})
    part['turret_mount_position']=part['centroid']
    part['turret_initial_heading_deg']=None

design={
    'schema_version':1,
    'source':'codex-clipboard-9d62c91d-5100-427a-af6b-1bf310f6ec8e.png',
    'status':'geometric reconstruction from image; not a Unity runtime implementation',
    'assumptions':['Blue tiles are squares.','Orange tiles are equilateral triangles.','All tile edges have length 1.','Tile junctions visible in the image are intended exact junctions.'],
    'coordinate_system':{'origin':'center of the central hexagon','x':'right','y':'up','z':0,'angles':'degrees, counterclockwise from +X','vertex_winding':'counterclockwise','unit':'one tile edge'},
    'image_alignment_estimate':{'origin_px':[205,146],'pixels_per_unit':50,'mapping':'pixel_x = 205 + 50*x; pixel_y = 146 - 50*y','status':'approximate visual alignment, not pixel-precise tracing'},
    'counts':{'squares':7,'equilateral_triangles':13,'parts':20,'outline_corners':11},
    'vertices':[{'id':vertex_ids[p],'position':clean(vertices[p])} for p in sorted(vertices)],
    'outline':outline,
    'parts':parts,
    'validation':{'unit_tile_edges':True,'maximum_pair_overlap_area':overlap,'tile_area_sum':7+13*math.sqrt(3)/4,'outline_area':area(boundary),'outline_angle_sum_deg':sum(x['interior_angle_deg'] for x in outline),'boundary_single_loop':True},
    'not_specified':['overall world scale','HP and armor','turret type assignment','turret initial headings','death and detachment rules']
}
(OUT/'design.json').write_text(json.dumps(design,ensure_ascii=False,indent=2),encoding='utf-8')

im=Image.new('RGB',(920,940),'white'); d=ImageDraw.Draw(im)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
def xy(p):
    return (460+p[0]*155,380-p[1]*155)
lookup={x['id']:x['position'] for x in design['vertices']}
for part in parts:
    p=[xy(lookup[a]) for a in part['vertex_ids']]
    d.polygon(p,fill='#1483e8' if part['shape']=='square' else '#ff8a00',outline='white',width=2)
    x,y=xy(part['centroid'])
    d.ellipse((x-3,y-3,x+3,y+3),fill='black')
    d.text((x,y+7),part['id'],font=small,fill='black',anchor='mt')
for p in outline:
    x,y=xy(p['position']); d.ellipse((x-4,y-4,x+4,y+4),fill='#111111')
    dx,dy=x-460,y-420; length=math.hypot(dx,dy)
    d.text((x+dx/length*28,y+dy/length*28),p['id'],font=small,fill='black',anchor='mm')
d.text((30,20),'11-sided outline / 7 squares + 13 equilateral triangles',font=font,fill='black')
d.text((30,875),'P: part centroid     B: outline corner     tile edge = 1',font=font,fill='black')
d.text((30,905),'Reconstructed geometry; turret direction is not specified by the image.',font=small,fill='black')
im.save(OUT/'numbered-design.png')

lines=['# 11角形の部位配置：画像からの復元案','',
'青を正方形、橙を正三角形、すべての部位の辺長を1と仮定して復元。画像の画素を厳密にトレースしたものではない。',
'原点は中央の六角形の中心。Xは右、Yは上、Zは0。角度は右向き0°・反時計回り。外周と部位の頂点順は反時計回り。',
'正方形7枚、正三角形13枚、計20部位。外周は一直線上の継ぎ目を除いて11角形で、正11角形ではない。',
'左右の長い辺は長さ2、ほか9辺は長さ1。全体の幅は2+√3、高さは2+3√3/2。',
'内部の図形角度、外周内角、各辺の方位角をJSONに収録。砲台の初期照準方向は画像から特定できないためnull。',
'','## 外周','', '| ID | X | Y | 内角（度） | 次の辺の長さ | 次の辺の方位角（度） |','|---|---:|---:|---:|---:|---:|']
for p in outline:
    lines.append(f"| {p['id']} | {p['position'][0]:.6f} | {p['position'][1]:.6f} | {p['interior_angle_deg']:g} | {p['next_edge_length']:g} | {p['next_edge_heading_deg']:g} |")
lines+=['','## 部位の重心（砲台配置候補）','','| ID | 形状 | X | Y |','|---|---|---:|---:|']
for p in parts:
    lines.append(f"| {p['id']} | {'正方形' if p['shape']=='square' else '正三角形'} | {p['centroid'][0]:.6f} | {p['centroid'][1]:.6f} |")
lines+=['','## 検証','',
'全タイルの辺長1、部位同士の重なり面積0、外周が単一閉路、部位面積合計と外周面積の一致、外周内角の総和1620°を数値検証（許容誤差1e-8）。',
'面積は7+13√3/4 ≈ 12.629165125。Unityへの組込み・実行確認は未実施。',
'','## 使用','',
'design.json の vertices を共有頂点表とし、parts[].vertex_ids で各部位を構成する。centroid と turret_mount_position は同じ位置。neighbors は辺を共有する部位のみ。',
'実寸へは座標全体に辺長を掛ける。部位の頂点を重心基準へ変換する場合は、各頂点から centroid を引く。',
'寸法・HP・装甲・砲台種類・初期照準・撃破条件・脱落条件は未指定。']
(OUT/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps(design['validation']))
print(json.dumps(outline))

