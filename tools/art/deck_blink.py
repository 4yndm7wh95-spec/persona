"""牌组立绘（art/p3pile_deck/deck_cand_c.png）的眨眼帧：Codex 这次没画，这里按平涂画风直接画。
闭眼：眼睛（上睫毛粗线 + 眼白 + 虹膜）整块盖成周围的肤色，画一道向下弯的闭合睫毛线 + 外眼角两三根睫毛；
半睁：只盖住上半，粗上睫毛线压到虹膜中间。4 倍超采样再缩回，边缘和原画一样平滑。
输出 deck_base.png（= cand_c）/ deck_eyes_half.png / deck_eyes_closed.png + 眼区放大对比 blink_preview.png。
用法：python3 tools/art/deck_blink.py"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
D = ROOT / "art/p3pile_deck"
SS = 4
BOX = (520, 380, 690, 530)          # 只在这个框里画
SKIN = (248, 250, 255, 255)
LASH = (12, 18, 46, 255)


def bez(p0, p1, p2, n=40):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1])
            for t in (i / n for i in range(n + 1))]


def thick(d, pts, w0, w1, color):
    """沿折线画两头渐细的粗线（中间 w1，两端 w0）。"""
    n = len(pts)
    for i in range(n - 1):
        t = i / (n - 1)
        w = w0 + (w1 - w0) * max(0.0, 1 - abs(t - 0.55) / 0.55) ** 0.6
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        d.line([(x0 * SS, y0 * SS), (x1 * SS, y1 * SS)], fill=color, width=max(1, int(w * SS)))
        r = w * SS / 2
        d.ellipse([x1 * SS - r, y1 * SS - r, x1 * SS + r, y1 * SS + r], fill=color)


def frame(base, mode):
    x0, y0, x1, y1 = BOX
    W, H = (x1 - x0) * SS, (y1 - y0) * SS
    crop = base.crop(BOX).resize((W, H), Image.LANCZOS)
    o = lambda pts: [((x - x0) * SS, (y - y0) * SS) for x, y in pts]
    # 要盖成肤色的区域：闭眼 = 上睫毛粗线上沿到下眼睑睫毛下沿；半睁 = 上睫毛上沿到虹膜中间
    upper = [(553, 452), (563, 444), (585, 435), (612, 427), (640, 415), (657, 409), (662, 420)]
    lower = [(659, 450), (648, 466), (625, 482), (598, 494), (574, 502), (561, 503), (553, 495)]
    mid = [(660, 428), (632, 441), (604, 452), (578, 462), (562, 471), (553, 476)]
    poly = upper + (lower if mode == "closed" else mid)
    mask = Image.new("L", (W, H))
    ImageDraw.Draw(mask).polygon(o(poly), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(SS * 0.8))
    crop.paste(Image.new("RGBA", (W, H), SKIN), (0, 0), mask)
    lay = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(lay)
    rel = lambda pts: [(x - x0, y - y0) for x, y in pts]
    if mode == "closed":
        # 闭合的睫毛：一道向下弯、中间粗两头尖的黑线 + 外眼角三根短睫毛
        thick(d, rel(bez((552, 487), (602, 505), (656, 446), 60)), 1.0, 8.0, LASH)
        for (ax, ay), (bx, by) in (((628, 470), (636, 483)), ((640, 461), (651, 472)), ((649, 452), (662, 458))):
            thick(d, rel([(ax, ay), (bx, by)]), 0.6, 3.0, LASH)
    else:
        # 半睁：粗上睫毛压到虹膜中间
        thick(d, rel(bez((552, 479), (598, 459), (660, 428), 60)), 1.4, 9.0, LASH)
        for (ax, ay), (bx, by) in (((648, 432), (662, 437)), ((638, 438), (649, 447))):
            thick(d, rel([(ax, ay), (bx, by)]), 0.6, 2.6, LASH)
    crop.alpha_composite(lay)
    small = crop.resize((x1 - x0, y1 - y0), Image.LANCZOS)
    # 只替换改过的区域附近（多边形外扩 6px 羽化），其余像素一律保持原样
    keep = Image.new("L", (x1 - x0, y1 - y0))
    ImageDraw.Draw(keep).polygon([(x - x0, y - y0) for x, y in upper + lower], fill=255)
    keep = keep.filter(ImageFilter.MaxFilter(13)).filter(ImageFilter.GaussianBlur(3))
    region = Image.composite(small, base.crop(BOX), keep)
    out = base.copy()
    out.paste(region, BOX[:2])
    return out


def main():
    base = Image.open(D / "deck_cand_c.png").convert("RGBA")
    base.save(D / "deck_base.png")
    half = frame(base, "half")
    closed = frame(base, "closed")
    half.save(D / "deck_eyes_half.png")
    closed.save(D / "deck_eyes_closed.png")
    diff = [int((np.asarray(f) != np.asarray(base)).any(-1).sum()) for f in (half, closed)]
    print("changed pixels", diff)
    sheet = Image.new("RGB", (3 * 510, 450), (20, 30, 80))
    for i, f in enumerate((base, half, closed)):
        bg = Image.new("RGBA", f.size, (20, 30, 80, 255))
        bg.alpha_composite(f)
        sheet.paste(bg.crop(BOX).resize((510, 450), Image.LANCZOS).convert("RGB"), (i * 510, 0))
    sheet.save(D / "blink_preview.png")


if __name__ == "__main__":
    main()
