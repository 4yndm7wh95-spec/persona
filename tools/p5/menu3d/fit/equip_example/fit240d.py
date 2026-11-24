"""牌组收尾「240」第四版（用户 2026-11-24：角度压根不对——原版立绘里左手完全看不见，3D 里左手从大衣和白条之间露出来；要大改角度）。
目标 = 全身剪影双向倒角距离（立绘 alpha；x > 1000 的手臂 / 枪权重减半，立绘的枪是夸张画法）+ 右耳 / 右眼 + 左手凸出身体轮廓的像素（要 0）。
搜索：当前机位绕头（世界 y 轴）转 −60…+60°、绕相机 x 轴 −25…+25°，每个起点粗调，最好的几个再细调；fov 一起解。动作帧 main_0005 第 9.0 帧。
用法：python3 fit240d.py → 打印最好的机位，写进 final.json 的 "240" 之后再跑 coat240.py、runtime_keys.py。"""
import os
os.environ.setdefault('P5FIT_KEEP', 'gun_main,gun_outline')
import sys, json, numpy as np
from pathlib import Path
from PIL import Image, ImageDraw
from scipy.optimize import minimize
from scipy.ndimage import distance_transform_edt as edt
sys.path.insert(0, str(Path(__file__).resolve().parent)); sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import cam_from, m3d, head_pts
from scipy.spatial import cKDTree
import rast
import fit240 as F1, fit240b as F2

F = 9.0
V = m3d.verts('main_0005', F).astype(np.float64)
hs = m3d.mesh_slice('c0001_hand_051B_UI_main')
_d, _ = cKDTree(V[m3d.mesh_slice('gun_main')]).query(V[hs])
LEFT = np.zeros(len(V), bool); LEFT[np.arange(hs.start, hs.stop)[_d >= 6]] = True
TL = rast.TRIS[LEFT[rast.TRIS].all(1)]; TR = rast.TRIS[~LEFT[rast.TRIS].any(1)]
HP = head_pts(V); HEAD = ['earR', 'eyeR']
PH = np.array([HP[n] for n in HEAD]); OH = np.array([F2.OBS[n] for n in HEAD])
CTR = V[m3d.mesh_slice('c0001_faceUI_main', 0)].mean(0)


def raster(tris, xy, ok, W, H):
    im = Image.new('L', (W, H), 0); d = ImageDraw.Draw(im)
    for a, b, c in tris:
        if ok[a] and ok[b] and ok[c]: d.polygon([tuple(xy[a]), tuple(xy[b]), tuple(xy[c])], fill=255)
    return np.asarray(im) > 0


def make_obj(W, H, w_lm=2.0, w_lh=0.5):
    F1.W, F1.H = W, H
    T = F1.target(); Dt = edt(~T); wx = np.ones((H, W)); wx[:, int(1000 * W / 1920):] = 0.5
    def obj(q, detail=False):
        if not (15 < q[6] < 70): return 1e6
        cam = cam_from(list(q[:6]) + [float(q[6])])
        xy, z = m3d.project(V, cam); ok = z > 1
        if (m3d.project(PH, cam)[1] <= 5).any(): return 1e6
        xs = xy * (W / 1920.0)
        rest = raster(TR, xs, ok, W, H); lh = raster(TL, xs, ok, W, H); s = rest | lh
        if not s.any(): return 1e6
        Ds = edt(~s)
        e_sil = ((Dt * wx)[s].mean() + (Ds * wx)[T].mean()) * (1920 / W)
        e_lm = np.hypot(*(m3d.project(PH, cam)[0] - OH).T).mean()
        e_lh = (lh & ~rest).sum() * (1920 / W) ** 2 / 100          # 百像素
        iou = (s & T).sum() / max((s | T).sum(), 1)
        v = e_sil + w_lm * e_lm + w_lh * e_lh
        return (v, e_sil, e_lm, e_lh, iou) if detail else v
    return obj


def orbit(cam, yaw, pitch):
    """机位绕头中心转：先绕世界 y 轴 yaw，再绕相机 x 轴 pitch（度）"""
    R = m3d.rot_yxz(cam[3:6]); p = np.array(cam[:3], float)
    def rot(axis, a):
        axis = axis / np.linalg.norm(axis); a = np.radians(a); K = np.array([[0, -axis[2], axis[1]], [axis[2], 0, -axis[0]], [-axis[1], axis[0], 0]])
        return np.eye(3) + np.sin(a) * K + (1 - np.cos(a)) * K @ K
    M = rot(R[:, 0], pitch) @ rot(np.array([0, 1.0, 0]), yaw)
    p2 = CTR + M @ (p - CTR); R2 = M @ R
    return list(p2) + list(m3d.euler_yxz(R2)) + [cam[6]]


def nm(obj, q0, it, steps=(6, 6, 6, 3, 3, 3, 3)):
    simplex = np.array([q0] + [q0 + np.eye(7)[k] * s for k, s in enumerate(steps)])
    r = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': it, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex})
    return r.x, r.fun


if __name__ == '__main__':
    fin = json.load(open(Path(__file__).with_name('final.json')))
    base = fin['240'][1]
    small = make_obj(240, 135)
    print('current', np.round(small(np.array(base), True), 2).tolist(), flush=True)
    cands = []
    for yaw in range(-60, 61, 15):
        for pitch in (-25, -12.5, 0, 12.5, 25):
            q0 = np.array(orbit(base, yaw, pitch))
            q, f = nm(small, q0, 120)
            cands.append((f, yaw, pitch, q)); print(yaw, pitch, round(f, 1), flush=True)
    cands.sort(key=lambda c: c[0])
    big = make_obj(480, 270); best = None
    for f, yaw, pitch, q in cands[:4]:
        for _ in range(2): q, fv = nm(big, q, 400, (3, 3, 3, 1.5, 1.5, 1.5, 1.5))
        d = big(q, True); print('refined', yaw, pitch, np.round(q, 3).tolist(), np.round(d, 2).tolist(), flush=True)
        if best is None or d[0] < best[1][0]: best = (q, d)
    print('best', json.dumps([round(float(v), 3) for v in best[0]]), np.round(best[1], 2).tolist())
