import sys, json, numpy as np
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent)); sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import refine as R
from scipy.optimize import minimize
import m3d
i = int(sys.argv[1]); starts = [float(x) for x in sys.argv[2].split(',')]
cam0 = sys.argv[3] if len(sys.argv) > 3 else None
if cam0: c = json.loads(cam0)
else:
    rot = np.array([0.0, -10.0, 0.0]); Rm = m3d.rot_yxz(rot)
    pos = np.array([-12.0, 158.0, 75.0]) + Rm[:, 0] * -14.0
    c = list(pos) + list(rot) + [44.0]
obj = R.make_obj(i, c[6], c, w_ref=0.03)
best = None
for f0 in starts:
    q0 = np.array(list(c[:6]) + [f0], float)
    print('start', f0, obj(q0, True), flush=True)
    for rnd in range(3):
        simplex = np.array([q0] + [q0 + np.eye(7)[k] * s for k, s in enumerate([8, 8, 8, 3, 3, 3, 0.6])])
        r = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': 450, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex}); q0 = r.x
    d = obj(r.x, True); print('end', f0, np.round(r.x, 3).tolist(), d, flush=True)
    if best is None or d[0] < best[1][0]: best = (r.x, d)
json.dump({'i': i, 'q': [float(v) for v in best[0]], 'fov': c[6], 'det': [float(x) for x in best[1]]}, open(f'/tmp/p5fit/top_{i}.json', 'w'))
