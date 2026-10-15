"""P3R 风格宣传视频模板（真透明底），给剪辑软件的画中画直接套用。

输出（每个模板）：
  <名字>.mov          ProRes 4444（带 Alpha，1920×1080 60fps，Premiere / 剪映 / 达芬奇 / FCP 都能读透明通道），含音轨（P3R 原声拼的，可静音）
  <名字>.wav          同一段音效单独一份
  <名字>_preview.mp4  预览：套在两段示意画面（镜头 A / 镜头 B）上的效果，不带透明，只用来看

模板：
  T1_allout_ripple   转场「总攻击水波」：右上角外落下水滴，同心圆一圈圈扫满全屏 → 再一滴深蓝盖满 → 透明水波从同一圆心挖开露出下一个镜头。
                     （游戏里总攻击那段，去掉背景大字和立绘）。1.8s；画面完全被盖住的区间 0.62–1.40s，在 1.00s 处切镜头。
  T2_stripe_wipe     转场「斜条扫屏」：P3R 菜单式的斜向色条从左往右层层扫过盖满，再从左往右层层退出。1.2s；盖满区间 0.48–0.62s，在 0.55s 处切镜头。
  T3_lockon          提示「看这里」：P3R 锁定框 —— 四个角括号旋转收拢锁定、内圈脉冲、外圈虚线慢转、上方小三角跳动 + 斜标签「CHECK!」。
  T3_lockon_notext   同上，不带文字。框在画面正中，画中画里拖到要强调的位置、缩放即可。4.0s（0.35s 锁定入场，循环脉冲，最后 0.3s 散开）。
  T4_cutin_band      标题 / 技能名切入条：深色斜切底条从右侧砸入、白光扫过、停留后向左滑出。中间留空，自己在上面加字。2.4s。
  T4_cutin_band_text 同上，带示例字「ONE MORE!」。
  T5_critical        强调弹字「CRITICAL!」（P3R 原版字图）：砸入 + 白闪 + 放射速度线，停留后淡出。1.4s。
  T6_butterfly       装饰：游戏结局的蓝蝶从左下飞入、在画面中间绕一圈、从右上飞走（无音效）。4.0s。

用法：python3 tools/promo/p3r_templates.py <输出目录> [模板名…]
依赖：numpy、Pillow、ffmpeg（prores_ks、libx264）。
"""
import math
import os
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "vfx"))
import allout_ripple  # noqa: E402

W, H, FPS = 1920, 1080, 60
ROOT = Path(__file__).resolve().parents[2]
SFX = ROOT / "mod/pck_stage/p3makoto/sfx"
FONT = ROOT / "mod/pck_stage/p3makoto/fonts/BarlowCondensed-900Italic.ttf"

# 总攻击配色（游戏里 AllOut.cs 从原版结算截图取的色）
BASE = (53, 172, 239)
RING_BLUE = (23, 105, 216)
BAND = (23, 97, 175)
NAVY = (8, 26, 82)
DEEP = (10, 30, 120)
CYAN = (118, 212, 248)
RIM = (224, 250, 255)
WHITE = (255, 255, 255)

YY, XX = np.mgrid[0:H, 0:W].astype(np.float32) + 0.5


def ease_out_cubic(x):
    x = min(max(x, 0.0), 1.0)
    return 1 - (1 - x) ** 3


def ease_in_out_cubic(x):
    x = min(max(x, 0.0), 1.0)
    return 4 * x ** 3 if x < 0.5 else 1 - (-2 * x + 2) ** 3 / 2


def ease_out_back(x, s=1.7):
    x = min(max(x, 0.0), 1.0)
    return 1 + (s + 1) * (x - 1) ** 3 + s * (x - 1) ** 2


class Canvas:
    """预乘 alpha 的 float 画布，按"后画的盖在上面"合成。"""

    def __init__(self):
        self.rgb = np.zeros((H, W, 3), np.float32)
        self.a = np.zeros((H, W), np.float32)

    def paint(self, cov, color, alpha=1.0):
        cov = np.clip(cov, 0, 1) * alpha
        c = np.array(color, np.float32) / 255
        self.rgb = c * cov[..., None] + self.rgb * (1 - cov[..., None])
        self.a = cov + self.a * (1 - cov)

    def tint(self, k, color):
        """只改颜色不改透明度（细光边画在已有色块上）。"""
        k = np.clip(k, 0, 1)[..., None]
        c = np.array(color, np.float32) / 255
        self.rgb = self.rgb * (1 - k) + c * self.a[..., None] * k

    def cut(self, keep):
        keep = np.clip(keep, 0, 1)
        self.rgb *= keep[..., None]
        self.a *= keep

    def rgba8(self):
        a = np.clip(self.a, 0, 1)
        rgb = np.where(a[..., None] > 1e-5, self.rgb / np.maximum(a[..., None], 1e-5), 0)
        out = np.dstack([np.clip(rgb, 0, 1), a])
        return (out * 255 + 0.5).astype(np.uint8)


