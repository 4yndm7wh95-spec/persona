"""ITEM 页入场逐帧细调：机位 6 个自由度 + 动作帧（连续），目标 = 脸部特征点误差 + 黑区里白色身体 / 手臂的轮廓（倒角距离）。
原版里 Joker 在黑区里是白的（反色），在星纹底上是黑的：比较只在「不是星纹、不是红底」的地方做（那里原版白 = Joker）。"""
import sys, json, numpy as np
from pathlib import Path
from scipy.optimize import minimize
from scipy.ndimage import distance_transform_edt as edt, uniform_filter, binary_dilation
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import pts3, cam_from, m3d, head_pts
from rast import silhouette
from common import orig
from landmarks import OBS
W, H = 480, 270

SIL_FRAMES = {179, 180, 181, 182, 183, 184, 185}      # 177、178 Joker 整个在星纹底上（黑的），没有白色身体可比

def masks(i):
    im = np.asarray(orig(i, (W, H))).astype(float); L = im.mean(-1); sat = im.max(-1) - im.min(-1)
    m = uniform_filter(L, 5); sd = np.sqrt(np.maximum(uniform_filter(L * L, 5) - m * m, 0))
    red = (im[..., 0] > 70) & (im[..., 0] > im[..., 1] * 1.8)
    star = binary_dilation((sd > 40) & ~red, iterations=1)
    M = ~star & ~binary_dilation(red, iterations=1)
    # 脸（星纹底上也是白的）和 UI 字不算：去掉脸部特征点周围一圈，去掉画面最下面 70px（¥ / 按键提示）
    yy, xx = np.mgrid[0:H, 0:W]; X, Y = (xx + 0.5) * 4, (yy + 0.5) * 4
    o = OBS.get(i, {})
    if o:
        P = np.array(list(o.values()), float); c = P.mean(0); r = max(90.0, np.hypot(*(P - c).T).max() * 1.6)
        M &= np.hypot(X - c[0], Y - c[1]) > r
    M &= Y < 1010
    white = (L > 160) & (sat < 35) & M
    return M, white

def make_obj(i, fov, w_lm=1.0, w_sil=4.0):
    M, white = masks(i); Dw = edt(~white); nw = max(white.sum(), 1)
    o = OBS.get(i, {}); names = list(o); O = np.array([o[n] for n in names], float) if names else None
    def obj(q, detail=False):
        f = float(np.clip(q[6], 0, 10)); V = m3d.verts('main_0010', f).astype(np.float64)
        cam = cam_from(list(q[:6]) + [fov])
        e_lm = 0.0
        if names:
            hp = head_pts(V); xy, z = m3d.project(np.array([hp[n] for n in names]), cam)
            if (z <= 5).any(): return 1e6
            e_lm = np.hypot(*(xy - O).T).mean()
        e_sil = 0.0; iou = None
        if i in SIL_FRAMES and nw > 50:
            s = silhouette(V, cam, W, H) & M
            Ds = edt(~s) if s.any() else np.full(s.shape, 50.0)
            e_sil = ((Dw[s].mean() if s.any() else 50.0) + Ds[white].mean()) * 4      # 1920 像素
            iou = (s & white).sum() / max((s | white).sum(), 1)
        v = w_lm * e_lm + w_sil * e_sil * 0.25
        return (v, e_lm, e_sil, iou) if detail else v
    return obj

def refine(i, p0, f0, fov, iters=400):
    obj = make_obj(i, fov)
    q0 = np.array(list(p0[:6]) + [f0], float)
    simplex = np.array([q0] + [q0 + np.eye(7)[k] * s for k, s in enumerate([4, 4, 4, 2, 2, 2, 0.6])])
    r = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': iters, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex})
    return r.x, obj(r.x, True)

if __name__ == '__main__':
    from joint import solve, clip_frame
    fov = float(sys.argv[1]) if len(sys.argv) > 1 else 30.0
    off = float(sys.argv[2]) if len(sys.argv) > 2 else -0.5
    cams, errs, _ = solve(off, 1.0, 2.0, fov0=fov)
    out = {}
    for i in range(177, 186):
        q, (v, e_lm, e_sil, iou) = refine(i, cams[i], clip_frame(i, off), fov)
        print(i, 'f %.2f' % q[6], 'lm %.1f sil %.1f iou %s' % (e_lm, e_sil, None if iou is None else round(iou, 3)), np.round(q[:6], 2).tolist(), flush=True)
        out[i] = [float(v) for v in q[:6]] + [fov, float(q[6])]
    json.dump(out, open(f'/tmp/p5fit/item_refine_{fov:.0f}.json', 'w'))
