from pathlib import Path
import json,shutil,numpy as np
from PIL import Image,ImageDraw,ImageFont
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/gun/front';out=Path('outputs/p5_gun_front_reference');out.mkdir(parents=True,exist_ok=True)
rec=json.loads((p/'render_records.json').read_text(encoding='utf-8'));peak=max(next(c for c in rec['clips'] if c['number']==53)['frames'],key=lambda x:x['hand_world'][2])['frame']
keys=[('key_gun_raise',51,10),('key_gun',52,0),('key_gun_recoil',53,peak),('key_gun_lower',54,4)]
board=Image.new('RGB',(2048,832),'#bababa');d=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25);records=[]
for i,(name,n,f) in enumerate(keys):
 src=p/f'{n}_frames/frame_{f:03}.png';shutil.copy2(src,p/(name+'.png'));shutil.copy2(src,out/(name+'.png'))
 im=Image.open(src).convert('RGBA');bg=Image.new('RGBA',im.size,(186,186,186,255));bg.alpha_composite(im);board.paste(bg.resize((512,768),Image.Resampling.LANCZOS).convert('RGB'),(i*512,64));d.text((i*512+12,12),f'{name} / {n} f{f}',font=font,fill='black');records.append({'name':name,'clip':n,'main':n+2,'frame':f,'seconds':f/30})
board.save(p/'keyframes.png');shutil.copy2(p/'keyframes.png',out/'keyframes.png')
comparison=Image.new('RGB',(1536,864),'#bababa');dd=ImageDraw.Draw(comparison)
paths=[('idle_a3',r/'art/p5/poses/idle_a3.png'),('previous key_gun',r/'research/p5r/gun/key_gun.png'),('front/key_gun',p/'key_gun.png')]
for i,(label,path) in enumerate(paths):
 im=Image.open(path).convert('RGBA');bg=Image.new('RGBA',im.size,(186,186,186,255));bg.alpha_composite(im);comparison.paste(bg.resize((512,768),Image.Resampling.LANCZOS).convert('RGB'),(i*512,64));dd.text((i*512+12,14),label,font=font,fill='black')
comparison.save(p/'angle_comparison.png');shutil.copy2(p/'angle_comparison.png',out/'angle_comparison.png')
for n in [51,52,53,54]:
 shutil.copy2(p/f'{n}.gif',out/f'{n}.gif');shutil.copy2(p/f'{n}_sheet.png',out/f'{n}_sheet.png')
for path in p.glob('*_frames/*.png'):
 im=Image.open(path);a=np.array(im);assert im.size==(1024,1536) and im.mode=='RGBA' and a[:,:,3].max()>0
 assert a[0,:,3].max()==0 and a[-1,:,3].max()==0 and a[:,0,3].max()==0 and a[:,-1,3].max()==0,path
(p/'keyframes.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
s=p/'scripts';s.mkdir(exist_ok=True)
for n in ['gun_front_render.py','gun_front_sheets.py','gun_front_finalize.py']:shutil.copy2(Path('work')/n,s/n)
report='''# 开枪参考：更正面的固定相机（2026-10-26）

用户要求先做参考、确认角度后才生成gun_a2。本次只完成第1步；没有调用生图工具，没有制作或覆盖gun_a2。

沿用上一版51～54 / main[53]～main[56]四段原动作、D方案（rotation + Bip01根translation）、joker_thief_nomask修复模型、GUN0001/tokarev原版手枪、h_Motion_hand_R挂载及比例。只调整固定相机方向：从(0.8,1,0)改为(1,0,0)，让保持帧的胸口更朝镜头，参考idle_a3；仍使用全序列联合包围范围一次取景，四段相机位置、角度、正交大小固定，不逐帧改变。

沿用上一版统一水平镜像输出以保持向画面右方展示；3D模型仍挂右手，骨骼与动作不改。镜像后的服装左右细节不能作为画风/服装参考，后续以idle_a3为准。原版51开头和54末尾存在自身转身，固定镜头不会人为把每帧都转成正面。相机、挂点、采样及每帧世界位置完整记录在render_records.json。

30fps参考时基、每2帧一张，共34张1024×1536透明RGBA。四张联系表标帧号，四个GIF宽320，按源动作时长近似播放。GIF铺灰底便于看清，单帧PNG/关键帧真透明；检查尺寸、非空和四边alpha为0。没有修改原模型动作和旧版gun参考。

重新查看新角度采样序列后，四张关键帧如下（与前版相同的动作时刻，便于直接比较镜头改变）：
'''
for x in records:report+='- '+x['name']+'：序号'+str(x['clip'])+' / main['+str(x['main'])+']，第'+str(x['frame'])+'帧。\n'
report+='\nkeyframes.png为四张关键帧并排；angle_comparison.png为idle_a3/旧key/新key同画布50%缩放对比，没有单独调整人物大小。原渲染取景留白比立绘多，本次只供用户确认角度，不是最终立绘比例。\n\n本次仅提交research/p5r/gun/front，提交号见本报告Git历史。推送后用ls-remote确认origin。等待用户确认角度后，才以front/key_gun作编辑底图，严格保留手肘弯曲、手腕、头脚位置；身份比例服装与脸另参考idle_a3/face_standard。未打开结城理图片，未读取obs.json或密码，art/cards原样保留不提交。\n'
(p/'REPORT.md').write_text(report,encoding='utf-8');(out/'报告.md').write_text(report,encoding='utf-8')
print('front refs ready',json.dumps(records))
