import sys, json, numpy as np
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent)); sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import refine2 as R
from fitlib import pts3, fit, init_cam, cam_from, m3d
from landmarks import OBS
from scipy.optimize import minimize
i = int(sys.argv[1]); fin = json.load(open(str(Path(__file__).resolve().parent / 'final.json')))
f0, c0 = fin[str(i)]
for fov in (14, 18, 22, 26, 30):
    # 同一视线方向、按 fov 改距离（脸大小不变）
    hp, V = pts3('main_0005', f0)
    C = np.array(c0[:3]); eye = (hp['eyeR'] + hp.get('eyeL', hp['eyeR'])) / 2
    k = np.tan(np.radians(30 / 2)) / np.tan(np.radians(fov / 2))
    p0 = list(eye + (C - eye) * k) + list(c0[3:6])
    obj = R.make_obj(i, fov, p0 + [fov], w_ref=0.0)
    q0 = np.array(p0 + [f0])
    best = None
    for rnd in range(2):
        simplex = np.array([q0] + [q0 + np.eye(7)[j] * s for j, s in enumerate([8 * k, 8 * k, 8 * k, 3, 3, 3, 0.4])])
        r = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': 400, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex}); q0 = r.x
    print(i, fov, np.round(r.x, 2).tolist(), np.round(obj(r.x, True), 3).tolist(), flush=True)