# ───────────────────────── T1 总攻击水波 ─────────────────────────

_TAB = allout_ripple.build()          # 60Hz × 6 圈半径（720p 像素）


def ripple(time):
    f = time * 60
    i = int(math.floor(f))
    if i < 0:
        return np.zeros(_TAB.shape[1])
    if i + 1 >= len(_TAB):
        return _TAB[-1].copy()
    u = f - i
    return _TAB[i] * (1 - u) + _TAB[i + 1] * u


def t1_allout(t, state):
    c = Canvas()
    center = (W * 0.95, -H * 0.45)
    d = np.hypot(XX - center[0], YY - center[1])
    cover = max(math.hypot(px - center[0], py - center[1]) for px, py in ((0, 0), (W, 0), (0, H), (W, H)))
    k = H / 720
    cols = [BAND, BASE, RING_BLUE, BASE, RING_BLUE, BASE]
    r = ripple(t) * k
    prev = state.get("prev")
    for i in range(6):
        if r[i] <= 0:
            break
        c.paint(r[i] - d + 0.5, cols[i])
    # 波前细光边：跑得快的那几圈前沿带一道很淡的青白细光
    for i in range(4):
        if r[i] <= 0:
            continue
        speed = (r[i] - prev[i]) * FPS if prev is not None else 0
        a = 0.3 * min(max((speed - 300 * k) / (2500 * k), 0), 1)
        if a > 0:
            c.tint(a * (1 - np.clip(np.abs(d - r[i]) / 2.5, 0, 1)), RIM)
    state["prev"] = r
    # 盖满：两圈实心水波（深一档蓝 → 中蓝）
    tc = 1.0
    born = (0.0, 0.08)
    sweep = 0.42
    for i, col in enumerate((BAND, RING_BLUE)):
        u = (t - tc - born[i]) / sweep
        if u <= 0:
            continue
        rad = cover * 1.04 * ease_out_cubic(u)
        c.paint(rad - d + 0.5, col)
        ra = 0.45 * (1 - min(u / 0.75, 1))
        if ra > 0:
            c.tint(ra * (1 - np.clip(np.abs(d - rad) / 2.5, 0, 1)), RIM)
    # 透明水波：从圆心挖开，边缘一道亮边
    clear_at = tc + born[1] + 0.75 * sweep
    u = (t - clear_at) / 0.55
    if u > 0:
        hole = cover * 1.06 * ease_out_cubic(u) + 1
        edge = 1 - np.clip(np.abs(d - hole) / 7, 0, 1)
        c.tint(edge * 0.8, (235, 252, 255))
        c.cut(d - hole + 0.5)
    return c


# ───────────────────────── T2 斜条扫屏 ─────────────────────────

ANG = math.radians(70)          # 色条和水平线的夹角（P3R 菜单的斜切）


def t2_stripe(t, state):
    c = Canvas()
    nx, ny = math.sin(ANG), -math.cos(ANG)   # 垂直于色条的方向（从左往右）
    s = XX * nx + YY * ny
    corners = [x * nx + y * ny for x, y in ((0, 0), (W, 0), (0, H), (W, H))]
    s0, s1 = min(corners) - 60, max(corners) + 60
    span = s1 - s0
    # (颜色, 入场开始, 入场时长, 退场开始, 退场时长, 宽度占比 —— 1=铺满)
    layers = [
        (CYAN, 0.00, 0.34, 0.56, 0.40, 1.0),
        (BASE, 0.04, 0.36, 0.60, 0.40, 1.0),
        (RING_BLUE, 0.08, 0.38, 0.55, 0.42, 1.0),
        (DEEP, 0.12, 0.36, 0.52, 0.44, 1.0),
        (NAVY, 0.16, 0.32, 0.50, 0.46, 1.0),
    ]
    for col, a0, ad, b0, bd, _ in layers:
        fu = (t - a0) / ad
        if fu <= 0:
            continue
        front = s0 + span * ease_out_cubic(fu)
        back = s0 + span * ease_in_out_cubic((t - b0) / bd)
        if back >= front:
            continue
        cov = np.minimum(front - s + 0.5, s - back + 0.5)
        c.paint(cov, col)
        # 前沿 / 后沿各一道白色细线（只在移动时）
        if 0 < fu < 1:
            c.tint((1 - np.clip(np.abs(s - front + 3) / 2.2, 0, 1)) * 0.9 * (1 - fu), WHITE)
        bu = (t - b0) / bd
        if 0 < bu < 1:
            c.tint((1 - np.clip(np.abs(s - back - 3) / 2.2, 0, 1)) * 0.9 * (1 - abs(2 * bu - 1)), WHITE)
    # 盖满时最上层深蓝里有几道细斜纹缓慢滑过（P3R 菜单底纹）
    full = min(max((t - 0.40) / 0.08, 0), 1) * (1 - min(max((t - 0.50) / 0.06, 0), 1))
    if full > 0:
        u = (s - t * 900) % 140
        stripe = np.clip(1 - np.abs(u - 70) / 1.5, 0, 1) * 0.12 * full
        c.tint(stripe, CYAN)
    return c


