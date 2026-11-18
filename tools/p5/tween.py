"""In-between pictures for the Joker rig: motion-compensated morphs between two neighbouring poses.

Why (user 2026-11-17: "make the moves smooth, nothing else matters"): every pose is a whole picture and a switch
was a hard cut (a crossfade showed two Jokers at once), so each change of picture was a visible jump. Here the
picture in between is made by moving the pixels, not by blending: optical flow (OpenCV DIS) finds where each
pixel of A goes in B, both pictures are pushed to the middle position and only then blended, so a hand travels
instead of fading out in one place and in at another.
- Flow runs on a half-size copy (premultiplied colour over grey, so the black coat still has structure against
  the transparent background) and is scaled back up; both pictures are warped at full size.
- The red gloves are found in both pictures and matched; their travel seeds the flow and, close to each glove,
  replaces it outright (DIS on its own loses a small hand that moves far and leaves two ghost hands).
- Results are cached in art/p5/poses/tween/<a>__<b>__<t>.png (a, b sorted; t is the share of b, in percent).
  A hand-drawn replacement in art/p5/poses/tween_override/ with the same name wins - that is where any new
  drawn in-betweens go.
    python tools/p5/tween.py [--force] [--sheet OUT.png]     (all pairs used by build_rig_p5.ANIMS)
"""
import itertools
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
FINAL = ROOT / "art/p5/poses/final"
CACHE = ROOT / "art/p5/poses/tween"
OVERRIDE = ROOT / "art/p5/poses/tween_override"
FLOW_SCALE = 0.5


def name_of(a, b, t):
    """cache name; the pair is stored sorted, t as the share of the second (sorted) picture."""
    if a > b:
        a, b, t = b, a, 1 - t
    return f"{a}__{b}__{round(t * 100):02d}"


def override(a, b, t):
    """a drawn in-between for a -> b at share t of b, if one exists. Accepted names: the cache name, or the
    travel-order name <a>__<b>__<percent of the way to b> (either direction)."""
    pct = round(t * 100)
    for n in (name_of(a, b, t), f"{a}__{b}__{pct:02d}", f"{b}__{a}__{100 - pct:02d}"):
        if (OVERRIDE / f"{n}.png").exists():
            return OVERRIDE / f"{n}.png"
    return None


def has_override(a, b):
    return OVERRIDE.exists() and any(f.name.startswith((f"{a}__{b}__", f"{b}__{a}__")) for f in OVERRIDE.glob("*.png"))


def _load(n):
    a = np.asarray(Image.open(FINAL / f"{n}.png").convert("RGBA")).astype(np.float32) / 255
    a = a.copy()
    a[..., :3] *= a[..., 3:]
    return a


def _small(a):
    h, w = a.shape[:2]
    return cv2.resize(a, (int(w * FLOW_SCALE), int(h * FLOW_SCALE)), interpolation=cv2.INTER_AREA)


def _gray(a):
    g = a[..., :3] @ np.array([0.3, 0.59, 0.11], np.float32) + (1 - a[..., 3]) * 0.45
    return np.clip(g * 255, 0, 255).astype(np.uint8)


def gloves(a, min_area=150):
    """centres of the (at most two) red gloves on a half-size premultiplied picture."""
    rgb = a[..., :3] / np.maximum(a[..., 3:], 1e-3)
    red = (rgb[..., 0] > 0.55) & (rgb[..., 1] < 0.3) & (rgb[..., 2] < 0.3) & (a[..., 3] > 0.5)
    red = ndimage.binary_opening(red, iterations=1)
    lab, n = ndimage.label(ndimage.binary_closing(red, iterations=3))
    out = []
    for i in range(1, n + 1):
        m = lab == i
        if m.sum() >= min_area:
            ys, xs = np.nonzero(m)
            out.append((float(xs.mean()), float(ys.mean()), int(m.sum())))
    out.sort(key=lambda g: -g[2])
    return out[:2]


def _match(ga, gb):
    k = min(len(ga), len(gb))
    if k == 0:
        return []
    best = None
    for pa in itertools.permutations(range(len(ga)), k):
        for pb in itertools.permutations(range(len(gb)), k):
            c = sum(np.hypot(ga[i][0] - gb[j][0], ga[i][1] - gb[j][1]) for i, j in zip(pa, pb))
            if best is None or c < best[0]:
                best = (c, list(zip(pa, pb)))
    return [(ga[i], gb[j]) for i, j in best[1]]


