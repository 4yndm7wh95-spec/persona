from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,shutil
repo=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');out=repo/'research/p5r/model/diag';local=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\round3_work');font=ImageFont.truetype(r'C:\Windows\Fonts\arial.ttf',20);small=ImageFont.truetype(r'C:\Windows\Fonts\arial.ttf',13)
def text(im,pos,t,ft=font):ImageDraw.Draw(im).text(pos,t,font=ft,fill='white',stroke_width=2,stroke_fill='black')
sheet=Image.new('RGBA',(1536,1254),(0,0,0,0))
for row,(variant,desc) in enumerate([('A','A: all channels (264 T / 264 R / 137 S)'),('C','C: rotation only; model translations and scales retained'),('D','D: rotation + Bip01.translation x1; all scale channels omitted')]):
 text(sheet,(8,row*418+6),'BB0001_051_main_0000 | '+desc)
 for col,frame in enumerate([0,8,16,24,32,40]):
  im=Image.open(local/f'anim_{variant}_{frame:02}.png').convert('RGBA').resize((256,384),Image.Resampling.LANCZOS);text(im,(5,5),f'frame {frame} | t={frame/30:.6f}s',small);sheet.alpha_composite(im,(col*256,row*418+34))
sheet.save(out/'anim_test2.png')
for view in ['front','threequarter']:
 im=Image.open(local/f'found_PS0201_{view}.png').convert('RGBA');text(im,(18,16),'Arsene | PS0201.GMD | '+view);im.save(out/f'arsene_found_{view}.png')
candidates=json.loads((local/'found_candidates.json').read_text());contact=Image.new('RGBA',(5*256,384),(0,0,0,0))
for i,c in enumerate(candidates):
 im=Image.open(c['front']).convert('RGBA').resize((256,384),Image.Resampling.LANCZOS);text(im,(6,6),Path(c['package_path']).stem,small);contact.alpha_composite(im,(i*256,0))
contact.save(out/'arsene_texture_candidates.png')
for src,name in [('anim_compare_summary.json','anim_compare_summary.json'),('retarget_variants.json','retarget_variants.json'),('animation_render_info.json','animation_render_info_round3.json'),('texture_search_summary.json','texture_search_summary.json'),('found_candidates.json','arsene_found_sources.json'),('camera_fixed2.json','camera_fixed2.json')]:shutil.copy2(local/src,out/name)
