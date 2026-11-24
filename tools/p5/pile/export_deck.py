"""牌组（P5R EQUIP 页）运行时素材：把 mock_deck.py 排版图里静态的部分分层导出（1080p，裁到不透明范围），
运行时（p5mod/src/Vfx/PileDeck.cs）按 layout.json 摆回去；红底、类型列表的字 / 数字、选中斜条、入场退场在运行时做。
用法：python3 tools/p5/pile/export_deck.py   →  art/p5/ui/pile_deck/*.png + layout.json"""
import sys, glob, json, numpy as np
from pathlib import Path
sys.path.insert(0, 'tools/p5/pile')
import mock_deck as mk
from mock_draw import stars, spr, place, R, SS, W, H, tint
from PIL import Image, ImageDraw

OUT = Path('art/p5/ui/pile_deck')
OUT.mkdir(parents=True, exist_ok=True)
S = SS
layout = {}


def save(name, cv):
    im = cv.resize((W, H), Image.LANCZOS)
    bb = im.getchannel('A').getbbox()
    if bb is None:
        return
    im.crop(bb).save(OUT / f'{name}.png')
    layout[name] = [bb[0], bb[1]]


def new():
    return Image.new('RGBA', (W * S, H * S))


m = mk.m
# 1. 背景块面（红色以外）：黑块 + 浅灰板 + 左上星纹拼贴；红色那块透明（运行时红星纹着色器）
cv = new(); d = ImageDraw.Draw(cv)
red_poly = [m(p) for p in mk.RED[:2]] + [(W + 40, -40), (W + 40, H + 40)] + [m(p) for p in mk.RED[3:]]
full = Image.new('L', (W * S, H * S), 255); ImageDraw.Draw(full).polygon([(x * S, y * S) for x, y in red_poly], fill=0)
black = Image.new('RGBA', (W * S, H * S), (8, 8, 8, 255)); cv.paste(black, (0, 0), full)
mk.poly(d, mk.GRAY, (214, 214, 214), S)
st = stars(W * S, H * S, base=118, n=8)
sm = Image.new('L', (W * S, H * S)); ImageDraw.Draw(sm).polygon([(x * S, y * S) for x, y in [(-40, -40)] + [m(p) for p in mk.STAR[1:3]] + [(-40, H + 40)]], fill=255)
st.putalpha(sm); cv.alpha_composite(st)
save('bg', cv)
layout['red'] = red_poly

# 2. 立绘 CAMPEQUIP_2D01
art = Image.open(glob.glob(R + 'art/portrait/BASE__P5_CAMPEQUIP_2D01__*.png')[0]).convert('RGBA')
k = 0.5 * mk.MAP
a2 = art.resize((round(art.width * k * S), round(art.height * k * S)), Image.LANCZOS)
ox = int(mk.ART_AT[0] * S)
a2 = a2.crop((-ox, 0, min(a2.width, -ox + W * S), min(a2.height, H * S)))
cv = new(); cv.alpha_composite(a2, (0, 0))
save('portrait', cv)

# 3. 标题牌 MeNu&DECK（右上）
plate = Image.new('RGBA', (430 * S, 130 * S)); pd = ImageDraw.Draw(plate)
pd.rectangle([0, 0, 430 * S, 130 * S], fill=(235, 235, 235)); pd.rectangle([6 * S, 6 * S, 424 * S, 124 * S], fill=(10, 10, 10))
t = Image.open('art/p5/ui/pile_text/txt_menu_deck.png').convert('RGBA')
hm = spr('00048'); hm = hm.resize((int(hm.width * 0.36 * S), int(hm.height * 0.36 * S)), Image.LANCZOS); plate.alpha_composite(hm, (14 * S, 8 * S))
tt = t.resize((int(t.width * 52 * S / t.height), 52 * S), Image.LANCZOS); plate.alpha_composite(tt, (min(190 * S, plate.width - tt.width - 10 * S), 10 * S))
cs = spr('00479'); cs = cs.resize((int(cs.width * 0.5 * S), int(cs.height * 0.5 * S)), Image.LANCZOS); plate.alpha_composite(cs, ((plate.width - cs.width) // 2, 76 * S))
cv = new(); place(cv, plate, 1665, 84, 1 / S, -4)
save('title', cv)

# 4. COMMAND（灰字压在黑身体上）
cv = new(); place(cv, tint(spr('00201'), (214, 214, 214)), 330, 840, 0.36, 4)
save('command', cv)

# 5. 原版大数字（列表张数）
for c in range(10):
    spr(f'{235 + c:05d}').save(OUT / f'big{c}.png')

layout['list'] = {'x': 880, 'y': 70, 'step': 88, 'slant': 46}
(OUT / 'layout.json').write_text(json.dumps(layout, indent=1, ensure_ascii=False))
print(sorted(p.name for p in OUT.iterdir()))
