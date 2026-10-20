"""弃牌堆 = 原版营地菜单 SKILL 页（选队员那一屏）的离线预览，60fps。
python3 tools/p3r/skill_preview.py <scratch>   （需要先用 hero_render 渲好 <scratch>/skill/start/color_NNNN.png，60fps 子帧）
原版素材：T_UI_Camp_06/07（队伍条：黑 / 白 / 蓝三种状态，第 0 行是结城理）、T_UI_Camp_09（LEADER / PARTY 斜体标签）、
CampSkillStart 动作 + DT_CampCameraTable 的 Skill 行偏移、营地背景曲线（同 COMMU）、焦散。
"""
import sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
S = sys.argv[1]; R = 'research/p3r_camp/'
W, H = 1920, 1080; FPS = 60
A6 = Image.open(R + 'tex/Xrd777/UI/Camp/Root/Texture/T_UI_Camp_06_texture.png').convert('RGBA')
A7 = Image.open(R + 'tex/Xrd777/UI/Camp/Root/Texture/T_UI_Camp_07_texture.png').convert('RGBA')
A9 = Image.open(R + 'tex/Xrd777/UI/Camp/Root/Texture/T_UI_Camp_09_texture.png').convert('RGBA')
BAND = {'black': A6.crop((0, 1, 1024, 203)), 'white': A7.crop((0, 1, 1024, 203)), 'blue': A6.crop((0, 1021, 1024, 1223))}
BODY = {'black': (0, 0, 0), 'white': (255, 255, 255), 'blue': None}
def label(box):
    return A9.crop(box)
def find_labels():
    a = np.asarray(A9)[1830:1975, 0:820, 3] > 30
    from scipy import ndimage
    lab, n = ndimage.label(ndimage.binary_dilation(a, iterations=6))
    out = []
    for s in ndimage.find_objects(lab):
        out.append((s[1].start, 1830 + s[0].start, s[1].stop, 1830 + s[0].stop))
    out.sort()
    return out
LB = find_labels()            # 按 x 排：LEADER(≈14~185)、PARTY(≈210~370)、NAVI、RESERVE
# 斜体字的笔画会伸进邻近区域，直接按图集里的固定范围裁（再裁到实际像素）
LEADER = A9.crop((0, 1820, 205, 1990)); LEADER = LEADER.crop(LEADER.getbbox())
PARTY = A9.crop((200, 1820, 385, 1990)); PARTY = PARTY.crop(PARTY.getbbox())
fname = ImageFont.truetype('mod/pck_stage/p3makoto/fonts/LXGWWenKai-Bold-CardTitles.ttf', 40)
fnum = ImageFont.truetype('mod/pck_stage/p3makoto/fonts/BarlowCondensed-900Italic.ttf', 54)
fsmall = ImageFont.truetype('mod/pck_stage/p3makoto/fonts/BarlowCondensed-900Italic.ttf', 22)
CARDS = [('打击', 'strike_regent', 'attack', 1), ('防御', 'defend_regent', 'skill', 1), ('铸剑者', 'the_smith', 'skill', 1),
         ('大爆炸', 'big_bang', 'skill', 0), ('暴政', 'tyranny', 'power', 2), ('陨星', 'falling_star', 'attack', 2),
         ('打击', 'strike_regent', 'attack', 1), ('防御', 'defend_regent', 'skill', 1)]
