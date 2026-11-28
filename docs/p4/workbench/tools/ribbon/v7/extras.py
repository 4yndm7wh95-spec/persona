"""Arrowhead, thin lines T1 / T2 and decorations, drawn on top of the bands (PIL preview)."""
import numpy as np
Y, O, W_ = (255, 230, 0), (255, 138, 0), (255, 255, 255)
def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * t * (t * (6 * t - 15) + 10)
def at(R, sv):
    s = R['s']; return (np.interp(sv, s, R['x']), np.interp(sv, s, R['y']), np.interp(sv, s, R['NX']), np.interp(sv, s, R['NY']), np.interp(sv, s, R['D']))
def line(d, R, s0, s1, cofn, wfn, col, SS, n=160):
    """a thin line along B0: centre offset cofn(s) px, width wfn(s) px, round caps"""
    sv = np.linspace(s0, s1, n); X, Yv, NX, NY, D = at(R, sv); co = cofn(sv, D); hw = wfn(sv, D) / 2
    pa = list(zip((X + NX * (co - hw)) * SS, (Yv + NY * (co - hw)) * SS)); pb = list(zip((X + NX * (co + hw)) * SS, (Yv + NY * (co + hw)) * SS))[::-1]
    d.polygon(pa + pb, fill=col)
    for j in (0, -1):
        cx, cy, r = (X[j] + NX[j] * co[j]) * SS, (Yv[j] + NY[j] * co[j]) * SS, hw[j] * SS
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)
def star4(d, cx, cy, r, col, SS, rot=0.0, pinch=0.28):
    pts = []
    for i in range(64):
        a = rot + i / 64 * 2 * np.pi
        # astroid-like four-point star: concave sides
        rr = r * (pinch + (1 - pinch) * abs(np.cos(2 * (a - rot))) ** 6)
        pts.append(((cx + np.cos(a) * rr) * SS, (cy + np.sin(a) * rr) * SS))
    d.polygon(pts, fill=col)
def extras(R):
    s = R['s']; SN = float(R['SN']); i_end = len(s) - 1
    def f(d, SS):
        # T2: inside the orange band, at 27 % of its width, two dashes broken by the star
        oo = lambda sv, D: 0.27 * np.interp(sv, s, R['OFF'][:, 0])
        ow = lambda sv, D: np.maximum(2.2, 0.17 * np.interp(sv, s, R['OFF'][:, 0]))
        line(d, R, 870, 962, oo, ow, Y, SS); line(d, R, 986, 1196, oo, ow, Y, SS)
        # T1: hanging above the bottom arm, gap to the orange shrinking towards the curl; broken by the star
        t1o = lambda sv, D: -D * (0.039 - 0.02 * smoothstep(650, 1060, sv))
        t1w = lambda sv, D: np.maximum(4.2, D * 0.0083)
        line(d, R, 0, 948, t1o, t1w, Y, SS); line(d, R, 972, 1052, t1o, t1w, Y, SS)
        # the barbed arrowhead: notch on the outer edge at SN, barb tip, then the tip and back along B0
        xn, yn, nxn, nyn, Dn = at(R, SN); oN = np.interp(SN, s, R['OFF'][:, 6])
        notch = (xn + nxn * oN, yn + nyn * oN)
        sv = np.linspace(s[-1], SN, 60); X, Yv, _, _, _ = at(R, sv)
        tip = (X[0], Yv[0]); barb = (343.0, 338.0)
        poly = [notch, barb, tip] + list(zip(X[1:], Yv[1:]))
        d.polygon([(px * SS, py * SS) for px, py in poly], fill=Y)
        # decorations: orange stars (one in the gap of T1 / T2), the orange dash in the inner yellow, small white sparkle
        xs, ys, nxs, nys, Ds = at(R, 961); star4(d, xs + nxs * -6, ys + nys * -6, 13, O, SS, rot=0.25)
        star4(d, 96, 572, 16, O, SS, rot=0.1); star4(d, 778, 700, 19, O, SS, rot=0.3); star4(d, 186, 398, 7, (255, 250, 210), SS, rot=0.2)
        dl = lambda sv, D: 0.385 * D * np.ones_like(sv); dw = lambda sv, D: np.full_like(sv, 15.0)
        line(d, R, 1040, 1088, dl, dw, O, SS)
    return f
