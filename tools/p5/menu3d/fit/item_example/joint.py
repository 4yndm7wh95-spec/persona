"""ITEM 页入场（tools/p5/menu3d/item_entrance.py 用的就是这个结果：off −0.5、平滑 (1, 2)、fov 30）：177–185 每帧一个机位，一起解——特征点误差 + 机位轨迹平滑（二阶差分），fov 基本固定。
动作按 30fps 实时播放：原版第 i 帧 = main_0010 第 1.09 × (i − 177) 帧（录像 27.5fps）。183 没有特征点，靠平滑项插出来。"""
import sys, json, numpy as np
from pathlib import Path
from scipy.optimize import least_squares
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import pts3, best_fit, cam_from, m3d
from landmarks import OBS
IDX = list(range(177, 186))
W = {'eyeR': 1.0, 'eyeL': 1.0, 'earR': 0.8, 'nose': 1.0, 'mouth': 0.6, 'chin': 0.25}   # 下巴在低头侧脸里不好量，权重低

def clip_frame(i, off=0.0): return float(np.clip(1.09 * (i - 177) + off, 0, 10))

# 原版这一段动作快慢不均匀（手 181 还伸在右边画外、182 张开在右边、183 已经扫到脸前）：按逐帧叠原版挑的动作帧（hand_cands*.jpg）
SCHEDULE = {177: 0.0, 178: 0.59, 179: 1.68, 180: 2.77, 181: 3.4, 182: 4.6, 183: 7.0, 184: 7.13, 185: 8.22}

def solve(off=0.0, lam_pos=2.0, lam_rot=4.0, fov0=40.0, init=None, fov_w=20.0, schedule=None):
    fr = (lambda i: schedule[i]) if schedule else (lambda i: clip_frame(i, off))
    HP = {i: pts3('main_0010', fr(i))[0] for i in IDX}
    n = len(IDX)
    if init is None:
        p_end, _ = best_fit(HP[185], OBS[185], fov=fov0)
        init = np.concatenate([np.tile(p_end[:6], n), [fov0]])
    def res(x):
        fov = x[-1]; P = x[:-1].reshape(n, 6); r = []
        for k, i in enumerate(IDX):
            o = OBS.get(i)
            if not o: continue
            cam = cam_from(list(P[k]) + [fov])
            names = [nm for nm in o if nm in HP[i]]
            xy, z = m3d.project(np.array([HP[i][nm] for nm in names]), cam)
            for j, nm in enumerate(names): r += list((xy[j] - np.array(o[nm])) * W[nm])
        d2 = P[2:] - 2 * P[1:-1] + P[:-2]
        r += list((d2[:, :3] * lam_pos).ravel()) + list((d2[:, 3:] * lam_rot).ravel())
        r.append((fov - fov0) * fov_w)        # fov 基本固定（只靠特征点定不住，会塌成长焦）
        return np.array(r)
    sol = least_squares(res, init, x_scale=np.concatenate([np.tile([10, 10, 10, 5, 5, 5], n), [3]]), max_nfev=4000)
    x = sol.x; fov = x[-1]; P = x[:-1].reshape(n, 6)
    errs = {}
    for k, i in enumerate(IDX):
        o = OBS.get(i)
        if not o: continue
        cam = cam_from(list(P[k]) + [fov]); names = list(o)
        xy, _ = m3d.project(np.array([HP[i][nm] for nm in names]), cam)
        errs[i] = np.hypot(*(xy - np.array([o[nm] for nm in names])).T).round(1).tolist()
    return {i: [float(v) for v in P[k]] + [float(fov)] for k, i in enumerate(IDX)}, errs, sol.x

if __name__ == '__main__':
    out = {}
    for off in (-0.5, 0.0, 0.5):
        for lp, lr in ((1.0, 2.0),):
            cams, errs, _ = solve(off, lp, lr)
            print(f'off {off} smooth ({lp},{lr}) fov {list(cams.values())[0][-1]:.1f}')
            for i in IDX: print('  ', i, 'f%.2f' % clip_frame(i, off), np.round(cams[i][:6], 1).tolist(), errs.get(i))
            out[f'{off}_{lp}'] = cams
    json.dump(out, open('/tmp/p5fit/item_joint.json', 'w'))
