"""战斗界面左下 / 右下的 P3R 风格图标预览：能量（环形格子 + 大号斜体数字）、辉星（塔罗菱形牌）、抽牌堆 / 弃牌堆（两张塔罗牌背叠放 + 斜切黑底张数牌）。
在实机截图上先把原版图标糊掉再画新的，只用来确认样式。
用法：python3 tools/ui/hud_preview.py <战斗截图> <输出.png>"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
LAT = str(ROOT / "mod/pck_stage/p3makoto/fonts/BarlowCondensed-900Italic.ttf")
NAVY = (10, 22, 70)
INK = (6, 12, 40)
CYAN = (96, 214, 244)
RED = (226, 32, 58)
BACK = Image.open(ROOT / "art/fx/pile_card_back.png").convert("RGBA")


def lat(s):
    return ImageFont.truetype(LAT, s)


def text(im, xy, s, size, fill, stroke=0, stroke_fill=INK, anchor="mm"):
    ImageDraw.Draw(im).text(xy, s, font=lat(size), fill=fill, stroke_width=stroke, stroke_fill=stroke_fill, anchor=anchor)


def ink(im, xy, s, size, fill, stroke=0, left=False):
    """和 HudSkin.PlaceInk 一致：数字笔画中心（不是行高中心）放在 xy；left=True 时 xy.x 是左边缘。数字高 0.7em，基线 = 中心 + 0.35em。"""
    x, y = xy
    ImageDraw.Draw(im).text((x, y + 0.35 * size), s, font=lat(size), fill=fill, stroke_width=stroke, stroke_fill=INK, anchor="ls" if left else "ms")


def erase(im, box):
    """把原版图标所在区域糊掉（实机里是把原版图层设成透明，这里只是近似）。"""
    reg = im.crop(box).filter(ImageFilter.GaussianBlur(28))
    im.paste(reg, box)


def ring_segments(d, c, r0, r1, n, filled, a0=-90, gap=7):
    span = 360 / n
    for i in range(n):
        s = a0 + i * span + gap / 2
        e = a0 + (i + 1) * span - gap / 2
        pts = []
        for k in range(13):
            a = math.radians(s + (e - s) * k / 12)
            pts.append((c[0] + math.cos(a) * r1, c[1] + math.sin(a) * r1))
        for k in range(12, -1, -1):
            a = math.radians(s + (e - s) * k / 12)
            pts.append((c[0] + math.cos(a) * r0, c[1] + math.sin(a) * r0))
        d.polygon(pts, fill=CYAN + (255,) if i < filled else (30, 52, 120, 255))


def tag(im, xy, s, size, h, red=True):
    """P3R 斜切标签：黑底 + 白边 + 红色尾边，白色粗斜体字。"""
    f = lat(size)
    w = f.getlength(s) + h * 0.9
    x, y = xy
    d = ImageDraw.Draw(im)
    lean = h * 0.3
    d.polygon([(x - 2 + lean, y - 2), (x + w + lean + 2, y - 2), (x + w + 2, y + h + 2), (x - 2, y + h + 2)], fill=(255, 255, 255, 255))
    d.polygon([(x + lean, y), (x + w + lean, y), (x + w, y + h), (x, y + h)], fill=INK + (255,))
    if red:
        d.polygon([(x + w - h * 0.28 + lean, y), (x + w + lean, y), (x + w, y + h), (x + w - h * 0.28, y + h)], fill=RED + (255,))
    ink(im, (x + (w - (h * 0.28 if red else 0)) / 2 + h * 0.15, y + h / 2), s, size, (255, 255, 255, 255))


def energy(im, c, cur, mx):
    lay = Image.new("RGBA", im.size)
    d = ImageDraw.Draw(lay)
    r = 60
    sh = Image.new("RGBA", im.size)
    ImageDraw.Draw(sh).ellipse([c[0] - r + 6, c[1] - r + 8, c[0] + r + 6, c[1] + r + 8], fill=(0, 0, 10, 150))
    lay.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)))
    # 外圈慢转的青色细弧 + 刻度点
    d.arc([c[0] - r - 12, c[1] - r - 12, c[0] + r + 12, c[1] + r + 12], 150, 380, fill=CYAN + (220,), width=3)
    for k in range(8):
        a = math.radians(200 + k * 20)
        p = (c[0] + math.cos(a) * (r + 12), c[1] + math.sin(a) * (r + 12))
        d.ellipse([p[0] - 2.5, p[1] - 2.5, p[0] + 2.5, p[1] + 2.5], fill=(220, 245, 255, 255))
    d.ellipse([c[0] - r, c[1] - r, c[0] + r, c[1] + r], fill=(255, 255, 255, 255))
    d.ellipse([c[0] - r + 4, c[1] - r + 4, c[0] + r - 4, c[1] + r - 4], fill=NAVY + (255,))
    ring_segments(d, c, r - 16, r - 7, mx, cur)
    # 中心：斜切深色底板 + 大号数字
    d.polygon([(c[0] - 34, c[1] - 22), (c[0] + 40, c[1] - 22), (c[0] + 32, c[1] + 24), (c[0] - 42, c[1] + 24)], fill=INK + (235,))
    im.alpha_composite(lay)
    big = 62
    dw = lat(big).getlength(str(cur))
    bx = c[0] - 1 - (lat(26).getlength(f"/{mx}") + 1) / 2
    ink(im, (bx, c[1] + 1), str(cur), big, (255, 255, 255, 255), 4)
    ink(im, (bx + dw / 2 + 1, c[1] + 1 + 0.35 * big - 0.35 * 26), f"/{mx}", 26, CYAN + (255,), 3, left=True)
    tag(im, (c[0] - 52, c[1] - r - 26), "ENERGY", 20, 24)


def stars(im, c, n):
    lay = Image.new("RGBA", im.size)
    d = ImageDraw.Draw(lay)
    s = 40

    def dia(k):
        return [(c[0], c[1] - k), (c[0] + k * 0.78, c[1]), (c[0], c[1] + k), (c[0] - k * 0.78, c[1])]
    sh = Image.new("RGBA", im.size)
    ImageDraw.Draw(sh).polygon([(x + 5, y + 6) for x, y in dia(s)], fill=(0, 0, 10, 150))
    lay.alpha_composite(sh.filter(ImageFilter.GaussianBlur(5)))
    d.polygon(dia(s), fill=(255, 255, 255, 255))
    d.polygon(dia(s - 4), fill=(12, 30, 92, 255))
    d.line(dia(s - 11) + [dia(s - 11)[0]], fill=CYAN + (255,), width=2)
    # 上方一颗小四角星（辉星）
    st = (c[0], c[1] - s + 13)
    d.polygon([(st[0], st[1] - 9), (st[0] + 2.5, st[1] - 2.5), (st[0] + 9, st[1]), (st[0] + 2.5, st[1] + 2.5), (st[0], st[1] + 9), (st[0] - 2.5, st[1] + 2.5), (st[0] - 9, st[1]), (st[0] - 2.5, st[1] - 2.5)], fill=(255, 255, 255, 255))
    im.alpha_composite(lay)
    ink(im, (c[0], c[1] + s * 0.12), str(n), 40, (255, 255, 255, 255), 3)


def pile(im, c, n, word, mirror=False):
    lay = Image.new("RGBA", im.size)
    h = 74
    card = BACK.resize((round(h * BACK.width / BACK.height), h), Image.LANCZOS)
    for ang, off in ((-16, (-9, 4)), (-5, (0, 0))):
        a = -ang if mirror else ang
        o = (-off[0], off[1]) if mirror else off
        sh = Image.new("RGBA", (card.width + 30, card.height + 30))
        ImageDraw.Draw(sh).rounded_rectangle([15, 15, 15 + card.width, 15 + card.height], 6, fill=(0, 0, 10, 140))
        sh = sh.filter(ImageFilter.GaussianBlur(4)).rotate(a, expand=True, resample=Image.BICUBIC)
        lay.alpha_composite(sh, (int(c[0] + o[0] - sh.width / 2 + 4), int(c[1] + o[1] - sh.height / 2 + 5)))
        r = card.rotate(a, expand=True, resample=Image.BICUBIC)
        lay.alpha_composite(r, (int(c[0] + o[0] - r.width / 2), int(c[1] + o[1] - r.height / 2)))
    im.alpha_composite(lay)
    # 张数牌：斜切黑底白边红尾，压在牌的右下（弃牌堆在左下）
    tx = c[0] + (14 if not mirror else -64)
    tag(im, (tx, c[1] + 12), str(n), 30, 34)
    text(im, (c[0] + (-2 if not mirror else 2), c[1] - 50), word, 17, CYAN + (255,), 3)


def main(shot, out):
    im = Image.open(shot).convert("RGBA")
    erase(im, (60, 790, 270, 980))
    erase(im, (0, 975, 120, 1078))
    erase(im, (1790, 975, 1917, 1078))
    energy(im, (172, 872), 3, 3)
    stars(im, (104, 935), 3)
    pile(im, (52, 1022), 6, "DRAW")
    pile(im, (1858, 1022), 0, "DISCARD", mirror=True)
    im.convert("RGB").save(out)
    # 放大图
    big = Image.new("RGB", (900, 330), (8, 10, 20))
    big.paste(im.crop((0, 760, 330, 1078)).convert("RGB").resize((450, 433)).crop((0, 0, 450, 330)), (0, 0))
    big.paste(im.crop((1700, 900, 1917, 1078)).convert("RGB").resize((380, 312)), (480, 10))
    big.save(out.replace(".png", "_zoom.png"))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
