"""ITEM 页入场逐帧的区域（原版 1920×1080 坐标多边形）：星纹底 / 黑区 / 红底。
Joker 本身的区域靠渲染判断：渲染里暗（衣服头发）原版却是白 → 在黑区（反色了）；渲染里亮（脸）原版却是黑 → 黑区；两边一致 → 星纹底。
输入：原版帧序号、同一机位渲染的 960×540 透明 PNG。"""
import sys, numpy as np
from pathlib import Path
from PIL import Image
from scipy.ndimage import uniform_filter, binary_opening, binary_closing, binary_fill_holes, label, distance_transform_edt as edt
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from common import orig
from regions import polys

def labels(i, render_png, band=20):
    """v5：Joker 身上和身边先不判，只用离 Joker 远的地方定「星纹底 / 黑区」，再把边界平滑地延伸穿过 Joker
    （原版的分界本来就是光滑的直线 / 弧线，人站在上面不改变它）。唯一例外：原版里反成白色的身体一定在黑区里。"""
    from scipy.ndimage import binary_erosion, binary_dilation, gaussian_filter
    im = np.asarray(orig(i, (960, 540))).astype(float); L = im.mean(-1)
    m = uniform_filter(L, 9); sd = np.sqrt(np.maximum(uniform_filter(L * L, 9) - m * m, 0))
    red = (im[..., 0] > 70) & (im[..., 0] > im[..., 1] * 1.8)
    star = (sd > 40) & ~red
    starC = binary_closing(star, iterations=6) & ~red          # 星纹里粗的黑条也算星纹底
    rr = np.asarray(Image.open(render_png).convert('RGBA')).astype(float)
    S = rr[..., 3] > 128; rl = rr[..., :3].mean(-1)
    far = edt(~S) > band
    face = binary_dilation(S & (rl >= 110), iterations=25)
    cloth = binary_erosion(S & (rl < 110), iterations=3) & ~star & ~face
    # 我们身体深处（离剪影边 25 像素以上）原版是黑的 = Joker 的黑身体站在星纹底上；边上一圈不判（我们的剪影比原版大出来的那点可能盖在黑区背景上）
    deep = binary_erosion(S, iterations=25)
    plate_k = (starC & far) | (S & face & ~red) | (deep & ~face & ~red & (L < 70))
    dark_k = (far & ~starC & ~red & (L < 70)) | (cloth & ~red & (L > 150))
    # 黑区里的 UI 字（¥ 金额、按键提示）有纹理会被当成星纹：小块星纹（离大片星纹底远）不算
    lp, n = label(plate_k)
    if n:
        sz = np.bincount(lp.ravel()); keep = sz >= 3000; keep[0] = False
        plate_k = keep[lp] | (S & face & ~red) | (deep & ~face & ~red & (L < 70))
    w = (plate_k | dark_k).astype(float); v = dark_k.astype(float)
    out = np.full((540, 960), 0, int)
    num = gaussian_filter(v * w, 30); den = gaussian_filter(w, 30)
    num2 = gaussian_filter(v * w, 90); den2 = gaussian_filter(w, 90)
    ratio = np.where(den > 0.05, num / np.maximum(den, 1e-6), num2 / np.maximum(den2, 1e-6))
    unk = ~(plate_k | dark_k)
    out[dark_k] = 1
    out[unk & (ratio > 0.5)] = 1
    out[plate_k & ~dark_k] = 0
    out[red] = 2
    return out

def clean(mask, k=4, keep_area=0.02):
    """开闭运算去毛刺；只留贴着画面边的块或大块（头发顶上那种细条：原版头发比我们的剪影高一点，单独成一小块，不要）"""
    mask = binary_closing(binary_opening(mask, iterations=k), iterations=k)
    lab, n = label(mask); out = np.zeros_like(mask)
    border = np.zeros_like(mask); border[:6] = border[-6:] = True; border[:, :6] = border[:, -6:] = True
    for j in range(1, n + 1):
        c = lab == j
        if c.sum() >= keep_area * c.size or (c & border).any() and c.sum() >= 2500:
            out |= binary_fill_holes(c)
    return out

def drop_near(mask, S, near=40, frac=0.5, small=0.06):
    """面积小、大部分贴着我们剪影的黑区块是误判（原版 Joker 自己的手臂 / 贴纸字），丢掉"""
    d = edt(~S) <= near
    lab, n = label(mask); out = mask.copy()
    for j in range(1, n + 1):
        c = lab == j
        if c.sum() < small * c.size and (c & d).sum() > frac * c.sum():
            out[c] = False
    return out

def regions(i, render_png, eps=1.6):
    lab = labels(i, render_png)
    S = np.asarray(Image.open(render_png).convert('RGBA'))[..., 3] > 128
    dark = drop_near(clean(lab == 1), S); red = clean(lab == 2, 14) & ~dark   # 开运算加大：原版右上道具列表的红色选中条（细条）不算红底
    return {'dark': polys(dark, eps), 'red': polys(red, eps)}, lab
