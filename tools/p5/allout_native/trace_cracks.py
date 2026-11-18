"""
把 Codex 画的 P5 风格裂纹（art/p5/fx/gen_codex/allout_cracks_v2/crack_p5_*.png）描成折线，给 p5mod/src/Vfx/AllOut.cs 切画面用：
画面按「主裂纹」切成扇形的几块（每块各自错开 / 倾斜 → 屏幕真的裂开了），碎开时沿同一套裂缝飞出去。

做法：裂纹不透明部分 → 形态学骨架 → 从撞击点（画布中心）沿骨架做 BFS，求到每个端点的路径 →
按路径在撞击点外 r=170 处的角度聚类，每簇留最长的一条 = 一条主裂纹 → approxPolyDP 化简。
输出 JSON：{"size": 2048, "center": [x, y], "cracks": [[[x, y], ...], ...]}（按角度排序，从撞击点往外），坐标是画布像素。

用法：python3 tools/p5/allout_native/trace_cracks.py <裂纹 png> <输出 json> [调试图 png]
"""
import json
import math
import sys
from collections import deque

import cv2
import numpy as np


def skeleton(mask):
    img = (mask > 0).astype(np.uint8) * 255
    skel = np.zeros_like(img)
    k = cv2.getStructuringElement(cv2.MORPH_CROSS, (3, 3))
    while cv2.countNonZero(img):
        opened = cv2.morphologyEx(img, cv2.MORPH_OPEN, k)
        skel = cv2.bitwise_or(skel, cv2.subtract(img, opened))
        img = cv2.erode(img, k)
    # 粗骨架再连一下（形态学骨架会断成小段）
    skel = cv2.dilate(skel, k)
    return skel > 0


def trace(path_png):
    im = cv2.imread(path_png, cv2.IMREAD_UNCHANGED)
    h, w = im.shape[:2]
    alpha = im[:, :, 3]
    mask = alpha > 128
    sk = skeleton(mask)
    cx, cy = w / 2, h / 2
    ys, xs = np.nonzero(sk)
    d2 = (xs - cx) ** 2 + (ys - cy) ** 2
    # 撞击点中间是个洞：从离中心 90px 以内的全部骨架点同时出发（多源 BFS）
    near = [(ys[i], xs[i]) for i in np.nonzero(d2 < 230 ** 2)[0]]
    if not near:
        i0 = int(np.argmin(d2))
        near = [(ys[i0], xs[i0])]
    parent = {p: None for p in near}
    dist = {p: 0 for p in near}
    q = deque(near)
    nb = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]
    while q:
        y, x = q.popleft()
        for dy, dx in nb:
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and sk[ny, nx] and (ny, nx) not in parent:
                parent[(ny, nx)] = (y, x)
                dist[(ny, nx)] = dist[(y, x)] + 1
                q.append((ny, nx))
    # 叶子：没有更远的邻居的点（在 BFS 树里没有孩子）
    has_child = set(p for p in parent.values() if p is not None)
    leaves = [p for p in parent if p not in has_child]

    def path_to(p):
        out = []
        while p is not None:
            out.append((p[1], p[0]))
            p = parent[p]
        return out[::-1]                      # 从撞击点往外

    def radius(pt):
        return math.hypot(pt[0] - cx, pt[1] - cy)

    paths = []
    for lf in leaves:
        r = radius((lf[1], lf[0]))
        if r < w * 0.12:
            continue
        pth = path_to(lf)
        probe = next((p for p in pth if radius(p) >= 320), pth[-1])
        ang = math.atan2(probe[1] - cy, probe[0] - cx)
        paths.append((ang, r, pth))
    # 按角度聚类（12° 内算同一条主裂纹，留最长的）
    paths.sort(key=lambda t: t[0])
    clusters = []
    for ang, r, pth in paths:
        if clusters and abs(math.remainder(ang - clusters[-1][-1][0], math.tau)) < math.radians(9):
            clusters[-1].append((ang, r, pth))
        else:
            clusters.append([(ang, r, pth)])
    if len(clusters) > 1 and abs(math.remainder(clusters[0][0][0] - clusters[-1][-1][0], math.tau)) < math.radians(9):
        clusters[0].extend(clusters.pop())
    cracks = []
    for cl in clusters:
        ang, r, pth = max(cl, key=lambda t: t[1])
        pts = np.array(pth, dtype=np.int32).reshape(-1, 1, 2)
        simp = cv2.approxPolyDP(pts, 3.0, False).reshape(-1, 2)
        simp[0] = (int(cx), int(cy))          # 都从撞击点出发
        cracks.append((math.atan2(simp[-1][1] - cy, simp[-1][0] - cx), simp.tolist()))
    cracks.sort(key=lambda t: t[0])
    return w, (cx, cy), [c for _, c in cracks], im


def main():
    src, out = sys.argv[1], sys.argv[2]
    w, c, cracks, im = trace(src)
    json.dump({"size": w, "center": [c[0], c[1]], "cracks": cracks}, open(out, "w"))
    print(out, len(cracks), "main cracks")
    if len(sys.argv) > 3:
        dbg = np.full((w, w, 3), (40, 30, 200), np.uint8)
        a = im[:, :, 3:4] / 255.0
        dbg = (dbg * (1 - a) + im[:, :, :3] * a).astype(np.uint8)
        for i, cr in enumerate(cracks):
            col = [(0, 255, 255), (0, 255, 0), (255, 255, 0), (255, 0, 255)][i % 4]
            cv2.polylines(dbg, [np.array(cr, np.int32)], False, col, 5)
        cv2.imwrite(sys.argv[3], cv2.resize(dbg, (800, 800)))


if __name__ == "__main__":
    main()
