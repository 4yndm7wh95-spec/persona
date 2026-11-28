"""Composition guide for the Yu finish-card sword pose (user 2026-11-28): where the katana goes, how big each part is and
how the blade recedes, for imagegen to follow as a layout reference. Draws flat shapes only (no style, no text).
Usage: python3 -I docs/p4/workbench/tools/sword_layout_guide.py <out.png>

Picture 1536x1024. Light grey = Yu (head with glasses, the jacket); mid grey = his raised right arm and fist;
black = the katana: the handle leaves the frame at the top-left corner (nearest the camera, widest), the round guard is seen
almost face-on as a big disc, the blade sweeps down to the right in a long curve, getting thinner as it goes away, and turns
up a little at the tip near the right edge. It passes in front of his collar, below his chin, with a clear gap."""
import math
import sys

from PIL import Image, ImageDraw

W, H = 1536, 1024
S = 2   # supersample


def P(x, y):
    return (x * S, y * S)


def bez(p0, p1, p2, p3, t):
    u = 1 - t
    return (u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
            u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1])


def band(d, pts, w0, w1, fill):
    """a band along a polyline whose width goes from w0 to w1"""
    n = len(pts); left, right = [], []
    for i, (x, y) in enumerate(pts):
        a = pts[min(i + 1, n - 1)]; b = pts[max(i - 1, 0)]
        tx, ty = a[0] - b[0], a[1] - b[1]; L = math.hypot(tx, ty) or 1
        nx, ny = -ty / L, tx / L; w = (w0 + (w1 - w0) * i / (n - 1)) / 2
        left.append(P(x + nx * w, y + ny * w)); right.append(P(x - nx * w, y - ny * w))
    d.polygon(left + right[::-1], fill=fill)


def main(out):
    im = Image.new('RGB', (W * S, H * S), (255, 255, 255)); d = ImageDraw.Draw(im)
    body, arm, sword, guard = (205, 205, 210), (150, 150, 158), (20, 20, 24), (60, 60, 66)
    # Yu: jacket and shoulders (flaring out to the right), neck, head
    d.polygon([P(400, 1024), P(470, 640), P(600, 560), P(820, 560), P(1000, 610), P(1230, 700), P(1450, 820), P(1330, 900), P(1250, 1024)], fill=body)
    d.polygon([P(1000, 610), P(1380, 560), P(1536, 600), P(1450, 820)], fill=body)          # the jacket hem blown out to the right
    d.ellipse([P(560, 70), P(880, 520)], fill=body)                                          # head
    d.rectangle([P(620, 470), P(800, 600)], fill=body)                                       # neck / collar
    d.line([P(600, 300), P(845, 300)], fill=(120, 120, 128), width=10 * S)                   # glasses line (eye level)
    d.ellipse([P(615, 270), P(705, 335)], outline=(120, 120, 128), width=6 * S)
    d.ellipse([P(740, 270), P(830, 335)], outline=(120, 120, 128), width=6 * S)
    # his right arm, raised: shoulder at the left of the head, elbow high up at the top left, forearm down to the fist
    band(d, [(560, 600), (470, 360), (400, 120)], 190, 150, arm)                             # upper arm up to the elbow
    band(d, [(400, 120), (310, 260), (250, 380)], 150, 150, arm)                             # forearm down to the fist
    d.ellipse([P(125, 280), P(350, 485)], fill=arm)                                          # the fist round the handle (near the camera: big)
    # the katana: handle out of the top-left corner (nearest, widest) -> guard -> long curved blade receding to the right
    band(d, [(-40, 140), (100, 265), (250, 400), (330, 465)], 135, 84, sword)                # handle (widest at the corner: nearest)
    d.ellipse([P(232, 345), P(462, 600)], fill=guard)                                        # guard, almost face-on: a big disc
    d.ellipse([P(320, 440), P(370, 500)], fill=sword)                                        # where the blade leaves the guard
    p0, p1, p2, p3 = (360, 480), (700, 610), (1150, 720), (1500, 600)
    band(d, [bez(p0, p1, p2, p3, i / 60) for i in range(61)], 84, 6, sword)                  # blade: wide at the guard, thin at the tip
    im = im.resize((W, H), Image.LANCZOS)
    im.save(out)
    print('saved', out)


if __name__ == '__main__':
    main(sys.argv[1])
