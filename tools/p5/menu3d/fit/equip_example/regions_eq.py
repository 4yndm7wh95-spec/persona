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
from zones import ZONE, PLATE, PLATE_LIGHT
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
    # 星纹 = 边缘很密的地方（大窗口里一半以上是明暗交界）：Joker 剪影的边、字只是零散的线，不算
    m5 = uniform_filter(L, 5); sd5 = np.sqrt(np.maximum(uniform_filter(L * L, 5) - m5 * m5, 0))
    edge = (sd5 > 40) & ~red
    core = uniform_filter(edge.astype(float), 41) > 0.42
    star = binary_closing(edge, iterations=4) & binary_dilation(core, iterations=22) & ~red
    star = big(star, 4000)
    A = pmask([ZONE[i]])
    star &= ~A                                   # 这几帧星纹都在黑区外面；黑区里有纹理的是装备栏的字牌（入场不画）
    star = big(star, 4000)
    holes = binary_fill_holes(star) & ~star
    lab, n = label(holes)
    if n:
        sz = np.bincount(lab.ravel()); small = sz < 3000; small[0] = False
        star = star | small[lab]
    star = star | (S & binary_dilation(star, iterations=30) & ~A)
    light = (L > 160) & (sat < 35) & ~star & ~red
    gray = big(binary_opening(light & ~A & ~binary_dilation(S, iterations=6), iterations=3), 2500) | pmask(PLATE.get(i, []))
    gray |= big(binary_opening(light & pmask(PLATE_LIGHT.get(i, [])), iterations=2), 2000)
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
    # 多边形只取外轮廓：先补满洞，免得里面的块和外面的块重叠（黑区重叠会被反色两次）
    return {k: polys(binary_fill_holes(clean(v)), eps) for k, v in lab.items()}, lab
