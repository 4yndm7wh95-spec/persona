"""art/p5/props/knife_c.png: Joker's knife for the spins (the image generator refused a lone knife).

The blade is lifted from the poses themselves (knife_ready_b: the steel over the black coat is separated by colour),
straightened to point right, given the usual ink outline; the grip and guard (hidden in the fist there) are drawn in
code to match - black oval guard, black rounded grip with a small pommel. Pivot (middle of the grip) in knife_c.json.
    python tools/p5/draw_knife.py
"""
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "art/p5/poses/final/knife_ready_b.png"
BOX = (520, 700, 735, 860)          # x0, y0, x1, y1 around the blade (final canvas)
OUT = ROOT / "art/p5/props"
INK, GRIP = (14, 14, 18, 255), (26, 26, 32, 255)


def main():
    a = np.array(Image.open(SRC).convert("RGBA").crop(BOX)).astype(int)
    rgb = a[..., :3]
    steel = (rgb.max(-1) > 105) & (np.ptp(rgb, -1) < 45) & (a[..., 3] > 200)
    lab, n = ndimage.label(steel)
    blade = lab == np.bincount(lab[lab > 0]).argmax()
    blade = ndimage.binary_closing(blade, iterations=2)
    ys, xs = np.nonzero(blade)
    # direction of the blade (PCA), guard end = the end nearest the fist (up-right)
    pts = np.c_[xs, ys].astype(float)
    c = pts.mean(0)
    w, v = np.linalg.eigh(np.cov((pts - c).T))
    d = v[:, w.argmax()]
    if d[0] > 0:
        d = -d                                   # point from the guard towards the tip (down-left)
    ang = math.degrees(math.atan2(d[1], d[0]))
    img = np.zeros_like(a, dtype=np.uint8)
    img[blade] = a[blade]
    blade_im = Image.fromarray(img).rotate(ang - 180, resample=Image.BICUBIC, expand=True, center=None)
    # rotate(theta) is counter-clockwise: the blade (pointing at `ang`, y down) now points right
    bb = blade_im.getchannel("A").point(lambda q: 255 if q > 60 else 0).getbbox()
    blade_im = blade_im.crop(bb).rotate(180)
    # the guard end was under the fist (ragged): start the blade a few px under the guard
    blade_im = blade_im.crop((0, 0, blade_im.width, blade_im.height))
    HIDE = 30                                    # ragged where the fist covered it: tucked under the guard + grip
    bw, bh = blade_im.size
    # canvas: grip 64 + guard 10 + blade, padded 8 for the outline
    pad, grip_l, guard_w = 8, 64, 10
    W, H = pad * 2 + grip_l + guard_w + bw - HIDE, max(bh, 30) + pad * 2
    cy = H // 2
    body = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    x_blade = pad + grip_l + guard_w - HIDE
    body.alpha_composite(blade_im, (x_blade, cy - bh // 2))
    body = body.crop((0, 0, W, H))
    # nothing of the blade left of the guard: the grip covers it
    clear = Image.new("RGBA", (pad + grip_l + 2, H), (0, 0, 0, 0))
    body.paste(clear, (0, 0))
    dr = ImageDraw.Draw(body)
    dr.rounded_rectangle([pad + 4, cy - 8, pad + grip_l, cy + 8], radius=7, fill=GRIP)            # grip
    dr.ellipse([pad, cy - 6, pad + 12, cy + 6], fill=GRIP)                                        # pommel
    dr.ellipse([pad + grip_l - 1, cy - 13, pad + grip_l + guard_w + 1, cy + 13], fill=INK)        # oval guard
    dr.line([(pad + 10, cy - 4), (pad + grip_l - 6, cy - 4)], fill=(78, 78, 90, 255), width=2)    # grip sheen
    # ink outline: everything grown by 4 px, drawn underneath
    al = np.array(body.getchannel("A")) > 40
    ring = ndimage.binary_dilation(al, iterations=4)
    ink = np.zeros((H, W, 4), np.uint8)
    ink[ring] = INK
    out = Image.fromarray(ink)
    out.alpha_composite(body)
    OUT.mkdir(parents=True, exist_ok=True)
    out.save(OUT / "knife_c.png")
    pivot = [pad + grip_l // 2, cy]
    (OUT / "knife_c.json").write_text(json.dumps({"pivot": pivot, "tip": [W - pad, cy], "blade_angle_in_pose": round(ang, 1),
                                                  "note": "final-canvas px"}, indent=1))
    print(OUT / "knife_c.png", out.size, "pivot", pivot, "blade angle", round(ang, 1))


if __name__ == "__main__":
    main()
