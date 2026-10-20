"""弃牌堆的离线预览，60fps：牌堆本身和抽牌堆 / 牌组一样（标题 + 牌网格），背景、人物和入场动画用原版营地 ITEM 页。
python3 tools/p3r/item_preview.py <scratch>   （先用 hero_render 渲好 <scratch>/item/start/color_NNNN.png：CampItemStart，0.5 帧一张）
原版数据：DT_CampParamItem（各部件入场帧数 / 等待 / 曲线）、DT_CampParamCommon（切页遮罩、Item 页人物填色、水光）、
DT_UILayout_CampItem（说明 / 提示位置）、SPR_UI_Camp 图集（队伍条、× 和数字、箭头、圆点、标题）、T_UI_ItemIcon（列表图标）、键位提示图集（LB / RB）。
原版没有的（程序里写死、资源里查不到）：曲线形状（CurveUpN 按 1-(1-t)^(N+1) 近似）、各部件的屏幕位置（按参考视频 ref3 54.83–55.67s 量）。
"""
import sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
sys.path.insert(0, os.path.dirname(__file__))
from camp_spr import spr

S = sys.argv[1]
R = 'research/p3r_camp/'
W, H = 1920, 1080
FPS = 60
CJ = '/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc'
BARLOW = 'mod/pck_stage/p3makoto/fonts/BarlowCondensed-900Italic.ttf'


def font(sz): return ImageFont.truetype(CJ, sz)


# ── 原版参数（30fps 帧） ──
TICK = 1 / 30
P = dict(siro=(10, 0, 1), title=(8, 0, 2), lst=(10, 2, 2), tab=(8, 0, 2), sil=(20, 10, 2), party=(4, 0, 2))   # (帧数, 等待, CurveUpN)
IN_DIST = -100
SIL_TO = (-90, -110)
HERO_DX = -520                  # 人物整体左移，给右边的牌堆让位（用户：不必完整露出人物）
FILL = np.array([0.027746, 0.082817, 0.255208])        # Edit_Item_FillColor


def up(n, x):
    x = min(max(x, 0.0), 1.0)
    return 1 - (1 - x) ** (n + 1)


def down(n, x):
    x = min(max(x, 0.0), 1.0)
    return x ** (n + 1)


def prog(key, t):
    fr, wait, n = P[key]
    return up(n, (t / TICK - wait) / fr)


def tint(im, rgb, a=1.0):
    im = im.copy()
    px = np.asarray(im).astype(float)
    px[..., :3] = np.array(rgb, float)[None, None] * (px[..., :3] / 255.0)
    px[..., 3] *= a
    return Image.fromarray(px.clip(0, 255).astype(np.uint8))


def flat(im, rgb, a=1.0):
    px = np.asarray(im).astype(float)
    px[..., :3] = np.array(rgb, float)[None, None]
    px[..., 3] *= a
    return Image.fromarray(px.clip(0, 255).astype(np.uint8))


def scale(im, s): return im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)


# ── 素材 ──
ICON = Image.open(R + 'tex/Xrd777/UI/Common/ItemIcon/Platforms/_Win64/T_UI_ItemIcon_00_texture.png').convert('RGBA')


def icon(r, c):
    box = (8 + 167 * c, 10 + 61 * r, 8 + 167 * c + 152, 10 + 61 * r + 42)
    im = ICON.crop(box)
    return im.crop(im.getbbox())


ICONS = {'attack': icon(5, 0), 'skill': icon(2, 0), 'power': icon(1, 4), 'status': icon(3, 0), 'curse': icon(3, 4)}
KH = Image.open(R + 'pages/tex/Xrd777/UI/KeyHelp/Platforms/_Win64/T_UI_Keyhelp_Camp_Button_00_texture.png').convert('RGBA')


def kh_letters():
    """键位提示图集左上角三种大小的 LB（上排）/ RB（下排），各取最大的那个。"""
    from scipy import ndimage
    out = []
    for y0, y1 in ((100, 228), (228, 345)):
        a = np.asarray(KH)[y0:y1, 0:560, 3] > 40
        lab, n = ndimage.label(ndimage.binary_dilation(a, iterations=2))
        boxes = [(s[1].start, y0 + s[0].start, s[1].stop, y0 + s[0].stop) for s in ndimage.find_objects(lab)]
        out.append(KH.crop(max(boxes, key=lambda b: (b[2] - b[0]) * (b[3] - b[1]))))
    return out


