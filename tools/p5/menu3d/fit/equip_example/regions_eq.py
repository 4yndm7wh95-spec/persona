"""EQUIP 页入场 233–239 逐帧的区域（原版 1920×1080 坐标多边形）：
STAR 星纹（233–235 主菜单的星纹弧带 / 拼贴，236 起左上的星纹拼贴）、GRAY 浅灰板、RED 红底、DARK 黑区（Joker 在里面反色变白），其余是黑底（Joker 照常渲染）。
星纹 / 红底 / 浅灰板的边从原版截图按纹理 / 颜色取（看得见、要准）；黑区和黑底之间的分界只有穿过 Joker 时看得见，用 zones.py 从网格图读的线。
Joker 身上（3D 剪影 S 下面）的星纹带补满（反正被 Joker 盖住，不留缝）。"""
import sys, numpy as np
from pathlib import Path
from PIL import Image, ImageDraw
from scipy.ndimage import uniform_filter, binary_closing, binary_opening, binary_fill_holes, binary_dilation, label
sys.path.insert(0, str(Path(__file__).resolve().parents[1])); sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'item_example'))
from common import orig
from regions import polys
from zones import ZONE, PLATE
W, H = 960, 540

def pmask(ps):
    im = Image.new('L', (W, H), 0); d = ImageDraw.Draw(im)
    for p in ps: d.polygon([(x * W / 1920, y * H / 1080) for x, y in p], fill=255)
    return np.asarray(im) > 0

def big(m, n_min):
    lab, n = label(m)
    if n == 0: return m
    sz = np.bincount(lab.ravel()); keep = sz >= n_min; keep[0] = False
    return keep[lab]

def labels(i, S):
    im = np.asarray(orig(i, (W, H))).astype(float); L = im.mean(-1); sat = im.max(-1) - im.min(-1)
    m = uniform_filter(L, 9); sd = np.sqrt(np.maximum(uniform_filter(L * L, 9) - m * m, 0))
    red = binary_opening((im[..., 0] > 70) & (im[..., 0] > im[..., 1] * 1.8), iterations=3)
    red = big(binary_closing(red, iterations=3), 3000)
    star = big(binary_closing((sd > 40) & ~red, iterations=6) & ~red, 4000)
    A = pmask([ZONE[i]])
    holes = binary_fill_holes(star) & ~star
    lab, n = label(holes)
    if n:
        sz = np.bincount(lab.ravel()); small = sz < 3000; small[0] = False
        star = star | small[lab]
    star = star | (S & binary_dilation(star, iterations=30) & ~A)
    light = (L > 160) & (sat < 35) & ~star & ~red
    gray = big(binary_opening(light & ~A & ~binary_dilation(S, iterations=6), iterations=3), 2500) | pmask(PLATE.get(i, []))
    gray &= ~star & ~red
    dark = A & ~star & ~red & ~gray
    return {'star': star, 'gray': gray, 'red': red, 'dark': dark}

def clean(mask, k=3, n_min=1500):
    mask = binary_closing(binary_opening(mask, iterations=k), iterations=k)
    mask = big(mask, n_min)
    holes = binary_fill_holes(mask) & ~mask
    lab, n = label(holes)
    if n:
        sz = np.bincount(lab.ravel()); small = sz < 2000; small[0] = False
        mask = mask | small[lab]
    return mask

def regions(i, sil_png, eps=1.6):
    S = np.asarray(Image.open(sil_png).convert('RGBA').resize((W, H)))[..., 3] > 128
    lab = labels(i, S)
    return {k: polys(clean(v), eps) for k, v in lab.items()}, lab
