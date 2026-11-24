import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent)); sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import refine as R
import numpy as np
from scipy.optimize import minimize
i = int(sys.argv[1]); wref = float(sys.argv[2]); wlm = float(sys.argv[3]); src = sys.argv[4]; tag = sys.argv[5]
d = json.load(open(src)); cam = d['cams'][str(i)]; f0 = d['sched'][str(i)]
obj = R.make_obj(i, cam[6], cam, w_lm=wlm, w_ref=wref)
q0 = np.array(list(cam[:6]) + [f0], float)
print('start', obj(q0, True), flush=True)
best = None
for rnd in range(3):
    simplex = np.array([q0] + [q0 + np.eye(7)[k] * s for k, s in enumerate([8, 8, 8, 3, 3, 3, 0.6])])
    r = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': 500, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex})
    q0 = r.x
    print('round', rnd, obj(r.x, True), flush=True)
json.dump({'i': i, 'q': [float(v) for v in r.x], 'fov': cam[6], 'det': [float(x) for x in obj(r.x, True)]}, open(f'/tmp/p5fit/eq_{tag}_{i}.json', 'w'))
