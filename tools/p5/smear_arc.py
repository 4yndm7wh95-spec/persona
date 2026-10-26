"""Bake the slash smear into a clean P5 Joker mid-swing frame (like the Makoto sword_smear frame).

The image generator put its arc in the wrong place (it ran past the blade), so the base frame is drawn
clean and the motion is added here, always ending at the blade:
  1. ghost: the fist + knife copied many times, rotated about the right shoulder back toward where
     the swing started, fading out - one fused smear, not separate hands;
  2. streaks: a bundle of thin translucent white arcs traced by points along the fist->blade line, plus a
     bright edge along the blade tip's path; every arc ends where its point is now, so the trail
     ends exactly at the blade (nothing ahead of it);
  3. drag: the coat tails get a short directional blur opposite the swing.
The crisp arm and knife are put back on top last.
Usage: python tools/p5/smear_arc.py base.png out.png
Geometry below is measured on art/p5/poses/slash_mid_a.png (1024x1536).
"""
import sys
import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

PIVOT = (545, 275)          # right (near) shoulder
ARM = [(505, 235), (660, 250), (740, 232), (845, 245), (865, 320), (868, 470), (800, 350), (660, 375), (515, 345)]
HAND = [(722, 232), (845, 240), (870, 320), (872, 472), (828, 472), (792, 345), (726, 330)]  # fist + knife
FIST = (786, 296)
BLADE = [(790, 330), (815, 318), (868, 460), (845, 470)]   # knife blade quad
SWEEP = 75                  # degrees the arm swept since knife_ready (fist was down at the belly)
STEPS = 28
COAT = [(0, 560), (330, 600), (360, 1000), (300, 1250), (0, 1200)]  # coat tails flung back (image-left)


def poly_mask(size, poly, blur=2):
    m = Image.new('L', size, 0)
    ImageDraw.Draw(m).polygon(poly, fill=255)
    return m.filter(ImageFilter.GaussianBlur(blur))


def layer_from(im, mask):
    out = im.copy()
    out.putalpha(ImageChops.multiply(im.getchannel('A'), mask))
    return out


def rot(img, deg):
    # PIL rotates counter-clockwise for positive angles; image y points down, so a downward
    # (clockwise-on-screen) swing back toward the start is a negative angle.
    return img.rotate(-deg, resample=Image.BICUBIC, center=PIVOT)


def rot_pt(p, deg):
    a = np.radians(deg)
    x, y = p[0] - PIVOT[0], p[1] - PIVOT[1]
    return (PIVOT[0] + x * np.cos(a) - y * np.sin(a), PIVOT[1] + x * np.sin(a) + y * np.cos(a))


def main():
    base, out = sys.argv[1], sys.argv[2]
    im = Image.open(base).convert('RGBA')
    size = im.size
    arm = layer_from(im, poly_mask(size, ARM))
    hand = layer_from(im, poly_mask(size, HAND))

    # 1. arm ghost
    ghost = Image.new('RGBA', size, (0, 0, 0, 0))
    for i in range(STEPS, 0, -1):
        t = i / STEPS
        g = rot(hand, SWEEP * t)
        a = g.getchannel('A').point(lambda v, k=(1 - t) ** 1.2 * 0.38: int(v * k))
        g.putalpha(a)
        ghost.alpha_composite(g)
    ghost = ghost.filter(ImageFilter.GaussianBlur(4))
    # the ghost is a light grey blur, not a red/black copy: it reads as speed, not as extra hands
    ghost = Image.merge('RGBA', (*Image.new('RGB', size, (228, 228, 236)).split(), ghost.getchannel('A')))

    # 2. streaks: points along the fist->tip line (and a bit beyond) each trace their own arc,
    # every streak ends where that point is now, so the whole bundle ends at the blade.
    rng = np.random.default_rng(5)
    tip = np.array(BLADE[2], float)
    f = np.array(FIST, float)
    band = Image.new('RGBA', size, (0, 0, 0, 0))
    d = ImageDraw.Draw(band)
    for u in np.linspace(0.6, 1.15, 11):
        p = f + (tip - f) * u
        strength = rng.uniform(0.35, 1.0) * (0.5 + 0.5 * min(u, 1))
        width = rng.uniform(2, 7) * (1.3 if u > 0.8 else 1)
        path = [rot_pt(p, SWEEP * (1 - t)) for t in np.linspace(0, 1, 40)]
        for i in range(len(path) - 1):
            t = i / (len(path) - 1)
            d.line([path[i], path[i + 1]], fill=(248, 248, 255, int(220 * strength * t ** 1.6)), width=int(width))
    band = band.filter(ImageFilter.GaussianBlur(2.5))
    path = [rot_pt(tip, SWEEP * (1 - t)) for t in np.linspace(0, 1, 50)]
    edge = Image.new('RGBA', size, (0, 0, 0, 0))
    de = ImageDraw.Draw(edge)
    for i in range(len(path) - 1):
        t = i / (len(path) - 1)
        de.line([path[i], path[i + 1]], fill=(255, 255, 255, int(30 + 225 * t ** 1.5)), width=int(3 + 14 * t))
    edge = edge.filter(ImageFilter.GaussianBlur(1.5))

    # 3. coat drag
    coat_m = poly_mask(size, COAT, 20)
    coat = layer_from(im, coat_m)
    drag = Image.new('RGBA', size, (0, 0, 0, 0))
    for k in range(1, 7):
        sh = Image.new('RGBA', size, (0, 0, 0, 0))
        sh.alpha_composite(coat, (-k * 9, -k * 2))
        sh.putalpha(sh.getchannel('A').point(lambda v, k=k: int(v * (0.22 - k * 0.03))))
        drag.alpha_composite(sh)

    res = Image.new('RGBA', size, (0, 0, 0, 0))
    res.alpha_composite(drag)
    res.alpha_composite(im)
    res.alpha_composite(band)
    res.alpha_composite(ghost)
    res.alpha_composite(edge)
    res.alpha_composite(arm)        # crisp arm + knife on top
    res.save(out)


if __name__ == '__main__':
    main()