# ───────────────────────── T3 锁定框（看这里） ─────────────────────────

SS = 2           # 超采样倍数


def _font(size):
    return ImageFont.truetype(str(FONT), size * SS)


def t3_lockon(t, state, text=True):
    L = 4.0
    cx, cy = W / 2, H / 2
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    glow = Image.new("RGBA", (W * SS, H * SS), (*CYAN, 0))   # 透明处也是青色：模糊时边缘不会混进黑色发灰
    dr = ImageDraw.Draw(img)
    dg = ImageDraw.Draw(glow)
    intro = ease_out_back(t / 0.35, 2.2)
    outro = min(max((t - (L - 0.3)) / 0.3, 0), 1)
    alpha = min(t / 0.08, 1) * (1 - outro)
    pulse = 0.5 + 0.5 * math.cos((t - 0.35) * 2 * math.pi / 1.0) if t > 0.35 else 1
    R = 215 * (2.2 - 1.2 * intro) * (1 + 0.6 * outro ** 2) * (1 + 0.025 * (1 - pulse))
    rot = (1 - intro) * math.pi / 4 + 0.08 * math.sin(t * 1.3)

    def P(x, y):
        return (x * SS, y * SS)

    def col(c, a):
        return (c[0], c[1], c[2], int(255 * max(0, min(1, a * alpha))))

    # 四个角括号（L 形）
    arm, th = 78, 12
    for q in range(4):
        ang = rot + q * math.pi / 2 + math.pi / 4
        ux, uy = math.cos(ang), math.sin(ang)
        px, py = cx + ux * R, cy + uy * R
        # 两条臂沿切线方向往回收
        a1 = ang + math.pi * 0.75
        a2 = ang - math.pi * 0.75
        for aa in (a1, a2):
            ex, ey = px + math.cos(aa) * arm, py + math.sin(aa) * arm
            for d, w_, cc, k in ((dg, th + 18, CYAN, 0.35), (dr, th, WHITE, 1.0)):
                d.line([P(px, py), P(ex, ey)], fill=col(cc, k), width=int(w_ * SS))
            dr.ellipse([P(px - th / 2, py - th / 2), P(px + th / 2, py + th / 2)], fill=col(WHITE, 1))
    # 内圈（脉冲）
    ri = 145 * (0.6 + 0.4 * intro) * (1 + 0.06 * pulse)
    for d, w_, cc, k in ((dg, 22, CYAN, 0.35 + 0.3 * pulse), (dr, 7, CYAN, 1.0)):
        d.ellipse([P(cx - ri, cy - ri), P(cx + ri, cy + ri)], outline=col(cc, k), width=int(w_ * SS))
    # 脉冲扩散环
    if t > 0.35:
        ph = ((t - 0.35) % 1.0)
        rr = ri + 150 * ease_out_cubic(ph)
        dr.ellipse([P(cx - rr, cy - rr), P(cx + rr, cy + rr)], outline=col(CYAN, 0.8 * (1 - ph)), width=int(5 * SS))
    # 外圈虚线慢转
    ro = R + 38
    n = 36
    for i in range(n):
        if i % 3 == 2:
            continue
        a0 = t * 0.6 + i * 2 * math.pi / n
        a1 = a0 + 2 * math.pi / n * 0.55
        dr.arc([P(cx - ro, cy - ro), P(cx + ro, cy + ro)], math.degrees(a0), math.degrees(a1), fill=col(BASE, 0.9 * intro), width=int(5 * SS))
    # 中心十字准星
    for (x0, y0, x1, y1) in ((cx - 26, cy, cx - 10, cy), (cx + 10, cy, cx + 26, cy), (cx, cy - 26, cx, cy - 10), (cx, cy + 10, cx, cy + 26)):
        dr.line([P(x0, y0), P(x1, y1)], fill=col(WHITE, 0.9 * intro), width=int(4 * SS))
    # 上方小三角跳动（指向中心）
    bob = 10 * abs(math.sin(t * math.pi * 2.0)) if t > 0.35 else 0
    ty = cy - R - 70 - bob - 40 * (1 - intro)
    tri = [P(cx - 22, ty - 20), P(cx + 22, ty - 20), P(cx, ty + 14)]
    dg.polygon(tri, fill=col(CYAN, 0.4))
    dr.polygon(tri, fill=col(WHITE, intro))
    # 斜标签 CHECK!（P3R UI 的白底黑字斜框，从右侧滑入）
    if text:
        lu = ease_out_cubic((t - 0.15) / 0.3)
        if lu > 0:
            f = _font(80)
            word = "CHECK!"
            tw = f.getbbox(word)[2] / SS
            bx = cx + R * 0.72 + 30 + 120 * (1 - lu)
            by = cy - R * 0.72 - 40
            sk = 18
            box = [P(bx - 14, by + 44), P(bx + sk - 14, by - 40), P(bx + tw + sk + 30, by - 40), P(bx + tw + 30, by + 44)]
            dg.polygon(box, fill=col(CYAN, 0.35 * lu))
            dr.polygon(box, fill=col(WHITE, lu))
            dr.text(P(bx + 10, by - 46), word, font=f, fill=col((10, 22, 70), lu))
            # 标签和框之间一条细引线
            dr.line([P(cx + R * 0.72, cy - R * 0.72), P(bx - 6, by + 20)], fill=col(WHITE, 0.9 * lu), width=int(3 * SS))
    from PIL import ImageFilter
    glow = glow.filter(ImageFilter.GaussianBlur(10 * SS))
    out = Image.alpha_composite(glow, img).resize((W, H), Image.LANCZOS)
    return np.asarray(out)


