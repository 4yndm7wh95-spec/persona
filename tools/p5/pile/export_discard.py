"""弃牌堆（P5R ITEM 页）运行时素材：把 mock_discard.py 排版图里静态的部分分层导出（1080p，裁到不透明范围），
运行时（p5mod/src/Vfx/PileDiscard.cs）按 layout.json 摆回去；数字、斜带、面板斜边、入场退场在运行时做。
用法：python3 tools/p5/pile/export_discard.py   →  art/p5/ui/pile_discard/*.png + layout.json"""
import sys, glob, json, numpy as np
from pathlib import Path
sys.path.insert(0, 'tools/p5/pile')
import mock_discard as md
from mock_draw import stars, spr, place, R, SS, W, H, tint
from PIL import Image, ImageDraw

OUT = Path('art/p5/ui/pile_discard')
OUT.mkdir(parents=True, exist_ok=True)
S = SS
layout = {}


def save(name, cv, scale=1.0):
    """cv 是 SS 倍的画布（scale = 画布单位 → 1080p 的倍数）：缩到 1080p、裁掉透明边，记左上角。"""
    k = scale / S
    im = cv.resize((max(1, round(cv.width * k)), max(1, round(cv.height * k))), Image.LANCZOS)
    bb = im.getchannel('A').getbbox()
    if bb is None:
        return
    im.crop(bb).save(OUT / f'{name}.png')
    layout[name] = [bb[0], bb[1]]


def new(w=W, h=H):
    return Image.new('RGBA', (int(w * S), int(h * S)))


def lx(y):
    return md.A_TOP[0] + (md.A_BOT[0] - md.A_TOP[0]) * (y - md.A_TOP[1]) / (md.A_BOT[1] - md.A_TOP[1])


m = md.m
TL, TR, BR = m((1456, 0)), m((1700, 0)), m((1930, 905))
TIP, BOT = m((1335, 1075)), m((1290, 1120))
BOT2 = (BOT[0] + (BOT[0] - TIP[0]) / (BOT[1] - TIP[1]) * (H - BOT[1]), H)
TRI = m((1078, 112))

# 1. 黑块 + 黑白星纹条
cv = new(); d = ImageDraw.Draw(cv)
slab = [(lx(TRI[1]), TRI[1]), TL, (TR[0], 0), BR, TIP, BOT, BOT2, (lx(H), H)]
d.polygon([(x * S, y * S) for x, y in slab], fill=(8, 8, 8))
st = stars(W * S, H * S, base=118, n=8)
band = [(690, 0), (lx(0), 0), (lx(440), 440), (740, 440)]
mk = Image.new('L', (W * S, H * S)); ImageDraw.Draw(mk).polygon([(x * S, y * S) for x, y in band], fill=255)
st.putalpha(mk); cv.alpha_composite(st)
save('slab', cv)

# 2. 撞色立绘（斜线右边：黑 → 浅灰，白 → 黑）
art = Image.open(glob.glob(R + 'art/portrait/BASE__P5_CAMPITEM_2D01__*.png')[0]).convert('RGBA')
size = round(art.width * md.ART_K * S)
a2 = np.asarray(art.resize((size, size), Image.LANCZOS)).astype(np.float32)
ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
X = xs / S + md.ART_AT[0]; Y = ys / S + md.ART_AT[1]
right = X >= lx(Y)
lum = a2[..., :3].mean(-1)
inv = np.clip(222 - lum * (214 / 255), 8, 230)
a2[right, :3] = inv[right][:, None]
lay = Image.fromarray(a2.astype(np.uint8))
ox, oy = int(md.ART_AT[0] * S), int(md.ART_AT[1] * S)
if ox < 0:
    lay = lay.crop((-ox, 0, lay.width, lay.height)); ox = 0
cv = new(); cv.alpha_composite(lay, (ox, oy))
save('portrait', cv)