KIND = {'attack': ('black', 'ATTACK'), 'skill': ('white', 'SKILL'), 'power': ('blue', 'POWER')}
RH, PITCH, Y0, RW = 86, 93, 40, 860
def row_image(name, art, kind, cost, leader):
    style, word = KIND[kind]
    band = BAND[style]; s = RH / 202
    b = np.asarray(band).astype(float)
    # 剪影位（结城理侧脸 + 彩色斜楔）换成卡图：只换"不是条带底色、也不是灰影"的像素，轮廓保持原版
    body = np.asarray(band.crop((900, 60, 960, 140)).convert('RGB')).reshape(-1, 3).mean(0)
    rgb = b[..., :3]; al = b[..., 3]
    grey = (np.abs(rgb - rgb.mean(-1, keepdims=True)).max(-1) < 12) & (rgb.mean(-1) > 40) & (rgb.mean(-1) < 90)
    face = (al > 128) & (np.abs(rgb - body).max(-1) > 40) & ~grey
    face[:, 700:] = False
    cart = Image.open('mod/pck_stage/p3makoto/cards/%s.png' % art).convert('RGB')
    cw, ch = cart.size; tw, th = 700, 202
    sc = max(tw / cw, th / ch); cart = cart.resize((int(cw * sc), int(ch * sc)))
    cart = np.asarray(cart.crop(((cart.width - tw) // 2, (cart.height - th) // 3, (cart.width - tw) // 2 + tw, (cart.height - th) // 3 + th))).astype(float)
    full = np.zeros_like(rgb); full[:, :700] = cart
    rgb = np.where(face[..., None], full * 0.92, rgb)
    band = Image.fromarray(np.dstack([rgb, al]).clip(0, 255).astype(np.uint8))
    left = band.crop((0, 0, 640, 202)).resize((int(640 * s), RH), Image.LANCZOS)
    right = band.crop((640, 0, 1024, 202)).resize((int(384 * s), RH), Image.LANCZOS)
    img = Image.new('RGBA', (RW, RH), (0, 0, 0, 0))
    mid = Image.new('RGBA', (RW - left.width - right.width + 2, RH), tuple(int(v) for v in body) + (255,))
    img.alpha_composite(left, (0, 0)); img.alpha_composite(mid, (left.width - 1, 0)); img.alpha_composite(right, (RW - right.width, 0))
    d = ImageDraw.Draw(img)
    ink = (0, 0, 0) if style == 'white' else (255, 255, 255)
    d.text((int(RW * 0.36), RH // 2), name, font=fname, fill=ink, anchor='mm')
    # 原版 PLV / HP / SP 的位置：费用 / 类型（青底线）/ 张数（黄底线）
    x = int(RW * 0.50)
    d.line([(x - 8, RH - 14), (x - 24, RH - 2)], fill=(150, 150, 170), width=2)
    d.text((x, RH - 30), 'COST', font=fsmall, fill=ink); d.text((x + 50, RH - 8), str(cost), font=fnum, fill=ink, anchor='ls')
    x2 = int(RW * 0.62)
    d.text((x2, RH - 8), word, font=fnum, fill=(60, 215, 240), anchor='ls')
    d.rectangle((x2, RH - 8, x2 + int(fnum.getlength(word)), RH - 4), fill=(60, 215, 240))
    x3 = int(RW * 0.86)
    d.text((x3, RH - 8), '×1', font=fnum, fill=(248, 224, 64), anchor='ls')
    d.rectangle((x3, RH - 8, x3 + int(fnum.getlength('×1')), RH - 4), fill=(248, 224, 64))
    return img
ROWS = [row_image(n, a, k, c, i == 0) for i, (n, a, k, c) in enumerate(CARDS)]
# ── 背景：战斗画面套营地曲线（原版 SKILL / STATUS 页同一条曲线）+ 浅水 + 焦散
game = np.asarray(Image.open(S + '/../images/209.webp').convert('RGB').resize((W, H))).astype(float) / 255
l = (game ** 2.2) @ np.array([0.2126, 0.7152, 0.0722]); l = np.clip((l - 0.5) * 1.2 + 0.5, 0, 1)
ci = lambda ks: np.interp(l, [k[0] for k in ks], [k[1] for k in ks])
under = np.stack([ci([(0, 0), (0.1, 0.1), (0.9, 0.6), (1, 0.8)]), ci([(0, .3), (.1, .5), (.7, .7), (1, .85)]), ci([(0, .7), (.1, .8), (.7, .85), (1, .9)])], -1)
yy = np.linspace(0, 1, H)[:, None, None]
under = under * (1 - 0.7 * yy) + np.array([0, 0.063, 0.506]) * 0.7 * yy
sh = np.clip(1 - yy / 0.32, 0, 1) ** 1.5
under = under * (1 - 0.75 * sh) + np.array([0.235, 0.843, 0.941]) * 0.75 * sh
ct = np.asarray(Image.open(R + 'tex/Xrd777/UI/Camp/Root/Texture/T_UI_Camp_Caustics_00.png').convert('RGBA')).astype(float) / 255
cl = ct[..., :3].mean(-1) * ct[..., 3]
tile = np.asarray(Image.fromarray((cl * 255).astype(np.uint8)).resize((900, 240))).astype(float) / 255
def caustics(t):
    out = np.zeros((H, W)); off = int(t * 0.012 * 900 * 60) % 900
    for r, y in enumerate(range(0, H, 240)):
        row = np.roll(np.tile(tile, (1, W // 900 + 3)), -(off + (450 if r % 2 else 0)), axis=1)[:, :W]
        out[y:y + 240] = row[:min(240, H - y)]
    return out * np.clip(1 - yy[..., 0] * 1.4, 0, 1)
HERO_SHIFT = 0   # 原版 CampSkillStart 结束时人物本来就停在右半边（和截图一致），不用平移
def hero(i):
    a = np.asarray(Image.open(S + '/skill/start/color_%04d.png' % min(i, 190)).convert('RGBA')).astype(float) / 255
    rgb, al = a[..., :3], a[..., 3]
    rgb = np.clip(rgb / np.maximum(al, 1e-3)[..., None], 0, 1)
    s = np.clip(rgb[..., 1] - np.maximum(rgb[..., 0], rgb[..., 2]), 0, 1)
    c = rgb.copy(); c[..., 1] -= s; c = np.clip(c / np.maximum(1 - s, 1e-3)[..., None], 0, 1)
    a2 = (1 - s) * al
    c2 = np.zeros_like(c); a3 = np.zeros_like(a2)
    c2[:, HERO_SHIFT:] = c[:, :W - HERO_SHIFT]; a3[:, HERO_SHIFT:] = a2[:, :W - HERO_SHIFT]
    return c2, a3
def ease_out_expo(x): return 1 - 2 ** (-10 * x) if x < 1 else 1.0
# 白色底板（原版队伍条后面那块白，左缘贴屏幕，右缘斜切）+ 灰色斜块
slab = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(slab)
d.polygon([(0, 0), (0.45 * W, 0), (0.40 * W, H), (0, H)], fill=(255, 255, 255, 255))
for i in range(5):
    x = 0.05 * W + i * 0.07 * W
    d.polygon([(x, 0.15 * H + i * 60), (x + 70, 0.15 * H + i * 60), (x - 120, 0.95 * H), (x - 190, 0.95 * H)], fill=(226, 229, 234, 255))
slab = np.asarray(slab).astype(float) / 255
os.makedirs(S + '/skill/frames', exist_ok=True)
N = int(3.2 * FPS)
for n in range(N):
    t = n / FPS
    # 圆孔从右下（弃牌堆按钮）张开，0.45s
    r = ease_out_expo(min(t / 0.45, 1)) * 2.3
    Y, X = np.ogrid[0:H, 0:W]
    d2 = np.sqrt(((X / W - 0.96) * W / H) ** 2 + (Y / H - 0.94) ** 2)
    m = np.clip((r - d2) / 0.01, 0, 1)[..., None]
    img = under.copy()
    cs = caustics(t); img = 1 - (1 - img) * (1 - cs[..., None] * np.array([0.6, 0.95, 1.0]) * 0.8)
    # 白板从左滑入
    so = ease_out_expo(min(max(t - 0.10, 0) / 0.40, 1))
    sx = int((so - 1) * 0.5 * W)
    sl = np.zeros_like(slab)
    if sx >= 0: sl[:] = slab
    elif sx > -W: sl[:, :W + sx] = slab[:, -sx:]
    img = img * (1 - sl[..., 3:]) + sl[..., :3] * sl[..., 3:]
    c, a = hero(n)
    img = img * (1 - a[..., None]) + c * a[..., None]
    pil = Image.fromarray((img * 255).clip(0, 255).astype(np.uint8)).convert('RGBA')
    # 队伍条：从左逐条滑入（0.18s 起，每条间隔 0.035s，0.32s 到位）
    for i, row in enumerate(ROWS):
        p = ease_out_expo(min(max(t - 0.18 - i * 0.035, 0) / 0.32, 1))
        if p <= 0: continue
        x = int((p - 1) * (RW + 80)); y = Y0 + i * PITCH
        if p < 1:
            row2 = row.copy(); row2.putalpha(Image.eval(row.getchannel('A'), lambda v: int(v * min(1, p * 2))))
        else: row2 = row
        pil.alpha_composite(row2, (max(x, -RW), y)) if x > -RW else None
        if i == 0 and p > 0.6:
            lb = LEADER.resize((int(LEADER.width * 0.62), int(LEADER.height * 0.62)), Image.LANCZOS)
            pil.alpha_composite(lb, (max(0, x + 2), y - 14))
        elif CARDS[i][2] == 'attack' and p > 0.6:
            lb = PARTY.resize((int(PARTY.width * 0.62), int(PARTY.height * 0.62)), Image.LANCZOS)
            pil.alpha_composite(lb, (max(0, x + 2), y - 10))
    out = Image.composite(pil.convert('RGB'), Image.fromarray((game * 255).astype(np.uint8)), Image.fromarray((m[..., 0] * 255).astype(np.uint8)))
    out.save(S + '/skill/frames/f%04d.png' % n)
print('ok', N)
