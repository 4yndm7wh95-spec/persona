from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,csv,textwrap,math
REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');OUT=REPO/'research/p5r/model/diag';LOCAL=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\diag_work')
FONT=r'C:\Windows\Fonts\arial.ttf';small=ImageFont.truetype(FONT,12);title=ImageFont.truetype(FONT,19)
def text(im,pos,s,font=small):ImageDraw.Draw(im).multiline_text(pos,s,font=font,fill='white',stroke_width=1,stroke_fill='black',spacing=1)
renders=json.loads((LOCAL/'mesh_renders.json').read_text())
for label,rows in renders.items():
    assert len(rows)==(22 if label=='thief' else 21)
    table=list(csv.DictReader((OUT/f'mesh_table_{label}.csv').open(encoding='utf-8-sig')));byname={r['node_name']:r for r in table};sheet=Image.new('RGBA',(6*256,math.ceil(len(rows)/6)*384),(0,0,0,0))
    for i,r in enumerate(rows):
        im=Image.open(LOCAL/r['render']).convert('RGBA');im=im.resize((256,384),Image.Resampling.LANCZOS)
        g=byname[r['object_name']];name=r['object_name'];mat=', '.join(r['materials'])
        labeltext='\n'.join(textwrap.wrap(f'{i:02} {name}',34)+textwrap.wrap('mat: '+mat,34)+[f'vertices: {g["vertex_count"]}'])
        text(im,(5,5),labeltext);sheet.alpha_composite(im,((i%6)*256,(i//6)*384))
        # Add Blender evaluated/world bounds and render index to the corresponding source row.
        g['render_index']=i;g['blender_vertex_count']=r['vertex_count'];g['bbox_blender_world_corners_json']=json.dumps(r['world_bbox'],separators=(',',':'))
    with (OUT/f'mesh_table_{label}.csv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,list(table[0]));w.writeheader();w.writerows(table)
    sheet.save(OUT/f'mesh_sheet_{label}.png')
frames=[0,8,16,24,32,40];sheet=Image.new('RGBA',(1536,2*(384+34)),(0,0,0,0))
for row,(variant,desc) in enumerate([('original','A: original channels (root rotation included)'),('skip_root_rotation','B: omit exact node root.rotation channel only')]):
    text(sheet,(8,row*418+6),f'BB0001_051_main_0000 | {desc}',title)
    for col,frame in enumerate(frames):
        im=Image.open(LOCAL/f'anim_{variant}_{frame:02}.png').convert('RGBA').resize((256,384),Image.Resampling.LANCZOS)
        text(im,(6,6),f'frame {frame} | t={frame/30:.6f}s')
        sheet.alpha_composite(im,(col*256,row*418+34))
sheet.save(OUT/'anim_test.png')
for view in ['front','threequarter']:
    p=OUT/f'arsene_ps0001_{view}.png';im=Image.open(p).convert('RGBA');text(im,(18,16),f'PS0001 | {view} | identity unconfirmed (not Arsene)',title);im.save(p)
search=LOCAL/'persona_search.json'
if search.exists():
    rows=json.loads(search.read_text());sheet=Image.new('RGBA',(1536,math.ceil(len(rows)/6)*384),(0,0,0,0))
    for i,r in enumerate(rows):
        im=Image.open(r['front_image']).convert('RGBA').resize((256,384),Image.Resampling.LANCZOS);text(im,(5,5),f'PS{r["id"]}\n'+'\n'.join(textwrap.wrap(', '.join(r['texture_names']),33)[:4]));sheet.alpha_composite(im,((i%6)*256,(i//6)*384))
    sheet.save(OUT/'arsene_candidates.png')
    with (OUT/'arsene_candidates.csv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,['id','source_path','texture_names','mesh_count','front_image']);w.writeheader();w.writerows([{**r,'texture_names':json.dumps(r['texture_names'])} for r in rows])
print('sheets complete')
