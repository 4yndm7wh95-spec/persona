"""牌组收尾「240」第三版（用户 2026-11-24：3D 最后一帧左下角明显比立绘空——立绘的大衣下摆铺到画面左边，3D 身子窄、偏右）。
fit240b.py 只对了耳 / 眼 + 枪的几个点，身子没管。这一版：耳 / 眼（权重高）+ 身子剪影（原版 x < 1000，双向倒角距离）+ 枪口 / 击锤（权重低，立绘的枪是夸张画法），
机位 6 个自由度 + fov 一起解，动作帧 main_0005 第 9.0 帧。用法：python3 fit240c.py → 打印机位和各项误差。"""
import os
os.environ.setdefault('P5FIT_KEEP', 'gun_main,gun_outline')
import sys, json, numpy as np
from pathlib import Path
from scipy.optimize import minimize
from scipy.ndimage import distance_transform_edt as edt
sys.path.insert(0, str(Path(__file__).resolve().parent)); sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import cam_from, m3d, head_pts
from rast import silhouette
import fit240 as F1, fit240b as F2

W, H = 480, 270
F = 9.0

def make_obj(w_lm=3.0, w_sil=1.0, w_gun=0.3):
    T = F1.target(); BODY = np.zeros((H, W), bool); BODY[:, :int(1000 * W / 1920)] = True
    Tb = T & BODY; Dt = edt(~Tb)
    V = m3d.verts('main_0005', F).astype(np.float64)
    hp = head_pts(V)
    for k, i in F2.GUN.items(): hp[k] = V[i]
    head = ['earR', 'eyeR']; gun = ['back', 'muzzle']
    Ph = np.array([hp[n] for n in head]); Oh = np.array([F2.OBS[n] for n in head])
    Pg = np.array([hp[n] for n in gun]); Og = np.array([F2.OBS[n] for n in gun])
    def obj(q, detail=False):
        cam = cam_from(list(q[:6]) + [float(q[6])])
        xy, z = m3d.project(Ph, cam)
        if (z <= 5).any() or not (15 < q[6] < 70): return 1e6
        e_lm = np.hypot(*(xy - Oh).T).mean()
        xg, _ = m3d.project(Pg, cam); e_gun = np.hypot(*(xg - Og).T).mean()
        s = silhouette(V, cam, W, H) & BODY
        if not s.any(): return 1e6
        Ds = edt(~s)
        e_sil = (Dt[s].mean() + Ds[Tb].mean()) * 4
        iou = (s & Tb).sum() / max((s | Tb).sum(), 1)
        v = w_lm * e_lm + w_sil * e_sil + w_gun * e_gun
        return (v, e_lm, e_sil, e_gun, iou) if detail else v
    return obj

if __name__ == '__main__':
    fin = json.load(open(Path(__file__).with_name('final.json')))
    q0 = np.array(fin['240'][1], float)
    obj = make_obj()
    print('start', np.round(obj(q0, True), 2).tolist(), flush=True)
    for rnd in range(3):
        simplex = np.array([q0] + [q0 + np.eye(7)[k] * s for k, s in enumerate([8, 8, 8, 4, 4, 4, 4])])
        r = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': 600, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex}); q0 = r.x
        print('round', rnd, np.round(q0, 3).tolist(), np.round(obj(q0, True), 2).tolist(), flush=True)
    print('final', json.dumps([round(float(v), 3) for v in q0]))
