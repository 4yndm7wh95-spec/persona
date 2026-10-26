"""Match idle_a's head size to the other P5 Joker poses.

idle_a came out with a ~20% smaller head (and a correspondingly longer body) than every later pose.
Fix without regenerating (the face/eyes are approved): shrink the whole figure 4% about the feet,
then scale only the head (hair + face, above the collar) up about the chin.
Usage: python tools/p5/idle_head_fix.py art/p5/poses/idle_a.png out.png [head_scale=1.26] [body_scale=0.955]  (idle_a2.png was made with these)
"""
import sys
from PIL import Image, ImageDraw, ImageFilter

src, out = sys.argv[1], sys.argv[2]
hs = float(sys.argv[3]) if len(sys.argv) > 3 else 1.26
bs = float(sys.argv[4]) if len(sys.argv) > 4 else 0.955
im = Image.open(src).convert('RGBA')
W, H = im.size


def scale_about(layer, s, px, py):
    big = layer.resize((round(W * s), round(H * s)), Image.LANCZOS)
    res = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    res.alpha_composite(big, (round(px - px * s), round(py - py * s)) if px - px * s >= 0 else (0, 0),
                        (0, 0) if px - px * s >= 0 else (round(px * s - px), round(py * s - py)))
    return res


def paste_scaled(layer, s, px, py):
    big = layer.resize((round(W * s), round(H * s)), Image.LANCZOS)
    res = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ox, oy = round(px - px * s), round(py - py * s)
    sx, sy = max(0, -ox), max(0, -oy)
    res.alpha_composite(big, (max(0, ox), max(0, oy)), (sx, sy))
    return res


# 1. whole figure 4% smaller about the feet
body = paste_scaled(im, bs, 550, 1509)

# 2. head mask (coordinates measured on the original idle_a, mapped through the body scale)
def m(p):
    return (550 + (p[0] - 550) * bs, 1509 + (p[1] - 1509) * bs)

poly = [m(p) for p in [(440, 0), (670, 0), (665, 150), (602, 160), (580, 198), (558, 212), (520, 207), (482, 188), (458, 168), (442, 150)]]
mask = Image.new('L', (W, H), 0)
ImageDraw.Draw(mask).polygon(poly, fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(2))
head = body.copy()
head.putalpha(Image.composite(body.getchannel('A'), Image.new('L', (W, H), 0), mask))
px, py = m((540, 212))
res = body.copy()
res.alpha_composite(paste_scaled(head, hs, px, py))
res.save(out)
