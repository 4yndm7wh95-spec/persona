from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
local=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\round3_work');out=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload\research\p5r\model\joker\fixed2_check.png');sheet=Image.new('RGBA',(1024,2304),(0,0,0,0));font=ImageFont.truetype(r'C:\Windows\Fonts\arial.ttf',20)
for row,name in enumerate(['joker_thief_nomask','joker_thief','joker_student']):
 for col,view in enumerate(['front','threequarter']):
  im=Image.open(local/f'{name}_{view}.png').convert('RGBA').resize((512,768),Image.Resampling.LANCZOS);ImageDraw.Draw(im).text((10,10),name+' | '+view,font=font,fill='white',stroke_width=2,stroke_fill='black');sheet.alpha_composite(im,(col*512,row*768))
sheet.save(out)