LB, RB = kh_letters()
TITLE = spr(16)                 # ITEM
BAND = spr(200)                 # 结城理队伍条（黑）
LEADER = spr(240)
XMARK, DIG = spr(280), [spr(281 + i) for i in range(10)]
HPDIG = [spr(610 + i) for i in range(10)]          # 队伍条 HP / SP 的细斜体数字
PLV, SLASH = spr(609), spr(608)
ARROW = spr(295)
DOT, RING = spr(296), spr(297)
PILL = spr(192)

# 弃牌堆示例（名字, 类型, 张数, 说明）
CARDS = [('打击', 'attack', 3, '造成6点伤害。'), ('防御', 'skill', 2, '获得5点格挡。'), ('星界脉冲', 'attack', 1, '对所有敌人造成6点伤害两次。'),
         ('陨星', 'attack', 1, '造成8点伤害。给予1层虚弱。给予1层易伤。'), ('崇拜', 'skill', 1, '获得✦✦。'), ('铸剑者', 'power', 1, '每回合开始时，获得1把君王之剑。'),
         ('受伤', 'status', 1, '不能被打出。')]
SEL = 0

# ── 背景：战斗画面套营地曲线（和 COMMU / SKILL 同一条）+ 浅水 + 焦散 ──
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


def hero(t):
    """CampItemStart（进入 3.33s），0.5 帧一张；色键 = 透明（透出水）；ITEM 页人物整体压成原版填色（深海军蓝），只留高光。"""
    i = min(int(round(t * 60)), 180)
    a = np.asarray(Image.open(S + '/item/start/color_%04d.png' % i).convert('RGBA')).astype(float) / 255
    rgb, al = a[..., :3], a[..., 3]
    rgb = np.clip(rgb / np.maximum(al, 1e-3)[..., None], 0, 1)
    s = np.clip(rgb[..., 1] - np.maximum(rgb[..., 0], rgb[..., 2]), 0, 1)
    c = rgb.copy(); c[..., 1] -= s; c = np.clip(c / np.maximum(1 - s, 1e-3)[..., None], 0, 1)
    c = FILL + (c - FILL) * np.array([0.20, 0.22, 0.36])   # 原版 ITEM 页：人物几乎整个压成填色（深海军蓝），只留一点偏蓝的明暗层次（参考 ref3 55.33s）
    c = c * (1 - s[..., None]) + FILL * s[..., None]          # 色键（外套 / 头发暗部）也填成深海军蓝，不透出水（用户：原版是深色、不透明）
    return c, al


def shift(c, a, dx, dy):
    dx, dy = int(round(dx)), int(round(dy))
    c2 = np.zeros_like(c); a2 = np.zeros_like(a)
    ys, yd = (slice(0, H - dy), slice(dy, H)) if dy >= 0 else (slice(-dy, H), slice(0, H + dy))
    xs, xd = (slice(0, W - dx), slice(dx, W)) if dx >= 0 else (slice(-dx, W), slice(0, W + dx))
    c2[yd, xd] = c[ys, xs]; a2[yd, xd] = a[ys, xs]
    return c2, a2


# ── 各层（PIL RGBA，原点在最终位置；入场时整体平移 / 淡入） ──
def layer_siro():
    im = Image.new('RGBA', (W, H)); d = ImageDraw.Draw(im)
    d.polygon([(0, 0), (W, 0), (W, 150), (0, 150)], fill=(255, 255, 255, 255))
    d.polygon([(1040, 158), (W, 158), (W, 196), (1060, 196)], fill=(18, 70, 190, 200))
    return im


def layer_title():
    im = Image.new('RGBA', (W, H))
    t = scale(TITLE, 2.0)
    im.alpha_composite(tint(t, (214, 219, 228)), (40, -108))
    return im


def number(n, digs, s, rgb):
    ims = [scale(digs[int(ch)], s) for ch in str(n)]
    w = sum(i.width for i in ims) - 4 * (len(ims) - 1)
    out = Image.new('RGBA', (w, max(i.height for i in ims)))
    x = 0
    for i in ims: out.alpha_composite(tint(i, rgb), (x, out.height - i.height)); x += i.width - 4
    return out


