"""原版 ITEM 页入场（177–185）逐帧的三块区域：星纹底 PLATE（主菜单的黑白星纹拼贴，Joker 在上面是黑的）、
黑区 DARK（Joker 进到里面变白）、其余是红底。从原版截图分出来，用拟合好的 3D 剪影把 Joker 本身归到它所在的区域
（原版里 Joker 是白的 → 在黑区；是黑的 → 在星纹底上），再取最大的连通块、补洞、简化成多边形（原版 1920×1080 坐标）。
用法：python3 regions.py <每帧 3D 剪影 png 的目录（is_<序号>.png，960×540）> <输出 json>"""
import sys, json, numpy as np
from pathlib import Path
from PIL import Image, ImageDraw
from scipy.ndimage import uniform_filter, binary_closing, binary_opening, binary_fill_holes, binary_dilation, label
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from common import orig

def classify(i, sil_png, near=14):
    im = np.asarray(orig(i, (960, 540))).astype(float)
    L = im.mean(-1); sat = im.max(-1) - im.min(-1)
    m = uniform_filter(L, 9); sd = np.sqrt(np.maximum(uniform_filter(L * L, 9) - m * m, 0))
    red = (im[..., 0] > 70) & (im[..., 0] > im[..., 1] * 1.8)
    star = (sd > 40) & ~red
    dark = (L < 45) & ~star & ~red
    light = (L > 160) & (sat < 35) & ~star
    joker = np.asarray(Image.open(sil_png).convert('RGBA'))[..., 3] > 128
    jb, jw = joker & ~light, joker & light            # 原版里 Joker 是黑的（星纹底上）/ 白的（黑区里）
    # 剪影外面一点（渲染和原版轮廓差几像素，头发外缘等）：黑色像素归到最近的 Joker 像素那一边
    from scipy.ndimage import distance_transform_edt as edt
    db, ib = edt(~jb, return_indices=True)
    dw = edt(~jw)
    nearj = (np.minimum(db, dw) <= near) & ~joker & dark
    to_plate = nearj & (db <= dw)
    plate = (star | jb | to_plate) & ~red
    dk = ((dark & ~joker & ~to_plate) | jw) & ~red
    return plate, dk, red

def biggest(m, keep_min=0.15):
    lab, n = label(m)
    if n == 0: return m
    sizes = np.bincount(lab.ravel()); sizes[0] = 0
    keep = sizes >= max(sizes.max() * keep_min, 2000)
    return keep[lab]

def _boundary(mask):
    """外轮廓（Moore 邻域跟踪），返回 [(x, y)]（mask 像素坐标）"""
    m = np.pad(mask, 1); ys, xs = np.nonzero(m)
    start = (ys[0], xs[0]); dirs = [(-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1)]
    pts = [start]; cur = start; prev = 6
    for _ in range(400000):
        for k in range(8):
            d = (prev + k) % 8; ny, nx = cur[0] + dirs[d][0], cur[1] + dirs[d][1]
            if m[ny, nx]:
                cur = (ny, nx); pts.append(cur); prev = (d + 6) % 8; break
        else: break
        if cur == start and len(pts) > 2: break
    return np.array([(x - 1, y - 1) for y, x in pts], float)

def _dp(pts, eps):
    if len(pts) < 3: return pts
    a, b = pts[0], pts[-1]; ab = b - a; L = np.hypot(*ab) + 1e-9
    q = pts - a; d = np.abs(ab[0] * q[:, 1] - ab[1] * q[:, 0]) / L; i = int(np.argmax(d))
    if d[i] > eps: return np.vstack([_dp(pts[:i + 1], eps)[:-1], _dp(pts[i:], eps)])
    return np.array([a, b])

def _dpc(pts, eps):
    j = int(np.argmax(np.hypot(*(pts - pts[0]).T)))
    return np.vstack([_dp(pts[:j + 1], eps)[:-1], _dp(np.vstack([pts[j:], pts[:1]]), eps)[:-1]])

def polys(mask, eps=1.6):
    """mask（960×540）每个连通块的外轮廓 → 简化多边形（1920 坐标）"""
    lab, n = label(mask); out = []
    for k in range(1, n + 1):
        c = lab == k
        if c.sum() < 1500: continue
        p = _dpc(_boundary(c), eps) * 2.0
        out.append([[float(x), float(y)] for x, y in p])
    return out

