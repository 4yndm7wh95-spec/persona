"""The black TV (gen/tv_main.webp) as a grid of square cells, for the All-Out's "enemy breaks into squares that build the TV".
Usage: python3 -I docs/p4/workbench/tools/tv_cells.py [cell px, default 40]
Prints `var TVCELLS = [...]` for p4r_workbench.html: one [gx, gy, colour] per cell, gx/gy in cells of the 1536x1024 picture
(cell centre = (g + 0.5) * size px), colour = the cell's average colour as an int 0xRRGGBB, or -1 for a cell of the screen
(the transparent hole the screen content shows through). A cell is part of the set when at least half of it is opaque."""
import sys

import numpy as np
from PIL import Image

CS = int(sys.argv[1]) if len(sys.argv) > 1 else 40
im = Image.open('docs/p4/workbench/gen/tv_main.webp').convert('RGBA')
a = np.asarray(im).astype(float); H, W, _ = a.shape
al = a[..., 3] / 255
HX, HY, HW, HH = 679.5, 566.5, 764, 526   # the screen hole (TVART.tvMain)
out = []
for gy in range(H // CS + 1):
    for gx in range(W // CS + 1):
        y0, x0 = gy * CS, gx * CS
        blk = al[y0:y0 + CS, x0:x0 + CS]
        if blk.size == 0:
            continue
        cx, cy = x0 + CS / 2, y0 + CS / 2
        if blk.mean() > 0.5:
            w = blk[..., None]
            rgb = (a[y0:y0 + CS, x0:x0 + CS, :3] * w).sum((0, 1)) / max(w.sum(), 1e-6)
            r, g, b = [int(round(v)) for v in rgb]
            out.append('[%d,%d,%d]' % (gx, gy, (r << 16) | (g << 8) | b))
        elif abs(cx - HX) < HW / 2 - CS * 0.3 and abs(cy - HY) < HH / 2 - CS * 0.3:
            out.append('[%d,%d,-1]' % (gx, gy))
print('    var TVCS = %d, TVCELLS = [%s];' % (CS, ','.join(out)))
