from pathlib import Path
import json,math
from PIL import Image,ImageDraw,ImageFont
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/summon'
d=json.loads((p/'render_records.json').read_text(encoding='utf-8'));font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20)
if (p/'postprocessed.json').exists():raise RuntimeError('Already mirrored/packaged')
stats=[]
for clip in d['clips']:
 frames=[];paths=[]
 for f in clip['frames']:
  path=r/f['path'];im=Image.open(path).convert('RGBA').transpose(Image.Transpose.FLIP_LEFT_RIGHT);im.save(path);frames.append(im)
  al=im.getchannel('A');stats.append({'path':f['path'],'size':list(im.size),'alpha_range':list(al.getextrema()),'alpha_bbox':al.point(lambda a:255 if a>128 else 0).getbbox()})
 sheet=Image.new('RGB',(1024,math.ceil(len(frames)/4)*416),'#b8b8b8');draw=ImageDraw.Draw(sheet)
 for i,(im,f) in enumerate(zip(frames,clip['frames'])):
  bg=Image.new('RGBA',im.size,(184,184,184,255));bg.alpha_composite(im)
  x=i%4*256;y=i//4*416;sheet.paste(bg.convert('RGB').resize((256,384),Image.Resampling.LANCZOS),(x,y+28));draw.text((x+8,y+3),f"#{clip['number']} frame {f['frame']:02}",font=font,fill='black')
 sheet.save(p/f"{clip['number']}_sheet.png")
 gifs=[];ts=[];duration=clip['duration_seconds']
 for im,f in zip(frames,clip['frames']):
  if duration-f['time_seconds']<.001:continue
  bg=Image.new('RGBA',im.size,(184,184,184,255));bg.alpha_composite(im);gifs.append(bg.convert('RGB').resize((320,480),Image.Resampling.LANCZOS));ts.append(f['time_seconds'])
 ends=[round(t*100) for t in ts]+[round(duration*100)]
 ds=[max(10,(ends[i+1]-ends[i])*10) for i in range(len(ts))]
 gifs[0].save(p/f"{clip['number']}.gif",save_all=True,append_images=gifs[1:],duration=ds,loop=0,disposal=2)
 clip['gif_duration_ms']=sum(ds);clip['gif_frame_durations_ms']=ds
(p/'postprocessed.json').write_text(json.dumps({'horizontal_mirror_applied':True,'gif_width':320,'frame_step':2,'fps_reference':30,'note':'PNG frames true transparent. GIF previews neutral grey, same as gun/pick. GIF centisecond rounding; no artificial slowdown. Terminal sample at exact clip duration omitted from GIF.','clips':d['clips'],'image_stats':stats},ensure_ascii=False,indent=2),encoding='utf-8')
print('Packaged',len(stats),'transparent PNG frames')
print(json.dumps([{'clip':c['number'],'GIF_ms':c['gif_duration_ms']} for c in d['clips']]))
