"""rig v6 第一步：零件抠图 + 自动配准。
imagegen 编辑出的分离图常有整体缩放 / 平移 / 轻微旋转（"尺寸出错"）。这里对每张零件图
搜索一个相似变换（缩放 + 旋转 + 平移），使它和参考图（9B → base_a；9C → 对应手臂分离图）
在共有可见区域上最吻合，然后把整张零件（含补画的被遮挡部分）一起变换回去。
1) 粗搜索：1/4 分辨率下按 缩放 × 旋转 网格，用带掩码的 matchTemplate（RGB + alpha 四通道）找平移；
2) 细化：1/2 分辨率下用稳健误差（截断的颜色差，参考图透明处记满分误差）做 Nelder-Mead；
3) 输出：art/v9/reg/<名字>.png（RGBA，对齐到 base_a 画布）+ art/v9/reg/registration.json（变换和残差）。
用法：python3 tools/rig/v6_register.py [名字…]"""
import json
import math
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from scipy.optimize import minimize

ROOT = Path(__file__).resolve().parents[2]
V9 = ROOT / "art/v9"
OUT = V9 / "reg"


def key(path):
    """绿幕抠图（同 tools/chroma_key.py）+ 去掉小碎块。返回 float32 RGBA（0~1）。"""
    im = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
    r, g, b = im[..., 0], im[..., 1], im[..., 2]
    excess = g - np.maximum(r, b)
    alpha = 1.0 - np.clip((excess - 40.0) / 90.0, 0.0, 1.0)
    g2 = np.minimum(g, np.maximum(r, b) + 10.0)
    solid = (alpha > 0.5).astype(np.uint8)
    n, lab, stats, _ = cv2.connectedComponentsWithStats(solid, 8)
    keep = np.zeros(n, bool)
    if n > 1:
        big = stats[1:, cv2.CC_STAT_AREA].max()
        keep[1:] = stats[1:, cv2.CC_STAT_AREA] >= max(400, big * 0.01)
    near = cv2.dilate(keep[lab].astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    alpha = alpha * near
    return np.dstack([r / 255, g2 / 255, b / 255, alpha]).astype(np.float32)


def warp(img, M, size=None):
    h, w = img.shape[:2]
    return cv2.warpAffine(img, M, size or (w, h), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))


def sim(s, rot_deg, tx, ty, c):
    """绕 c 缩放旋转后平移。返回 2×3。"""
    a = math.radians(rot_deg)
    ca, sa = s * math.cos(a), s * math.sin(a)
    M = np.array([[ca, -sa, 0], [sa, ca, 0]], np.float64)
    M[:, 2] = np.array(c) - M[:, :2] @ np.array(c) + np.array([tx, ty])
    return M


def robust_cost(P, R, cap=0.35):
    """P、R 同尺寸 RGBA。P 不透明处：R 也不透明 → 截断颜色差；R 透明 → 记 cap。"""
    a = P[..., 3]
    m = a > 0.5
    if m.sum() < 50:
        return 1e9
    d = np.sqrt(((P[..., :3] - R[..., :3]) ** 2).sum(-1))
    d = np.minimum(d, cap)
    d = np.where(R[..., 3] > 0.5, d, cap)
    return float(d[m].mean())


