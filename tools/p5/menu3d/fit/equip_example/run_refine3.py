"""细调一帧：参考机位 p_ref（不要偏太远）+ 动作帧夹在 [lo, hi] 里。"""
import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent)); sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import refine as R
import numpy as np
from scipy.optimize import minimize
a = json.loads(sys.argv[1])
i, wref, wlm, lo, hi, tag = a['i'], a['wref'], a.get('wlm', 1.0), a['lo'], a['hi'], a['tag']
p0, f0, pref = a['p0'], a['f0'], a['ref']
base = R.make_obj(i, p0[6], pref, w_lm=wlm, w_ref=wref)
def obj(q, detail=False):
    pen = 200 * (max(0, lo - q[6]) + max(0, q[6] - hi))
    r = base(np.r_[q[:6], np.clip(q[6], lo, hi)], detail)
    return (r[0] + pen,) + tuple(r[1:]) if detail else r + pen
q0 = np.array(list(p0[:6]) + [f0], float)
print('start', obj(q0, True), flush=True)
for rnd in range(3):
    simplex = np.array([q0] + [q0 + np.eye(7)[k] * s for k, s in enumerate([8, 8, 8, 3, 3, 3, 0.4])])
    r = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': 500, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex})
    q0 = r.x; print('round', rnd, obj(r.x, True), flush=True)
q0[6] = float(np.clip(q0[6], lo, hi))
json.dump({'i': i, 'q': [float(v) for v in q0], 'fov': p0[6], 'det': [float(x) for x in obj(q0, True)]}, open(f'/tmp/p5fit/eq_{tag}_{i}.json', 'w'))
