"""Run with Python + Pillow; preserves atlas region pixels and records margins."""
from pathlib import Path
import re, csv, math
from PIL import Image, ImageDraw
root=Path(__file__).resolve().parents[2]/'research/card_frame_src'
out=root/'sprites'
out.mkdir(exist_ok=True)
rows=[]
thumbs=[]
for p in sorted((root/'images/atlases/ui_atlas.sprites/card').glob('*.tres')):
    t=p.read_text(encoding='utf-8')
    refs={i:path for path,i in re.findall(r'\[ext_resource[^\n]*path="res://([^"]+)"[^\n]*id="([^"]+)"',t)}
    atlas=refs[re.search(r'atlas\s*=\s*ExtResource\("([^"]+)"\)',t)[1]]
    v=[float(s.strip()) for s in re.search(r'region\s*=\s*Rect2\(([^)]+)\)',t)[1].split(',')]
    assert len(v)==4 and all(n==int(n) for n in v),p
    x,y,w,h=map(int,v)
    margin=re.search(r'margin\s*=\s*Rect2\(([^)]+)\)',t)
    with Image.open(root/atlas) as im:
        assert x>=0 and y>=0 and w>0 and h>0 and x+w<=im.width and y+h<=im.height,p
        crop=im.convert('RGBA').crop((x,y,x+w,y+h))
    crop.save(out/(p.stem+'.png'))
    rows.append([p.stem+'.png',atlas,x,y,w,h,margin[1] if margin else ''])
    crop.thumbnail((280,260),Image.Resampling.LANCZOS)
    thumbs.append((crop,p.stem,f'{w} x {h}'))
with (out/'index.csv').open('w',encoding='utf-8',newline='') as f:
    wr=csv.writer(f);wr.writerow(['file','atlas','x','y','w','h','margin']);wr.writerows(rows)
preview=Image.new('RGB',(1360,math.ceil(len(rows)/4)*315),(38,38,42))
draw=ImageDraw.Draw(preview)
for i,(im,name,size) in enumerate(thumbs):
    x,y=i%4*340,i//4*315
    preview.paste(im,(x+(340-im.width)//2,y+8+(260-im.height)//2),im)
    draw.text((x+8,y+278),name,fill='white');draw.text((x+8,y+296),size,fill=(160,200,225))
preview.save(out/'preview.png')
print('Cropped',len(rows))
for r in rows: print(f'{r[0]}: {r[4]} x {r[5]}')