def layer_party():
    im = Image.new('RGBA', (W, H)); d = ImageDraw.Draw(im)
    RH, RW, y = 82, 832, 78
    s = RH / BAND.height
    left = BAND.crop((0, 0, 640, BAND.height)).resize((int(640 * s), RH), Image.LANCZOS)
    right = BAND.crop((640, 0, BAND.width, BAND.height)).resize((int((BAND.width - 640) * s), RH), Image.LANCZOS)
    im.alpha_composite(left, (0, y)); d.rectangle((left.width - 1, y + 2, RW - right.width + 1, y + RH - 1), fill=(0, 0, 0, 255))
    im.alpha_composite(right, (RW - right.width, y))
    im.alpha_composite(scale(LEADER, 0.6).rotate(14, expand=True, resample=Image.BICUBIC), (-2, y - 20))
    d.text((290, y + RH / 2), '结城 理', font=font(30), fill=(255, 255, 255), anchor='lm')
    im.alpha_composite(tint(scale(SLASH, 0.7), (120, 130, 150)), (462, y + 18))
    im.alpha_composite(tint(scale(PLV, 0.6), (150, 160, 180)), (492, y + 44))
    im.alpha_composite(number(52, HPDIG, 0.6, (230, 235, 245)), (540, y + 30))
    for (x, val, rgb, lab) in ((600, 17, (60, 225, 235), 'HP'), (720, 5, (240, 214, 60), 'SP')):
        d.text((x, y + 60), lab, font=ImageFont.truetype(BARLOW, 18), fill=rgb, anchor='ls')
        im.alpha_composite(number(val, HPDIG, 0.75, rgb), (x + 28, y + 16))
        d.line((x, y + 66, x + 108, y + 66), fill=rgb, width=3)
    return im