# 3. 标题牌
cv = new()
plate = Image.new('RGBA', (430 * S, 130 * S)); pd = ImageDraw.Draw(plate)
pd.rectangle([0, 0, 430 * S, 130 * S], fill=(235, 235, 235)); pd.rectangle([6 * S, 6 * S, 424 * S, 124 * S], fill=(10, 10, 10))
place(cv, plate, 1680, 90, 1 / S, 4)
place(cv, spr('00048'), 1555, 72, 0.36, 4)
t = Image.open('art/p5/ui/pile_text/txt_menu_discard.png').convert('RGBA')
place(cv, t, 1745, 64, 52 / t.height, 4)
place(cv, spr('00479'), 1685, 130, 0.5, 4)
save('title', cv)

# 4. COMMAND（黑字压在浅灰身体上）
cv = new()
place(cv, tint(spr('00201'), (12, 12, 12)), 840, 940, 0.36, 4)
save('command', cv)

# 5. 左边列表：每行的头像底 + 图标（数字、文字运行时画），两个标签牌 —— 画在 1/K 倍的画布上再缩
K = md.LIST_K
Hh = H / K; n = 1 + len(md.TYPE_ROWS); rh = Hh / n
rows = [('discard', None)] + list(md.TYPE_ROWS)
row_info = []
for i, (key, icon) in enumerate(rows):
    yc = (i + 0.5) * rh
    right_ = (372 if i == 0 else 272) / 0.8
    dx = (md.edge_x(yc * K) - 52) / K - right_
    hx = (150 if i == 0 else 88) + dx
    k8 = i % 8 + 1
    cv = new(W / K, Hh)
    md.draw_plg(cv, f'party_0{k8}_02', hx, yc - 4, 168, (240, 240, 240), -6)
    if icon is not None:
        md.draw_plg(cv, f'party_0{k8}_04', hx + 4, yc - 2, 168 * 222 / 267, (8, 8, 8), -6)
        ic = spr(f'{icon:05d}'); sc = 100 / max(ic.size); ic = ic.resize((int(ic.width * sc), int(ic.height * sc)), Image.LANCZOS)
        md.paste_at(cv, ic, hx - ic.width / 2, yc - ic.height / 2 - 4, 1.0, 8)
    else:
        head = spr('00245').resize((156, 156), Image.LANCZOS)
        md.paste_at(cv, head, hx - 76, yc - 90, 1.0)
    save(f'row{i}', cv, K)
    row_info.append({'key': key, 'hx': hx, 'yc': yc, 'tilt': -4 if i % 2 == 0 else -2})
for key, y, rot, w in (('discard', 2, 14, 140), ('bytype', rh + 96, 10, 168)):
    cv = new(W / K, Hh)
    tag = spr('00253').resize((w, int(w * 222 / 298)), Image.LANCZOS)
    tt = md.txt(key, 26); tt = tt.resize((min(tt.width, int(w * 0.62)), 26), Image.LANCZOS)
    tg = Image.new('RGBA', tag.size); tg.alpha_composite(tag); tg.alpha_composite(tt, ((tag.width - tt.width) // 2 + 2, (tag.height - 26) // 2 + 2))
    md.paste_at(cv, tg, -22, y, 1.0, rot)
    save(f'tag_{key}', cv, K)

# 6. 原版数字（大：张数；小：已升级，青色）+ 下划线条（白，运行时染色）
for c in range(10):
    spr(f'{235 + c:05d}').save(OUT / f'big{c}.png')
    md.tinted(spr(f'{257 + c:05d}'), md.TEAL).save(OUT / f'small{c}.png')
spr('00256').save(OUT / 'underline.png')

layout['list'] = {'k': K, 'rows': row_info, 'edge': md.LIST_EDGE}
layout['band'] = [m((1890, 785)), BR, TIP]
layout['under'] = [BR, (W + 40, BR[1] + 8), (W + 40, H + 40), (BOT2[0], H + 40), BOT2, BOT, TIP]
(OUT / 'layout.json').write_text(json.dumps(layout, indent=1, ensure_ascii=False))
print(sorted(p.name for p in OUT.iterdir()))
