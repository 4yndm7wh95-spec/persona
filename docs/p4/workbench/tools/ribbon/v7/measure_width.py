"""Measure the ribbon's total width along B0's outward normals: where each normal meets the (fitted) outer top edge.
Writes D_raw.npy ([s, D] rows, NaN where the normal misses). Run after fit_inner.py and fit_outer.py."""
import numpy as np
from geom import inner, outer
s, x, y, th, k = inner()
so, xo, yo, tho = outer(back=900)
NX, NY = np.sin(th), -np.cos(th)
A = np.stack([xo[:-1], yo[:-1]], 1); B = np.stack([xo[1:], yo[1:]], 1)
D = np.full(len(s), np.nan)
for i in range(len(s)):
    P = np.array([x[i], y[i]]); n = np.array([NX[i], NY[i]]); e = B - A; w = A - P
    den = n[0] * e[:, 1] - n[1] * e[:, 0]; ok = np.abs(den) > 1e-9
    t = np.where(ok, (w[:, 0] * e[:, 1] - w[:, 1] * e[:, 0]) / np.where(ok, den, 1), np.nan)
    u = np.where(ok, (w[:, 0] * n[1] - w[:, 1] * n[0]) / np.where(ok, den, 1), np.nan)
    m = ok & (t > 0) & (u >= 0) & (u <= 1)
    if m.any(): D[i] = t[m].min()
np.save('D_raw.npy', np.stack([s, D], 1))
