"""选人立绘分层（阶段 11）：底图 01 的像素一个不动，只在"手 + 镜片"遮住的区域里补画。
- 手层：用 01 原图自己的像素；轮廓取自 12（12 补好了镜片里的绿洞），先把 12 配准到 01。
- 底层：手外面 = 01 原像素（逐像素一致）；手里面 = 供体（13/15 去手图局部配准 + 颜色匹配）+ OpenCV 修补兜底。
  只有手轮廓以内一圈（手可能移开的最大距离 + 羽化）会被看见，所以供体只需要在这一圈里像样。
- 闭眼：10 与 01 的差异只在眼睛附近 → 贴片；露出的眼睛贴在底层，镜片里的眼睛贴在手层。
用法：python3 tools/ui/v11_layers.py"""
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/rig"))
from v6_register import key, register, warp  # noqa: E402

SRC = ROOT / "art/v11/all_generated"
OUT = ROOT / "art/v11/layers"
REVEAL = 26            # 手层最多移开的距离（像素）+ 余量：底层只有这一圈会被看见


def save(img, name):
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), "RGBA").save(OUT / name)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    base = key(SRC / "01_portrait_a_original.png")
    hand12 = key(SRC / "12_hand_glass_fixed.png")
    M, c = register(hand12, base, s_range=(0.85, 1.15), rot_range=4)
    print("12 → 01", np.round(M, 3).tolist(), c)
    hand12 = warp(hand12, M)
    # 手的轮廓：12 的 alpha，收紧到 01 自己的轮廓里（不让绿幕边进来），再按 01 的颜色修边：
    # 12 与 01 颜色接近的地方才算手（12 是重画的，边缘可能多 / 少几像素）
    a12 = hand12[..., 3] > 0.5
    same = np.abs(hand12[..., :3] - base[..., :3]).sum(-1) < 0.35
    core = cv2.erode(a12.astype(np.uint8), np.ones((9, 9), np.uint8)) > 0
    hand = core | (a12 & same)
    hand = cv2.morphologyEx(hand.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8)) > 0
    n, lab, st, _ = cv2.connectedComponentsWithStats(hand.astype(np.uint8), 8)
    hand = lab == (1 + np.argmax(st[1:, cv2.CC_STAT_AREA]))
    hand &= base[..., 3] > 0.5
    soft = cv2.GaussianBlur(hand.astype(np.float32), (0, 0), 1.0)
    hand_layer = base.copy()
    hand_layer[..., 3] *= np.maximum(soft, hand * 1.0)
    save(hand_layer, "hand.png")

    # 底层：手里面要补的区域（只需要靠边的一圈可信）
    hole = cv2.dilate(hand.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    inner = hole & ~(cv2.erode(hole.astype(np.uint8), np.ones((2 * REVEAL + 1,) * 2, np.uint8)) > 0)
    donors = []
    for name in ("13_nohand_fix2.png", "15_nohand_remove_from_original.png", "11_nohand_fix1.png"):
        d = key(SRC / name)
        Md, cd = register(d, base * (~hole)[..., None], s_range=(0.85, 1.15), rot_range=6)
        d = warp(d, Md)
        # 颜色匹配：在洞外一圈（两张都可见的区域）上做每通道线性映射
        ring = (cv2.dilate(hole.astype(np.uint8), np.ones((41, 41), np.uint8)) > 0) & ~hole & (d[..., 3] > 0.5) & (base[..., 3] > 0.5)
        for ch in range(3):
            x, y = d[..., ch][ring], base[..., ch][ring]
            A = np.vstack([x, np.ones_like(x)]).T
            k, b = np.linalg.lstsq(A, y, rcond=None)[0]
            d[..., ch] = np.clip(d[..., ch] * k + b, 0, 1)
        err = np.abs(d[..., :3] - base[..., :3]).sum(-1)[ring].mean()
        donors.append((err, name, d))
        print("donor", name, "边缘误差", round(float(err), 4))
    donors.sort(key=lambda e: e[0])
    _, dname, donor = donors[0]
    # OpenCV 修补作兜底（供体透明处）
    b8 = (np.clip(base[..., :3], 0, 1) * 255).astype(np.uint8)
    inp = cv2.inpaint(b8, hole.astype(np.uint8) * 255, 9, cv2.INPAINT_TELEA).astype(np.float32) / 255
    fill = np.where((donor[..., 3] > 0.5)[..., None], donor[..., :3], inp)
    # 洞的边上和原图羽化接缝（只在洞内往里 6px 过渡，洞外一律原像素）
    dist = cv2.distanceTransform(hole.astype(np.uint8), cv2.DIST_L2, 5)
    w = np.clip(dist / 6.0, 0, 1)[..., None]
    under = base.copy()
    under[..., :3] = np.where(hole[..., None], base[..., :3] * (1 - w) + fill * w, base[..., :3])
    under[..., 3] = np.where(hole, np.maximum(base[..., 3], donor[..., 3]), base[..., 3])
    save(under, "under.png")
    print("采用供体", dname)

    # 闭眼贴片
    blink = key(SRC / "10_blink.png")
    Mb, cb = register(blink, base, s_range=(0.9, 1.1), rot_range=3)
    blink = warp(blink, Mb)
    dif = (np.abs(blink[..., :3] - base[..., :3]).sum(-1) > 0.3) & (blink[..., 3] > 0.5)
    dif = cv2.morphologyEx(dif.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, lab, st, cen = cv2.connectedComponentsWithStats(dif, 8)
    keep = np.zeros_like(dif)
    for i in range(1, n):
        if st[i, cv2.CC_STAT_AREA] > 60:
            keep[lab == i] = 1
    keep = cv2.dilate(keep, np.ones((13, 13), np.uint8))
    m = np.clip(cv2.GaussianBlur(keep.astype(np.float32), (0, 0), 3) * 1.5, 0, 1)
    for nm, region in (("blink_face.png", ~hand), ("blink_glass.png", hand)):
        p = blink.copy()
        p[..., 3] *= m * region
        save(p, nm)
    print("闭眼贴片区域", int(keep.sum()))

    # 校验：静止时 底层 + 手层 == 原图
    comp = under[..., :3] * (1 - hand_layer[..., 3:]) + hand_layer[..., :3] * hand_layer[..., 3:]
    vis = base[..., 3] > 0.5
    print("静止合成与原图的平均差（/255）", round(float(np.abs(comp - base[..., :3]).sum(-1)[vis].mean() * 255 / 3), 3))


if __name__ == "__main__":
    main()
