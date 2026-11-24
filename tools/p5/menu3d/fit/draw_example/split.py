"""原版 46–55 每帧红底 / 星纹拼贴的分界线（= Joker 白剪影 / 照常渲染的分界）：每行红色的最右端和星纹的最左端挨得近（< 30px）的行没被 Joker 挡住，
取两者中点拟合直线 x = a + b·y（原版 1920×1080 坐标）。"""
import sys, json, numpy as np
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from common import orig
from scipy.ndimage import uniform_filter

def line(i):
    im = np.asarray(orig(i)).astype(float); L = im.mean(-1)
    red = (im[..., 0] > 60) & (im[..., 0] > im[..., 1] * 1.8)
    m = uniform_filter(L, 7); sd = np.sqrt(np.maximum(uniform_filter(L * L, 7) - m * m, 0))
    star = (sd > 40) & ~red
    ys, xs = [], []
    for y in range(0, 1000, 4):
        r = np.nonzero(red[y])[0]; s = np.nonzero(star[y])[0]
        if len(r) == 0 or len(s) == 0: continue
        rmax = r.max(); s2 = s[s > rmax - 5]
        if len(s2) == 0: continue
        smin = s2.min()
        if smin - rmax < 30: ys.append(y); xs.append((rmax + smin) / 2)
    ys, xs = np.array(ys), np.array(xs)
    if len(ys) < 10: return None
    for _ in range(3):
        b, a = np.polyfit(ys, xs, 1); res = np.abs(xs - (a + b * ys)); keep = res < max(6, np.percentile(res, 80))
        ys, xs = ys[keep], xs[keep]
    return float(a), float(b), int(len(ys))

if __name__ == '__main__':
    out = {}
    for i in range(46, 56):
        r = line(i); out[i] = r; print(i, None if r is None else (round(r[0], 1), round(r[1], 4), r[2], 'x@0', round(r[0]), 'x@1080', round(r[0] + r[1] * 1080)))
    json.dump(out, open('/tmp/p5fit/top_split.json', 'w'))
