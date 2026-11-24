"""描原版 SKILL 页入场（文件序号 106–127，即 frame_0107–0128）里白板等大块浅色区域的轮廓（1920×1080 坐标），给 PileExhaust 照原版做形状动画。"""
import glob, json, numpy as np, scipy.ndimage as nd
from PIL import Image, ImageDraw
fs=sorted(glob.glob('docs/p5/preview/p5r-20261121-223943-frames-02-12/frame_*'))  # 在 codex/p5-0951-integration 分支里
W,H=960,540; K=1920/W
def disk(r):
    y,x=np.ogrid[-r:r+1,-r:r+1]; return x*x+y*y<=r*r
def largest(mask, minarea):
    lab,n=nd.label(mask)
    if n==0: return []
    sizes=nd.sum(mask,lab,range(1,n+1))
    return [lab==(j+1) for j in np.argsort(-sizes) if sizes[j]>=minarea]
def boundary(mask):
    # Moore neighbour tracing of the outer boundary
    m=np.pad(mask,1)
    ys,xs=np.nonzero(m)
    start=(ys[0],xs[0])
    dirs=[(-1,0),(-1,1),(0,1),(1,1),(1,0),(1,-1),(0,-1),(-1,-1)]
    pts=[start]; cur=start; prev_dir=6
    for _ in range(200000):
        found=False
        for k in range(8):
            d=(prev_dir+k)%8
            ny,nx=cur[0]+dirs[d][0],cur[1]+dirs[d][1]
            if m[ny,nx]:
                cur=(ny,nx); pts.append(cur); prev_dir=(d+6)%8; found=True; break
        if not found or (cur==start and len(pts)>2): break
    return np.array([(x-1,y-1) for y,x in pts],float)
def dpc(pts, eps):
    # closed loop: split at the point farthest from the start, simplify both halves
    if len(pts)<4: return pts
    j=int(np.argmax(np.hypot(*(pts-pts[0]).T)))
    a=dp(pts[:j+1],eps); b=dp(np.vstack([pts[j:],pts[:1]]),eps)
    return np.vstack([a[:-1],b[:-1]])
def dp(pts, eps):
    if len(pts)<3: return pts
    a,b=pts[0],pts[-1]; ab=b-a; L=np.hypot(*ab)+1e-9
    q=pts-a; d=np.abs(ab[0]*q[:,1]-ab[1]*q[:,0])/L
    i=np.argmax(d)
    if d[i]>eps:
        return np.vstack([dp(pts[:i+1],eps)[:-1], dp(pts[i:],eps)])
    return np.array([a,b])
res={}
vis=[]
for i in range(106,128):
    im=np.asarray(Image.open(fs[i]).convert('RGB').resize((W,H),Image.BILINEAR)).astype(int)
    mn=im.min(-1); mx=im.max(-1)
    light=nd.binary_opening((mn>180)&(mx-mn<28),structure=disk(5))
    dark=nd.binary_opening(mx<45,structure=disk(5))
    plates=[dpc(boundary(c),1.2)*K for c in largest(light,15000)]
    res[i]={'plate':[p.round(1).tolist() for p in plates]}
    v=Image.fromarray(im.astype(np.uint8)).convert('RGB'); dr=ImageDraw.Draw(v)
    for p in plates: dr.line([tuple(q/K) for q in p]+[tuple(p[0]/K)],fill=(0,255,0),width=2)
    dr.text((4,4),'idx %d'%i,fill='yellow')
    vis.append(v)
json.dump(res,open('tools/p5/pile/data/skill_entrance_plates.json','w'))
sh=Image.new('RGB',(480*6,270*4))
for k,v in enumerate(vis): sh.paste(v.resize((480,270)),((k%6)*480,(k//6)*270))
sh.save('/tmp/plates_sheet.jpg',quality=85)
print({i:[len(p) for p in r['plate']] for i,r in res.items()})
