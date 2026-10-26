from pathlib import Path
import json,shutil,numpy as np
from PIL import Image,ImageDraw,ImageFont
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/gun/pick';out=Path('outputs/p5_gun_pick');out.mkdir(parents=True,exist_ok=True)
rec=json.loads((p/'render_records.json').read_text(encoding='utf-8'));peak=max(next(c for c in rec['clips'] if c['number']==53)['frames'],key=lambda x:x['hand_world'][2])['frame']
keys=[('key_gun_raise',51,10),('key_gun',52,0),('key_gun_recoil',53,peak),('key_gun_lower',54,4)]
board=Image.new('RGB',(2048,832),'#bababa');d=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25);records=[]
for i,(name,n,f) in enumerate(keys):
 src=p/f'{n}_frames/frame_{f:03}.png';shutil.copy2(src,p/(name+'.png'));shutil.copy2(src,out/(name+'.png'))
 im=Image.open(src).convert('RGBA');bg=Image.new('RGBA',im.size,(186,186,186,255));bg.alpha_composite(im);board.paste(bg.resize((512,768),Image.Resampling.LANCZOS).convert('RGB'),(i*512,64));d.text((i*512+12,12),f'{name} / {n} f{f}',font=font,fill='black');records.append({'name':name,'clip':n,'main':n+2,'frame':f,'seconds':f/30})
board.save(p/'keyframes.png');shutil.copy2(p/'keyframes.png',out/'keyframes.png')
for path in p.glob('*_frames/*.png'):
 im=Image.open(path);a=np.array(im);assert im.size==(1024,1536) and im.mode=='RGBA' and a[:,:,3].max()>0
 assert a[0,:,3].max()==0 and a[-1,:,3].max()==0 and a[:,0,3].max()==0 and a[:,-1,3].max()==0,path
for n in [51,52,53,54]:shutil.copy2(p/f'{n}.gif',out/f'{n}.gif')
(p/'keyframes.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
s=p/'scripts';s.mkdir(exist_ok=True)
for n in ['gun_pick_render.py','gun_pick_sheets.py','gun_pick_finalize.py']:shutil.copy2(Path('work')/n,s/n)
report='''# 用户选定50%开枪参考（2026-10-26）

用户从angle_options中选定50%。相机真实方位角25.670095872954956°（atan2(worldY,worldX)，统一水平镜像前），方向约(0.901303,0.433189,0)。保持模型、原版GUN0001/tokarev、h_Motion_hand_R挂载、D方案rotation+Bip01根translation不变。

以选定方向把51～54 / main[53]～main[56]四段各按30fps参考每2帧渲一张，共34张1024×1536透明RGBA。使用同一固定正交相机，从四段联合包围范围一次取景，不逐帧换镜头。取景尺度重新适配完整序列，方位角严格沿用用户选择。完整相机及每帧记录见render_records.json；沿用之前统一镜像展示朝右的约定。GIF宽320、中灰底，时长按源动作近似；PNG单帧真透明。

关键帧：
'''
for x in records:report+='- '+x['name']+'：序号'+str(x['clip'])+' / main['+str(x['main'])+']，第'+str(x['frame'])+'帧。\n'
report+='\n四张关键帧和联系表已查看用于选帧；检查所有PNG的RGBA/尺寸/非空及四边alpha为0。先仅提交pick参考，推送后确认origin，再以pick/key_gun作为编辑底图生成gun_a2，身份服装比例按idle_a3，脸眼按face_standard。用户未确认gun_a2前不做其他立绘过渡帧。\n\n未打开结城理mod图片，未读obs.json或密码；art/cards原样保留不提交。提交号见本报告Git历史。\n'
(p/'REPORT.md').write_text(report,encoding='utf-8');(out/'报告.md').write_text(report,encoding='utf-8')
print('Pick keyframes ready')
