"""消耗堆的离线预览，60fps：牌堆本身和抽牌堆 / 牌组 / 弃牌堆一样（标题 + 段标题 + 牌网格），背景、人物和入场动画用原版营地 SKILL 页。
python3 tools/p3r/skill_pile_preview.py <scratch>   （先用 hero_render 渲好 <scratch>/skill/start/color_NNNN.png：CampSkillStart + Skill 页偏移，0.5 帧一张）
原版画面（参考视频 ref3 53.67–54.17s）：左边白底（原版放队伍条 → 这里放牌堆），中右一大块斜切的蓝色水面面板，右下白三角；
结城理倒挂在蓝面板左缘、手里举着召唤器（3D 道具，原版灰色平涂材质），外套实心深蓝（不透水），白衬衫、蓝头发。
原版数据：DT_CampParamSkill —— 白板 10 帧 CurveUp1、标题 10 帧 CurveUp1、列表 10 帧 CurveUp1、队伍条 6 帧 CurveUp3 每条间隔 1 帧（→ 牌依次滑入）、
人物 Silhouette 等 23 帧后 10 帧 CurveUp1 偏移 (10, 170)；InDist -100。DT_CampCameraTable 的 Skill 行（人物 x +6.87cm、偏航 -2°）已在渲染里。
"""
import sys, os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

S = sys.argv[1]
R = 'research/p3r_camp/'
W, H, FPS = 1920, 1080, 60
OUT = S + '/skill_pile'
os.makedirs(OUT + '/frames', exist_ok=True)
CJ = '/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc'
BARLOW = 'mod/pck_stage/p3makoto/fonts/BarlowCondensed-900Italic.ttf'
TICK = 1 / 30
P = dict(siro=(10, 0, 1), title=(10, 0, 1), lst=(10, 0, 1), sil=(10, 23, 1))
SIL_TO = (10, 170)
HERO_DX = 40                                   # 人物基本在原版位置（中右），只右移一点给左边的牌堆让位
JACKET = np.array([0.05, 0.10, 0.40])          # 外套（色键部分）填实心深蓝，不透水（参考视频里外套是实心的）


def up(n, x): x = min(max(x, 0.0), 1.0); return 1 - (1 - x) ** (n + 1)
def prog(key, t): fr, wait, n = P[key]; return up(n, (t / TICK - wait) / fr)
def out_expo(x): return 1 if x >= 1 else 1 - 2 ** (-10 * x)


# ── 背景：战斗画面套营地曲线的水下蓝（同 COMMU / ITEM）──
game = np.asarray(Image.open(S + '/../images/209.webp').convert('RGB').resize((W, H))).astype(float) / 255
l = (game ** 2.2) @ np.array([0.2126, 0.7152, 0.0722]); l = np.clip((l - 0.5) * 1.2 + 0.5, 0, 1)
ci = lambda ks: np.interp(l, [k[0] for k in ks], [k[1] for k in ks])
under = np.stack([ci([(0, 0), (0.1, 0.1), (0.9, 0.6), (1, 0.8)]), ci([(0, .3), (.1, .5), (.7, .7), (1, .85)]), ci([(0, .7), (.1, .8), (.7, .85), (1, .9)])], -1)
yy = np.linspace(0, 1, H)[:, None, None]
under = under * (1 - 0.7 * yy) + np.array([0, 0.063, 0.506]) * 0.7 * yy
under = under * 0.82 + np.array([0.0, 0.05, 0.45]) * 0.18          # 蓝面板比 COMMU 更深一点（参考视频）