# ───────────────────────── T4 技能名切入条 ─────────────────────────

def t4_cutin(t, state, text=None):
    """P3R 技能切入条：深色斜切底条从右侧猛地砸入（1.6 倍 → 1，带回弹），上沿一道青色亮线，
    底条里一道白光扫过；停留；最后向左滑出淡掉。底条中间留空给自己的字（或带示例字）。"""
    L = 2.4
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    glow = Image.new("RGBA", (W * SS, H * SS), (*CYAN, 0))   # 透明处也是青色：模糊时边缘不会混进黑色发灰
    dr, dg = ImageDraw.Draw(img), ImageDraw.Draw(glow)
    slam = ease_out_back(t / 0.16, 1.6)
    out = ease_in_out_cubic((t - (L - 0.35)) / 0.35)
    a = min(t / 0.05, 1) * (1 - out)
    sc = 1.6 - 0.6 * slam
    cx, cy = W / 2 - 520 * out, H / 2
    bw, bh, sk = 1180, 150, 46

    def P(x, y):
        return ((cx + (x - W / 2) * sc) * SS, (cy + (y - H / 2) * sc) * SS)

    def col(c, k):
        return (c[0], c[1], c[2], int(255 * max(0, min(1, k * a))))

    x0, x1, y0, y1 = W / 2 - bw / 2, W / 2 + bw / 2, H / 2 - bh / 2, H / 2 + bh / 2
    band = [P(x0 + sk, y0), P(x1 + sk, y0), P(x1 - sk, y1), P(x0 - sk, y1)]
    dg.polygon(band, fill=col(CYAN, 0.35))
    dr.polygon(band, fill=col((6, 12, 34), 0.9))
    # 下层错位的蓝色副条（P3R 切入的双层色带）
    off = 26
    sub = [P(x0 + sk + off, y1 - 6), P(x1 + sk - 60, y1 - 6), P(x1 - sk - 60 - 8, y1 + 22), P(x0 - sk + off - 8, y1 + 22)]
    dr.polygon(sub, fill=col(RING_BLUE, 0.95))
    # 上沿亮线
    dr.line([P(x0 + sk, y0 + 2), P(x1 + sk, y0 + 2)], fill=col(CYAN, 1), width=int(5 * sc * SS))
    dg.line([P(x0 + sk, y0 + 2), P(x1 + sk, y0 + 2)], fill=col(CYAN, 0.8), width=int(18 * sc * SS))
    # 底条里扫过的一道白光（入场后 0.15s 起，0.35s 扫完）
    u = (t - 0.15) / 0.35
    if 0 < u < 1:
        sx = x0 - 120 + (bw + 240) * ease_in_out_cubic(u)
        streak = [P(sx + sk, y0), P(sx + sk + 60, y0), P(sx - sk + 60, y1), P(sx - sk, y1)]
        dg.polygon(streak, fill=col(WHITE, 0.6 * math.sin(math.pi * u)))
    # 左侧的三道小斜杠（装饰）
    for i in range(3):
        bx = x0 - 70 - i * 26
        dr.polygon([P(bx + 22, y0 + 20), P(bx + 34, y0 + 20), P(bx - 4, y1 - 20), P(bx - 16, y1 - 20)], fill=col(CYAN if i == 0 else WHITE, 0.9 - 0.25 * i))
    if text:
        f = ImageFont.truetype(str(FONT), int(118 * sc * SS))
        tw = f.getbbox(text)[2]
        tx, ty = P(W / 2, H / 2)
        # 字的投影（偏右下的青色）+ 白字
        dr.text((tx - tw / 2 + 7 * SS * sc, ty - 76 * sc * SS + 6 * SS * sc), text, font=f, fill=col(RING_BLUE, 0.9))
        dr.text((tx - tw / 2, ty - 76 * sc * SS), text, font=f, fill=col(WHITE, 1))
    from PIL import ImageFilter
    glow = glow.filter(ImageFilter.GaussianBlur(12 * SS))
    return np.asarray(Image.alpha_composite(glow, img).resize((W, H), Image.LANCZOS))


