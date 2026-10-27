"""Shrink a hand that a generated P5 Joker frame drew too big, tapering into the wrist.

glove_b (2026-10-27) drew the raised hand ~15% bigger than glove_a, so the hand pulsed when the tug
played. This scales the box above the wrist pivot about that pivot: factor 1 at the wrist, s from
45px up, so the wrist still joins the sleeve. Pivot / box are set for glove_b's raised hand (source
1024x1536 coordinates); change them for another frame.
    python tools/p5/hand_shrink.py art/p5/poses/glove_b.png art/p5/poses/glove_b.png 0.85
s > 1 enlarges instead; a 4th argument px,py,x0,x1,y0,y1,taper sets another pivot / box, e.g. hurt_a4's
head (drawn 8% small) about the neck:
    python tools/p5/hand_shrink.py art/p5/poses/hurt_a4.png art/p5/poses/hurt_a4.png 1.08 505,265,330,700,0,265,50
"""
import sys
import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates
src, out, s = sys.argv[1], sys.argv[2], float(sys.argv[3])
px, py = 768, 300          # wrist pivot
x0, x1, y0, y1 = 690, 880, 120, 300
taper = 45
if len(sys.argv) > 4:      # px,py,x0,x1,y0,y1,taper for another part (e.g. a head drawn too small)
    px, py, x0, x1, y0, y1, taper = (int(v) for v in sys.argv[4].split(','))
a = np.array(Image.open(src).convert('RGBA')).astype(float)
yy, xx = np.mgrid[y0:y1, x0:x1].astype(float)
d = np.clip((py - yy) / taper, 0, 1)
f = 1 - (1 - s) * d
sy = py + (yy - py) / f
sx = px + (xx - px) / f
res = a.copy()
for c in range(4):
    res[y0:y1, x0:x1, c] = map_coordinates(a[..., c], [sy, sx], order=1, cval=0)
Image.fromarray(res.clip(0, 255).astype(np.uint8)).save(out)
