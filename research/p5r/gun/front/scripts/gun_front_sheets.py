from pathlib import Path
import json,math
from PIL import Image,ImageDraw,ImageFont
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/gun/front'
data=json.loads((p/'render_records.json').read_text(encoding='utf-8'));font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',20)
assert not (p/'postprocessed.json').exists()
for clip in data['clips']:
 frames=[]
 for f in clip['frames']:
  path=Path(f['path']);im=Image.open(path).convert('RGBA').transpose(Image.Transpose.FLIP_LEFT_RIGHT);im.save(path);frames.append(im)
 sheet=Image.new('RGB',(1024,math.ceil(len(frames)/4)*416),'#b8b8b8');d=ImageDraw.Draw(sheet)
 for i,(im,f) in enumerate(zip(frames,clip['frames'])):
  preview=Image.new('RGBA',im.size,(184,184,184,255));preview.alpha_composite(im);preview=preview.resize((256,384),Image.Resampling.LANCZOS)
  x=(i%4)*256;y=(i//4)*416;sheet.paste(preview.convert('RGB'),(x,y+28));d.text((x+8,y+3),f"#{clip['number']}  frame {f['frame']:02}",font=font,fill='black')
 sheet.save(p/f"{clip['number']}_sheet.png")
 gifs=[];times=[];duration={'51':.5,'52':2/3,'53':13/30,'54':.5}[str(clip['number'])]
 for im,f in zip(frames,clip['frames']):
  if duration-f['time_seconds']<.001:continue
  bg=Image.new('RGBA',im.size,(184,184,184,255));bg.alpha_composite(im);gifs.append(bg.convert('RGB').resize((320,480),Image.Resampling.LANCZOS));times.append(f['time_seconds'])
 ds=[max(10,round(((times[i+1] if i+1<len(times) else duration)-t)*1000/10)*10) for i,t in enumerate(times)]
 gifs[0].save(p/f"{clip['number']}.gif",save_all=True,append_images=gifs[1:],duration=ds,loop=0,disposal=2)
(p/'postprocessed.json').write_text(json.dumps({'horizontal_mirror_applied':True,'gif_width':320,'frame_step':2,'fps_reference':30,'note':'GIF uses neutral grey background for visibility; full PNG frames true transparent; terminal sample at exact duration omitted from GIF to preserve original timing.'},indent=2),encoding='utf-8')
print('Sheets and GIFs ready')
