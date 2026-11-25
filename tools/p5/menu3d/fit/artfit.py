"""找「定格姿势 + 机位」：让 3D Joker 的剪影和原版 2D 立绘的剪影重合（原版 2D 立绘就是从 3D 定格画面描出来的）。
SKILL 页这样找到的：main_0016 + 机位 C2（位置 (-57, 44, 73.6)，旋转 (41.2, -48.9, 11.1)，fov 40.5），IoU 0.85，37 个候选里明显最好。
用法：python3 artfit.py <立绘 png> <缩放> <左上 x> <左上 y> <头中心 x> <头中心 y> "<片段:帧;片段:帧;…>" [输出 json]
  缩放 / 左上 = reg2d.py 的结果（立绘在原版屏幕里的位置）；头中心 = 原版里 Joker 脸中心的屏幕坐标（粗略即可，用来初始对位）。
  候选片段可以先全扔进去（main_0000…main_0042 每段最后一帧、blend_0000）；4 个进程并行跑，每个候选约 1 分钟。"""
import sys, json, numpy as np
from PIL import Image, ImageFilter
from scipy.optimize import minimize
from scipy.ndimage import distance_transform_edt as edt
from common import ROOT
from fitlib import m3d, cam_from
from rast import silhouette

def art_target(art_path, s, tx, ty, W, H):
    """立绘按 (s, tx, ty) 放进 1920×1080，alpha 往里收 8px（去掉白描边）→ W×H 掩码"""
    art = Image.open(art_path).convert('RGBA')
    im = art.resize((round(art.width * s), round(art.height * s)), Image.LANCZOS)
    x, y = round(tx), round(ty)
    im = im.crop((max(0, -x), max(0, -y), im.width, im.height))      # 伸出屏幕左 / 上的部分裁掉
    big = Image.new('RGBA', (1920, 1080)); big.alpha_composite(im, (max(0, x), max(0, y)))
    a = big.getchannel('A').point(lambda v: 255 if v > 128 else 0).filter(ImageFilter.MinFilter(17))
    return np.asarray(a.resize((W, H), Image.BILINEAR)) > 127

def head_center(V): return V[m3d.mesh_slice('c0001_faceUI_main', 0)].mean(0)

def make_score(t):
    Dt = edt(~t)
    def score(V, cam, W, H):
        sil = silhouette(V, cam, W, H)
        if sil.sum() < 10: return 1e3, 0
        iou = (sil & t).sum() / (sil | t).sum()
        ch = (Dt[sil].mean() + edt(~sil)[t].mean()) * (1920 / W) / 1920 * 100     # 双向倒角距离（屏宽的 %）
        return ch, iou
    return score

def cam_dir(ctr, yaw, pitch, roll, D, fov=42):
    a, b = np.radians(yaw), np.radians(pitch)
    cam = m3d.look_at(ctr + np.array([np.sin(a) * np.cos(b), np.sin(b), np.cos(a) * np.cos(b)]) * D, ctr)
    cam['rotation_degrees'][2] += roll; cam['fov'] = fov
    return cam

def align(V, cam, t, head_xy, W, H, iters=3):
    """沿机位自己的平面挪 / 沿视线推拉，让头中心落在 head_xy、剪影高度和目标一致"""
    ty_ = np.nonzero(t.any(1))[0]; th = (ty_.max() - ty_.min() + 1) * (1080 / H); hc = head_center(V)
    for _ in range(iters):
        R = m3d.rot_yxz(cam['rotation_degrees']); C = np.array(cam['position'])
        xy, z = m3d.project(hc[None], cam)
        if z[0] <= 5: return None
        ys = np.nonzero(silhouette(V, cam, W, H).any(1))[0]
        if len(ys) == 0: return None
        k = np.clip((ys.max() - ys.min() + 1) * (1080 / H) / th, 0.5, 2.0)
        f = 540 / np.tan(np.radians(cam['fov']) / 2)
        C = C + R[:, 0] * (xy[0, 0] - head_xy[0]) * z[0] / f - R[:, 1] * (xy[0, 1] - head_xy[1]) * z[0] / f
        C = hc + (C - hc) * k
        cam = dict(cam); cam['position'] = C.tolist()
    return cam

def fit_pose(V, t_small, t_big, head_xy):
    sc_small, sc_big = make_score(t_small), make_score(t_big)
    body = V[m3d.mesh_slice('c0001_body_051_UI_main')]; ctr = body.mean(0)
    cands = []
    for yaw in range(-180, 180, 30):
        for pitch in (-25, 0, 25):
            for roll in (-30, 0, 30):
                cam = align(V, cam_dir(ctr, yaw, pitch, roll, 260), t_small, head_xy, *t_small.shape[::-1])
                if cam is None: continue
                ch, iou = sc_small(V, cam, *t_small.shape[::-1]); cands.append((ch - iou * 5, cam))
    cands.sort(key=lambda c: c[0])
    def obj(p):
        cam = cam_from(p)
        if not 15 < cam['fov'] < 75: return 1e3
        if m3d.project(head_center(V)[None], cam)[1][0] <= 5: return 1e3
        ch, iou = sc_big(V, cam, *t_big.shape[::-1]); return ch - iou * 5
    best = None
    for _, cam in cands[:2]:
        p0 = np.array(cam['position'] + list(cam['rotation_degrees']) + [cam['fov']], float)
        r = minimize(obj, p0, method='Nelder-Mead', options={'maxiter': 400, 'xatol': 0.05, 'fatol': 0.001})
        if best is None or r.fun < best.fun: best = r
    return best.x, sc_big(V, cam_from(best.x), *t_big.shape[::-1])

if __name__ == '__main__':
    art = sys.argv[1] if sys.argv[1].startswith('/') else str(ROOT / sys.argv[1])
    s, tx, ty, hx, hy = (float(v) for v in sys.argv[2:7])
    keys = sys.argv[7].split(';'); out = sys.argv[8] if len(sys.argv) > 8 else None
    t_small, t_big = art_target(art, s, tx, ty, 240, 135), art_target(art, s, tx, ty, 480, 270)
    res = []
    for key in keys:
        clip, f = key.split(':')
        V = m3d.verts(clip, float(f))
        p, (ch, iou) = fit_pose(V, t_small, t_big, (hx, hy))
        print(clip, f, 'chamfer %.2f%%  iou %.3f' % (ch, iou), np.round(p, 2).tolist(), flush=True)
        res.append({'clip': clip, 'f': float(f), 'ch': float(ch), 'iou': float(iou), 'p': [float(v) for v in p]})
    if out: json.dump(res, open(out, 'w'))
