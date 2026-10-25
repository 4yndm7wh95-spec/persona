from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,csv
REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');LOCAL=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\pose_stage1');OUT=REPO/'research/p5r/pose';OUT.mkdir(exist_ok=True)
rows=json.loads((LOCAL/'clips.json').read_text());font=ImageFont.truetype(r'C:\Windows\Fonts\arial.ttf',19)
for si in range(8):
 sheet=Image.new('RGBA',(1536,3312),(0,0,0,0))
 for ri,r in enumerate(rows[si*8:si*8+8]):
  y=ri*414;label=f'{r["number"]:02}  {r["original_name"]}  {r["frames_at_30fps_reference"]} ref-frames  {float(r["duration_seconds"]):.6f}s'
  ImageDraw.Draw(sheet).text((8,y+4),label,font=font,fill='white',stroke_width=2,stroke_fill='black')
  for k in range(6):sheet.alpha_composite(Image.open(LOCAL/f'pose_{r["number"]:02}_{k}.png').convert('RGBA'),(k*256,y+30))
 sheet.save(OUT/f'sheet_{si+1:02}.png')
 # Viewing copies, not source-image edits: split contact sheet for detailed human inspection.
 for half in range(2):sheet.crop((0,half*1656,1536,(half+1)*1656)).save(LOCAL/f'view_sheet_{si+1:02}_{half}.png')
print('8 sheets completed')
