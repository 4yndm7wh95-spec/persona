"""Render a design (npz) with PIL at 3x and downsample; painter's algorithm outer -> inner; boundaries' curvature check."""
import sys, numpy as np
from PIL import Image, ImageDraw
Y, O, L, G, DK = (255, 230, 0), (255, 138, 0), (138, 230, 42), (108, 108, 118), (23, 23, 28)
def load(fn):
    z = np.load(fn); return {k: z[k] for k in z.files}
def strip(R, i0, i1, oa, ob, SS):
    X, Yv, NX, NY = R['x'], R['y'], R['NX'], R['NY']
    pa = [((X[i] + NX[i] * oa[i]) * SS, (Yv[i] + NY[i] * oa[i]) * SS) for i in range(i0, i1 + 1)]
    pb = [((X[i] + NX[i] * ob[i]) * SS, (Yv[i] + NY[i] * ob[i]) * SS) for i in range(i1, i0 - 1, -1)]
    return pa + pb
def render(R, bg=None, SS=3, extra=None):
    W, H = 1280, 720
    im = Image.new('RGB', (W * SS, H * SS), (20, 20, 20)) if bg is None else bg.convert('RGB').resize((W * SS, H * SS))
    d = ImageDraw.Draw(im)
    s = R['s']; i1 = int(np.searchsorted(s, R['SN'])); OFF = R['OFF']; Z = np.zeros(len(s)); ol = np.full(len(s), 3.0)
    ol = np.clip(R['D'] * 0.012, 1.4, 3.0)
    seq = [(OFF[:, 6], Y), (OFF[:, 5], O), (OFF[:, 4], Y), (OFF[:, 3], DK), (np.maximum(OFF[:, 3] - ol, 0), G),
           (np.minimum(OFF[:, 3], OFF[:, 2] + ol), DK), (OFF[:, 2], Y), (OFF[:, 1], L), (OFF[:, 0], O)]
    for ob, col in seq: d.polygon(strip(R, 0, i1, Z, ob, SS), fill=col)
    if extra: extra(d, SS)
    return im.resize((W, H), Image.LANCZOS)
def curvature_report(R, names=('orange', 'lime', 'inner yellow', 'grey', 'outer yellow', 'outer orange', 'outermost')):
    s = R['s']; i1 = int(np.searchsorted(s, R['SN']))
    out = []
    for k in range(7):
        bx = R['x'][:i1] + R['NX'][:i1] * R['OFF'][:i1, k]; by = R['y'][:i1] + R['NY'][:i1] * R['OFF'][:i1, k]
        vis = (bx > -5) & (bx < 1285) & (by > -5) & (by < 725)
        if vis.sum() < 10: out.append((names[k], 'offscreen')); continue
        ax, ay = bx[vis], by[vis]
        # resample by arc length (2 px) and compute the heading's turning
        seg = np.hypot(np.diff(ax), np.diff(ay)); ls = np.concatenate([[0], np.cumsum(seg)])
        u = np.arange(0, ls[-1], 2.0); qx = np.interp(u, ls, ax); qy = np.interp(u, ls, ay)
        h = np.unwrap(np.arctan2(np.diff(qy), np.diff(qx))); kk = np.diff(h) / 2.0
        kk = np.convolve(kk, np.ones(5) / 5, mode='valid')
        sgn = np.sign(kk[np.abs(kk) > 2e-4]); flips = int((np.diff(sgn) != 0).sum())
        dk = np.diff(kk); sg2 = np.sign(dk[np.abs(dk) > 4e-5]); bumps = int((np.diff(sg2) != 0).sum())
        out.append((names[k], 'len %d px, curvature sign flips %d, curvature turning points %d, max |k| %.2f deg/px' % (ls[-1], flips, bumps, np.degrees(np.abs(kk).max()))))
    return out
if __name__ == '__main__':
    R = load(sys.argv[1]); pref = sys.argv[2]
    render(R).save(pref + '_flat.png')
    ref = Image.open('../../../../../../research/p4r/allout/S02_0743_allout_start_yellow_orange_swipe_party_rush.jpg')
    im = render(R); blend = Image.blend(ref.convert('RGB'), im, 0.55); blend.save(pref + '_blend.png')
    for nm, rep in curvature_report(R): print(nm, ':', rep)
