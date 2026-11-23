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
    from scipy.ndimage import binary_erosion
    im = np.asarray(orig(i, (960, 540))).astype(float); L = im.mean(-1)
    m = uniform_filter(L, 9); sd = np.sqrt(np.maximum(uniform_filter(L * L, 9) - m * m, 0))
    red = (im[..., 0] > 70) & (im[..., 0] > im[..., 1] * 1.8)
    star = (sd > 40) & ~red
    rr = np.asarray(Image.open(render_png).convert('RGBA')).astype(float)
    S = rr[..., 3] > 128; rl = rr[..., :3].mean(-1)
    far = edt(~S) > band                                # 离我们的剪影 band 像素以外（原版头发 / 衣服边比剪影宽一点，挨着的黑不算黑区）
    lab = np.full((540, 960), -1, int)                 # -1 未定、0 星纹底、1 黑区、2 红
    lab[red] = 2
    lab[~S & star] = 0
    lab[far & ~star & ~red & (L < 60)] = 1
    # Joker 身上：衣服 / 头发内部（渲染暗、离轮廓 2–3 像素以上）原版白 → 黑区（反色了），原版黑 → 星纹底；脸（渲染亮）这一段一直在星纹底上
    # 不信：原版那里是星纹（我们的头发 / 衣服比原版大一点、盖到星纹上了）、脸附近（发梢盖在原版的脸上）
    from scipy.ndimage import binary_dilation
    face = binary_dilation(S & (rl >= 110), iterations=25)
    cloth = binary_erosion(S & (rl < 110), iterations=3) & binary_erosion(S, iterations=2) & ~star & ~face
    lab[cloth & ~red & (L > 150)] = 1
    lab[cloth & ~red & (L < 70)] = 0
    lab[S & face & ~red] = 0
    # 原版 177–181 左下角的 ¥ 金额字压在黑区上（字有纹理，会被当成星纹底）：这一块交给周围决定
    if i <= 181:
        lab[440:500, 250:445] = -1
    known = lab >= 0
    _, (iy, ix) = edt(~known, return_indices=True)
    lab = lab[iy, ix]
    # 黑区里的 UI 字（¥ 金额、按键提示）也有纹理，会被当成星纹底、从黑区里伸出一条细舌头：离 Joker 远的星纹底做开运算，细条归黑区
    pf = (lab == 0) & (edt(~S) > 30)
    lab[pf & ~binary_opening(pf, iterations=8)] = 1
    return lab

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

def regions(i, render_png, eps=1.6):
    lab = labels(i, render_png)
    dark = clean(lab == 1); red = clean(lab == 2, 2) & ~dark
    return {'dark': polys(dark, eps), 'red': polys(red, eps)}, lab
