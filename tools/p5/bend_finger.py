"""gun_spin_g = gun_spin_f with the index finger bent a little more at its middle joint.

The user (2026-10-28): the joint is there but too subtle in 2D - bend it until it reads. The finger beyond the joint
(raw canvas) is rotated down about the joint's centre line; the angle eases in along the finger over ~14 px, so the
top edge stretches and the bottom edge folds like a real knuckle and the outline stays one line (no seam).
Premultiplied resampling, no fringes. Also writes the final (art/p5/poses/final) placed like normalize_poses.py
does (1.0, rear foot on idle_a3's) without touching its registry, so Codex's normalize runs stay conflict-free.
    python tools/p5/bend_finger.py [degrees]
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

SRC, DST = "art/p5/poses/gun_spin_f.png", "art/p5/poses/gun_spin_g.png"
PIVOT = np.array([928.0, 240.0])        # the index finger's middle joint, on its centre line (raw gun_spin_f)
R0, R1 = -4.0, 12.0                     # the bend eases in over this stretch along the finger (px from the joint)
DEG = float(sys.argv[1]) if len(sys.argv) > 1 else 18.0


def main():
    a = np.array(Image.open(SRC).convert("RGBA")).astype(float)
    h, w = a.shape[:2]
    pm = a.copy()
    pm[..., :3] *= pm[..., 3:4] / 255.0
    ys, xs = np.mgrid[0:h, 0:w].astype(float)
    # the index finger right of the knuckles; below y 253 the middle finger starts, so only past its tip (x 932)
    region = (xs >= 905) & (xs <= 995) & (ys >= 216) & ((ys <= 253) | ((xs >= 932) & (ys <= 262)))
    finger = pm.copy()
    finger[~region] = 0
    # output pixel -> source: rotate back by the angle for its distance from the pivot (rotation keeps the distance)
    dx, dy = xs - PIVOT[0], ys - PIVOT[1]
    u = np.clip((dx - R0) / (R1 - R0), 0, 1)           # by distance along the finger (it points right)
    th = np.radians(DEG) * u * u * (3 - 2 * u)
    c, s = np.cos(-th), np.sin(-th)
    sx, sy = PIVOT[0] + c * dx - s * dy, PIVOT[1] + s * dx + c * dy
    bent = np.stack([ndimage.map_coordinates(finger[..., k], [sy, sx], order=1, mode="constant") for k in range(4)], -1)
    base = pm.copy()
    base[region] = 0
    out = bent + base * (1 - bent[..., 3:4] / 255.0)
    al = out[..., 3:4]
    out[..., :3] = np.where(al > 0, out[..., :3] * 255.0 / np.maximum(al, 1e-6), 0)
    img = Image.fromarray(out.clip(0, 255).astype(np.uint8))
    img.save(DST)
    sys.path.insert(0, str(Path(__file__).parent))
    import normalize_poses as nz
    ref = nz.boots(np.array(nz.load(nz.REF)).astype(int))[0]
    feet = nz.boots(np.array(img).astype(int))[0]
    ox, oy = round(ref[0] - feet[0]) + nz.PAD, round(ref[1] - feet[1]) + nz.TOP
    res = Image.new("RGBA", (img.width + 2 * nz.PAD, img.height + nz.TOP), (0, 0, 0, 0))
    res.alpha_composite(img, (max(0, ox), max(0, oy)), (max(0, -ox), max(0, -oy)))
    res.save(nz.OUT / "gun_spin_g.png")
    print(DST, f"{DEG} deg, final offset", [ox, oy])


if __name__ == "__main__":
    main()