# ───────────────────────── T5 CRITICAL! 强调弹字 ─────────────────────────

_CRIT = None


def t5_critical(t, state):
    """P3R 原版 CRITICAL! 字图（白 → 青渐变斜体）：从大到小砸进来 + 白闪 + 一圈放射速度线，停留时缓慢往右上漂，淡出。"""
    global _CRIT
    if _CRIT is None:
        _CRIT = Image.open(ROOT / "mod/pck_stage/p3makoto/ui/critical.png").convert("RGBA")
    L = 1.4
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    slam = ease_out_back(t / 0.12, 1.8)
    fade = min(max((t - (L - 0.35)) / 0.35, 0), 1)
    sc = (1.25 - 0.45 * slam) * 1.25
    drift = (40 * t, -22 * t)
    cx, cy = W / 2 + drift[0], H / 2 + drift[1]
    # 放射速度线
    lines = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    dl = ImageDraw.Draw(lines)
    lu = ease_out_cubic(t / 0.35)
    if t < 0.5:
        rng = np.random.default_rng(3)
        for i in range(26):
            ang = rng.uniform(0, 2 * math.pi)
            r0 = 160 + 260 * lu + rng.uniform(0, 60)
            r1 = r0 + 120 + rng.uniform(0, 160) * (1 - lu)
            al = (1 - lu) * 0.9
            dl.line([((W / 2 + math.cos(ang) * r0) * SS, (H / 2 + math.sin(ang) * r0 * 0.6) * SS),
                     ((W / 2 + math.cos(ang) * r1) * SS, (H / 2 + math.sin(ang) * r1 * 0.6) * SS)],
                    fill=(200, 240, 255, int(255 * al)), width=int(rng.uniform(2, 6) * SS))
    lines = lines.resize((W, H), Image.LANCZOS)
    img = Image.alpha_composite(img, lines)
    # 字
    w, h = int(_CRIT.width * sc), int(_CRIT.height * sc)
    word = _CRIT.resize((w, h), Image.LANCZOS).rotate(7, resample=Image.BICUBIC, expand=True)
    a = min(t / 0.04, 1) * (1 - fade)
    word.putalpha(Image.fromarray((np.asarray(word)[..., 3] * a).astype(np.uint8)))
    img.alpha_composite(word, (int(cx - word.width / 2), int(cy - word.height / 2)))
    # 砸入白闪（加在字上）
    fl = max(0, 1 - t / 0.18)
    if fl > 0:
        arr = np.asarray(img).astype(np.float32)
        wa = np.zeros((H, W), np.float32)
        x0, y0 = int(cx - word.width / 2), int(cy - word.height / 2)
        wa[max(0, y0):y0 + word.height, max(0, x0):x0 + word.width] = np.asarray(word)[: H - max(0, y0), : W - max(0, x0), 3][: wa[max(0, y0):y0 + word.height, max(0, x0):x0 + word.width].shape[0], : wa[max(0, y0):y0 + word.height, max(0, x0):x0 + word.width].shape[1]] / 255
        arr[..., :3] = arr[..., :3] * (1 - fl * wa[..., None]) + 255 * fl * wa[..., None]
        img = Image.fromarray(arr.astype(np.uint8))
    return np.asarray(img)


