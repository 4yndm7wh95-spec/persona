"""SKILL 页（消耗堆）实际用的做法，留作弃牌堆 / 牌组的参考：
机位固定在 C2（= 2D 立绘的机位），每帧只按原版里量的双眼位置解「俯仰 / 偏航」小偏移；动作 = 片段帧或往定格姿势 main_0016 的线性混合。
EYES 是原版逐帧量的双眼屏幕坐标（1920×1080，序号 → (右眼, 左眼)；原版 113–117 是闭眼，量的是眼缝中点）。"""
import sys, numpy as np
from pathlib import Path
from scipy.optimize import least_squares
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import m3d, cam_from, head_pts
C2 = np.array([-57.0, 44.0, 73.6, 41.2, -48.9, 11.1, 40.5])
EYES = {112: ((623, 63), (697, 112)), 113: ((635, 72), (700, 121)), 114: ((648, 80), (702, 130)), 115: ((658, 72), (711, 124)),
        116: ((668, 63), (720, 117)), 117: ((680, 52), (737, 113)), 118: ((730, 30), (782.5, 90)),
        119: ((685, 185), (747.5, 252.5)), 120: ((742, 142), (810, 206)), 121: ((796.5, 119), (875, 185)), 122: ((825, 127.5), (899, 199))}
A16 = m3d.verts('main_0016', 0).astype(np.float64)

def pose(spec):
    """('clip', 片段, 帧) 或 ('blend', 片段, 帧, 往 main_0016 的权重)"""
    if spec[0] == 'clip': return m3d.verts(spec[1], spec[2]).astype(np.float64)
    a = m3d.verts(spec[1], spec[2]).astype(np.float64); return a * (1 - spec[3]) + A16 * spec[3]

def cam_d(d):
    p = C2.copy(); p[3:3 + len(d)] += np.array(d); return cam_from(p)

def eyes_xy(V, cam):
    hp = head_pts(V); xy, _ = m3d.project(np.array([hp['eyeR'], hp['eyeL']]), cam); return xy

def fit_delta(V, idx, roll=False):
    O = np.array(EYES[idx], float)
    s = least_squares(lambda d: (eyes_xy(V, cam_d(d)) - O).ravel(), np.zeros(3 if roll else 2), diff_step=1e-3)
    return s.x, np.hypot(*s.fun.reshape(-1, 2).T)

if __name__ == '__main__':
    design = {112: ('clip', 'main_0015', 2.5), 114: ('clip', 'main_0015', 2.7), 116: ('clip', 'main_0015', 2.85), 118: ('clip', 'main_0015', 3.0)}
    for idx, spec in design.items():
        d, e = fit_delta(pose(spec), idx)
        print(idx, spec, 'delta (pitch, yaw)', np.round(d, 2).tolist(), 'eye err px', np.round(e, 1).tolist())
