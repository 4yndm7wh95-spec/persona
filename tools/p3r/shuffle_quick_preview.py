"""选卡「精简」（StageFx.QuickDeal / QuickTitle）的离线预览，60fps，时间轴和缓动照抄游戏代码。
python3 tools/p3r/shuffle_quick_preview.py <scratch>
牌面取实机截图 images/291.webp（牌组界面那一行技能牌），背景取战斗截图 images/209.webp 压暗（游戏里是原版选牌界面的暗底 + 我们的水之圆背景）。
"""
import sys, os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = sys.argv[1]
W, H, FPS = 1920, 1080, 60
OUT = S + '/shuffle_quick'
os.makedirs(OUT + '/frames', exist_ok=True)


def out_cubic(t): return 1 - (1 - t) ** 3
def out_back(t, s=1.70158): t -= 1; return t * t * ((s + 1) * t + s) + 1
def out_expo(t): return 1 if t >= 1 else 1 - 2 ** (-10 * t)
def clamp(x, a=0.0, b=1.0): return max(a, min(b, x))


row = Image.open(S + '/../images/291.webp').convert('RGBA')
CARD_BOX = [(903, 622, 1080, 870), (1086, 622, 1262, 870), (1268, 622, 1444, 870)]
cards = []
for b in CARD_BOX:
    c = row.crop(b)
    m = Image.new('L', c.size, 0)
    ImageDraw.Draw(m).rounded_rectangle((3, 3, c.width - 4, c.height - 4), radius=10, fill=255)
    c.putalpha(m)
    cards.append(c.resize((int(c.width * 1.75), int(c.height * 1.75)), Image.LANCZOS))
cw, ch = cards[0].size

bg = Image.open(S + '/../images/209.webp').convert('RGB').resize((W, H))
bg = Image.blend(bg, Image.new('RGB', (W, H), (2, 8, 30)), 0.72)
# 水之圆背景（RewardBackdrop 的近似：牌后一圈淡青圆 + 两道弧）
d = ImageDraw.Draw(bg)
cx, cy = W // 2, int(H * 0.52)
for r, a in ((430, 60), (470, 35)):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(80, 200, 255), width=3)
bg = bg.filter(ImageFilter.GaussianBlur(0.6))

logo = Image.open('mod/pck_stage/p3makoto/ui/shuffle_title.png').convert('RGBA')
ls = W * 0.34 / logo.width
logo = logo.resize((int(logo.width * ls), int(logo.height * ls)), Image.LANCZOS)
TILT = -0.12


def tinted(im, rgb, a):
    px = np.asarray(im).astype(float)
    px[..., :3] = np.array(rgb, float)
    px[..., 3] *= a
    return Image.fromarray(px.clip(0, 255).astype(np.uint8))


def glint(base, at, t, size):
    """落位十字闪光（Glint：0.35s，十字星随 sin 缩放 + 旋转）。"""
    if not (0 <= t <= 0.35): return
    k = math.sin(t / 0.35 * math.pi)
    lay = Image.new('RGBA', base.size, (0, 0, 0, 0)); dd = ImageDraw.Draw(lay)
    r = size * 0.42 * k; w = r * 0.05 + 2; rot = t / 0.35 * 0.8
    for ang in (rot, rot + math.pi / 2):
        dx, dy = math.cos(ang) * r, math.sin(ang) * r
        nx, ny = -math.sin(ang) * w, math.cos(ang) * w
        dd.polygon([(at[0] - dx, at[1] - dy), (at[0] + nx, at[1] + ny), (at[0] + dx, at[1] + dy), (at[0] - nx, at[1] - ny)], fill=(255, 255, 255, int(230 * k)))
    glow = Image.new('RGBA', base.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((at[0] - size * 0.45 * k, at[1] - size * 0.45 * k, at[0] + size * 0.45 * k, at[1] + size * 0.45 * k), fill=(57, 216, 255, int(120 * k)))
    base.alpha_composite(glow.filter(ImageFilter.GaussianBlur(40)))
    base.alpha_composite(lay)


SPREAD = 0.3
spacing = cw * 1.12
N = int(1.6 * FPS)
for n in range(N):
    t = n / FPS
    img = bg.copy().convert('RGBA')
    # 标题（QuickTitle：三层，滞后 0.06 / 0.03 / 0，0.25s OutExpo 从左滑到中间，0.7s 后横向化开 0.3s 淡出）
    for lag, a, glow in ((0.06, 0.25, True), (0.03, 0.45, True), (0.0, 1.0, False)):
        u = clamp((t - lag) / 0.25)
        x = -W * 0.35 * (1 - out_expo(u))
        fade = clamp(u * 3) if t < 0.7 else 1 - clamp((t - 0.7) / 0.3)
        if fade <= 0: continue
        sx = 1 + (max(0, t - 0.7) * 1.2)
        lg = logo.resize((int(logo.width * sx), logo.height), Image.LANCZOS)
        lg = tinted(lg, (77, 153, 255), a * fade) if glow else Image.fromarray((np.asarray(lg).astype(float) * np.array([1, 1, 1, a * fade])).astype(np.uint8))
        lg = lg.rotate(-math.degrees(TILT), expand=True, resample=Image.BICUBIC)
        px, py = W / 2 + x * math.cos(TILT), H * 0.14 + x * math.sin(TILT)
        img.alpha_composite(lg, (int(px - lg.width / 2), int(py - lg.height / 2)))
    # 三张牌（QuickDeal：全部从中间出发，一直是正面；0.3s 位置 OutCubic、缩放 0.9→1 OutBack、0.075s 内淡入）
    u = clamp(t / SPREAD)
    order = [1, 0, 2]                                   # 中间那张画在最上面（叠在中间时看得到）
    for i in reversed(order):
        final = (i - 1) * spacing
        x = cx + final * out_cubic(u)
        s = 0.9 + 0.1 * out_back(u)
        a = clamp(u * 4)
        c = cards[i].resize((int(cw * s), int(ch * s)), Image.LANCZOS)
        if a < 1: c = Image.fromarray((np.asarray(c).astype(float) * np.array([1, 1, 1, a])).astype(np.uint8))
        img.alpha_composite(c, (int(x - c.width / 2), int(cy - c.height / 2)))
    for i in range(3):
        glint(img, (cx + (i - 1) * spacing, cy), t - SPREAD * 0.75, ch)
    img.convert('RGB').save(OUT + '/frames/f%04d.png' % n)
print('ok', N)