def layer_tab():
    im = Image.new('RGBA', (W, H)); d = ImageDraw.Draw(im)
    pink = (240, 96, 140)
    lb, rb = scale(LB, 0.62), scale(RB, 0.62)
    ar = tint(scale(ARROW, 0.62), pink)
    im.alpha_composite(ar, (1040, 52)); im.alpha_composite(lb, (1040 + ar.width - 6, 46))
    im.alpha_composite(rb, (1820 - ar.width - rb.width + 6, 46)); im.alpha_composite(ar.transpose(Image.FLIP_LEFT_RIGHT), (1820 - ar.width, 52))
    d.text((1455, 86), '弃牌堆', font=font(28), fill=(30, 40, 70), anchor='mm')
    for k in range(5):
        dd = scale(RING if k == 0 else DOT, 0.5)
        im.alpha_composite(tint(dd, (30, 40, 70)), (1395 + k * 26 - dd.width // 2, 118))
    return im


def pill(n):
    p = flat(PILL, (8, 10, 18)).resize((150, 42), Image.LANCZOS)
    x = tint(scale(XMARK, 0.78), (235, 240, 250)); p.alpha_composite(x, (22, 5))
    num = number(n, DIG, 0.78, (235, 240, 250)); p.alpha_composite(num, (112 - num.width // 2, 2))
    return p


def layer_list(sel, pos=None):
    """sel：选中行（文字变色）；pos：白条所在行（小数，滑动中）。"""
    pos = sel if pos is None else pos
    im = Image.new('RGBA', (W, H)); d = ImageDraw.Draw(im)
    y0, pitch, slant = 210, 56, 9
    yb = y0 + pos * pitch; xb = 1057 - pos * slant
    d.polygon([(xb, yb - 6), (W, yb - 6), (W, yb + 54), (xb - 7, yb + 54)], fill=(255, 255, 255, 255))
    d.rectangle((xb, yb - 9, W, yb - 6), fill=(230, 40, 70, 255))
    for i, (name, kind, n, _) in enumerate(CARDS):
        y = y0 + i * pitch
        x = 1118 - i * slant
        ic = scale(ICONS[kind], 0.74)
        im.alpha_composite(ic, (x, y + 24 - ic.height // 2))
        on = i == sel
        d.text((x + ic.width + 12, y + 24), name, font=font(31), fill=(20, 24, 40) if on else (110, 232, 255), anchor='lm',
               stroke_width=1, stroke_fill=(20, 24, 40) if on else (4, 20, 70))
        im.alpha_composite(pill(n), (1628 - i * slant, y + 4))
    d.rounded_rectangle((1782, 456, 1790, 650), radius=4, fill=(10, 12, 20, 255))
    d.rounded_rectangle((1783, 456, 1789, 520), radius=3, fill=(255, 255, 255, 255))
    return im


def layer_info(sel):
    im = Image.new('RGBA', (W, H)); d = ImageDraw.Draw(im)
    name, kind, n, desc = CARDS[sel]
    d.rounded_rectangle((1210, 728, 1262, 748), radius=10, outline=(200, 235, 255), width=3)
    lines = [desc[i:i + 14] for i in range(0, len(desc), 14)]
    for k, ln in enumerate(lines[:2]):
        d.text((1295, 738 + k * 38), ln, font=font(28), fill=(200, 240, 255), anchor='lm')
    d.text((1635, 932), '要查看哪张牌？', font=font(28), fill=(235, 240, 250), anchor='lm')
    d.line((1630, 952, 1915, 952), fill=(235, 240, 250), width=2)
    d.text((1700, 1000), 'Ⓐ 详情   Ⓑ 返回', font=font(24), fill=(235, 240, 250), anchor='lm')
    return im


def layer_head():
    """和抽牌堆 / 牌组同一套标题：DISCARD PILE + 张数，下面说明条。"""
    im = Image.new('RGBA', (W, H)); d = ImageDraw.Draw(im)
    tx = 1080                       # 牌堆在右边（用户：牌堆放右边，人物往左挪）
    fb = ImageFont.truetype(BARLOW, 92); fc = ImageFont.truetype(BARLOW, 112)
    d.text((tx + 8, 98), 'DISCARD PILE', font=fb, fill=(31, 92, 255))
    d.text((tx, 90), 'DISCARD PILE', font=fb, fill=(255, 255, 255), stroke_width=5, stroke_fill=(8, 18, 36))
    w = d.textlength('DISCARD PILE', font=fb)
    d.text((tx + 26 + w, 74), '9', font=fc, fill=(80, 230, 255), stroke_width=5, stroke_fill=(8, 18, 36))
    d.polygon([(tx - 2, 200), (tx + 502, 200), (tx + 494, 228), (tx - 10, 228)], fill=(10, 30, 90, 235))
    d.polygon([(tx - 2, 200), (tx + 12, 200), (tx + 4, 228), (tx - 10, 228)], fill=(80, 230, 255))
    d.text((tx + 26, 214), '按进入牌堆的先后排列 · 最新在前', font=font(19), fill=(204, 237, 255), anchor='lm')
    # 段标题 RECENT ×9（和抽牌堆的段标题同一个样式）
    y = 310; x = 1000
    d.polygon([(x + 12, y - 22), (x + 330, y - 22), (x + 320, y + 22), (x, y + 22)], fill=(5, 8, 23, 240))
    d.polygon([(x + 12, y - 22), (x + 24, y - 22), (x + 14, y + 22), (x + 2, y + 22)], fill=(80, 230, 255))
    d.text((x + 34, y), 'RECENT', font=ImageFont.truetype(BARLOW, 36), fill=(255, 255, 255), anchor='lm')
    d.text((x + 310, y), '×9', font=ImageFont.truetype(BARLOW, 38), fill=(80, 230, 255), anchor='rm')
    d.line((x + 338, y, 1880, y), fill=(153, 219, 255, 140), width=2)
    return im


def layer_grid():
    """牌网格：用实机截图里的牌（游戏里是同一套牌堆网格代码，只换背景和人物）。
    第一行取牌组截图 291 的技能行，第二行取抽牌堆截图 293 的第二行（293 第一行上有用户画的红圈，不用）。"""
    def feather(src, f=16):
        a = np.ones((src.height, src.width))
        r = np.clip(np.arange(src.width) / f, 0, 1); a *= np.minimum(r, r[::-1])[None]
        r = np.clip(np.arange(src.height) / f, 0, 1); a *= np.minimum(r, r[::-1])[:, None]
        px = np.asarray(src.convert('RGBA')).copy(); px[..., 3] = (a * 255).astype(np.uint8)
        return Image.fromarray(px)
    row1 = Image.open(S + '/../images/291.webp').convert('RGBA').crop((895, 618, 1812, 868))
    row2 = Image.open(S + '/../images/293.webp').convert('RGBA').crop((205, 592, 928, 840))
    im = Image.new('RGBA', (W, H))
    im.alpha_composite(feather(row1.resize((890, round(250 * 890 / 917)))), (1000, 338))
    im.alpha_composite(feather(row2), (1000, 590))
    return im


def layer_siro_item():
    """原版 ITEM 页的白板（左上，人物头部压在上面）+ 白板里的淡灰大字 ITEM；右边留给牌堆标题。"""
    im = Image.new('RGBA', (W, H)); d = ImageDraw.Draw(im)
    d.polygon([(0, 0), (960, 0), (930, 150), (0, 150)], fill=(255, 255, 255, 255))
    d.polygon([(0, 158), (926, 158), (918, 196), (0, 196)], fill=(18, 70, 190, 200))
    t = scale(TITLE, 2.0)
    im.alpha_composite(tint(t, (214, 219, 228)), (40, -60))
    return im


LAY = {'siro': layer_siro_item(), 'head': layer_head(), 'grid': layer_grid()}


def place(base, im, p, dx_from):
    """入场：从 dx_from 偏移滑到原位，同时淡入。"""
    if p <= 0: return
    x = int(round((1 - p) * dx_from))
    a = im if p >= 1 else Image.fromarray((np.asarray(im).astype(float) * np.array([1, 1, 1, min(1, p * 1.6)])).astype(np.uint8))
    base.alpha_composite(a, (x, 0)) if x >= 0 else base.alpha_composite(a.crop((-x, 0, W, H)), (0, 0))


T_PAGE = 0.10                                    # 圆孔张开 0.1s 后页面各部件开始入场（同 SKILL 预览，用户认可）
CURSOR_AT = [(1.70, 1), (2.05, 2), (2.40, 3)]       # 光标下移（原版 ListRedSlide 6 帧 CurveUp2）
N = int(3.0 * FPS)
YY, XX = np.ogrid[0:H, 0:W]
os.makedirs(S + '/item/frames', exist_ok=True)
ONLY = [int(x) for x in sys.argv[2].split(',')] if len(sys.argv) > 2 else range(N)   # 调试：只渲几帧
for n in ONLY:
    t = n / FPS
    # 页面
    tp = t - T_PAGE
    img = under.copy()
    cs = caustics(t); img = 1 - (1 - img) * (1 - cs[..., None] * np.array([0.6, 0.95, 1.0]) * 0.8)
    if tp > 0:
        c, a = hero(tp)
        sp = prog('sil', tp)
        c, a = shift(c, a, HERO_DX + SIL_TO[0] * sp, SIL_TO[1] * sp)
    pil = Image.fromarray((img * 255).clip(0, 255).astype(np.uint8)).convert('RGBA')
    if tp > 0:
        place(pil, LAY['siro'], prog('siro', tp), IN_DIST)
        arr = np.asarray(pil).astype(float) / 255
        arr[..., :3] = arr[..., :3] * (1 - a[..., None]) + c * a[..., None]
        pil = Image.fromarray((arr * 255).clip(0, 255).astype(np.uint8))
        place(pil, LAY['head'], prog('title', tp), -IN_DIST)
        place(pil, LAY['grid'], prog('lst', tp), -IN_DIST)
    page = np.asarray(pil.convert('RGB')).astype(float) / 255
    # 切入：从右下角弃牌堆按钮张开圆孔（0.45s，EaseOutExpo），孔里是新页面，孔外还是战斗画面（用户：之前那种从按钮展开就挺好）
    r = (1 - 2 ** (-10 * min(t / 0.45, 1))) * 2.3
    d2 = np.sqrt(((XX / W - 0.96) * W / H) ** 2 + (YY / H - 0.94) ** 2)
    m = np.clip((r - d2) / 0.01, 0, 1)[..., None]
    out = game * (1 - m) + page * m
    Image.fromarray((out * 255).clip(0, 255).astype(np.uint8)).save(S + '/item/frames/f%04d.png' % n)
print('ok', N)
