import json, numpy as np
from scipy.optimize import least_squares
from spiral import integrate, nearest
PTS = [(1,385),(19,376),(37,368),(44,366),(54,362),(65,358),(81,353),(91,350),(102,348),(114,346),(133,342),(144,341),(151,340),(157,340),(163,340),(182,339),(188,339),(235,340),(241,340),(247,340),(253,341),(265,342),(271,342),(283,344),(289,345),(295,346),(301,346),(307,348),(313,348),(319,350),(325,351),(331,352),(337,353)]
REF = np.array(PTS, float); NOTCH = np.array([352.0, 357.0])
def build(p, back=0):
    y0, th0, L, k0, k1 = p
    # starts at the left edge (x=0); curvature linear in s (a clothoid); 'back' extends it backwards off-screen
    if back:
        s, x, y, th, k = integrate(0, y0, th0 + np.pi, back, lambda s: -(k0 - k1 * s), n=2000)
        return s, x, y, th - np.pi, k
    return integrate(0, y0, th0, L, lambda s: k0 + k1 * s)
def res(p):
    s, x, y, th, k = build(p); d, _ = nearest(REF, x, y)
    return np.concatenate([d, (np.array([x[-1], y[-1]]) - NOTCH) * 3])
D = np.radians
r = least_squares(res, [386, D(-27), 370, D(0.13), 0.0], x_scale='jac', max_nfev=2000)
p = r.x; s, x, y, th, k = build(p); d, _ = nearest(REF, x, y)
print('rms %.2f max %.2f  end (%.1f,%.1f) heading %.1f->%.1f  k %.3f..%.3f deg/px' % (np.sqrt((d**2).mean()), d.max(), x[-1], y[-1], np.degrees(th[0]), np.degrees(th[-1]), np.degrees(k[0]), np.degrees(k[-1])))
json.dump({'p': [float(v) for v in p]}, open('outer_fit.json', 'w'))
