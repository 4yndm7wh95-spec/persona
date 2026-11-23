"""SKILL 页 119–122：同时解「往 main_0016 的混合权重 + 机位小偏移」，目标 = 双眼位置 + 反色区里白腿的轮廓（倒角距离）。"""
import numpy as np
from scipy.optimize import minimize
from scipy.ndimage import distance_transform_edt as edt
from seqdesign import m3d, A16, EYES, cam_d, eyes_xy
from legmask import masks_r
from rast import silhouette
W, H = 480, 270
def obj_factory(idx, src):
    leg, E = masks_r(idx, W, H); Dleg = edt(~leg); O = np.array(EYES[idx], float)
    def obj(q, detail=False):
        w = np.clip(q[0], 0, 1.2); V = src * (1 - w) + A16 * w; cam = cam_d([q[1], q[2]])
        ee = np.hypot(*(eyes_xy(V, cam) - O).T).mean()
        sil = silhouette(V, cam, W, H); s = sil & E
        ch = (Dleg[s].mean() if s.any() else 30) + edt(~sil)[leg].mean()
        iou = (s & leg).sum() / max((s | leg).sum(), 1)
        v = ee * 0.25 + ch
        return (v, ee, ch * 4, iou) if detail else v
    return obj
if __name__ == '__main__':
    src = m3d.verts('main_0015', 3).astype(np.float64)
    for idx, w0 in [(119, 0.2), (120, 0.6), (121, 0.85), (122, 1.0)]:
        obj = obj_factory(idx, src)
        r = minimize(obj, np.array([w0, 0.0, 0.0]), method='Nelder-Mead', options={'maxiter': 300, 'xatol': 0.002, 'fatol': 0.001})
        v, ee, ch, iou = obj(r.x, True)
        print(idx, 'w %.3f dp %.2f dy %.2f' % tuple(r.x), 'eye err %.1f chamfer %.1fpx iou %.3f' % (ee, ch, iou), flush=True)