def _guide(a, b, sigma=45, lock=30):
    h, w = a.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    f = np.zeros((h, w, 2), np.float32)
    trust = np.zeros((h, w), np.float32)
    for (xa, ya, _), (xb, yb, _) in _match(gloves(a), gloves(b)):
        if np.hypot(xb - xa, yb - ya) < 6:
            continue
        d2 = (xx - xa) ** 2 + (yy - ya) ** 2
        wt = np.exp(-d2 / (2 * sigma ** 2))
        f[..., 0] += wt * (xb - xa)
        f[..., 1] += wt * (yb - ya)
        trust = np.maximum(trust, np.clip(1.6 * np.exp(-d2 / (2 * lock ** 2)), 0, 1))
    return f, trust


def _flow(a, b):
    """dense flow a -> b on half-size pictures, in half-size pixels."""
    dis = cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
    dis.setFinestScale(0)
    dis.setGradientDescentIterations(25)
    dis.setPatchSize(12)
    dis.setPatchStride(4)
    init, trust = _guide(a, b)
    fl = dis.calc(_gray(a), _gray(b), init.copy())
    return fl * (1 - trust[..., None]) + init * trust[..., None]


def _warp(img, fl):
    h, w = img.shape[:2]
    xx, yy = np.meshgrid(np.arange(w, dtype=np.float32), np.arange(h, dtype=np.float32))
    return cv2.remap(img, xx + fl[..., 0], yy + fl[..., 1], cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT,
                     borderValue=0)


def _up(fl, shape):
    h, w = shape[:2]
    return cv2.resize(fl, (w, h), interpolation=cv2.INTER_LINEAR) / FLOW_SCALE


def _to_image(p):
    a = p.copy()
    al = a[..., 3:]
    a[..., :3] = np.where(al > 1e-3, a[..., :3] / np.maximum(al, 1e-3), 0)
    return Image.fromarray(np.clip(a * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA")


def make_pair(a, b, ts, force=False):
    """write the in-betweens of a -> b at shares ts of b; returns {t: path}."""
    out = {}
    todo = []
    for t in ts:
        p = override(a, b, t)
        if p is None:
            p = CACHE / f"{name_of(a, b, t)}.png"
            if force or not p.exists():
                todo.append(t)
        out[t] = p
    if todo:
        A, B = _load(a), _load(b)
        sa, sb = _small(A), _small(B)
        fab, fba = _up(_flow(sa, sb), A.shape), _up(_flow(sb, sa), A.shape)
        CACHE.mkdir(parents=True, exist_ok=True)
        for t in todo:
            mid = (1 - t) * _warp(A, -t * fab) + t * _warp(B, -(1 - t) * fba)
            _to_image(mid).save(out[t], optimize=True)
    return out


def path(a, b, t):
    return override(a, b, t) or CACHE / f"{name_of(a, b, t)}.png"


def sheet(pairs, ts, dst):
    """contact sheet for review: a, in-betweens, b on one row per pair."""
    rows = []
    for a, b in pairs:
        ims = [Image.open(FINAL / f"{a}.png")] + [Image.open(path(a, b, t)) for t in ts] + [Image.open(FINAL / f"{b}.png")]
        rows.append([im.convert("RGBA").resize((im.width // 6, im.height // 6), Image.LANCZOS) for im in ims])
    w, h = rows[0][0].size
    c = Image.new("RGBA", (w * len(rows[0]), h * len(rows)), (48, 52, 70, 255))
    for r, ims in enumerate(rows):
        for k, im in enumerate(ims):
            c.alpha_composite(im, (k * w, r * h))
    c.convert("RGB").save(dst)


if __name__ == "__main__":
    sys.path.insert(0, str(ROOT / "tools/rig"))
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import build_rig_p5 as rig
    pairs = rig.tween_pairs()
    for a, b in pairs:
        make_pair(a, b, rig.TWEEN_T, force="--force" in sys.argv)
        print(a, "->", b)
    if "--sheet" in sys.argv:
        sheet(pairs, rig.TWEEN_T, sys.argv[sys.argv.index("--sheet") + 1])
