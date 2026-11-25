"""抽牌堆入场（= P5R 暂停菜单主页）原版序号 46–52 的 3D Joker 逐帧细调：动作 main_0000，机位 6 个自由度 + 动作帧，目标 = 全画面剪影。
原版这一段：分界线左边是红底（Joker 在上面是白剪影）、右边是星纹拼贴（Joker 照常渲染 = 黑的）；53 白手掌冲进来，54 定格 2D 立绘 CAMPTOP（0.51295、左上 (401.93, −22.15)）。
Joker = 红底上的亮色 + 星纹上的大黑块；不是 Joker = 红色、星纹纹理；右上角露出来的咖啡馆（深蓝）和鼠标光标不比。"""
import sys, json, numpy as np
from pathlib import Path
from PIL import Image
from scipy.optimize import minimize
from scipy.ndimage import uniform_filter, binary_dilation, binary_opening, binary_erosion, label
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import cam_from, m3d
from rast import silhouette
from common import orig
W, H = 480, 270
CLIP = 'main_0000'
IDX = list(range(46, 53))

def masks(i):
    im = np.asarray(orig(i, (W, H))).astype(float); L = im.mean(-1); sat = im.max(-1) - im.min(-1)
    m = uniform_filter(L, 5); sd = np.sqrt(np.maximum(uniform_filter(L * L, 5) - m * m, 0))
    red = (im[..., 0] > 60) & (im[..., 0] > im[..., 1] * 1.8)
    blue = im[..., 2] > im[..., 0] + 12
    star = (sd > 40) & ~red
    light = (L > 150) & (sat < 40)
    from scipy.ndimage import binary_closing, binary_fill_holes
    sr = binary_fill_holes(binary_closing(star, iterations=6))
    lab0, n0 = label(sr); sz0 = np.bincount(lab0.ravel()); k0 = sz0 >= 2000; k0[0] = False
    starzone = binary_dilation(k0[lab0], iterations=2)
    J = binary_opening(light & ~starzone, iterations=1)
    cand = binary_opening((L < 45) & ~binary_dilation(star, iterations=1) & ~red & ~blue, iterations=2)
    lab, n = label(cand); sz = np.bincount(lab.ravel()); keep = sz >= 500; keep[0] = False
    J |= keep[lab]
    NJ = binary_erosion(red | star, iterations=1) & ~binary_dilation(J, iterations=1)
    yy = np.mgrid[0:H, 0:W][0]; ok = (yy + 0.5) * 4 < 1000
    return J & ok, NJ & ok

def make_obj(i, fov, p_ref, w_ref=0.1):
    J, NJ = masks(i); nj = max(J.sum(), 1)
    def obj(q, detail=False):
        f = float(np.clip(q[6], 0, 11)); V = m3d.verts(CLIP, f).astype(np.float64)
        cam = cam_from(list(q[:6]) + [fov])
        s = silhouette(V, cam, W, H)
        e_sil = ((s & NJ).sum() + (J & ~s).sum()) / nj * 100
        e_ref = np.abs((np.array(q[:6]) - np.array(p_ref[:6])) / np.array([10, 10, 10, 5, 5, 5])).sum()
        v = e_sil + w_ref * e_ref * 10
        return (v, e_sil, (s & J).sum() / max(((s & ~NJ) | J).sum(), 1)) if detail else v
    return obj

if __name__ == '__main__':
    tiles = []
    for i in IDX:
        J, NJ = masks(i); o = np.asarray(orig(i, (W, H))).astype(float) * 0.45
        o[NJ] = o[NJ] * 0.4 + np.array([0, 0, 150]); o[J] = (255, 255, 0)
        tiles.append(Image.fromarray(o.astype(np.uint8)))
    S = Image.new('RGB', (W * 4, H * 2))
    for k, t in enumerate(tiles): S.paste(t, ((k % 4) * W, (k // 4) * H))
    S.save('/tmp/p5fit/top_masks.png')
