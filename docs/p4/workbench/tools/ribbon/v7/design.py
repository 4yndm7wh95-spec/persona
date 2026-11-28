"""Ribbon design: inner edge B0 (fitted spiral), total width D(s) (monotone, smooth), stripe boundaries as smoothly blended
shares of D, the barbed arrowhead, the two thin yellow lines and the decorations."""
import json, numpy as np
from geom import inner
from spiral import unimodal2
def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * t * (t * (6 * t - 15) + 10)
s, x, y, th, k = inner(6000)
NX, NY = np.sin(th), -np.cos(th)
l0, g0, g1, gp, c, wr, gt, wf = json.load(open('width_fit.json'))['p']
g = unimodal2(g0, g1, gp, c, wr, gt, wf)(s)
D = np.exp(l0 - np.concatenate([[0], np.cumsum((g[1:] + g[:-1]) / 2 * np.diff(s))]))
NEAR = np.array([0.11, 0.23, 0.50, 0.74, 0.83, 0.91, 1.0])
FAR = np.array([0.27, 0.42, 0.60, 0.67, 0.78, 0.90, 1.0])
END = np.array([0.0, 0.0, 0.62, 0.68, 0.80, 0.90, 1.0])
b1 = smoothstep(1150, 1290, s)[:, None]; b2 = smoothstep(1270, 1360, s)[:, None]
F = NEAR * (1 - b1) + FAR * b1; F = F * (1 - b2) + END * b2
OFF = F * D[:, None]
SN = 1365.0
np.savez('design_v2.npz', s=s, x=x, y=y, NX=NX, NY=NY, D=D, OFF=OFF, SN=SN)
