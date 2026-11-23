"""把原版 2D 立绘（research/p5r/art/portrait）配准到原版截图里：求缩放 s 和左上角 (tx, ty)（1920×1080 屏幕坐标）。
用法：python3 reg2d.py <立绘 png> <序号> [x0 y0 x1 y1 比较区域]
例：SKILL 页 python3 reg2d.py research/p5r/art/portrait/BASE__P5_CAMPSKILL_2D01__5af504dfb6.png 124 640 0 1100 420
    → s 0.50795, (0.47, 18.05)；ITEM 页（序号 196）是 0.5、(0, 0)。区域选立绘头部附近、没被 UI 挡住的地方。"""
import sys, numpy as np
from PIL import Image
from scipy.optimize import minimize
from common import ROOT, orig

def register(art_path, idx, box=(640, 0, 1100, 420), scales=np.linspace(0.42, 0.6, 10), tx=np.linspace(-200, 200, 17), ty=np.linspace(-150, 100, 11)):
    A = np.asarray(Image.open(art_path).convert('RGBA')).astype(float) / 255.0
    hgt, wid = A.shape[:2]
    T = np.asarray(orig(idx).convert('L')).astype(float)[box[1]:box[3], box[0]:box[2]] / 255.0
    ys, xs = np.mgrid[box[1]:box[3], box[0]:box[2]].astype(float)
    def place(s, x, y):
        ax = (xs + 0.5 - x) / s - 0.5; ay = (ys + 0.5 - y) / s - 0.5
        ix = np.clip(np.round(ax).astype(int), 0, wid - 1); iy = np.clip(np.round(ay).astype(int), 0, hgt - 1)
        valid = (ax >= 0) & (ax < wid) & (ay >= 0) & (ay < hgt)
        px = A[iy, ix]; return px[..., :3].mean(-1), px[..., 3] * valid
    def cost(p):
        lum, a = place(*p); m = a > 0.9
        return 1e3 if m.sum() < 1000 else np.mean((lum[m] * a[m] - T[m]) ** 2)
    best = min(((cost((s, x, y)), (s, x, y)) for s in scales for x in tx for y in ty), key=lambda t: t[0])
    r = minimize(cost, np.array(best[1]), method='Nelder-Mead', options={'xatol': 1e-4, 'fatol': 1e-7, 'maxiter': 600})
    return r.x, r.fun

if __name__ == '__main__':
    art = sys.argv[1] if sys.argv[1].startswith('/') else str(ROOT / sys.argv[1])
    box = tuple(int(v) for v in sys.argv[3:7]) if len(sys.argv) >= 7 else (640, 0, 1100, 420)
    p, c = register(art, int(sys.argv[2]), box)
    print('scale %.5f  top-left (%.2f, %.2f)  mse %.5f' % (p[0], p[1], p[2], c))