# ───────────────────────── T6 蓝蝶飞过 ─────────────────────────

def _catmull(p0, p1, p2, p3, t):
    def kn(ti, a, b):
        return ti + max(math.sqrt(math.dist(a, b)), 1e-3)
    t0 = 0
    t1 = kn(t0, p0, p1)
    t2 = kn(t1, p1, p2)
    t3 = kn(t2, p2, p3)
    tt = t1 + (t2 - t1) * t
    def lerp(a, b, ta, tb):
        return tuple(((tb - tt) * a[i] + (tt - ta) * b[i]) / (tb - ta) for i in range(2))
    a1, a2, a3 = lerp(p0, p1, t0, t1), lerp(p1, p2, t1, t2), lerp(p2, p3, t2, t3)
    b1, b2 = lerp(a1, a2, t0, t2), lerp(a2, a3, t1, t3)
    return lerp(b1, b2, t1, t2)


_BF = {}


def _butterfly_path():
    if "pts" in _BF:
        return _BF["pts"], _BF["cum"]
    ctrl = [(-0.08, 1.10), (0.10, 0.86), (0.27, 0.66), (0.40, 0.56), (0.52, 0.58), (0.62, 0.49), (0.62, 0.36),
            (0.53, 0.28), (0.42, 0.32), (0.39, 0.44), (0.47, 0.52), (0.60, 0.46), (0.75, 0.30), (0.90, 0.13), (1.10, -0.10)]
    ctrl = [(x * W, y * H) for x, y in ctrl]
    ext = [ctrl[0]] + ctrl + [ctrl[-1]]
    pts = []
    for i in range(1, len(ext) - 2):
        for k in range(24):
            pts.append(_catmull(ext[i - 1], ext[i], ext[i + 1], ext[i + 2], k / 24))
    pts.append(ctrl[-1])
    cum = [0.0]
    for i in range(1, len(pts)):
        cum.append(cum[-1] + math.dist(pts[i - 1], pts[i]))
    _BF["pts"], _BF["cum"] = pts, cum
    return pts, cum


def _at(d):
    pts, cum = _butterfly_path()
    d = min(max(d, 0), cum[-1])
    i = max(1, np.searchsorted(cum, d))
    i = min(i, len(pts) - 1)
    u = (d - cum[i - 1]) / max(cum[i] - cum[i - 1], 1e-6)
    return (pts[i - 1][0] + (pts[i][0] - pts[i - 1][0]) * u, pts[i - 1][1] + (pts[i][1] - pts[i - 1][1]) * u)


