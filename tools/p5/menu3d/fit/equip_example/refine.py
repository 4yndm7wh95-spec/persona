"""EQUIP 页入场逐帧细调：机位 6 个自由度 + 动作帧，目标 = 特征点误差 + 黑区里白色剪影的倒角距离（只在黑区里、去掉红底 / 星纹 / EXCL 比）
+ 离联合解（joint.py）的机位不要太远（中间几帧没有特征点，光靠剪影机位和动作帧会互相抵）。"""
import sys, json, numpy as np
from pathlib import Path
from PIL import Image, ImageDraw
from scipy.optimize import minimize
from scipy.ndimage import distance_transform_edt as edt, uniform_filter, binary_dilation, binary_opening
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import cam_from, m3d, head_pts
from rast import silhouette
from common import orig
from landmarks import OBS
from zones import ZONE, EXCL
W, H = 480, 270
CLIP = 'main_0005'

def poly_mask(polys):
    im = Image.new('L', (W, H), 0); d = ImageDraw.Draw(im)
    for p in polys: d.polygon([(x * W / 1920, y * H / 1080) for x, y in p], fill=255)
    return np.asarray(im) > 0

def masks(i):
    im = np.asarray(orig(i, (W, H))).astype(float); L = im.mean(-1); sat = im.max(-1) - im.min(-1)
    m = uniform_filter(L, 5); sd = np.sqrt(np.maximum(uniform_filter(L * L, 5) - m * m, 0))
    red = (im[..., 0] > 70) & (im[..., 0] > im[..., 1] * 1.8)
    star = binary_dilation((sd > 40) & ~red, iterations=1)
    M = poly_mask([ZONE[i]]) & ~star & ~binary_dilation(red, iterations=1) & ~poly_mask(EXCL.get(i, []))
    yy = np.mgrid[0:H, 0:W][0]; M &= (yy + 0.5) * 4 < 1010
    white = binary_opening((L > 160) & (sat < 35) & M, iterations=1)
    return M, white

def make_obj(i, fov, p_ref, w_lm=1.0, w_sil=1.0, w_ref=0.6):
    M, white = masks(i); Dw = edt(~white); nw = max(white.sum(), 1)
    o = OBS.get(i, {}); names = list(o); O = np.array([o[n] for n in names], float) if names else None
    def obj(q, detail=False):
        f = float(np.clip(q[6], 0, 10)); V = m3d.verts(CLIP, f).astype(np.float64)
        cam = cam_from(list(q[:6]) + [fov]); e_lm = 0.0
        if names:
            hp = head_pts(V); xy, z = m3d.project(np.array([hp[n] for n in names]), cam)
            if (z <= 5).any(): return 1e6
            e_lm = np.hypot(*(xy - O).T).mean()
        s = silhouette(V, cam, W, H) & M
        Ds = edt(~s) if s.any() else np.full(s.shape, 60.0)
        e_sil = ((Dw[s].mean() if s.any() else 60.0) + Ds[white].mean()) * 4 if nw > 30 else 0.0
        iou = (s & white).sum() / max((s | white).sum(), 1)
        e_ref = np.abs((np.array(q[:6]) - np.array(p_ref[:6])) / np.array([10, 10, 10, 5, 5, 5])).sum()
        v = w_lm * e_lm + w_sil * e_sil + w_ref * e_ref * 10
        return (v, e_lm, e_sil, iou) if detail else v
    return obj

def refine(i, p0, f0, fov, p_ref=None, iters=500):
    obj = make_obj(i, fov, p_ref or p0)
    q0 = np.array(list(p0[:6]) + [f0], float)
    simplex = np.array([q0] + [q0 + np.eye(7)[k] * s for k, s in enumerate([6, 6, 6, 3, 3, 3, 0.8])])
    r = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': iters, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex})
    return r.x, obj(r.x, True)

if __name__ == '__main__':
    tiles = []
    for i in range(233, 240):
        M, white = masks(i); o = np.asarray(orig(i, (W, H))).astype(float) * 0.5
        o[M & ~white] = o[M & ~white] * 0.5 + np.array([0, 0, 120]); o[white] = (255, 255, 0)
        tiles.append(Image.fromarray(o.astype(np.uint8)))
    S = Image.new('RGB', (W * 4, H * 2))
    for k, t in enumerate(tiles): S.paste(t, ((k % 4) * W, (k // 4) * H))
    S.save('/tmp/p5fit/eq_masks.png')