def register(P, R, region=None, s_range=(0.55, 1.5), rot_range=6, refine=True):
    """返回 (M, cost)：M 把 P 的像素坐标映射到 R。region：只在 R 的这块矩形 (x0,y0,x1,y1) 内比较（用于手臂只比肩/上臂）。"""
    h, w = R.shape[:2]
    Rm = R.copy()
    Pm = P.copy()
    if region is not None:
        x0, y0, x1, y1 = region
        cut = np.zeros((h, w), bool)
        cut[y0:y1, x0:x1] = True
    ys, xs = np.nonzero(P[..., 3] > 0.5)
    c = (xs.mean(), ys.mean())
    # ── 粗搜索（1/4）──
    f = 0.25
    Rs = cv2.resize(Rm, None, fx=f, fy=f, interpolation=cv2.INTER_AREA)
    feat_R = np.dstack([Rs[..., :3] * Rs[..., 3:], Rs[..., 3:] * 1.5]).astype(np.float32)
    cands = []
    for s in np.arange(s_range[0], s_range[1] + 1e-6, 0.025):
        for r in np.arange(-rot_range, rot_range + 1e-6, 2.0):
            M = sim(s, r, 0, 0, c)
            W = warp(Pm, M)
            if region is not None:
                W = W * cut[..., None]
            Ws = cv2.resize(W, None, fx=f, fy=f, interpolation=cv2.INTER_AREA)
            a = Ws[..., 3]
            yy, xx = np.nonzero(a > 0.5)
            if len(xx) < 20:
                continue
            bx0, by0, bx1, by1 = xx.min(), yy.min(), xx.max() + 1, yy.max() + 1
            T = np.dstack([Ws[..., :3] * Ws[..., 3:], Ws[..., 3:] * 1.5])[by0:by1, bx0:bx1].astype(np.float32)
            mask = np.repeat((a[by0:by1, bx0:bx1] > 0.5)[..., None], 4, -1).astype(np.float32)
            if T.shape[0] >= Rs.shape[0] or T.shape[1] >= Rs.shape[1]:
                continue
            res = cv2.matchTemplate(feat_R, T, cv2.TM_SQDIFF, mask=mask)
            res = np.nan_to_num(res, nan=1e18, posinf=1e18)
            if region is not None:   # 平移不能离开太远（比较区已固定，防止滑走）
                pass
            mn, _, loc, _ = cv2.minMaxLoc(res)
            score = mn / mask[..., 0].sum()
            cands.append((score, (s, r, (loc[0] - bx0) / f, (loc[1] - by0) / f)))
    cands.sort(key=lambda e: e[0])
    if not refine:
        s, r, tx, ty = cands[0][1]
        return sim(s, r, tx, ty, c), cands[0][0]
    # ── 细化（1/2）──
    f = 0.5
    R2 = cv2.resize(Rm, None, fx=f, fy=f, interpolation=cv2.INTER_AREA)
    P2 = cv2.resize(Pm, None, fx=f, fy=f, interpolation=cv2.INTER_AREA)
    c2 = (c[0] * f, c[1] * f)
    cut2 = None if region is None else cv2.resize(cut.astype(np.uint8), None, fx=f, fy=f, interpolation=cv2.INTER_NEAREST).astype(bool)

    def cost(x):
        M = sim(x[0], x[1], x[2] * f, x[3] * f, c2)
        W = warp(P2, M)
        if cut2 is not None:
            W = W * cut2[..., None]
        return robust_cost(W, R2)

    # 粗搜索的前几名（彼此不太接近的）各细化一次，取稳健误差最小者
    picked = []
    for sc, x in cands:
        if all(abs(x[0] - y[0]) > 0.06 or math.hypot(x[2] - y[2], x[3] - y[3]) > 24 for y in picked):
            picked.append(x)
        if len(picked) >= 6:
            break
    best = None
    for x in picked:
        x0 = np.array(x)
        opt = minimize(cost, x0, method="Nelder-Mead",
                       options={"initial_simplex": [x0, x0 + [0.02, 0, 0, 0], x0 + [0, 1.5, 0, 0], x0 + [0, 0, 6, 0], x0 + [0, 0, 0, 6]],
                                "xatol": 0.05, "fatol": 1e-5, "maxiter": 400})
        if best is None or opt.fun < best.fun:
            best = opt
    s, r, tx, ty = best.x
    return sim(s, r, tx, ty, c), float(best.fun)


def params(M):
    s = math.hypot(M[0, 0], M[1, 0])
    return {"scale": round(s, 4), "rot": round(math.degrees(math.atan2(M[1, 0], M[0, 0])), 2),
            "M": [[round(v, 4) for v in row] for row in M.tolist()]}


def save(img, path):
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), "RGBA").save(path)


# 9C 手图 → 参考手臂。只在肩 / 上臂区域比较（手部本来就不同）。
HAND_REF = {"near": "arm_near_a", "far": "arm_far_a"}


def main(names):
    OUT.mkdir(exist_ok=True)
    base = key(V9 / "base_a.png")
    save(base, OUT / "base_a.png")
    log_path = OUT / "registration.json"
    log = json.loads(log_path.read_text()) if log_path.exists() else {}
    parts = sorted(p.stem for p in (V9 / "parts").glob("*.png"))
    hands = sorted(p.stem for p in (V9 / "hands").glob("*.png"))
    todo = names or parts + hands
    for n in todo:
        if n in parts:
            P = key(V9 / "parts" / f"{n}.png")
            M, cost = register(P, base)
            ref = "base_a"
        else:
            side = n.split("_")[0]
            refname = HAND_REF[side]
            # 参考手臂：已配准到 base_a 的版本；比较区域 = 该手臂 bbox 的上 55%
            R = np.asarray(Image.open(OUT / f"{refname}.png")).astype(np.float32) / 255
            ys, xs = np.nonzero(R[..., 3] > 0.5)
            y0, y1 = ys.min(), ys.min() + int((ys.max() - ys.min()) * 0.55)
            region = (max(xs.min() - 20, 0), max(y0 - 20, 0), xs.max() + 20, y1)
            P = key(V9 / "hands" / f"{n}.png")
            M, cost = register(P, R, region=region, s_range=(0.75, 1.35))
            ref = refname
        W = warp(P, M)
        save(W, OUT / f"{n}.png")
        log[n] = {"ref": ref, "cost": round(cost, 4), **params(M)}
        print(f"{n:18s} scale {log[n]['scale']:.3f} rot {log[n]['rot']:+.1f} t ({M[0, 2]:+.0f},{M[1, 2]:+.0f}) cost {cost:.4f}")
        log_path.write_text(json.dumps(log, indent=1, ensure_ascii=False))


if __name__ == "__main__":
    main(sys.argv[1:])