def t6_butterfly(t, state):
    """游戏结局里的那只蓝蝶：左下飞入 → 画面中间绕一圈 → 右上飞走，扇翅夹着滑翔、身后光晕和下落的鳞粉。"""
    L = 4.0
    if "dist" not in state:
        _, cum = _butterfly_path()
        n = 800
        speed = []
        for i in range(n + 1):
            u = i / n
            v = 0.35 + 0.65 * min(u / 0.18, 1)
            if 0.36 < u < 0.74:
                v *= 0.8
            if u > 0.8:
                v *= 1.15
            speed.append(v)
        acc = np.cumsum([0] + [(speed[i] + speed[i + 1]) / 2 for i in range(n)])
        state["dist"] = (acc / acc[-1] * cum[-1], cum[-1])
        state["parts"] = []
        state["angle"] = None
        base = ROOT / "art/v16/butterfly"
        size = int(H * 0.16)
        state["sp"] = {k: Image.open(base / f"butterfly_{k}.png").convert("RGBA").resize((size * 2, size * 2), Image.LANCZOS) for k in ("wing_l", "wing_r", "body")}
        state["size"] = size
    table, total = state["dist"]
    u = min(t / L, 1)
    d = np.interp(u, np.linspace(0, 1, len(table)), table)
    p = _at(d)
    ah = _at(d + total * 0.01)
    target = math.atan2(ah[1] - p[1], ah[0] - p[0]) + math.pi / 2
    if state["angle"] is None:
        state["angle"] = target
    da = (target - state["angle"] + math.pi) % (2 * math.pi) - math.pi
    state["angle"] += da * 0.12
    glide = (0.5 + 0.5 * math.cos(t * 2 * math.pi / 1.6)) ** 6
    flap = math.cos(t * 2 * math.pi * 6)
    open_ = (0.18 + 0.82 * (0.5 + 0.5 * flap)) * (1 - glide) + 0.92 * glide
    size = state["size"]
    img = Image.new("RGBA", (W, H), (90, 180, 255, 0))
    # 光晕
    gl = Image.new("RGBA", (W // 4, H // 4), (90, 180, 255, 0))
    gr = size * 0.16
    ImageDraw.Draw(gl).ellipse([p[0] / 4 - gr, p[1] / 4 - gr, p[0] / 4 + gr, p[1] / 4 + gr], fill=(90, 180, 255, int(110 * (0.8 + 0.2 * flap))))
    from PIL import ImageFilter
    gl = gl.filter(ImageFilter.GaussianBlur(size * 0.07)).resize((W, H), Image.BILINEAR)
    inout = min(t / 0.4, 1) * min((L - t) / 0.5, 1)
    gl.putalpha(Image.fromarray((np.asarray(gl)[..., 3] * inout).astype(np.uint8)))
    img = Image.alpha_composite(img, gl)
    # 鳞粉
    if "last" not in state or t - state["last"] > 0.03:
        state["last"] = t
        rng = np.random.default_rng(int(t * 1000))
        state["parts"].append([p[0] + rng.uniform(-12, 12), p[1] + rng.uniform(-12, 12), t, rng.uniform(-8, 8), rng.uniform(14, 34), rng.uniform(3, 6)])
    dd = ImageDraw.Draw(img)
    for q in state["parts"]:
        age = t - q[2]
        if age > 0.9:
            continue
        k = age / 0.9
        x = q[0] + q[3] * ease_out_cubic(k)
        y = q[1] + q[4] * ease_out_cubic(k)
        a = 0.8 * (1 - k) * (0.6 + 0.4 * abs(math.cos(k * 9)))
        r = q[5]
        dd.ellipse([x - r, y - r, x + r, y + r], fill=(150, 220, 255, int(255 * a * inout)))
    # 蝴蝶本体：两翅绕身体中轴横向缩放
    canvas = Image.new("RGBA", (size * 2, size * 2), (0, 0, 0, 0))
    for k in ("wing_l", "wing_r"):
        wsp = state["sp"][k]
        nw = max(1, int(size * 2 * open_))
        sq = wsp.resize((nw, size * 2), Image.BILINEAR)
        layer = Image.new("RGBA", (size * 2, size * 2), (0, 0, 0, 0))
        layer.alpha_composite(sq, (size - nw // 2, 0))
        canvas = Image.alpha_composite(canvas, layer)
    canvas = Image.alpha_composite(canvas, state["sp"]["body"])
    bob = (1 - glide) * size * 0.06 * math.sin(t * 2 * math.pi * 6 + 1.2)
    rot = -math.degrees(state["angle"] + 0.08 * math.sin(t * 2.3))
    bf = canvas.rotate(rot, resample=Image.BICUBIC, expand=True)
    bf.putalpha(Image.fromarray((np.asarray(bf)[..., 3] * inout).astype(np.uint8)))
    img.alpha_composite(bf, (int(p[0] - bf.width / 2), int(p[1] + bob - bf.height / 2)))
    return np.asarray(img)


# ───────────────────────── 输出 ─────────────────────────

TEMPLATES = {
    # 名字: (时长, 渲染函数, 音效 [(文件, 时刻, 音量)], 预览切镜头时刻)
    "T1_allout_ripple": (1.8, t1_allout, [("Btl_Allout_Wipe", 0.0, 0.5), ("swish_large_1", 0.36, 0.6), ("Btl_Allout_Wipe", 1.0, 0.55), ("swish_heavy_2", 1.08, 0.7)], 1.0),
    "T2_stripe_wipe": (1.2, t2_stripe, [("swish_mid_2", 0.02, 0.8), ("swish_large_1", 0.50, 0.7)], 0.55),
    "T3_lockon": (4.0, lambda t, s: t3_lockon(t, s, True), [("lock_tick", 0.05, 0.9), ("select_hit", 0.25, 0.6)], None),
    "T3_lockon_notext": (4.0, lambda t, s: t3_lockon(t, s, False), [("lock_tick", 0.05, 0.9), ("select_hit", 0.25, 0.6)], None),
    "T4_cutin_band": (2.4, lambda t, s: t4_cutin(t, s, None), [("flash_cutin", 0.0, 0.9)], None),
    "T4_cutin_band_text": (2.4, lambda t, s: t4_cutin(t, s, "ONE MORE!"), [("flash_cutin", 0.0, 0.9)], None),
    "T5_critical": (1.4, t5_critical, [("Btl_Critical", 0.0, 0.9)], None),
    "T6_butterfly": (4.0, t6_butterfly, [], None),
}


def audio(sfx, dur, path):
    sr = 48000
    mix = np.zeros((int(sr * dur) + sr, 2), np.float32)
    for name, at, vol in sfx:
        f = SFX / f"{name}.ogg"
        if not f.exists():
            continue
        raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(f), "-f", "f32le", "-ac", "2", "-ar", str(sr), "-"], capture_output=True, check=True).stdout
        x = np.frombuffer(raw, np.float32).reshape(-1, 2) * vol
        i = int(at * sr)
        n = min(len(x), len(mix) - i)
        mix[i:i + n] += x[:n]
    mix = mix[: int(sr * dur)]
    peak = np.abs(mix).max()
    if peak > 0.95:
        mix *= 0.95 / peak
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(sr), "-ac", "2", "-i", "-", "-c:a", "pcm_s16le", str(path)], input=mix.tobytes(), check=True)


