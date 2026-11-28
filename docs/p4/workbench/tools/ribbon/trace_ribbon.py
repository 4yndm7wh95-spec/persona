"""Trace the P4R All-Out ribbon from the clean reference frame.

python3 -I trace_ribbon.py <repo root> <out dir>

1. classifies pixels of research/p4r/allout/S02_0743_*.jpg into ribbon colours (Y yellow, O orange, L lime, G grey, . other);
   writes <out>/seg.png (flat-colour segmentation) so the stripes can be seen;
2. walks the outward normals of the ribbon's inner edge B0 (same points as RB_B0 in ribbon_model.js) and records the colour runs
   along each normal (offsets in screen heights, + = outward): <out>/normals.json and a printed table.
The keyframes RB_K / RB_T1 / RB_T2 in ribbon_model.js were read off this table by hand (smoothed, offscreen parts extrapolated).
"""
import sys, json
import numpy as np
from PIL import Image

root, out = sys.argv[1], sys.argv[2]
src = root + '/research/p4r/allout/S02_0743_allout_start_yellow_orange_swipe_party_rush.jpg'
im = np.asarray(Image.open(src).convert('RGB')).astype(int)
H, W = im.shape[:2]
r, g, b = im[..., 0], im[..., 1], im[..., 2]
C = np.full((H, W), '.', dtype='<U1')
C[(r > 30) & (r < 80) & (g > 30) & (g < 80) & (b > 30) & (b < 85) & (np.abs(r - b) < 14)] = 'G'
C[(g > 180) & (r > 110) & (r < 215) & (b < 90)] = 'L'
C[(r > 200) & (g > 90) & (g < 200) & (b < 90)] = 'O'
C[(r > 200) & (g > 200) & (b < 110)] = 'Y'
seg = np.zeros_like(im) + 20
for k, col in (('G', (120, 120, 140)), ('L', (140, 230, 40)), ('O', (255, 140, 0)), ('Y', (255, 240, 0))):
    seg[C == k] = col
Image.fromarray(seg.astype('uint8')).save(out + '/seg.png')

B0 = [(1.06, 0.715), (0.925, 0.744), (0.769, 0.796), (0.691, 0.812), (0.613, 0.824), (0.534, 0.829), (0.456, 0.822), (0.378, 0.803), (0.3, 0.763),
      (0.255, 0.735), (0.230, 0.720), (0.212, 0.706), (0.194, 0.682), (0.175, 0.650), (0.165, 0.615), (0.163, 0.585), (0.169, 0.552), (0.180, 0.533),
      (0.194, 0.521), (0.207, 0.514), (0.220, 0.510), (0.236, 0.507), (0.252, 0.506), (0.272, 0.507), (0.292, 0.513), (0.312, 0.521), (0.333, 0.531)]
P = np.array([[x * W, y * H] for x, y in B0])


def catmull_centripetal(P, n=40):
    Q = np.vstack([2 * P[0] - P[1], P, 2 * P[-1] - P[-2]])
    pts = []
    for i in range(1, len(Q) - 2):
        p0, p1, p2, p3 = Q[i - 1], Q[i], Q[i + 1], Q[i + 2]
        tj = lambda ti, a, c: ti + max(1e-6, np.linalg.norm(c - a)) ** 0.5
        t0 = 0; t1 = tj(t0, p0, p1); t2 = tj(t1, p1, p2); t3 = tj(t2, p2, p3)
        for k in range(n):
            t = t1 + (t2 - t1) * k / n
            A1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1
            A2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2
            A3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3
            B1 = (t2 - t) / (t2 - t0) * A1 + (t - t0) / (t2 - t0) * A2
            B2 = (t3 - t) / (t3 - t1) * A2 + (t - t1) / (t3 - t1) * A3
            pts.append((t2 - t) / (t2 - t1) * B1 + (t - t1) / (t2 - t1) * B2)
    pts.append(P[-1])
    return np.array(pts)


S = catmull_centripetal(P)
L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(S, axis=0), axis=1))])
M = 240
us = np.linspace(0, L[-1], M)
X = np.interp(us, L, S[:, 0]); Yc = np.interp(us, L, S[:, 1])
T = np.gradient(np.stack([X, Yc], 1), axis=0); T /= np.linalg.norm(T, axis=1)[:, None]
N = np.stack([T[:, 1], -T[:, 0]], 1)
rows = []
for i in range(M):
    runs = []
    for o in np.arange(-0.07 * H, 0.75 * H, 1.0):
        x = X[i] + N[i, 0] * o; y = Yc[i] + N[i, 1] * o
        c = '#' if (x < 0 or y < 0 or x >= W or y >= H) else C[int(y), int(x)]
        if runs and runs[-1][0] == c: runs[-1][2] = o
        else: runs.append([c, o, o])
    clean = []
    for q in runs:
        if q[2] - q[1] < 2.5 and clean: clean[-1][2] = q[2]
        elif clean and clean[-1][0] == q[0]: clean[-1][2] = q[2]
        else: clean.append(q)
    rows.append([[c, round(a / H, 4), round(e / H, 4)] for c, a, e in clean])
json.dump({'s': (np.arange(M) / (M - 1)).round(4).tolist(), 'X': (X / W).round(4).tolist(), 'Y': (Yc / H).round(4).tolist(), 'runs': rows}, open(out + '/normals.json', 'w'))
for i in range(0, M, 4):
    print('s=%.3f (%.3f,%.3f) ' % (i / (M - 1), X[i] / W, Yc[i] / H) + ' '.join('%s%.3f-%.3f' % tuple(q) for q in rows[i] if not (q[0] == '.' and q[2] - q[1] > 0.06))[:240])
