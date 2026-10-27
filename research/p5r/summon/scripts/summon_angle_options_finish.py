from pathlib import Path
import json,shutil
import numpy as np
from PIL import Image,ImageDraw,ImageFont
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/summon';folder=p/'angle_options_frames';o=Path('outputs/p5_summon_angle_options');o.mkdir(parents=True,exist_ok=True)
m=json.loads((folder/'camera_options.json').read_text(encoding='utf-8'))
if (folder/'postprocessed.json').exists():raise RuntimeError('Already mirrored')
tile=384;board=Image.new('RGB',(tile*6,tile+72),(188,188,188));d=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18);checks=[]
for i,c in enumerate(m['options']):
 path=folder/c['file'];im=Image.open(path).convert('RGBA').transpose(Image.Transpose.FLIP_LEFT_RIGHT);im.save(path);a=np.array(im);edge=int(max(a[0,:,3].max(),a[-1,:,3].max(),a[:,0,3].max(),a[:,-1,3].max()))
 assert im.size==(1536,1536) and edge==0 and a[:,:,3].max()==255
 bg=Image.new('RGBA',im.size,(188,188,188,255));bg.alpha_composite(im);board.paste(bg.convert('RGB').resize((tile,tile),Image.Resampling.LANCZOS),(i*tile,0))
 d.text((i*tile+12,tile+8),f"{c['azimuth_degrees']:.2f} deg",font=font,fill='black');d.text((i*tile+12,tile+39),f"offset {c['offset_degrees']:+d} deg"+(' (center)' if c['offset_degrees']==0 else ''),font=small,fill='black')
 checks.append({'file':c['file'],'four_edges_alpha_max':edge,'alpha_bbox':im.getchannel('A').getbbox()});shutil.copy2(path,o/path.name)
ref=Image.open(r/'art/p5/poses/final/gun_a2.png').convert('RGBA');refcanvas=Image.new('RGBA',(1536,1536),(188,188,188,255));refcanvas.alpha_composite(ref,((1536-ref.width)//2,0));board.paste(refcanvas.convert('RGB').resize((tile,tile),Image.Resampling.LANCZOS),(5*tile,0));d.text((5*tile+12,tile+8),'gun_a2 reference',font=font,fill='black');d.text((5*tile+12,tile+39),'same 25% display scale',font=small,fill='black')
board.save(p/'angle_options.png');shutil.copy2(p/'angle_options.png',o/'angle_options.png')
(folder/'postprocessed.json').write_text(json.dumps({'horizontal_mirror_applied':True,'same_display_scale':.25,'checks':checks},indent=2),encoding='utf-8')
print(json.dumps({'angles':[c['azimuth_degrees'] for c in m['options']],'resolution':m['resolution'],'ortho_scale':m['ortho_scale'],'all_edges_alpha_zero':True}))
