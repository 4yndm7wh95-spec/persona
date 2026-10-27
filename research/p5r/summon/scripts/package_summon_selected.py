from pathlib import Path
import json,math
import numpy as np
from PIL import Image,ImageDraw,ImageFont
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/summon'
d=json.loads((p/'render_records.json').read_text(encoding='utf-8'));assert len(d['clips'])==3 and not d['horizontal_mirror_applied'];font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20);stats=[]
for c in d['clips']:
 frames=[]
 for f in c['frames']:
  path=r/f['path'];im=Image.open(path).convert('RGBA').transpose(Image.Transpose.FLIP_LEFT_RIGHT);a=np.array(im);edge=int(max(a[0,:,3].max(),a[-1,:,3].max(),a[:,0,3].max(),a[:,-1,3].max()))
  assert im.size==(1536,1536) and edge==0 and a[:,:,3].max()==255,(path,edge)
  im.save(path);frames.append(im);stats.append({'path':f['path'],'four_edges_alpha_max':edge,'alpha_bbox':im.getchannel('A').getbbox()})
 sheet=Image.new('RGB',(1024,math.ceil(len(frames)/4)*288),(184,184,184));draw=ImageDraw.Draw(sheet)
 for i,(im,f) in enumerate(zip(frames,c['frames'])):
  bg=Image.new('RGBA',im.size,(184,184,184,255));bg.alpha_composite(im);x=i%4*256;y=i//4*288;sheet.paste(bg.convert('RGB').resize((256,256),Image.Resampling.LANCZOS),(x,y+28));draw.text((x+8,y+3),f"#{c['number']} frame {f['frame']:02}",font=font,fill='black')
 sheet.save(p/f"{c['number']}_sheet.png")
 gifs=[];ts=[];duration=c['duration_seconds']
 for im,f in zip(frames,c['frames']):
  if duration-f['time_seconds']<.001:continue
  bg=Image.new('RGBA',im.size,(184,184,184,255));bg.alpha_composite(im);gifs.append(bg.convert('RGB').resize((384,384),Image.Resampling.LANCZOS));ts.append(f['time_seconds'])
 ends=[round(t*100) for t in ts]+[round(duration*100)];ds=[max(10,(ends[i+1]-ends[i])*10) for i in range(len(ts))]
 gifs[0].save(p/f"{c['number']}.gif",save_all=True,append_images=gifs[1:],duration=ds,loop=0,disposal=2);c['gif_duration_ms']=sum(ds)
d['horizontal_mirror_applied']=True;(p/'render_records.json').write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
(p/'postprocessed.json').write_text(json.dumps({'horizontal_mirror_applied':True,'resolution':[1536,1536],'gif_size':[384,384],'selected_azimuth_degrees':d['selected_azimuth_degrees'],'frame_step':2,'fps_reference':30,'clips':d['clips'],'image_stats':stats,'all_four_edges_alpha_zero':True},ensure_ascii=False,indent=2),encoding='utf-8')
print('30 PNGs rendered, all four edges alpha=0; same camera, 321.856095851 degrees.')
