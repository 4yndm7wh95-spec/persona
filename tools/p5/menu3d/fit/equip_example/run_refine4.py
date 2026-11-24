import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent)); sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import refine2 as R
import numpy as np
from scipy.optimize import minimize
i = int(sys.argv[1]); starts = [float(x) for x in sys.argv[2].split(',')]; wref = float(sys.argv[3])
fin = json.load(open(str(Path(__file__).resolve().parent / 'final.json')))
cam = fin[str(i)][1]
obj = R.make_obj(i, cam[6], cam, w_ref=wref)
best = None
for f0 in starts:
    q0 = np.array(list(cam[:6]) + [f0], float)
    print('start', f0, obj(q0, True), flush=True)
    for rnd in range(2):
        simplex = np.array([q0] + [q0 + np.eye(7)[k] * s for k, s in enumerate([8, 8, 8, 3, 3, 3, 0.5])])
        r = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': 450, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex})
        q0 = r.x
    d = obj(r.x, True); print('end', f0, np.round(r.x, 2).tolist(), d, flush=True)
    if best is None or d[0] < best[1][0]: best = (r.x, d)
json.dump({'i': i, 'q': [float(v) for v in best[0]], 'fov': cam[6], 'det': [float(x) for x in best[1]]}, open(f'/tmp/p5fit/eq_r4_{i}.json', 'w'))
