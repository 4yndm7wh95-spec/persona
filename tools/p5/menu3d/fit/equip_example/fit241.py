"""牌组入场收尾（用户 2026-11-24 第五轮：原版 240、241 还是 3D，姿势已经和立绘几乎一样——侧身、手臂水平伸出、枪口朝右下，左臂 / 手肘完全藏在身后；
242 才硬切立绘、枪口闪一下）。之前把立绘当 240 拟合、整段机位往那边搬，角度不对。这一版直接对原版逐帧：
- 233–238 用原版拟合的机位（final.json）不动；
- 239（甩枪途中）、240、241 一起解：原版里量的右耳 / 右眼 + 240、241 的枪口 / 握把底（手），机位轨迹平滑（接 237、238），fov 和前面一样 30。
动作帧：239 在 8.3–8.7 里挑，240、241 = 9.0（main_0005 最后的姿势）。
结果写进 final.json 的 "239" "240" "241"。用法：python3 fit241.py"""
import sys, json, numpy as np
from pathlib import Path
from scipy.optimize import least_squares
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import cam_from, m3d, head_pts

FOV = 30.001191447421107
# 原版屏幕坐标（grid.py 放大量的）
OBS = {
    239: {'earR': (426, 262), 'eyeR': (529, 257)},
    240: {'earR': (386, 232), 'eyeR': (496, 240), 'muzzle': (1858, 935), 'grip': (1204, 974)},
    241: {'earR': (364, 225), 'eyeR': (469, 233), 'muzzle': (1825, 886), 'grip': (1138, 937)},
}
W = {'earR': 1.0, 'eyeR': 1.5, 'muzzle': 0.5, 'grip': 0.4}


def points(f):
    V = m3d.verts('main_0005', f).astype(np.float64)
    hp = head_pts(V)
    G = V[m3d.mesh_slice('gun_main')]; c = G.mean(0)
    _, _, vt = np.linalg.svd(G - c); a, b = vt[0], vt[1]
    H = V[m3d.mesh_slice('c0001_hand_051B_UI_main')]
    from scipy.spatial import cKDTree
    d, _ = cKDTree(G).query(H); hc = H[d < 6].mean(0)
    if (hc - c) @ a > 0: a = -a                       # a 指向枪口（离手远的一头）
    if (hc - c) @ b < 0: b = -b                       # b 指向握把
    pa = (G - c) @ a; hp['muzzle'] = G[pa > np.percentile(pa, 98.5)].mean(0)
    pb = (G - c) @ b; hp['grip'] = G[pb > np.percentile(pb, 99)].mean(0)
    return hp


def solve(f239, fin):
    idx = [239, 240, 241]; fr = {239: f239, 240: 9.0, 241: 9.0}
    HP = {i: points(fr[i]) for i in idx}
    fixed = [np.array(fin['237'][1][:6]), np.array(fin['238'][1][:6])]
    def res(x):
        P = x.reshape(3, 6); r = []
        for k, i in enumerate(idx):
            names = list(OBS[i]); cam = cam_from(list(P[k]) + [FOV])
            xy, z = m3d.project(np.array([HP[i][n] for n in names]), cam)
            for j, n in enumerate(names): r += list((xy[j] - np.array(OBS[i][n])) * W[n])
        allp = np.array(fixed + list(P))
        d2 = allp[2:] - 2 * allp[1:-1] + allp[:-2]
        r += list((d2[:, :3] * 0.6).ravel()) + list((d2[:, 3:] * 1.2).ravel())
        return np.array(r)
    x0 = np.tile(np.array(fin['239'][1][:6]), 3)
    s = least_squares(res, x0, x_scale=np.tile([10, 10, 10, 5, 5, 5], 3), max_nfev=6000)
    P = s.x.reshape(3, 6); errs = {}
    for k, i in enumerate(idx):
        names = list(OBS[i]); xy, _ = m3d.project(np.array([HP[i][n] for n in names]), cam_from(list(P[k]) + [FOV]))
        errs[i] = dict(zip(names, np.hypot(*(xy - np.array([OBS[i][n] for n in names])).T).round(1).tolist()))
    return {i: [fr[i], [round(float(v), 3) for v in P[k]] + [FOV]] for k, i in enumerate(idx)}, errs, s.cost


if __name__ == '__main__':
    p = Path(__file__).with_name('final.json'); fin = json.load(open(p))
    best = None
    for f in (8.3, 8.4, 8.5, 8.6, 8.7):
        keys, errs, cost = solve(f, fin)
        print(f, round(cost), errs)
        if best is None or cost < best[2]: best = (keys, errs, cost)
    print(json.dumps(best[0]))
    if '--write' in sys.argv:
        for i, v in best[0].items(): fin[str(i)] = v
        json.dump(fin, open(p, 'w'), indent=1)
