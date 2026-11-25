"""牌组入场收尾（用户 2026-11-24：3D 最后一帧和 2D 立绘差太多，切的时候跳一大下）：在 239 后面加一个关键帧「240」——
main_0005 里枪从举过头顶甩下来、枪口朝右下的那一帧（≈8.4–8.5；第二版见 fit240b.py：用 9.0，手臂水平伸出），机位对上 2D 立绘 CAMPEQUIP_2D01（0.4996 倍、左上 (0, 0)，原版第 240 帧起）。
目标 = 立绘剪影和渲染剪影的双向倒角距离 + 立绘上量的右耳 / 右眼位置 + 离 239 的机位别太远（3D 从 239 甩到 240，镜头只小动）。
用法：P5FIT_OUT=/tmp/p5fit python3 tools/p5/menu3d/fit/equip_example/fit240.py  → 打印 [动作帧, 机位]，写进 final.json 的 "240"。"""
import os
os.environ.setdefault('P5FIT_KEEP', 'gun_main,gun_outline')   # 这一页拿着枪：剪影里要有枪
import sys, json
import numpy as np
from pathlib import Path
from PIL import Image
from scipy.optimize import minimize
from scipy.ndimage import distance_transform_edt as edt, binary_erosion
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import cam_from, m3d, head_pts
from rast import silhouette

W, H = 480, 270
CLIP = 'main_0005'
ROOT = Path(__file__).resolve().parents[5]
ART = sorted((ROOT / 'research/p5r/art/portrait').glob('BASE__P5_CAMPEQUIP_2D01__*.png'))[0]
FOV = 30.0012
# 立绘上量的（原版 1920×1080 坐标，立绘 0.4996 倍贴左上角）：右耳（脸左边那个白描边的耳朵）、右眼（露出来的那只，瞳孔中心）
OBS = {'earR': (352.0, 230.0), 'eyeR': (465.0, 231.0)}


def target():
    """立绘的剪影（alpha），缩到 W×H；往里收 2px（立绘外面一圈白描边，3D 没有）。"""
    p = Image.open(ART).convert('RGBA')
    q = p.resize((round(p.width * 0.4996), round(p.height * 0.4996)), Image.LANCZOS)
    a = Image.new('L', (1920, 1080), 0)
    a.paste(q.getchannel('A').crop((0, 0, 1920, 1080)), (0, 0))
    m = np.asarray(a.resize((W, H), Image.LANCZOS)) > 127
    return binary_erosion(m, iterations=2)


# 立绘的枪是画出来的夸张透视（比模型的枪大得多），剪影只比头和身子（原版 x < 1000），枪的方向靠动作帧挑
BODY = np.zeros((H, W), bool); BODY[:, :int(1000 * W / 1920)] = True


def make_obj(ref, f_fix=None, w_lm=2.0, w_sil=1.0, w_ref=0.3):
    T = target() & BODY; Dt = edt(~T)
    names = list(OBS); O = np.array([OBS[n] for n in names])

    def obj(q, detail=False):
        f = f_fix if f_fix is not None else float(np.clip(q[6], 7.5, 9.5))
        V = m3d.verts(CLIP, f).astype(np.float64)
        cam = cam_from(list(q[:6]) + [FOV])
        hp = head_pts(V)
        xy, z = m3d.project(np.array([hp[n] for n in names]), cam)
        if (z <= 5).any():
            return 1e6
        e_lm = np.hypot(*(xy - O).T).mean()
        s = silhouette(V, cam, W, H) & BODY
        if not s.any():
            return 1e6
        Ds = edt(~s)
        e_sil = (Dt[s].mean() + Ds[T].mean()) * 4          # 原版像素
        iou = (s & T).sum() / max((s | T).sum(), 1)
        e_ref = np.abs((np.array(q[:6]) - np.array(ref[:6])) / np.array([10, 10, 10, 5, 5, 5])).sum()
        v = w_lm * e_lm + w_sil * e_sil + w_ref * e_ref * 10
        return (v, e_lm, e_sil, iou) if detail else v
    return obj


def main():
    fin = json.load(open(Path(__file__).with_name('final.json')))
    ref = fin['239'][1]
    f_fix = float(sys.argv[1]) if len(sys.argv) > 1 else 8.42
    obj = make_obj(ref, f_fix)
    q0 = np.array(list(ref[:6]) + [f_fix], float)
    simplex = np.array([q0] + [q0 + np.eye(7)[k] * s for k, s in enumerate([8, 8, 8, 4, 4, 4, 0.0])])
    r = minimize(obj, q0, method='Nelder-Mead', options={'maxiter': 1200, 'xatol': 0.02, 'fatol': 0.01, 'initial_simplex': simplex})
    d = obj(r.x, True)
    print(f'f {f_fix}: total {d[0]:.1f}  lm {d[1]:.1f}px  sil {d[2]:.1f}px  iou {d[3]:.3f}', flush=True)
    q = r.x
    out = [round(f_fix, 3), [round(float(v), 3) for v in q[:6]] + [FOV]]
    print(json.dumps(out))
    # 叠图：红 = 渲染剪影轮廓，绿 = 立绘剪影轮廓
    V = m3d.verts(CLIP, out[0]).astype(np.float64)
    s = silhouette(V, cam_from(out[1]), 960, 540)
    T = np.asarray(Image.fromarray(target().astype(np.uint8) * 255).resize((960, 540))) > 127   # 叠图看整张（含枪）
    base = Image.open(ART).convert('RGBA')
    base = base.resize((round(base.width * 0.4996), round(base.height * 0.4996)), Image.LANCZOS).crop((0, 0, 1920, 1080)).resize((960, 540))
    bg = Image.new('RGBA', (960, 540), (150, 30, 40, 255)); bg.alpha_composite(base)
    o = np.asarray(bg.convert('RGB')).copy()
    es = s ^ binary_erosion(s); et = T ^ binary_erosion(T)
    o[et] = (0, 255, 0); o[es] = (255, 40, 40)
    Image.fromarray(o).save(os.environ.get('P5FIT_OUT', '/tmp/p5fit') + f'/eq240_fit_{f_fix:.2f}.png')


if __name__ == '__main__':
    main()
