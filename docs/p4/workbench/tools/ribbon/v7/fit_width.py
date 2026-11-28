"""Total ribbon width D(s): ln D = lnD0 - integral of g, with g = unimodal2(...) >= 0 (so D only ever shrinks towards the far end)."""
import json, numpy as np
from scipy.optimize import least_squares
from spiral import unimodal2
from geom import inner
s, x, y, th, k = inner(6000)
raw = np.load('D_raw.npy'); m = (raw[:, 0] >= 1212) & ~np.isnan(raw[:, 1])
TS = np.concatenate([[0, 300, 650, 940, 1118, 1170], raw[m, 0][::6]])
TL = np.log(np.concatenate([[840, 780, 680, 560, 420, 315], raw[m, 1][::6]]))
TW = np.concatenate([[0.6, 0.6, 0.8, 0.8, 1.5, 0.8], np.full(len(TS) - 6, 6.0)])
def lnD(p, ss):
    l0, g0, g1, gp, c, wr, gt, wf = p
    g = unimodal2(g0, g1, gp, c, wr, gt, wf)(ss)
    return l0 - np.concatenate([[0], np.cumsum((g[1:] + g[:-1]) / 2 * np.diff(ss))])
def res(p): return (np.interp(TS, s, lnD(p, s)) - TL) * TW
p0 = [np.log(840), 0.0002, 0.002, 0.035, 1220, 60, 0.013, 40]
lo = [np.log(600), 0, 0, 0.005, 1150, 15, 0.0, 15]; hi = [np.log(1200), 0.002, 0.02, 0.08, 1280, 300, 0.05, 300]
r = least_squares(res, p0, bounds=(lo, hi), x_scale='jac', max_nfev=4000)
p = r.x; L = lnD(p, s); D = np.exp(L)
e = np.interp(TS, s, L) - TL
print('fit err (ln):', np.round(e[:6], 3), ' far rms px: %.2f' % np.sqrt(((np.exp(np.interp(TS[6:], s, L)) - np.exp(TL[6:])) ** 2).mean()))
print('params', [round(float(q), 5) for q in p])
print('monotone:', bool((np.diff(L) <= 1e-12).all()), ' D at', [(q, round(float(np.interp(q, s, D)), 1)) for q in (0, 300, 650, 940, 1118, 1170, 1200, 1226, 1262, 1300, 1365, 1442)])
json.dump({'p': [float(q) for q in p]}, open('width_fit.json', 'w'))
