"""Rebuild the fitted inner edge B0 and the outer top edge from their parameters."""
import json, numpy as np
from spiral import integrate, unimodal2
def inner(n=4000):
    J = json.load(open('inner_fit.json')); y0, th0, L, ka0, ka1, kp, sp, wr, kt, wf = J['p']
    return integrate(J['X0'], y0, th0, L, unimodal2(ka0, ka1, kp, sp, wr, kt, wf), n)
def outer(n=2000, back=0):
    y0, th0, L, k0, k1 = json.load(open('outer_fit.json'))['p']
    s, x, y, th, k = integrate(0, y0, th0, L, lambda s: k0 + k1 * s, n)
    if back:  # continue backwards (off-screen, down the left side) with the curvature law extended
        sb, xb, yb, thb, kb = integrate(0, y0, th0 + np.pi, back, lambda q: -(k0 - k1 * q), n)
        x = np.concatenate([xb[::-1], x[1:]]); y = np.concatenate([yb[::-1], y[1:]])
        th = np.concatenate([thb[::-1] - np.pi, th[1:]]); s = np.concatenate([-sb[::-1], s[1:]])
    return s, x, y, th