def hero(tp):
    i = min(int(round(tp * 60)), 190)
    a = np.asarray(Image.open(S + '/skill/start/color_%04d.png' % i).convert('RGBA')).astype(float) / 255
    rgb, al = a[..., :3], a[..., 3]
    c = np.clip(rgb / np.maximum(al, 1e-3)[..., None], 0, 1)
    s = np.clip(c[..., 1] - np.maximum(c[..., 0], c[..., 2]), 0, 1)
    c = np.clip((c - np.stack([0 * s, s, 0 * s], -1)) / np.maximum(1 - s, 1e-3)[..., None], 0, 1)
    c = c * (1 - s[..., None]) + JACKET * s[..., None]
    return c, al


def shift(c, a, dx, dy):
    dx, dy = int(round(dx)), int(round(dy))
    c2 = np.zeros_like(c); a2 = np.zeros_like(a)
    ys, yd = (slice(0, H - dy), slice(dy, H)) if dy >= 0 else (slice(-dy, H), slice(0, H + dy))
    xs, xd = (slice(0, W - dx), slice(dx, W)) if dx >= 0 else (slice(-dx, W), slice(0, W + dx))
    c2[yd, xd] = c[ys, xs]; a2[yd, xd] = a[ys, xs]
    return c2, a2


def layer_siro():
    """原版 SKILL 页的白底：左边一整块（斜切右缘）+ 右下白三角；右缘一道青线。"""
    im = Image.new('RGBA', (W, H)); d = ImageDraw.Draw(im)
    d.polygon([(0, 0), (0.47 * W, 0), (0.43 * W, H), (0, H)], fill=(244, 247, 252, 255))
    d.line((0.47 * W, 0, 0.43 * W, H), fill=(57, 216, 255), width=5)
    d.polygon([(W, 0.40 * H), (W, H), (0.80 * W, H)], fill=(244, 247, 252, 255))
    d.line((W, 0.40 * H, 0.80 * W, H), fill=(57, 216, 255), width=4)
    return im


def layer_head():
    im = Image.new('RGBA', (W, H)); d = ImageDraw.Draw(im)
    tx = 120
    fb = ImageFont.truetype(BARLOW, 92); fc = ImageFont.truetype(BARLOW, 112)
    d.text((tx + 8, 98), 'EXHAUST PILE', font=fb, fill=(31, 92, 255))
    d.text((tx, 90), 'EXHAUST PILE', font=fb, fill=(255, 255, 255), stroke_width=5, stroke_fill=(8, 18, 36))
    w = d.textlength('EXHAUST PILE', font=fb)
    d.text((tx + 26 + w, 74), '9', font=fc, fill=(80, 230, 255), stroke_width=5, stroke_fill=(8, 18, 36))
    d.polygon([(tx - 2, 200), (tx + 502, 200), (tx + 494, 228), (tx - 10, 228)], fill=(10, 30, 90, 235))
    d.polygon([(tx - 2, 200), (tx + 12, 200), (tx + 4, 228), (tx - 10, 228)], fill=(80, 230, 255))
    d.text((tx + 26, 214), '本场战斗中消耗的牌 · 最新在前', font=ImageFont.truetype(CJ, 19), fill=(204, 237, 255), anchor='lm')
    y, x = 310, 200
    d.polygon([(x + 12, y - 22), (x + 330, y - 22), (x + 320, y + 22), (x, y + 22)], fill=(5, 8, 23, 240))
    d.polygon([(x + 12, y - 22), (x + 24, y - 22), (x + 14, y + 22), (x + 2, y + 22)], fill=(80, 230, 255))
    d.text((x + 34, y), 'RECENT', font=ImageFont.truetype(BARLOW, 36), fill=(255, 255, 255), anchor='lm')
    d.text((x + 310, y), '×9', font=ImageFont.truetype(BARLOW, 38), fill=(80, 230, 255), anchor='rm')
    d.line((x + 338, y, 840, y), fill=(60, 120, 200, 200), width=2)
    return im


