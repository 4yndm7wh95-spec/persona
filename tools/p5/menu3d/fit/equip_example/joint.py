"""EQUIP 页入场：233–239 每帧一个机位一起解——特征点误差（233、234、239）+ 机位轨迹平滑（二阶差分），fov 基本固定。
动作 = main_0005（手在脸边 → 举过头顶 → 拔枪 → 枪口朝右下）；原版第 i 帧 = main_0005 第 a + 1.09 × (i − 233) 帧。
235–238 没有特征点，先靠平滑插出来，再用剪影细调（refine.py）。"""
import sys, json, numpy as np
from pathlib import Path
from scipy.optimize import least_squares
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import pts3, best_fit, cam_from, m3d
from landmarks import OBS
CLIP = 'main_0005'
IDX = list(range(233, 240))
W = {'eyeR': 1.0, 'eyeL': 0.6, 'earR': 0.8, 'nose': 0.7, 'mouth': 0.8, 'chin': 0.3}

def sched(a=2.5, rate=1.09): return {i: float(np.clip(a + rate * (i - 233), 0, 10)) for i in IDX}

def solve(schedule, lam_pos=1.0, lam_rot=2.0, fov0=30.0, fov_w=20.0, init=None):
    HP = {i: pts3(CLIP, schedule[i])[0] for i in IDX}
    n = len(IDX)
    if init is None:
        p0, _ = best_fit(HP[233], OBS[233], fov=fov0)
        init = np.concatenate([np.tile(p0[:6], n), [fov0]])
    elif isinstance(init, dict):          # 关键帧机位 → 其余帧线性插值（不经过角度回绕）
        ks = sorted(init); P0 = np.array([np.interp(IDX, ks, [init[k][j] for k in ks]) for j in range(6)]).T
        init = np.concatenate([P0.ravel(), [fov0]])
    def res(x):
        fov = x[-1]; P = x[:-1].reshape(n, 6); r = []
        for k, i in enumerate(IDX):
            o = OBS.get(i)
            if not o: continue
            names = [nm for nm in o if nm in HP[i]]
            xy, _ = m3d.project(np.array([HP[i][nm] for nm in names]), cam_from(list(P[k]) + [fov]))
            for j, nm in enumerate(names): r += list((xy[j] - np.array(o[nm])) * W[nm])
        d2 = P[2:] - 2 * P[1:-1] + P[:-2]
        r += list((d2[:, :3] * lam_pos).ravel()) + list((d2[:, 3:] * lam_rot).ravel())
        r.append((fov - fov0) * fov_w)
        return np.array(r)
    sol = least_squares(res, init, x_scale=np.concatenate([np.tile([10, 10, 10, 5, 5, 5], n), [3]]), max_nfev=4000)
    x = sol.x; fov = x[-1]; P = x[:-1].reshape(n, 6); errs = {}
    for k, i in enumerate(IDX):
        o = OBS.get(i)
        if not o: continue
        names = list(o)
        xy, _ = m3d.project(np.array([HP[i][nm] for nm in names]), cam_from(list(P[k]) + [fov]))
        errs[i] = np.hypot(*(xy - np.array([o[nm] for nm in names])).T).round(1).tolist()
    return {i: [float(v) for v in P[k]] + [float(fov)] for k, i in enumerate(IDX)}, errs

if __name__ == '__main__':
    out = {}
    for a in (1.5, 2.5, 3.5):
        for fov0 in (30.0, 40.0):
            s = sched(a); cams, errs = solve(s, fov0=fov0)
            print(f'a {a} fov {fov0}:', {i: errs[i] for i in errs})
            out[f'{a}_{fov0}'] = {'sched': s, 'cams': cams}
    json.dump(out, open('/tmp/p5fit/eq_joint.json', 'w'))
