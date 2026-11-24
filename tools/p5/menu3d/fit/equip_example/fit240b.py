"""牌组入场收尾「240」第二版（用户 2026-11-24：3D 最后一帧枪和视线都朝下，立绘是抬眼看玩家、手臂水平伸出、枪口朝右下）。
上一版（fit240.py）只对了头 / 耳 / 眼 + 身子剪影，枪的方向靠挑动作帧，结果手臂和枪收在身前、头低着。
这一版把枪也当特征点：立绘（CAMPEQUIP_2D01，0.4996 倍贴左上角，原版坐标，跟我们的排版只差 MapK）上量的
右耳、右眼、枪的后端（击锤）、枪口、握把底，和模型上对应的顶点一起解机位 + 动作帧（main_0005 8.2–8.8），fov 可以变（立绘的枪是近大远小的夸张透视）。
用法：python3 fit240b.py → 打印 [动作帧, 机位]、各点误差，写进 final.json 的 "240"。"""
import sys, json, numpy as np
from pathlib import Path
from scipy.optimize import least_squares
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import cam_from, m3d, head_pts

CLIP = 'main_0005'
GUN = {'muzzle': 16643, 'back': 19903, 'grip': 20462}       # gun_main 的顶点：主轴两端（远离手的是枪口）、握把底
OBS = {'earR': (356.0, 231.0), 'eyeR': (465.0, 229.0),
       'back': (1250.0, 598.0), 'muzzle': (1835.0, 840.0), 'grip': (1150.0, 905.0)}
W = {'earR': 1.0, 'eyeR': 1.5, 'back': 0.5, 'muzzle': 0.35, 'grip': 0.3}


def points(f):
    V = m3d.verts(CLIP, f).astype(np.float64)
    hp = head_pts(V)
    for k, i in GUN.items(): hp[k] = V[i]
    return hp


def solve(f, p0, fov0=30.0, fov_free=True):
    hp = points(f); names = list(OBS)
    P = np.array([hp[n] for n in names]); O = np.array([OBS[n] for n in names]); w = np.array([W[n] for n in names])
    def res(q):
        fov = q[6] if fov_free else fov0
        xy, z = m3d.project(P, cam_from(list(q[:6]) + [fov]))
        r = ((xy - O) * w[:, None]).ravel()
        return np.concatenate([r, [(fov - fov0) * 3.0], np.minimum(z - 10, 0) * 100])
    x0 = np.array(list(p0[:6]) + [fov0])
    s = least_squares(res, x0, x_scale=[10, 10, 10, 5, 5, 5, 3], max_nfev=3000)
    q = s.x; fov = q[6] if fov_free else fov0
    xy, _ = m3d.project(P, cam_from(list(q[:6]) + [fov]))
    return [round(float(v), 3) for v in q[:6]] + [round(float(fov), 3)], dict(zip(names, np.hypot(*(xy - O).T).round(1).tolist())), float((res(q) ** 2).sum())


if __name__ == '__main__':
    fin = json.load(open(Path(__file__).with_name('final.json')))
    starts = [fin['240'][1], fin['239'][1]]
    best = None
    for f in np.arange(8.2, 10.01, 0.1):
        for p0 in starts:
            cam, err, cost = solve(float(f), p0)
            if best is None or cost < best[3]: best = (round(float(f), 2), cam, err, cost)
        print(round(float(f), 2), cam, err, round(cost))
    print('best', json.dumps(best[:2]), best[2])
