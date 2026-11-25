"""fit241.py 的第二步：240、241 再加大衣左边缘（原版里 Joker 压在左边星纹条上的轮廓，grid.py 量的几行），逐帧 Nelder-Mead 细调机位。
原版机位比只对头 / 枪解出来的更偏向他身后（大衣往左铺开、胸前不往右凸）。用法：python3 fit241b.py [--write]"""
import os
os.environ.setdefault('P5FIT_KEEP', 'gun_main,gun_outline')
import sys, json, numpy as np
from pathlib import Path
from scipy.optimize import minimize
sys.path.insert(0, str(Path(__file__).resolve().parent)); sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import cam_from, m3d
from rast import silhouette
import fit241 as A

EDGE = {240: {500: 199, 600: 148, 700: 140, 800: 121}, 241: {500: 159, 600: 124, 700: 120, 800: 97}}
W, H = 960, 540


def make_obj(i, f):
    hp = A.points(f); V = m3d.verts('main_0005', f).astype(np.float64)
    names = list(A.OBS[i]); P = np.array([hp[n] for n in names]); O = np.array([A.OBS[i][n] for n in names]); w = np.array([A.W[n] for n in names])
    rows = sorted(EDGE[i])
    def obj(q, detail=False):
        cam = cam_from(list(q) + [A.FOV]); xy, z = m3d.project(P, cam)
        if (z <= 5).any(): return 1e6
        e_lm = (np.hypot(*(xy - O).T) * w).sum() / w.sum()
        s = silhouette(V, cam, W, H); e = []
        for y in rows:
            r = s[int(y * H / 1080)]
            x = np.argmax(r) * 1920 / W if r.any() else 1920
            e.append(x - EDGE[i][y])
        e_edge = float(np.sqrt(np.mean(np.square(e))))
        v = e_lm + 0.5 * e_edge
        return (v, e_lm, e_edge, [round(v, 0) for v in e]) if detail else v
    return obj


if __name__ == '__main__':
    p = Path(__file__).with_name('final.json'); fin = json.load(open(p))
    for i in (241, 240):
        f, cam = fin[str(i)]; obj = make_obj(i, f); q0 = np.array(cam[:6])
        print(i, 'start', obj(q0, True), flush=True)
        for _ in range(3):
            simplex = np.array([q0] + [q0 + np.eye(6)[k] * s for k, s in enumerate([8, 8, 8, 4, 4, 4])])
            q0 = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': 500, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex}).x
        print(i, 'end', np.round(q0, 3).tolist(), obj(q0, True), flush=True)
        fin[str(i)] = [f, [round(float(v), 3) for v in q0] + [A.FOV]]
    if '--write' in sys.argv: json.dump(fin, open(p, 'w'), indent=1)