def demo_bg(t, cut):
    """预览用的两段示意画面：镜头 A（暖色）→ 镜头 B（冷色）。"""
    b = cut is not None and t >= cut
    g = np.linspace(0, 1, W, dtype=np.float32)[None, :, None]
    c0 = np.array((70, 52, 40) if not b else (30, 52, 70), np.float32)
    c1 = np.array((160, 110, 70) if not b else (70, 130, 160), np.float32)
    img = (c0 * (1 - g) + c1 * g) * np.ones((H, 1, 1), np.float32)
    im = Image.fromarray(img.astype(np.uint8))
    d = ImageDraw.Draw(im)
    f = ImageFont.truetype(str(FONT), 160)
    d.text((120, 760), "SHOT B" if b else "SHOT A", font=f, fill=(255, 255, 255))
    return np.asarray(im).astype(np.float32)


def render(name, out):
    dur, fn, sfx, cut = TEMPLATES[name]
    out.mkdir(parents=True, exist_ok=True)
    wav = out / f"{name}.wav"
    audio(sfx, dur, wav)
    mov = out / f"{name}.mov"
    prev = out / f"{name}_preview.mp4"
    enc = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgba", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
                            "-i", str(wav), "-c:v", "prores_ks", "-profile:v", "4444", "-pix_fmt", "yuva444p10le", "-alpha_bits", "16",
                            "-vendor", "apl0", "-c:a", "pcm_s16le", "-shortest", str(mov)], stdin=subprocess.PIPE)
    pv = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W // 2}x{H // 2}", "-r", str(FPS), "-i", "-",
                           "-i", str(wav), "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", "-c:a", "aac", "-shortest", str(prev)], stdin=subprocess.PIPE)
    state = {}
    frames = int(round(dur * FPS))
    for fi in range(frames):
        t = fi / FPS
        r = fn(t, state)
        rgba = r.rgba8() if isinstance(r, Canvas) else np.array(r)
        rgba[rgba[..., 3] == 0] = 0                 # 全透明处颜色清零（ProRes 对纯黑透明区几乎不占体积）
        enc.stdin.write(rgba.tobytes())
        bg = demo_bg(t, cut)
        a = rgba[..., 3:4].astype(np.float32) / 255
        comp = (rgba[..., :3].astype(np.float32) * a + bg * (1 - a)).astype(np.uint8)
        pv.stdin.write(np.asarray(Image.fromarray(comp).resize((W // 2, H // 2), Image.BILINEAR)).tobytes())
    for p in (enc, pv):
        p.stdin.close()
        p.wait()
    print(name, f"{frames} frames", f"mov {mov.stat().st_size / 1e6:.1f}MB", f"preview {prev.stat().st_size / 1e6:.1f}MB")


if __name__ == "__main__":
    out = Path(sys.argv[1])
    names = sys.argv[2:] or list(TEMPLATES)
    for n in names:
        render(n, out)
