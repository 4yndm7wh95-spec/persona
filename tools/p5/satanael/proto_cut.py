"""Prototype cut of Satanael v6 for the raise / recoil motion test (until Codex's real layers arrive):
base (gun arm removed, hole filled from the nearest surrounding pixels), upper arm, forearm + hand + gun. Half resolution.
Output: art/p5/fx/v4/satanael/proto/{sat_base,sat_upper,sat_fore}.png + proto.json (pivots in output pixels)."""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / "art/p5/fx/v4/satanael/satanael_v6_draft.png"
OUT = ROOT / "art/p5/fx/v4/satanael/proto"
K = 0.5
SHOULDER, ELBOW, WRIST, MUZZLE = (1440, 2000), (1610, 2130), (1930, 2170), (3340, 2160)
# full-res polygons
UPPER = [(1330, 1930), (1500, 1955), (1625, 2030), (1665, 2330), (1560, 2330), (1420, 2270), (1340, 2120)]
FORE = [(1590, 2030), (1980, 2075), (2020, 1940), (3400, 1940), (3400, 2300), (2190, 2300), (2180, 2410), (1990, 2410),
        (1950, 2330), (1600, 2335)]

im = Image.open(SRC).convert("RGBA")
W, H = im.size
a = np.array(im)


def mask(poly, grow=0):
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).polygon(poly, fill=255)
    m = np.array(m) > 0
    return ndimage.binary_dilation(m, iterations=grow) if grow else m


mf, mu = mask(FORE), mask(UPPER)
fore = a.copy(); fore[~mf] = 0
upper = a.copy(); upper[~mu] = 0
base = a.copy()
hole = (mf | mu)
# fill each hole column from the nearest pixels above and below it: if both are part of the figure, blend between them
# (wing, chest behind the arm); otherwise it was sky there -> transparent
af = a.astype(np.float32)
for x in np.nonzero(hole.any(0))[0]:
    col = hole[:, x]
    ys = np.nonzero(col)[0]
    # split into runs
    runs = np.split(ys, np.nonzero(np.diff(ys) > 1)[0] + 1)
    for r in runs:
        y0, y1 = r[0] - 1, r[-1] + 1
        if y0 < 0 or y1 >= H:
            base[r, x] = 0
            continue
        top, bot = af[y0, x], af[y1, x]
        if top[3] > 128 and bot[3] > 128:
            t = ((r - y0) / (y1 - y0))[:, None]
            v = top * (1 - t) + bot * t
            v[:, :3] *= 0.75                                     # in shadow behind the arm
            base[r, x] = v.astype(np.uint8)
        else:
            base[r, x] = 0
OUT.mkdir(parents=True, exist_ok=True)
size = (round(W * K), round(H * K))
for name, arr in (("sat_base", base), ("sat_upper", upper), ("sat_fore", fore)):
    Image.fromarray(arr).resize(size, Image.LANCZOS).save(OUT / f"{name}.png")
s = lambda p: [round(p[0] * K, 1), round(p[1] * K, 1)]
json.dump({"size": list(size), "shoulder": s(SHOULDER), "elbow": s(ELBOW), "wrist": s(WRIST), "muzzle": s(MUZZLE),
           "head": s((1320, 1600)), "neck": s((1320, 1720))}, open(OUT / "proto.json", "w"), indent=1)
print("ok", size)
