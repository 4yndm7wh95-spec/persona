from pathlib import Path
import json,shutil,numpy as np
from PIL import Image,ImageDraw,ImageFont
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/gun';out=Path('outputs/p5_gun_reference');out.mkdir(parents=True,exist_ok=True)
rec=json.loads((p/'render_records.json').read_text(encoding='utf-8'));peak=max(next(c for c in rec['clips'] if c['number']==53)['frames'],key=lambda f:f['hand_world'][2])['frame']
keys=[('key_gun_raise',51,10),('key_gun',52,0),('key_gun_recoil',53,peak),('key_gun_lower',54,4)]
board=Image.new('RGB',(2048,832),'#bababa');d=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26);checks=[]
for i,(name,n,f) in enumerate(keys):
 src=p/f'{n}_frames/frame_{f:03}.png';shutil.copy2(src,p/(name+'.png'));shutil.copy2(src,out/(name+'.png'))
 im=Image.open(src).convert('RGBA');bg=Image.new('RGBA',im.size,(186,186,186,255));bg.alpha_composite(im);board.paste(bg.resize((512,768),Image.Resampling.LANCZOS).convert('RGB'),(i*512,64));d.text((i*512+12,12),f'{name} / {n} f{f}',font=font,fill='black')
 checks.append({'name':name,'clip_number':n,'original_main':n+2,'frame_30fps':f,'seconds':f/30})
board.save(out/'keyframes.png');shutil.copy2(out/'keyframes.png',p/'keyframes.png')
for path in p.glob('*_frames/*.png'):
 im=Image.open(path);a=np.array(im)
 assert im.size==(1024,1536) and im.mode=='RGBA' and a[:,:,3].max()>0
 assert a[0,:,3].max()==0 and a[-1,:,3].max()==0 and a[:,0,3].max()==0 and a[:,-1,3].max()==0,path
(p/'keyframes.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
scripts=p/'scripts';scripts.mkdir(exist_ok=True)
for n in ['gun_ref_prepare.py','gun_ref_prepare_clips.py','gun_ref_render.py','gun_ref_sheets.py','gun_ref_finalize.py']:shutil.copy2(Path('work')/n,scripts/n)
report='''# P5R 开枪原版参考（2026-10-26）

基线6464dcc3。按 codex_p5_gun_ref.md 完成第1步，仅提交 research/p5r/gun；立绘生成尚未开始。提交号以本报告Git历史为准，推送后另用ls-remote核对origin。

## 模型、枪和挂点

- Joker：本机 model/joker/fixed2/joker_thief_nomask.glb，沿用已修复去面具/Blur的模型。
- 原版枪：BASE.CPK / MODEL/WEAPON/GUN0001.GMD，GFDExport导出的内部网格名 tokarev/barrel，枪口辅助节点wp_muzzle。选用常规手枪序列首个0001；没有解析装备表，因此“怪盗服默认装备”对应关系为候选，不把编号推断写成装备表确认。另查GUN0000为调试/占位构造，未采用。
- 原始枪及导出GLB留本机 p5r_rip/model/gun_ref。枪无skins字段，先补空数组兼容现有修复脚本，再使用 fix_gfd_glb.py --flip-v（重定位4个无蒙皮accessor、翻转2个UVaccessor；无逆绑定矩阵需改）。没有更改游戏文件。
- 角色没有独立的武器骨骼，使用右手辅助节点 h_Motion_hand_R（Bip01___R___Hand的子节点）挂载。随每帧辅助节点世界矩阵更新，不逐帧手动挪枪；沿用Blender导入的坐标转换，无额外缩放。

## 动画、相机和采样

- 51～54对应 BB0001_051.GAP#main[53]～main[56]。
- 采用D：精确按节点名匹配rotation，额外只保留Bip01根translation；其他translation和scale使用模型静态值。源根位移范围约0.1～1.9cm，保留以便转身中的位置变化可见。
- 一个固定正交相机用于四段全部帧：位置/朝向/正交大小见render_records.json，从全部帧联合包围范围一次确定，不逐帧重新取景。
- 镜头从模型前方看保持姿势，原始投影朝左；为得到用户需要的朝右展示，最终PNG统一水平镜像（不是改动画骨骼）。3D右手挂点仍是右手，镜像会反转服装左右的屏幕投影，后续生图须以idle_a3的服装/左右手为准，只学姿势。
- 原版动作本身转身幅度较大：51开头和54末尾在这一固定镜头下呈背部；没有逐帧换镜头或改骨骼旋转强行保持正面。四张所选关键帧均来自持枪阶段的侧前方。
- 30fps参考时基，每2帧渲一张：51=8张（0～14），52=11张（0～20），53=7张（0～12），54=8张（0～14），共34张1024×1536透明RGBA。
- 联系表每格标帧号；320宽GIF铺中灰便于看清黑衣，速度按原时长近似（GIF时间以10ms量化；52循环末尾等时端点不额外停留）。逐帧PNG和关键帧保持透明，不把GIF灰底用于生图。

## 关键帧
'''
for k in checks:report+='- '+k['name']+'：序号'+str(k['clip_number'])+' / main['+str(k['original_main'])+']，30fps参考第'+str(k['frame_30fps'])+'帧。\n'
report+='\n51选手已过脸、手臂尚未平伸的第10帧；52选保持第0帧；53按已采样右手辅助节点世界高度最大值选帧；54选枪翻到脸前的第4帧。全34张均检查RGBA、尺寸、非空且四边alpha为0。以上为渲染数据检查，不是游戏验收。\n\n未打开结城理mod图片；未读取obs.json或密码。art/cards的11个修改文件及其他未跟踪资料原样保留、未提交。可复现脚本在scripts/，大GLB和原始游戏资源仍留本机。\n'
(p/'REPORT.md').write_text(report,encoding='utf-8');(out/'报告.md').write_text(report,encoding='utf-8')
print('KEYS',json.dumps(checks));print('BYTES',sum(f.stat().st_size for f in p.rglob('*') if f.is_file()))
