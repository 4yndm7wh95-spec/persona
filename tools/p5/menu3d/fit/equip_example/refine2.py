"""EQUIP 入场细调 v2：全画面的剪影代价。
黑区里：原版白 = Joker、原版黑 = 不是；黑区外：原版看得见的星纹 / 红底 / 浅灰（脸周围除外：照常渲染的脸是亮的）= 不是 Joker，
星纹里的大黑团（Joker 的黑身体站在星纹带上）= Joker；其余黑底分不出来，不比。
代价 = 盖错的 + 漏盖的（像素数 / Joker 像素数）× 100 + 特征点误差 + 离参考机位的距离。"""
import sys, json, numpy as np
from pathlib import Path
from PIL import Image, ImageDraw
from scipy.optimize import minimize
from scipy.ndimage import uniform_filter, binary_dilation, binary_opening, binary_closing, binary_erosion
sys.path.insert(0, str(Path(__file__).resolve().parents[1])); sys.path.insert(0, str(Path(__file__).resolve().parent))
from fitlib import cam_from, m3d, head_pts
from rast import silhouette
from common import orig
from landmarks import OBS
from zones import ZONE, EXCL
W, H = 480, 270
CLIP = 'main_0005'

def pmask(ps):
    im = Image.new('L', (W, H), 0); d = ImageDraw.Draw(im)
    for p in ps: d.polygon([(x * W / 1920, y * H / 1080) for x, y in p], fill=255)
    return np.asarray(im) > 0

def masks(i):
    im = np.asarray(orig(i, (W, H))).astype(float); L = im.mean(-1); sat = im.max(-1) - im.min(-1)
    m = uniform_filter(L, 5); sd = np.sqrt(np.maximum(uniform_filter(L * L, 5) - m * m, 0))
    red = binary_opening((im[..., 0] > 70) & (im[..., 0] > im[..., 1] * 1.8), iterations=1)
    star = (sd > 40) & ~red
    A = pmask([ZONE[i]]); ex = pmask(EXCL.get(i, []))
    yy, xx = np.mgrid[0:H, 0:W]; X, Y = (xx + 0.5) * 4, (yy + 0.5) * 4
    ok = (Y < 1010) & ~ex
    light = (L > 160) & (sat < 35)
    dark = L < 50
    face = np.zeros_like(A)
    o = OBS.get(i, {})
    if o:
        P = np.array(list(o.values()), float); c = P.mean(0); r = max(110.0, np.hypot(*(P - c).T).max() * 1.8)
        face = np.hypot(X - c[0], Y - c[1]) < r
    J = (A & binary_opening(light & ~star, iterations=1)) & ok
    from scipy.ndimage import label
    cand = binary_opening(dark & ~binary_dilation(star, iterations=1) & ~A, iterations=2) & ok
    lab, n = label(cand); sz = np.bincount(lab.ravel()); keep = sz >= 600; keep[0] = False
    blob = keep[lab]
    JA = J.copy()
    J |= blob
    NJ = ((A & binary_opening(dark & ~star, iterations=1)) | (~A & binary_erosion(star | red | light, iterations=1) & ~face & ~blob)) & ok
    NJ &= ~binary_dilation(J, iterations=1)
    return J, NJ, JA

def make_obj(i, fov, p_ref, w_lm=1.0, w_ref=0.3):
    J, NJ, JA = masks(i); nj = max(J.sum(), 1); JB = J & ~JA
    o = OBS.get(i, {}); names = list(o); O = np.array([o[n] for n in names], float) if names else None
    def obj(q, detail=False):
        f = float(np.clip(q[6], 0, 10)); V = m3d.verts(CLIP, f).astype(np.float64)
        cam = cam_from(list(q[:6]) + [fov]); e_lm = 0.0
        if names:
            hp = head_pts(V); xy, z = m3d.project(np.array([hp[n] for n in names]), cam)
            if (z <= 5).any(): return 1e6
            e_lm = np.hypot(*(xy - O).T).mean()
        s = silhouette(V, cam, W, H)
        wrong = (s & NJ).sum(); miss = (JA & ~s).sum() + 0.4 * (JB & ~s).sum()
        e_sil = (wrong + miss) / nj * 100
        e_ref = np.abs((np.array(q[:6]) - np.array(p_ref[:6])) / np.array([10, 10, 10, 5, 5, 5])).sum()
        v = w_lm * e_lm + e_sil + w_ref * e_ref * 10
        return (v, e_lm, e_sil, (s & J).sum() / max(((s & ~NJ) | J).sum(), 1)) if detail else v
    return obj

if __name__ == '__main__':
    tiles = []
    for i in range(233, 240):
        J, NJ, _ = masks(i); o = np.asarray(orig(i, (W, H))).astype(float) * 0.45
        o[NJ] = o[NJ] * 0.4 + np.array([0, 0, 150]); o[J] = (255, 255, 0)
        tiles.append(Image.fromarray(o.astype(np.uint8)))
    S = Image.new('RGB', (W * 4, H * 2))
    for k, t in enumerate(tiles): S.paste(t, ((k % 4) * W, (k // 4) * H))
    S.save('/tmp/p5fit/eq_masks2.png')
