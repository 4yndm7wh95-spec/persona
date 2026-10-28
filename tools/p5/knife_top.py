"""knife_top_f = slash_a10 (the cut, fist at its highest) with the blade below the fist erased, so the code-drawn knife
(art/p5/props/knife_c.png) can spin in the raised fist right after the cut (user 2026-10-28). The blade hangs over
the background only, so it is simply cut away (every non-glove pixel of the blade's connected piece below the fist) and the fist's
bared lower edge gets an ink outline.
Writes the final directly (same canvas as slash_a10's final).
    python tools/p5/knife_top.py
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

FINAL = Path(__file__).resolve().parents[2] / "art/p5/poses/final"
BOX = (1222, 1295, 399, 512)      # x0, x1, y0, y1: the blade below the fist (final canvas)


def main():
    a = np.array(Image.open(FINAL / "slash_a10.png").convert("RGBA"))
    x0, x1, y0, y1 = BOX
    sub = a[y0:y1, x0:x1].astype(int)
    full = a.astype(int)
    red = (full[..., 0] > 150) & (full[..., 1] < 90) & (full[..., 3] > 8)
    lab_r, _ = ndimage.label(red)
    fist = lab_r == np.bincount(lab_r[lab_r > 0]).argmax()            # the glove itself, not red glints on the steel
    glove = fist[y0:y1, x0:x1]
    solid = (sub[..., 3] > 8) & ~glove
    lab, n = ndimage.label(solid)
    # the blade: the piece that reaches the bottom of the box (the tip)
    pieces = set(np.unique(lab[-6:][lab[-6:] > 0]))
    blade = np.isin(lab, list(pieces))
    blade = ndimage.binary_dilation(blade, iterations=1) & (sub[..., 3] > 0) & ~glove
    a[y0:y1, x0:x1][blade] = 0
    # the guard used to cover the fist's lower edge: give the now-bare glove edge the usual ~3 px ink outline
    ring = ndimage.binary_dilation(glove, iterations=3) & ~glove & (a[y0:y1, x0:x1][..., 3] < 200)
    a[y0:y1, x0:x1][ring] = (14, 14, 18, 255)
    Image.fromarray(a).save(FINAL / "knife_top_f.png")
    print("erased", int(blade.sum()), "px ->", FINAL / "knife_top_f.png")


if __name__ == "__main__":
    main()