def cards():
    """牌：取实机截图里的牌（牌组 291 的技能行 5 张 + 抽牌堆 293 第二行 4 张），逐张切出来，入场时一张张滑进（原版队伍条：每条间隔 1 帧）。"""
    out = []
    r1 = Image.open(S + '/../images/291.webp').convert('RGBA')
    for x0 in (903, 1086, 1268, 1451, 1633):
        out.append(r1.crop((x0, 622, x0 + 176, 868)))
    r2 = Image.open(S + '/../images/293.webp').convert('RGBA')
    for x0 in (208, 388, 568, 748):
        out.append(r2.crop((x0, 596, x0 + 178, 838)))
    res = []
    for c in out:
        m = Image.new('L', c.size, 0); ImageDraw.Draw(m).rounded_rectangle((3, 3, c.width - 4, c.height - 4), radius=10, fill=255)
        c.putalpha(m); res.append(c.resize((166, 236), Image.LANCZOS))
    return res


SIRO, HEAD, CARDS = layer_siro(), layer_head(), cards()
YY, XX = np.ogrid[0:H, 0:W]
T_PAGE = 0.10
N = int(3.0 * FPS)
ONLY = [int(x) for x in sys.argv[2].split(",")] if len(sys.argv) > 2 else range(N)
for n in ONLY:
    t = n / FPS
    tp = t - T_PAGE
    img = under.copy()
    pil = Image.fromarray((img * 255).clip(0, 255).astype(np.uint8)).convert('RGBA')
    if tp > 0:
        p = prog('siro', tp)
        lay = SIRO if p >= 1 else Image.fromarray((np.asarray(SIRO).astype(float) * np.array([1, 1, 1, min(1, p * 1.6)])).astype(np.uint8))
        pil.alpha_composite(lay, (int(-100 * (1 - p)), 0)) if p < 1 else pil.alpha_composite(lay)
        c, a = hero(tp)
        sp = prog('sil', tp)
        c, a = shift(c, a, HERO_DX + SIL_TO[0] * sp, -120 + SIL_TO[1] * sp)   # 起点上移 120，落位后（+170）脸在画面下部露出（对照参考视频）
        arr = np.asarray(pil).astype(float) / 255
        arr[..., :3] = arr[..., :3] * (1 - a[..., None]) + c * a[..., None]
        pil = Image.fromarray((arr * 255).clip(0, 255).astype(np.uint8))
        p = prog('title', tp)
        if p > 0:
            hl = HEAD if p >= 1 else Image.fromarray((np.asarray(HEAD).astype(float) * np.array([1, 1, 1, min(1, p * 1.6)])).astype(np.uint8))
            pil.alpha_composite(hl.crop((int(100 * (1 - p)), 0, W, H)), (0, 0))
        # 牌：每张间隔 1 帧（原版 SkillPartyInterval），各 6 帧 CurveUp3 从左 100px 滑入
        for i, cd in enumerate(CARDS):
            q = up(3, (tp / TICK - i) / 6)
            if q <= 0: continue
            row, col = divmod(i, 5)
            x = 200 + col * 182 - row * 0 + int(-100 * (1 - q)) - int(row * 12)
            y = 340 + row * 252
            cc = cd if q >= 1 else Image.fromarray((np.asarray(cd).astype(float) * np.array([1, 1, 1, min(1, q * 1.6)])).astype(np.uint8))
            pil.alpha_composite(cc, (x, y))
    page = np.asarray(pil.convert('RGB')).astype(float) / 255
    # 切入：圆孔从消耗堆按钮（右下偏上，Iris 0.96, 0.82）张开，0.45s EaseOutExpo
    r = out_expo(min(t / 0.45, 1)) * 2.3
    d2 = np.sqrt(((XX / W - 0.96) * W / H) ** 2 + (YY / H - 0.82) ** 2)
    m = np.clip((r - d2) / 0.01, 0, 1)[..., None]
    out = game * (1 - m) + page * m
    Image.fromarray((out * 255).clip(0, 255).astype(np.uint8)).save(OUT + '/frames/f%04d.png' % n)
print('ok', N)
