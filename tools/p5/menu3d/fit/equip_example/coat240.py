"""（第五轮起不用了：牌组收尾改成对原版 240、241 拟合机位，大衣自然铺开。留作记录。）
牌组收尾「240」的大衣下摆（用户 2026-11-24：3D 最后一帧左下角比立绘空一大块——立绘的大衣往左铺开，模型的窄）。
机位拟合（fit240c.py）撑不开：头和枪对上以后，左边下摆还差 ≈ 30–80px（原版像素）。这里直接改模型：
main_0005 第 9.0 帧，大衣（body 网格）左半边的顶点在最终机位的屏幕里沿相机 x 往左推到立绘的左轮廓（逐行：立绘左边缘 vs 模型左边缘；
越靠左边缘推得越多、往身体中线渐变到 0；肩膀以上不动）。结果存成 research/p5r/menu3d_extra/main_0005.f16（1 帧 float16），
export_runtime.py 把它接在 main_0005 后面当第 11 帧；runtime_keys.py 的牌组「240」用第 11 帧。
用法：python3 coat240.py（会打印改之前 / 之后的剪影重合度）"""
import os
os.environ.setdefault('P5FIT_KEEP', 'gun_main,gun_outline')
import sys, json, numpy as np
from pathlib import Path
from scipy.ndimage import gaussian_filter1d
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parent)); sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import cam_from, m3d
from rast import silhouette
import fit240 as F1

ROOT = Path(__file__).resolve().parents[5]
OUT = ROOT / 'research/p5r/menu3d_extra/main_0005.f16'
F = 9.0
W, H = 960, 540            # 剪影分辨率（原版 1920×1080 的一半）
Y0 = 300                   # 原版 y 300（肩膀）以下才推


def left_edge(mask):
    """每行最左边的像素 x（没有 = nan）"""
    has = mask.any(1); x = np.argmax(mask, 1).astype(float); x[~has] = np.nan
    return x


def main():
    cam = cam_from(json.load(open(Path(__file__).with_name('final.json')))['240'][1])
    V = m3d.verts('main_0005', F).astype(np.float64)
    F1.W, F1.H = W, H; T = F1.target()
    BODY = np.zeros((H, W), bool); BODY[:, :int(1000 * W / 1920)] = True
    s0 = silhouette(V, cam, W, H)
    Lt, Ls = left_edge(T & BODY), left_edge(s0 & BODY)
    sc = 1920 / W
    d = np.nan_to_num((Ls - Lt) * sc, nan=0.0)                 # 原版像素：要往左推多少（>0）
    yy = np.arange(H) * sc
    d[yy < Y0] = 0; d = np.clip(d, 0, None)
    d *= np.clip((yy - Y0) / 120, 0, 1)                          # 肩膀下面渐入
    d = gaussian_filter1d(d, 6)
    # 身体中线：每行剪影（左边缘 + 右边缘）的中点，右边缘只看原版 x < 1000（手臂和枪另算）
    has = (s0 & BODY).any(1); Rs = np.where(has, W - 1 - np.argmax((s0 & BODY)[:, ::-1], 1), np.nan)
    C = np.nan_to_num((Ls + Rs) / 2 * sc, nan=0.0)
    R = m3d.rot_yxz(cam['rotation_degrees']); P0 = np.array(cam['position'])
    f = 540 / np.tan(np.radians(cam['fov']) / 2)
    sl = m3d.mesh_slice('c0001_body_051_UI_main')
    B = V[sl]; xy, z = m3d.project(B, cam)
    row = np.clip((xy[:, 1] / sc).astype(int), 0, H - 1)
    L = np.nan_to_num(Ls[row] * sc, nan=xy[:, 0])
    w = np.clip((C[row] - xy[:, 0]) / np.maximum(C[row] - L, 1), 0, 1) ** 1.2   # 左边缘 1 → 中线 0
    w[xy[:, 0] > 1000] = 0
    shift = -d[row] * w * z / f                                   # 相机空间 x（世界单位）
    V2 = V.copy(); V2[sl] = B + np.outer(shift, R[:, 0])
    s1 = silhouette(V2, cam, W, H)
    iou = lambda s: ((s & T & BODY).sum() / max(((s | T) & BODY).sum(), 1))
    print('iou before %.3f after %.3f, max push %.0f px' % (iou(s0), iou(s1), d.max()))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    V2.astype(np.float16).tofile(OUT)
    im = np.zeros((H, W, 3), np.uint8) + 40
    im[T & s1] = (200, 200, 200); im[T & ~s1] = (255, 60, 60); im[s1 & ~T] = (60, 160, 255)
    Image.fromarray(im).save(os.environ.get('P5FIT_OUT', '/tmp/p5fit') + '/coat240.png')
    print(OUT)


if __name__ == '__main__':
    main()
