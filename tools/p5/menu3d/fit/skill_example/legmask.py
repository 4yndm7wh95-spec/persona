"""SKILL 页 118–122：反色区（直线 L 右边、扫开的弧线左边）里的白腿掩码，用来对 3D 腿的轮廓。弃牌堆 / 牌组的撞色区要另外描。"""
import sys, numpy as np
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from common import orig
def Lx(y): return 784 - 0.625 * (y - 580)      # 原版反色区左边的直线（= 2D 立绘白腿的直边）
ARC = {118: [(90, 1634), (180, 1588), (270, 1539), (360, 1487), (450, 1431), (540, 1369), (630, 1300), (720, 1220), (810, 1124), (900, 1004)],
       119: [(180, 1655), (270, 1659), (360, 1614), (450, 1565), (540, 1511), (630, 1451), (720, 1380), (810, 1296), (990, 1033)],
       120: [(270, 1804), (360, 1749), (450, 1692), (540, 1631), (630, 1563), (720, 1488), (810, 1399), (900, 1290), (990, 1140)]}
def arc_x(idx, Y):
    if idx not in ARC: return np.full_like(Y, 1e9, dtype=float)
    ys = np.array([p[0] for p in ARC[idx]], float); xs = np.array([p[1] for p in ARC[idx]], float)
    return np.interp(Y, ys, xs)
def masks_r(idx, W=480, H=270, y0=0, y1=760, x1=1500, margin=12):
    im = np.asarray(orig(idx, (W, H))).astype(int)
    mn, mx = im.min(-1), im.max(-1); light = (mn > 165) & (mx - mn < 30)
    k = 1920 / W; yy, xx = np.mgrid[0:H, 0:W]; X, Y = (xx + 0.5) * k, (yy + 0.5) * k
    E = (Y >= y0) & (Y <= y1) & (X <= x1) & (X >= Lx(Y) + 4) & (X <= arc_x(idx, Y) - margin)
    E &= ~((X > 1390) & (X < 1720) & (Y > 40) & (Y < 175))      # 右上 UI 字挡住的框不算
    return light & E, E
