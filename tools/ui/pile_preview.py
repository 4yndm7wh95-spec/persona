"""牌堆界面（P3R 菜单风格）静态预览：打开动画关键帧、网格悬停、滚动后、弃牌堆。布局与 mod/src/Vfx/PileFx.cs 一致。
- 网格：按类型分段（抽牌堆 类型 → 费用 → 名字，不透露抽牌顺序；弃牌堆最新在前），每段一块斜切标题牌，段内逐张平铺卡面，
  整片沿屏幕向右下微斜；卡牌逐张弹入；悬停放大 + 白框 + 名牌，其余压暗。
- 右侧：白色大圆盘 + 立体大字 + 结城理立绘（art/p3pile/pile_base.png；焦散光斑、轮廓光、塔罗牌反光、光粒子，游戏里另有眨眼 / 呼吸）。
卡牌用用户截图里裁出来的 4 种牌面重复充当示例。
用法：python3 tools/ui/pile_preview.py <截图路径> <输出目录>"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
W, H = 1920, 1080
LAT = str(ROOT / "mod/pck_stage/p3makoto/fonts/BarlowCondensed-900Italic.ttf")
CJK = "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc"
NAVY = (10, 22, 66)
CYAN = (96, 214, 244)
RED = (226, 32, 58)

CARD_SCALE = 0.64
SRC_DRAW = np.array([110.0, 1000.0])    # 抽牌堆按钮
SRC_DISCARD = np.array([1810.0, 1000.0])  # 弃牌堆按钮
POR_SCALE, POR_X, POR_Y = 0.74, 1150, -20


def lat(s):
    return ImageFont.truetype(LAT, s)


def cjk(s):
    return ImageFont.truetype(CJK, s)


def text_img(s, font, fill, stroke=0, stroke_fill=None, shear=0.0, bold=0):
    l, t, r, b = font.getbbox(s, stroke_width=stroke + bold)
    im = Image.new("RGBA", (r - l + 6, b - t + 6))
    d = ImageDraw.Draw(im)
    if stroke:
        d.text((3 - l, 3 - t), s, font=font, fill=stroke_fill, stroke_width=stroke + bold, stroke_fill=stroke_fill)
    d.text((3 - l, 3 - t), s, font=font, fill=fill, stroke_width=bold, stroke_fill=fill)
    if shear:
        h = im.height
        extra = int(shear * h)
        im = im.transform((im.width + extra, h), Image.AFFINE, (1, shear, -extra, 0, 1, 0), resample=Image.BICUBIC)
    return im


def put(base, layer, xy, anchor="lt"):
    x, y = xy
    if anchor[0] == "r":
        x -= layer.width
    elif anchor[0] == "m":
        x -= layer.width / 2
    if anchor[1] == "m":
        y -= layer.height / 2
    elif anchor[1] == "b":
        y -= layer.height
    base.alpha_composite(layer, (int(round(x)), int(round(y))))


def ease_out_back(t, k=1.6):
    t = min(max(t, 0.0), 1.0) - 1
    return 1 + (k + 1) * t ** 3 + k * t ** 2


def spring(t, zeta=0.55, w=13.0):
    """欠阻尼弹簧：0 → 1，略微过冲后回弹落定。"""
    if t <= 0:
        return 0.0
    wd = w * math.sqrt(1 - zeta * zeta)
    return 1 - math.exp(-zeta * w * t) * (math.cos(wd * t) + zeta * w / wd * math.sin(wd * t))


# ---------------- 背景 ----------------

def caustic(xs, ys, t=0.0):
    a = np.sin(xs * 40 + np.sin(ys * 17 + xs * 6 + t) * 2.2)
    b = np.sin(xs * 27 - ys * 44 + np.sin(xs * 13 - t) * 1.7)
    return np.clip(1 - np.abs(a + b) * 2.2, 0, 1) ** 3


def background(t):
    y = np.linspace(0, 1, H)[:, None]
    x = np.linspace(0, 1, W)[None, :]
    top = np.array([38, 118, 226]) / 255
    bot = np.array([5, 16, 78]) / 255
    k = np.clip(y * 1.05 + (1 - x) * 0.1, 0, 1)
    img = top * (1 - k[..., None]) + bot * k[..., None]
    img = np.broadcast_to(img, (H, W, 3)).copy()
    net = caustic(x * 1.78, y, t * 1.3)
    img += (net * np.clip(1 - y * 1.9, 0, 1) * 0.42)[..., None] * np.array([0.5, 0.8, 1.0])
    rays = np.clip(np.sin(x * 14 + y * 3 + t * 0.4) * 0.5 + 0.5, 0, 1) ** 6 * np.clip(1 - y * 1.4, 0, 1) * 0.14
    img += rays[..., None]
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).convert("RGBA")


def bubbles(im, rng, t, n=30):
    d = ImageDraw.Draw(im)
    for _ in range(n):
        cx, cy, r, sp = rng.uniform(0, 1150), rng.uniform(80, H + 200), rng.uniform(3, 13), rng.uniform(60, 160)
        cy = (cy - sp * t) % (H + 200) - 50
        a = int(rng.uniform(60, 140))
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(170, 230, 255, a), width=2)
        d.arc([cx - r * 0.6, cy - r * 0.6, cx + r * 0.2, cy + r * 0.2], 190, 260, fill=(230, 250, 255, a), width=2)


def disc(im, word, slide):
    """右侧白色大圆盘 + 灰色立体大字（被圆盘裁掉一截）。slide = 0..1 从右侧滑入。"""
    off = int((1 - slide) * 700)
    cx, cy, r = 1760 + off, 500, 640
    yy, xx = np.mgrid[0:H, 0:W]
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    k = np.clip(dist / r, 0, 1)
    col = np.array([248, 251, 255]) * (1 - k[..., None] ** 3) + np.array([206, 228, 246]) * (k[..., None] ** 3)
    stripe = ((xx + yy) // 6) % 7 == 0
    col = np.where(stripe[..., None], col - 9, col)
    a = (np.clip((r - dist) * 1.5, 0, 1) * 255).astype(np.uint8)
    layer = Image.fromarray(np.dstack([col.astype(np.uint8), a]), "RGBA")
    mask = layer.split()[3]
    f = lat(250)
    shadow = text_img(word, f, (118, 128, 142, 255))
    body = text_img(word, f, (214, 220, 230, 255), stroke=3, stroke_fill=(150, 160, 175, 255))
    blk = Image.new("RGBA", (body.width + 30, body.height + 30))
    blk.alpha_composite(shadow, (22, 18))
    blk.alpha_composite(body, (0, 0))
    blk = blk.rotate(76, expand=True, resample=Image.BICUBIC)
    wl = Image.new("RGBA", (W, H))
    wl.alpha_composite(blk, (1340 - blk.width // 2 + off, H - blk.height + 30))
    wl.putalpha(ImageChops.multiply(wl.split()[3], mask))
    layer.alpha_composite(wl)
    d = ImageDraw.Draw(layer)
    for rr, w, c in ((r - 26, 2, (190, 206, 224, 255)), (r - 70, 1, (205, 218, 232, 255)), (r - 140, 1, (214, 224, 236, 255))):
        d.arc([cx - rr, cy - rr, cx + rr, cy + rr], 95, 265, fill=c, width=w)
    im.alpha_composite(layer)
    d = ImageDraw.Draw(im)
    for rr, a0, a1, c, w in ((r + 18, 120, 230, (150, 230, 255, 200), 3), (r + 34, 150, 185, (255, 255, 255, 220), 6), (r + 46, 196, 214, (150, 230, 255, 160), 2)):
        d.arc([cx - rr, cy - rr, cx + rr, cy + rr], a0, a1, fill=c, width=w)
    for i in range(14):
        ang = np.deg2rad(128 + i * 7)
        px, py = cx + np.cos(ang) * (r + 18), cy + np.sin(ang) * (r + 18)
        d.ellipse([px - 3, py - 3, px + 3, py + 3], fill=(220, 245, 255, 220))


def portrait(im, rng, slide, t, dim=0.0):
    meta = json.loads((ROOT / "art/p3pile/meta.json").read_text())
    por = Image.open(ROOT / "art/p3pile/pile_base.png").convert("RGBA")
    por = por.resize((int(por.width * POR_SCALE), int(por.height * POR_SCALE)), Image.LANCZOS)
    ox = POR_X + int((1 - ease_out_back(slide, 1.2)) * 600)
    oy = POR_Y + int(math.sin(t * 2.2) * 4)   # 呼吸
    a = por.split()[3]
    hh, ww = por.height, por.width
    ys, xs = np.mgrid[0:hh, 0:ww]
    net = caustic((xs + ox) / W * 1.78, (ys + oy) / H, 0.7 + t * 1.3)
    rgb = np.asarray(por).astype(np.float32)
    light = net * 0.20 * np.clip(1 - (ys / hh) * 1.2, 0, 1)
    rgb[..., :3] += light[..., None] * np.array([150, 220, 255])
    if dim:
        rgb[..., :3] *= 1 - dim * 0.45
    por = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGBA")
    rim = ImageChops.subtract(a, ImageChops.offset(a, 9, 0)).filter(ImageFilter.GaussianBlur(1.5))
    rim_l = Image.new("RGBA", por.size, (190, 245, 255, 0))
    rim_l.putalpha(rim)
    im.alpha_composite(rim_l.filter(ImageFilter.GaussianBlur(8)), (ox, oy))
    im.alpha_composite(por, (ox, oy))
    im.alpha_composite(rim_l, (ox, oy))
    if slide < 1:
        return
    cc = meta["card_corners"]
    card = [(ox + x * POR_SCALE, oy + y * POR_SCALE) for x, y in (cc["top_left"], cc["top_right"], cc["bottom_right"], cc["bottom_left"])]
    m = Image.new("L", (W, H))
    ImageDraw.Draw(m).polygon(card, fill=255)
    x0, y0 = card[0]
    sweep = Image.new("L", (W, H))
    ImageDraw.Draw(sweep).polygon([(x0 + 30, y0 - 30), (x0 + 62, y0 - 30), (x0 + 22, y0 + 200), (x0 - 10, y0 + 200)], fill=170)
    ImageDraw.Draw(sweep).polygon([(x0 + 74, y0 - 30), (x0 + 84, y0 - 30), (x0 + 44, y0 + 200), (x0 + 34, y0 + 200)], fill=120)
    s = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    s.putalpha(ImageChops.multiply(sweep.filter(ImageFilter.GaussianBlur(3)), m))
    im.alpha_composite(s)
    d = ImageDraw.Draw(im)
    ccx = sum(p[0] for p in card) / 4
    ccy = sum(p[1] for p in card) / 4
    for _ in range(12):
        px, py = rng.normal(ccx, 90), rng.normal(ccy, 120)
        rr = rng.uniform(1.2, 3.0)
        d.ellipse([px - rr * 3, py - rr * 3, px + rr * 3, py + rr * 3], fill=(140, 220, 255, 40))
        d.ellipse([px - rr, py - rr, px + rr, py + rr], fill=(235, 250, 255, 230))
    for (px, py), L in ((card[0], 20), (card[2], 14)):
        d.line([px - L, py, px + L, py], fill=(255, 255, 255, 230), width=2)
        d.line([px, py - L, px, py + L], fill=(255, 255, 255, 230), width=2)


# ---------------- 卡牌 ----------------

def count_badge(n):
    """×N 角标：P3R 式黑底白字斜切小牌，白边 + 红色尾边。"""
    t = text_img(f"×{n}", lat(54), (255, 255, 255, 255))
    w = t.width + 34
    im = Image.new("RGBA", (w + 8, 66))
    d = ImageDraw.Draw(im)
    d.polygon([(12, 0), (w + 8, 0), (w - 4, 66), (0, 66)], fill=(255, 255, 255, 255))
    d.polygon([(16, 4), (w + 2, 4), (w - 8, 62), (5, 62)], fill=(8, 10, 22, 255))
    d.polygon([(w - 6, 4), (w + 2, 4), (w - 8, 62), (w - 16, 62)], fill=RED + (255,))
    put(im, t, (w / 2 - 2, 33), "mm")
    return im


def name_plate(name, size=40):
    """名牌：白边黑底斜切条 + 红色尾边，牌名用粗斜体。"""
    tag = text_img(name, cjk(size), (255, 255, 255, 255), bold=1)
    h = int(size * 1.55)
    w = tag.width + 70
    plate = Image.new("RGBA", (w + 30, h))
    pd = ImageDraw.Draw(plate)
    pd.polygon([(14, 0), (w + 30, 0), (w + 18, h), (0, h)], fill=(250, 252, 255, 255))
    pd.polygon([(18, 4), (w + 6, 4), (w - 6, h - 4), (6, h - 4)], fill=(8, 10, 22, 255))
    pd.polygon([(w + 10, 4), (w + 26, 4), (w + 14, h - 4), (w - 2, h - 4)], fill=RED + (255,))
    put(plate, tag, (34, h / 2), "lm")
    return plate


def card_sprite(face, count, scale, frame=0.0, dim=0.0):
    """一张（一摞）牌：同名多张时身后露出 1~2 张深蓝牌边；frame = 白色描边强度；dim = 压暗。"""
    c = face.resize((max(1, int(face.width * scale)), max(1, int(face.height * scale))), Image.LANCZOS)
    pad = int(40 * scale / CARD_SCALE)
    lay = Image.new("RGBA", (c.width + pad * 2, c.height + pad * 2))
    d = ImageDraw.Draw(lay)
    sh = Image.new("RGBA", lay.size)
    ImageDraw.Draw(sh).rounded_rectangle([pad + 8, pad + 12, pad + 8 + c.width, pad + 12 + c.height], 10, fill=(2, 8, 40, 150))
    lay.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)))
    for g in range(min(count, 3) - 1, 0, -1):
        o = int(g * 9 * scale / CARD_SCALE)
        d.rounded_rectangle([pad + o, pad - o, pad + o + c.width, pad - o + c.height], 10, fill=(18, 44, 120, 255), outline=(190, 235, 255, 255), width=2)
    if dim > 0:
        dm = Image.new("RGBA", c.size, (6, 16, 60, int(150 * dim)))
        c = Image.alpha_composite(c, Image.composite(dm, Image.new("RGBA", c.size), c.split()[3]))
    lay.alpha_composite(c, (pad, pad))
    if frame > 0:
        g = Image.new("RGBA", lay.size)
        ImageDraw.Draw(g).rounded_rectangle([pad - 8, pad - 8, pad + c.width + 8, pad + c.height + 8], 14, outline=(170, 235, 255, int(255 * frame)), width=10)
        lay.alpha_composite(g.filter(ImageFilter.GaussianBlur(10)))
        ImageDraw.Draw(lay).rounded_rectangle([pad - 6, pad - 6, pad + c.width + 6, pad + c.height + 6], 12, outline=(255, 255, 255, int(255 * frame)), width=4)
    if count > 1:
        b = count_badge(count)
        k = scale / CARD_SCALE * 0.62
        b = b.resize((int(b.width * k), int(b.height * k)), Image.LANCZOS)
        put(lay, b, (pad + c.width + 6, pad + c.height - 6), "rb")
    return lay


COLS, CARD_W, CELL_W, CELL_H = 5, 160, 178, 252          # 与 mod/src/Vfx/PileFx.cs 一致
GRID_X, GRID_TOP, GRID_BOTTOM, HEADER_H, SLANT = 190, 262, 1010, 58, 0.05


def grid(im, faces, groups, mode, hover=None, scroll=0.0, t=2.0, focus=None, fp=0.0):
    """网格（与游戏里 PileFx 同一套布局）：按类型分段，段标题斜切牌，段内逐张平铺；整片沿屏幕向右下微斜；悬停放大 + 白框 + 名牌。"""
    if mode == "draw":
        sections = [("ATTACK", "攻击", groups[:5]), ("SKILL", "技能", groups[5:])]
    else:
        sections = [("RECENT", "最新在前", groups)]
    sc = CARD_W / 260
    lay = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(lay)

    def scr(x, y):
        sy = GRID_TOP + y - scroll
        return GRID_X + x + (sy - GRID_TOP) * SLANT, sy

    def fade(top, bottom):
        return max(0.0, min(1.0, (top - (GRID_TOP - 22)) / 36)) * max(0.0, min(1.0, (GRID_BOTTOM - bottom) / 50))
    y = 0.0
    k = 0
    hov = None
    foc = None
    cards = []
    for en, zh, gs in sections:
        flat = [n for n, c in gs for _ in range(c)]
        hx, hy = scr(0, y + HEADER_H / 2)
        hx -= 14
        if fade(hy - 20, hy + 20) > 0:
            d.polygon([(hx + 12, hy - 22), (hx + 330, hy - 22), (hx + 320, hy + 22), (hx, hy + 22)], fill=(5, 8, 23, 240))
            d.polygon([(hx + 12, hy - 22), (hx + 24, hy - 22), (hx + 14, hy + 22), (hx + 2, hy + 22)], fill=CYAN + (255,))
            put(lay, text_img(en, lat(36), (255, 255, 255, 255)), (hx + 34, hy), "lm")
            put(lay, text_img(zh, cjk(20), (150, 220, 255, 255), bold=1), (hx + 44 + lat(36).getlength(en), hy + 2), "lm")
            put(lay, text_img(f"×{len(flat)}", lat(38), CYAN + (255,)), (hx + 310, hy), "rm")
            d.line([(hx + 338, hy), (hx + COLS * CELL_W - 6, hy)], fill=(150, 220, 255, 140), width=2)
            d.polygon([(hx + COLS * CELL_W - 6, hy - 6), (hx + COLS * CELL_W + 6, hy), (hx + COLS * CELL_W - 6, hy + 6)], fill=(150, 220, 255, 200))
        y += HEADER_H
        for i, n in enumerate(flat):
            cx, cy = scr((i % COLS) * CELL_W + CELL_W / 2, y + (i // COLS) * CELL_H + CELL_H / 2)
            half = 358 * sc / 2
            f = fade(cy - half, cy + half)
            born = min(1.0, max(0.0, (t - 0.22 - 0.022 * k) / 0.38))
            if k == hover:
                hov = (n, cx, cy - 14)
            elif k == focus:
                foc = (n, cx, cy)
            elif f > 0.01 and born > 0:
                pop = ease_out_back(born, 1.7)
                r = 0.0
                if focus is not None:
                    r = min(1.0, max(0.0, fp * 1.6 - min(0.5, abs(k - focus) * 60 / 1600)))
                    r = 1 - (1 - r) ** 3
                spr = card_sprite(faces[n], 1, sc * (0.85 + 0.15 * pop) * (1 - 0.16 * r), dim=0.5 if hover is not None else 0.0).rotate(2 + 5 * r, expand=True, resample=Image.BICUBIC)
                a = f * min(1, born * 3) * (1 - 0.9 * r)
                spr.putalpha(spr.split()[3].point(lambda v: int(v * a)))
                cards.append((spr, (cx - 40 * (1 - pop) - 24 * r, cy + 46 * (1 - pop) + 80 * r)))
            k += 1
        y += math.ceil(len(flat) / COLS) * CELL_H + 16
    for spr, p in cards:
        put(lay, spr, p, "mm")
    if foc:
        im.alpha_composite(lay)
        lay = Image.new("RGBA", (W, H))
        focus_layer(lay, fp)
        n, cx, cy = foc
        e = fp * fp * (3 - 2 * fp)
        px = cx + (700 - cx) * e
        py = cy + (560 - cy) * e - 170 * math.sin(math.pi * e)
        scale = sc + (450 / 260 - sc) * e
        flip = max(0.03, abs(math.cos(math.pi * e)))
        spr = card_sprite(faces[n], 1, scale, frame=e)
        spr = spr.resize((max(1, int(spr.width * flip)), spr.height), Image.LANCZOS).rotate(12 * math.sin(math.pi * e), expand=True, resample=Image.BICUBIC)
        put(lay, spr, (px, py), "mm")
        if e > 0.45:
            info(lay, n, min(1.0, (fp - 0.45) / 0.4))
    if hov:
        n, cx, cy = hov
        spr = card_sprite(faces[n], 1, sc * 1.26, frame=1.0)
        put(lay, spr, (cx, cy), "mm")
        put(lay, name_plate(n, 32), (cx - 20, cy + 358 * sc * 1.26 / 2 + 8), "lt")
    im.alpha_composite(lay)


def focus_layer(lay, fp):
    """特写背景：压暗、穿过卡牌的白色斜带（红 / 青细条）、慢转光芒和细圆。与 PileFx.FocusLayer 一致。"""
    e = fp * fp * (3 - 2 * fp)
    lay.alpha_composite(Image.new("RGBA", (W, H), (3, 8, 33, int(255 * 0.62 * e))))
    c = np.array([700.0, 560.0])
    ang = math.radians(-16)
    ax = np.array([math.cos(ang), math.sin(ang)])
    nx = np.array([-ax[1], ax[0]])
    slide = 1 - (1 - 2 ** (-10 * min(1.0, max(0.0, fp * 1.4 - 0.1))))
    o = c + np.array([-1.0, 0.29]) / np.hypot(1, 0.29) * 2400 * slide
    band = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(band)

    def strip(a, b, col):
        pts = [o - ax * 2600 + nx * a, o + ax * 2600 + nx * a, o + ax * 2600 + nx * b, o - ax * 2600 + nx * b]
        d.polygon([tuple(p) for p in pts], fill=col)
    strip(-120, 120, (245, 250, 255, int(230 * e)))
    strip(130, 142, RED + (int(255 * e),))
    strip(-136, -130, CYAN + (int(255 * e),))
    rays = Image.new("RGBA", (W, H))
    rd = ImageDraw.Draw(rays)
    for i in range(16):
        a = math.tau * i / 16 + 0.3
        w = math.radians(3.2 if i % 2 == 0 else 1.6)
        r = 900 * (0.6 + 0.4 * e)
        rd.polygon([tuple(c), tuple(c + r * np.array([math.cos(a - w), math.sin(a - w)])), tuple(c + r * np.array([math.cos(a + w), math.sin(a + w)]))], fill=(115, 217, 255, int(40 * e)))
    for rr, wd, al in ((400, 2, 180), (430, 1, 115)):
        rr *= 0.6 + 0.4 * e
        rd.ellipse([c[0] - rr, c[1] - rr, c[0] + rr, c[1] + rr], outline=(205, 242, 255, int(al * e)), width=wd)
    lay.alpha_composite(band)
    lay.alpha_composite(rays)


def info(lay, name, a):
    """特写左侧说明：牌名（正体粗黑）、红线、类型 · 费用、牌堆中张数、返回提示。"""
    x = -90 * (1 - (1 - (1 - a) ** 3))
    tmp = Image.new("RGBA", (W, H))
    put(tmp, text_img(name, cjk(54), (255, 255, 255, 255), stroke=6, stroke_fill=NAVY + (255,), bold=1), (60 + x, 300))
    ImageDraw.Draw(tmp).polygon([(60 + x, 382), (400 + x, 382), (394 + x, 390), (54 + x, 390)], fill=RED + (255,))
    put(tmp, text_img("SKILL  ·  COST 1", lat(40), CYAN + (255,), stroke=4, stroke_fill=NAVY + (255,)), (60 + x, 398))
    put(tmp, text_img("牌堆中 ×3", cjk(30), (255, 255, 255, 255), stroke=4, stroke_fill=NAVY + (255,), bold=1), (60 + x, 452))
    put(tmp, text_img("点击任意处返回", cjk(20), (190, 225, 255, 205), stroke=3, stroke_fill=NAVY + (255,)), (62 + x, 512))
    tmp.putalpha(tmp.split()[3].point(lambda v: int(v * a)))
    lay.alpha_composite(tmp)


def chrome(im, shot):
    """游戏自己的顶栏和返回按钮（从截图裁），用来检查遮挡。"""
    sh = Image.open(shot).convert("RGBA").resize((W, H))
    im.alpha_composite(sh.crop((0, 0, W, 86)), (0, 0))
    im.alpha_composite(sh.crop((0, 700, 160, 800)), (0, 700))


def header(im, mode, total):
    d = ImageDraw.Draw(im)
    title = "DRAW PILE" if mode == "draw" else "DISCARD PILE"
    t = text_img(title, lat(84), (255, 255, 255, 255), stroke=5, stroke_fill=NAVY + (255,))
    put(im, t, (58, 96))
    put(im, text_img(str(total), lat(100), CYAN + (255,), stroke=5, stroke_fill=NAVY + (255,)), (58 + 30 + t.width, 82))
    d.polygon([(56, 200), (560, 200), (552, 228), (48, 228)], fill=(8, 18, 60, 240))
    d.polygon([(56, 200), (70, 200), (62, 228), (48, 228)], fill=CYAN + (255,))
    note = "抽牌顺序随机 · 按类型 → 费用排列" if mode == "draw" else "按进入牌堆的先后排列 · 最新在前"
    put(im, text_img(note, cjk(19), (205, 237, 255, 255), bold=1), (84, 214), "lm")


def deck(shot, mode):
    sh = Image.open(shot).convert("RGBA")
    faces = {
        "防御": sh.crop((255, 160, 515, 518)),
        "陨星": sh.crop((1133, 160, 1390, 518)),
        "打击": sh.crop((1425, 160, 1685, 518)),
        "崇拜": sh.crop((548, 550, 808, 912)),
    }
    if mode == "draw":
        # 示例：12 种牌（同名合并），按 类型 → 费用 → 名字（前 5 种攻击、后 7 种技能）
        groups = [("陨星", 1), ("打击", 4), ("陨星", 1), ("打击", 1), ("陨星", 2),
                  ("崇拜", 1), ("防御", 4), ("崇拜", 2), ("防御", 1), ("崇拜", 1), ("防御", 1), ("崇拜", 1)]
    else:
        groups = [("打击", 1), ("防御", 1), ("陨星", 1), ("防御", 1), ("打击", 1), ("崇拜", 1), ("防御", 1)]
    return faces, groups


def frame(shot, mode, t, hover=None, scroll=0.0, focus=None, fp=0.0):
    rng = np.random.default_rng(3)
    im = background(t)
    src = SRC_DRAW if mode == "draw" else SRC_DISCARD
    rad = 2300 * ease_out_back(t / 0.55, 0.6)
    bubbles(im, rng, t)
    disc(im, "DRAW" if mode == "draw" else "DISCARD", ease_out_back((t - 0.1) / 0.5, 1.0))
    portrait(im, np.random.default_rng(5), min(1.0, max(0.0, (t - 0.18) / 0.5)), t)
    faces, groups = deck(shot, mode)
    header(im, mode, sum(c for _, c in groups))
    grid(im, faces, groups, mode, hover, scroll, t, focus, fp)
    chrome(im, shot)
    if rad < 2300:
        old = Image.open(shot).convert("RGBA").resize((W, H))
        old = Image.alpha_composite(old, Image.new("RGBA", (W, H), (0, 0, 20, 120)))
        m = Image.new("L", (W, H), 255)
        box = [src[0] - rad, src[1] - rad, src[0] + rad, src[1] + rad]
        ImageDraw.Draw(m).ellipse(box, fill=0)
        old.putalpha(m)
        im.alpha_composite(old)
        ring = Image.new("RGBA", (W, H))
        ImageDraw.Draw(ring).ellipse(box, outline=(170, 240, 255, 255), width=10)
        im.alpha_composite(ring.filter(ImageFilter.GaussianBlur(4)))
        ImageDraw.Draw(im).ellipse(box, outline=(255, 255, 255, 255), width=3)
    return im


def main(shot, outdir):
    out = Path(outdir)
    out.mkdir(parents=True, exist_ok=True)
    times = [0.12, 0.25, 0.38, 0.5, 0.7, 1.2]
    frames = [frame(shot, "draw", t).convert("RGB").resize((640, 360), Image.LANCZOS) for t in times]
    sheet = Image.new("RGB", (640 * 3 + 40, 360 * 2 + 30), (8, 10, 20))
    d = ImageDraw.Draw(sheet)
    for k, (t, f) in enumerate(zip(times, frames)):
        x, y = 10 + (k % 3) * 650, 10 + (k // 3) * 370
        sheet.paste(f, (x, y))
        d.text((x + 8, y + 300), f"{t:.2f}s", font=lat(30), fill=(255, 230, 60))
    sheet.save(out / "pile_open_anim.png")
    frame(shot, "draw", 2.0, hover=3).convert("RGB").save(out / "pile_grid_hover.png")
    frame(shot, "draw", 2.0, scroll=300).convert("RGB").save(out / "pile_grid_scrolled.png")
    frame(shot, "discard", 2.0, hover=1).convert("RGB").save(out / "pile_discard.png")
    fps = [0.15, 0.3, 0.45, 0.6, 0.8, 1.0]
    frames = [frame(shot, "draw", 2.0, focus=10, fp=f).convert("RGB").resize((640, 360), Image.LANCZOS) for f in fps]
    sheet = Image.new("RGB", (640 * 3 + 40, 360 * 2 + 30), (8, 10, 20))
    d = ImageDraw.Draw(sheet)
    for k, (f, fr) in enumerate(zip(fps, frames)):
        x, y = 10 + (k % 3) * 650, 10 + (k // 3) * 370
        sheet.paste(fr, (x, y))
        d.text((x + 8, y + 300), f"{f * 0.5:.2f}s", font=lat(30), fill=(255, 230, 60))
    sheet.save(out / "pile_focus_anim.png")
    frame(shot, "draw", 2.0, focus=10, fp=1.0).convert("RGB").save(out / "pile_focus.png")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
