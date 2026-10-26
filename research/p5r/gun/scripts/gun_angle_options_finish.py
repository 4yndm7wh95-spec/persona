from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,shutil,numpy as np
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/gun';folder=p/'angle_options_frames';out=Path('outputs/p5_gun_angle_options');out.mkdir(parents=True,exist_ok=True)
meta=json.loads((folder/'camera_options.json').read_text(encoding='utf-8'));board=Image.new('RGB',(3072,864),'#bcbcbc');d=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',27);small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',23)
assert not (folder/'postprocessed.json').exists()
for i,cfg in enumerate(meta['options']):
 path=folder/cfg['file'];im=Image.open(path).convert('RGBA').transpose(Image.Transpose.FLIP_LEFT_RIGHT);im.save(path);a=np.array(im)
 assert im.size==(1024,1536) and a[:,:,3].max()>0 and a[0,:,3].max()==0 and a[-1,:,3].max()==0 and a[:,0,3].max()==0 and a[:,-1,3].max()==0
 bg=Image.new('RGBA',im.size,(188,188,188,255));bg.alpha_composite(im);board.paste(bg.resize((512,768),Image.Resampling.LANCZOS).convert('RGB'),(i*512,0))
 suffix=' (original)' if cfg['percent']==0 else ' (front)' if cfg['percent']==100 else ''
 d.text((i*512+14,782),str(cfg['percent'])+'%'+suffix,font=font,fill='black');d.text((i*512+14,822),f"azimuth {cfg['azimuth_degrees']:.3f} deg",font=small,fill='black');shutil.copy2(path,out/path.name)
board.save(p/'angle_options.png');shutil.copy2(p/'angle_options.png',out/'angle_options.png')
(folder/'postprocessed.json').write_text(json.dumps({'horizontal_mirror_applied':True,'all_images_same_scale':True,'board_scale':0.5,'original_files':'1024x1536 RGBA'},indent=2),encoding='utf-8')
scripts=p/'scripts'
for name in ['gun_angle_options_render.py','gun_angle_options_finish.py']:shutil.copy2(Path('work')/name,scripts/name)
report='''# 开枪相机中间角度候选（2026-10-26）

本次只用52 / BB0001_051.GAP#main[54]第0帧，保持原修复模型、GUN0001原版手枪、h_Motion_hand_R挂载、D方案（rotation+Bip01根translation）、Workbench材质/透明渲染不变。未生成gun_a2，未重渲51～54四段，等用户选择角度。

## 角度定义

相机绕模型世界Z竖直轴转动，方位角为atan2(worldY,worldX)，两端沿用原版参考与front参考的相机方向：原版(0.8,1,0)，front(1,0,0)。在这两个方向之间按方位角线性插值，而不是对方向向量直接插值。0%原版方位51.340192°，100%front方位0°。表内角度是统一水平镜像输出前的相机真实角；镜像设置与前两批参考完全相同。

| 比例 | 实际方位角 | 相对原版转动 |
|---|---:|---:|
'''
for c in meta['options']:report+=f"| {c['percent']}% | {c['azimuth_degrees']:.6f}° | {c['rotation_from_original_degrees']:.6f}° |\n"
report+='\n六张均1024×1536透明RGBA。为了固定其他设置，两端也重新渲染：同一模型中心、同一相机高度/目标/距离、同一正交大小；只改变绕Z方位。正交大小按六个角度一次取联合适配，不逐图重新取景、居中或缩放。图中身体和脚的不同仅为投影变化。详细坐标与正交大小见angle_options_frames/camera_options.json。\n\nangle_options.png按0%、20%、35%、50%、65%、100%从左到右排列，每张统一50%缩放，百分比在图下方。原始单帧放angle_options_frames/，0%/100%对应的是原/正面方向，并非直接粘贴旧取景文件，避免旧版相机正交大小不同造成比较误差。\n\n检查六张尺寸/RGBA/非空及四边alpha为0。没有打开结城理图片，没有读obs.json或密码；art/cards原样保留、不提交。此次提交范围为本候选图、六张单帧、坐标记录、报告和对应脚本。推送后核对origin。用户选定后才以选定角度重渲四段到pick/，再做gun_a2。\n'
(p/'REPORT_angle_options.md').write_text(report,encoding='utf-8');(out/'报告.md').write_text(report,encoding='utf-8')
print([(c['percent'],round(c['azimuth_degrees'],6)) for c in meta['options']])
