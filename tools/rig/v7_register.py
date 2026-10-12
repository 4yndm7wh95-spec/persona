"""把一张"由某张已校准姿势图编辑出来的"中间帧配准到那张图：SIFT 特征点 + RANSAC 相似变换，只取缩放 + 平移
（build_rig_v7.POSE_T 里 "via" 那一项：中间帧像素 p → 源图像素 k·p + (tx, ty)）。
默认只用头部和鞋这些编辑后基本不变的区域的特征点（手臂、剑、衣摆都动了，会干扰）。
用法：python3 tools/rig/v7_register.py <中间帧.png> <源图.png> [--out 叠图.png]"""
import sys
from pathlib import Path

import cv2
import numpy as np


def mask_regions(a, bbox):
    """头部（人物最上 18%）+ 鞋（最下 9%）的不透明区域。"""
    x0, y0, x1, y1 = bbox
    h = y1 - y0
    m = np.zeros(a.shape[:2], np.uint8)
    m[y0:int(y0 + h * 0.18), :] = 255
    m[int(y1 - h * 0.09):y1, :] = 255
    return cv2.bitwise_and(m, (a[..., 3] > 128).astype(np.uint8) * 255)


def register(mid_path, src_path, overlay=None):
    A = cv2.imread(str(mid_path), cv2.IMREAD_UNCHANGED)
    B = cv2.imread(str(src_path), cv2.IMREAD_UNCHANGED)
    res = []
    for im in (A, B):
        ys, xs = np.nonzero(im[..., 3] > 128)
        res.append((xs.min(), ys.min(), xs.max(), ys.max()))
    sift = cv2.SIFT_create(nfeatures=6000)
    ga, gb = cv2.cvtColor(A[..., :3], cv2.COLOR_BGR2GRAY), cv2.cvtColor(B[..., :3], cv2.COLOR_BGR2GRAY)
    ka, da = sift.detectAndCompute(ga, mask_regions(A, res[0]))
    kb, db = sift.detectAndCompute(gb, mask_regions(B, res[1]))
    matches = cv2.BFMatcher().knnMatch(da, db, k=2)
    good = [m for m, n in matches if m.distance < 0.75 * n.distance]
    pa = np.float32([ka[m.queryIdx].pt for m in good])
    pb = np.float32([kb[m.trainIdx].pt for m in good])
    M, inl = cv2.estimateAffinePartial2D(pa, pb, method=cv2.RANSAC, ransacReprojThreshold=4.0)
    k = float(np.hypot(M[0, 0], M[1, 0]))
    rot = float(np.degrees(np.arctan2(M[1, 0], M[0, 0])))
    # 只取缩放 + 平移：按内点重新求平移（不带旋转）
    sel = inl.ravel().astype(bool)
    t = (pb[sel] - k * pa[sel]).mean(axis=0)
    err = float(np.linalg.norm(pb[sel] - (k * pa[sel] + t), axis=1).mean())
    if overlay:
        warp = cv2.warpAffine(A, np.float32([[k, 0, t[0]], [0, k, t[1]]]), (B.shape[1], B.shape[0]))
        out = np.zeros_like(B)
        out[..., 2] = B[..., 3]
        out[..., 1] = warp[..., 3]
        out[..., 3] = 255
        cv2.imwrite(str(overlay), out)
    return k, float(t[0]), float(t[1]), int(sel.sum()), rot, err


if __name__ == "__main__":
    out = None
    if "--out" in sys.argv:
        out = sys.argv[sys.argv.index("--out") + 1]
    k, tx, ty, n, rot, err = register(Path(sys.argv[1]), Path(sys.argv[2]), out)
    print(f"k={k:.3f} tx={tx:.1f} ty={ty:.1f}  inliers={n} rot={rot:.2f}deg mean_err={err:.1f}px")
