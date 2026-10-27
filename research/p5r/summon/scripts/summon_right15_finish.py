from pathlib import Path
import json,shutil,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFont
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/summon';q=p/'right15_review';o=Path('outputs/p5_summon_right15');o.mkdir(parents=True,exist_ok=True)
recs=json.loads((q/'camera_records.json').read_text(encoding='utf-8'));checks=[];first2={n:hashlib.sha256((p/n).read_bytes()).hexdigest() for n in ['key_mask_hold.png','key_rip_mid.png']}
if (q/'postprocessed.json').exists():raise RuntimeError('Already postprocessed')
tile=512;cmp=Image.new('RGB',(4*tile,tile+64),(184,184,184));draw=ImageDraw.Draw(cmp);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
for i,c in enumerate(recs):
 name=c['name'];before=q/('before_'+name);shutil.copy2(p/name,before)
 im=Image.open(q/name).convert('RGBA').transpose(Image.Transpose.FLIP_LEFT_RIGHT);a=np.array(im);edge=int(max(a[0,:,3].max(),a[-1,:,3].max(),a[:,0,3].max(),a[:,-1,3].max()))
 assert im.size==(1536,1536) and edge==0,(name,edge)
 im.save(q/name);shutil.copy2(q/name,p/name);shutil.copy2(q/name,o/name)
 checks.append({'file':name,'four_edges_alpha_max':edge,'alpha_bbox':im.getchannel('A').getbbox()})
 for j,(path,label,angle) in enumerate([(before,'before',c['previous_azimuth_degrees']),(q/name,'after',c['azimuth_degrees'])]):
  src=Image.open(path).convert('RGBA');bg=Image.new('RGBA',src.size,(184,184,184,255));bg.alpha_composite(src);x=(i*2+j)*tile;cmp.paste(bg.convert('RGB').resize((tile,tile),Image.Resampling.LANCZOS),(x,0));draw.text((x+12,tile+4),name.replace('.png','')+' '+label,font=font,fill='black');draw.text((x+12,tile+33),f'{angle:.2f} deg',font=font,fill='black')
cmp.save(q/'comparison.png');shutil.copy2(q/'comparison.png',o/'comparison.png')
sheet=Image.new('RGB',(4*tile,tile+64),(184,184,184));draw=ImageDraw.Draw(sheet)
for i,n in enumerate(['key_mask_hold','key_rip_mid','key_rip_open','key_stance']):
 im=Image.open(p/(n+'.png')).convert('RGBA');bg=Image.new('RGBA',im.size,(184,184,184,255));bg.alpha_composite(im);sheet.paste(bg.convert('RGB').resize((tile,tile),Image.Resampling.LANCZOS),(i*tile,0));draw.text((i*tile+12,tile+4),n,font=font,fill='black');draw.text((i*tile+12,tile+33),'321.86 deg' if i<2 else '336.86 deg',font=font,fill='black')
sheet.save(p/'key_contact_sheet.png');shutil.copy2(p/'key_contact_sheet.png',o/'key_contact_sheet.png')
assert all(hashlib.sha256((p/n).read_bytes()).hexdigest()==h for n,h in first2.items())
check={'horizontal_mirror_applied':True,'first_two_files_unchanged_sha256':first2,'last_two_checks':checks,'full_animation_renders_unchanged':True}
(q/'postprocessed.json').write_text(json.dumps(check,indent=2),encoding='utf-8')
sel=json.loads((p/'key_selection.json').read_text(encoding='utf-8'))
for k in sel:
 if k['name'] in [c['name'] for c in recs]:k['selected_camera_degrees']=336.85609585103083;k['source']='right15_review/'+k['name'];k['status']='awaiting user adjusted angle confirmation'
(p/'key_selection.json').write_text(json.dumps(sel,ensure_ascii=False,indent=2),encoding='utf-8')
report='''# 召唤后两张关键帧向人物右侧调整（2026-10-26）

用户反馈“后面两帧往人物右边转一些”。本次按相机观察角度调整处理，采用15°的小幅调整；只改key_rip_open（16帧14）与key_stance（17帧0），前两张key_mask_hold和key_rip_mid保持不变。

开始前fetch+merge，最新仍721837b2。以角色右肩减左肩的世界水平向量确定人物自身右侧，比较相机绕竖直轴正负15°哪一边更靠向右肩侧；两张均为从321.856095851°转至336.856095851°（+15°）。不修改人物原始动作、模型、手部或身体旋转。

相机目标、距离、高度及正交大小285.331708331保持不变；1536×1536 RGBA透明底，统一水平镜像。不挂枪。两张四边alpha均0，未裁切衣摆。前两张文件SHA256在修改前后完全一致，记录见right15_review/postprocessed.json。

## 文件

- key_rip_open.png、key_stance.png：当前调整后的后两张关键帧。
- key_contact_sheet.png：前两张321.86°、后两张336.86°的新四张对比。
- right15_review/comparison.png：每个后段姿势的改前/改后并排，角度均写明。
- right15_review/before_key_*.png：保留本次调整前的两张，不改动历史。
- right15_review/camera_records.json：精确机位及人物右侧方向向量。
- scripts/summon_right15_render.py、summon_right15_finish.py：实际脚本。

这次仅供确认后两张观察角度，未重新渲完整16/17段；根目录15/16/17逐帧PNG、GIF、联系表及render_records仍是上轮321.86°固定相机结果，不能将其当作336.86°版本。key_selection已为后两张标明新角度与来源，避免混淆。等待用户确认后再决定后续整段统一机位/立绘步骤；没有调用生图工具。

art/cards另一会话11个已修改文件未触碰/提交；既有guard_a本地修改及其他无关未跟踪资源保留。不stash、还原、rebase、reset、强推，未打开结城理mod图片，未读obs.json或密码，未启动游戏。推送后用ls-remote确认origin收到提交。
'''
(p/'REPORT_summon_right15.md').write_text(report,encoding='utf-8');(o/'REPORT_summon_right15.md').write_text(report,encoding='utf-8')
(p/'README.md').write_text('''# 当前召唤参考

用户选择321.86°后，已重渲15/16/17三段并挑关键帧。当前逐帧PNG、联系表、GIF仍为该统一机位。

最新反馈：后两张向人物右侧转一些。本轮仅将key_rip_open和key_stance相机转至336.86°，前两张仍321.86°，新key_contact_sheet.png和right15_review/comparison.png待用户确认。详见REPORT_summon_right15.md及key_selection.json。

未开始召唤立绘生图。旧报告保留过程记录，以最新报告为准。
''',encoding='utf-8')
for n in ['summon_right15_render.py','summon_right15_finish.py']:shutil.copy2(Path('work')/n,p/'scripts'/n)
ap=r/'art/p5/poses/eye_fix/approval_progress.json';a=json.loads(ap.read_text(encoding='utf-8'));a['status']='awaiting_summon_adjusted_keyframe_confirmation';a['summon_reference_status']='前两张321.86度不变，后两张往人物右侧转15度至336.86度，待确认；完整段动画仍321.86度，未生图';ap.write_text(json.dumps(a,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Last two keyframes 336.856095851 degrees; first two unchanged; all edges alpha=0.')
