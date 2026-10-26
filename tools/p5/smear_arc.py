"""Bake a slash trail into a P5 Joker smear frame so it always trails the knife.

The image generator draws the arc in the wrong place (it ran past the blade). Instead the base frame is
drawn clean (no streak) and this script paints the trail: a tapered, semi-transparent white arc that
starts where the knife was in knife_ready and ENDS at the blade tip in the smear frame, thickest and
brightest at the blade, fading toward the tail. Nothing is drawn ahead of the blade.
Usage: python tools/p5/smear_arc.py base.png out.png  x0,y0  xm,ym  x1,y1
  x0,y0 = tail (blade tip in knife_ready), xm,ym = a point the path bends through, x1,y1 = blade tip now.
Coordinates are on the 1024x1536 canvas of the base image.
"""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def pt(s):
    return tuple(float(v) for v in s.split(','))


def main():
    base, out = sys.argv[1], sys.argv[2]
    p0, pm, p1 = (np.array(pt(s)) for s in sys.argv[3:6])
    im = Image.open(base).convert('RGBA')
    W, H = im.size
    # quadratic bezier through pm (pm is the curve's midpoint, so the control point is 2*pm - (p0+p1)/2)
    c = 2 * pm - (p0 + p1) / 2
    n = 80
    pts = [(1 - t) ** 2 * p0 + 2 * (1 - t) * t * c + t ** 2 * p1 for t in np.linspace(0, 1, n)]
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(n - 1):
        t = i / (n - 1)
        w = 4 + 46 * t ** 1.6          # tapered: thin tail, thick at the blade
        a = int(30 + 170 * t ** 1.3)    # faint tail, bright at the blade
        d.line([tuple(pts[i]), tuple(pts[i + 1])], fill=(245, 245, 250, a), width=int(w))
    layer = layer.filter(ImageFilter.GaussianBlur(3))
    core = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    dc = ImageDraw.Draw(core)
    for i in range(n // 2, n - 1):
        t = i / (n - 1)
        dc.line([tuple(pts[i]), tuple(pts[i + 1])], fill=(255, 255, 255, int(200 * t)), width=int(3 + 8 * t))
    layer.alpha_composite(core.filter(ImageFilter.GaussianBlur(1)))
    res = im.copy()
    res.alpha_composite(layer)
    res.save(out)


if __name__ == '__main__':
    main()