def run(sil_dir, out_json, idxs=range(177, 186)):
    res = {}
    for i in idxs:
        plate, dk, red = classify(i, Path(sil_dir) / f'is_{i}.png')
        # 清理：开闭运算、补洞；两块互斥（重叠处按原图亮度）
        P = binary_fill_holes(binary_closing(binary_opening(plate, iterations=1), iterations=6))
        D = binary_fill_holes(binary_closing(binary_opening(dk, iterations=2), iterations=6))
        P, D = biggest(P), biggest(D)
        D &= ~binary_opening(P & D & plate, iterations=1)
        P &= ~D
        res[i] = {'plate': polys(P), 'dark': polys(D)}
    json.dump(res, open(out_json, 'w'))
    return res

if __name__ == '__main__':
    run(sys.argv[1], sys.argv[2])

def arc_fit(i, sil_png, away=18):
    """黑区边界逐帧拟合成圆：取星纹底和黑区相邻、离 Joker 剪影 away 像素以外的边界点（960×540），稳健拟合。
    返回 (cx, cy, r, inside_is_dark, 中位残差, 点数)，原版 1920 坐标。"""
    from scipy.optimize import least_squares
    from scipy.ndimage import distance_transform_edt as edt
    plate, dk, red = classify(i, sil_png)
    joker = np.asarray(Image.open(sil_png).convert('RGBA'))[..., 3] > 128
    far = edt(~joker) > away
    P = biggest(binary_opening(plate, iterations=1)); D = biggest(binary_opening(dk, iterations=2))
    b = binary_dilation(P, iterations=2) & binary_dilation(D, iterations=2) & far & ~binary_dilation(red, iterations=3)
    ys, xs = np.nonzero(b); k = (xs > 3) & (xs < 956) & (ys > 3) & (ys < 536)
    X, Y = xs[k] * 2.0 + 1, ys[k] * 2.0 + 1
    def r(q): return np.hypot(X - q[0], Y - q[1]) - q[2]
    best = None
    mx, my = X.mean(), Y.mean()
    for ang in np.linspace(0, 2 * np.pi, 16, endpoint=False):
        for R0 in (600, 1500, 4000):
            s = least_squares(r, [mx + np.cos(ang) * R0, my + np.sin(ang) * R0, R0], loss='soft_l1', f_scale=3)
            if best is None or s.cost < best.cost: best = s
    cx, cy, rr = best.x
    # 哪一边是黑区：圆心那一侧的黑区像素多不多
    yy, xx = np.mgrid[0:540, 0:960]; inside = np.hypot(xx * 2 + 1 - cx, yy * 2 + 1 - cy) < rr
    inside_dark = (D & inside).sum() > (D & ~inside).sum()
    e = np.abs(r(best.x))
    return float(cx), float(cy), float(abs(rr)), bool(inside_dark), float(np.median(e)), int(len(X))

def red_polys(i, eps=1.6):
    im = np.asarray(orig(i, (960, 540))).astype(float)
    red = (im[..., 0] > 70) & (im[..., 0] > im[..., 1] * 1.8)
    R = binary_fill_holes(binary_closing(binary_opening(red, iterations=2), iterations=4))
    lab, n = label(R); out = []
    for k in range(1, n + 1):
        c = lab == k
        if c.sum() < 1200: continue
        out.append([[float(x), float(y)] for x, y in _dpc(_boundary(c), eps) * 2.0])
    return out

# 每帧黑区在哪一边（从画面中心指过去的方向）：177–179 左 / 左下，180–181 下，182–183 右下，184–185 右
DARK_SIDE = {177: (-1, 0), 178: (-1, 0), 179: (-1, 0.8), 180: (0, 1), 181: (0, 1), 182: (1, 1), 183: (1, 1), 184: (1, 0.3), 185: (1, 0.3)}

def regions_v3(i, sil_png):
    """→ {'dark': [...], 'ink': [...], 'red': [...]}（原版 1920 坐标多边形）。其余地方是星纹底。
    dark = 黑区（Joker 进去变白）；ink = 其他黑色块（原版 Joker 自己的黑色部分没被我们的剪影盖住的地方，只画黑、不反色）。"""
    plate, dk, red = classify(i, sil_png, near=0)
    D = binary_fill_holes(binary_closing(binary_opening(dk, iterations=2), iterations=5))
    lab, n = label(D); dark, ink = np.zeros_like(D), np.zeros_like(D)
    sx, sy = DARK_SIDE[i]
    for k in range(1, n + 1):
        c = lab == k; a = c.sum()
        if a < 300: continue
        ys, xs = np.nonzero(c)
        side = (xs.mean() - 480) * sx + (ys.mean() - 270) * sy
        (dark if side > 0 and a > 4000 else ink)[c] = True
    R = binary_fill_holes(binary_closing(binary_opening(red, iterations=2), iterations=4))
    return {'dark': polys(dark), 'ink': polys(ink), 'red': polys(R & ~dark)}
