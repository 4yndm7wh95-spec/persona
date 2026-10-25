"""左上角头像直接裁自已导出的 P3R 原版对话立绘，仅更新 HUD 的两个资源。"""
from pathlib import Path
import re
import sys
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/pck"))
from godot_tex import write_texture

stage = ROOT / 'mod/pck_stage'
out = ROOT / 'art/ui'
out.mkdir(exist_ok=True)
source = ROOT / 'art/p3r_tex/hero/T_BU_PC0001_PoseA_C000.png'
# 原画 2048 方图，保留完整发顶与下巴；不重画、不改色。
head = Image.open(source).convert('RGBA').crop((565, 650, 1445, 1530))
size = 352
portrait = Image.new('RGBA', (size, size), (0, 0, 0, 0))
draw = ImageDraw.Draw(portrait)
draw.ellipse((16, 16, size-17, size-17), fill='#142535')
portrait.alpha_composite(head.resize((320, 320), Image.Resampling.LANCZOS), (16, 16))
mask = Image.new('L', (size, size), 0)
ImageDraw.Draw(mask).ellipse((16, 16, size-17, size-17), fill=255)
portrait.putalpha(mask)
ImageDraw.Draw(portrait).ellipse((16, 16, size-17, size-17), outline='#55758b', width=4)
icon = portrait.resize((88,88), Image.Resampling.LANCZOS)
outline = Image.new('RGBA', (88,88), (255,255,255,0))
outline.putalpha(icon.getchannel('A').filter(ImageFilter.MaxFilter(7)))
for suffix, image, uid in [('', icon, 'ddwry85j3nb6'), ('_outline', outline, 'br0nmhqx2ci7a')]:
    name = f'character_icon_regent{suffix}.png'
    rel = f'images/ui/top_panel/{name}'
    image.save(out/name)
    write_texture(stage, rel, image, uid)
    # 私有引用和原路径共享同一个 CTEX，防止 AssetGuard 恢复旧头像。
    text = (stage/(rel+'.import')).read_text(encoding='utf-8')
    text = re.sub(r'^uid="[^\n]*"\n', '', text, flags=re.M)
    private = f'p3makoto/override/{name}'
    dest = stage/(private+'.import')
    dest.parent.mkdir(parents=True,exist_ok=True)
    dest.write_text(text.replace('res://'+rel+'"', 'res://'+private+'"'),encoding='utf-8')

preview = Image.new('RGB',(520,280),'#18212a')
preview.paste(portrait.resize((240,240),Image.Resampling.LANCZOS),(15,20),portrait.resize((240,240),Image.Resampling.LANCZOS))
preview.paste(icon,(325,90),icon)
preview.save(out/'p3r_hud_avatar_preview.png')
print('P3R original HUD avatar staged; exterior alpha=0; source:',source)
