"""用脸上的固定顶点（双眼球中心、鼻尖、下巴、两耳、两腮、嘴）对原版截图里量出来的屏幕坐标解 Godot Camera3D 参数。
用法见 README.md。参数 p = [x, y, z, 旋转 x, y, z（度，YXZ）, fov（竖直，度）]。"""
import numpy as np
from scipy.optimize import least_squares
import common  # noqa: F401  (sys.path)
import m3d

def head_pts(V):
    hp = m3d.head_points(V)
    hp['mouth'] = V[m3d.mesh_slice('c0001_faceUI_main', 2)].mean(0)
    return hp

def pts3(clip, f):
    V = m3d.verts(clip, f).astype(np.float64)
    return head_pts(V), V

def cam_from(p, fov=None):
    return {'position': [float(v) for v in p[:3]], 'rotation_degrees': [float(v) for v in p[3:6]], 'fov': float(p[6] if fov is None else fov)}

def fit(obs_list, p0, fix_fov=None, w=None):
    """obs_list: [(hp: 名字→3D 点, obs: 名字→(x, y) 1920×1080 屏幕坐标), …]（可以多帧共用一个机位）→ (p, 每个点的像素误差)"""
    def res(p):
        cam = cam_from(p, fix_fov); r = []
        for hp, obs in obs_list:
            names = [k for k in obs if k in hp]
            xy, z = m3d.project(np.array([hp[k] for k in names]), cam)
            for i, k in enumerate(names):
                r += list((xy[i] - np.array(obs[k])) * (w or {}).get(k, 1.0))
        return np.array(r)
    p0 = list(p0)
    x0 = np.array(p0[:6] + ([] if fix_fov else [p0[6]]), float)
    if fix_fov:
        sol = least_squares(lambda q: res(np.concatenate([q, [fix_fov]])), x0, x_scale=[10, 10, 10, 5, 5, 5])
        p = np.concatenate([sol.x, [fix_fov]])
    else:
        sol = least_squares(res, x0, x_scale=[10, 10, 10, 5, 5, 5, 3]); p = sol.x
    rr = res(p).reshape(-1, 2)
    return p, np.hypot(rr[:, 0], rr[:, 1])

def init_cam(hp, dist=110, fov=40, roll=0.0, side=0.0, lift=0.0):
    """以双眼中点为目标、从脸的正前方偏 side / lift 度、距离 dist 的初始机位（多试几组 roll / side / lift 取误差最小的）"""
    m = (hp['eyeR'] + hp['eyeL']) / 2; x = hp['eyeL'] - hp['eyeR']; x /= np.linalg.norm(x)
    up = np.array([0, 1., 0]); up -= x * up.dot(x); up /= np.linalg.norm(up); fw = np.cross(x, up)
    d = fw * np.cos(np.radians(side)) + x * np.sin(np.radians(side)); d = d * np.cos(np.radians(lift)) + up * np.sin(np.radians(lift))
    c = m3d.look_at(m + d * dist, m); r = c['rotation_degrees']; r[2] += roll
    return list(c['position']) + list(r) + [fov]

def best_fit(hp, obs, fov=42, starts=((-30, 0, 30), (-40, 0, 40), (-25, 15))):
    best = None
    for roll in starts[0]:
        for side in starts[1]:
            for lift in starts[2]:
                try: p, e = fit([(hp, obs)], init_cam(hp, 110, fov, roll, side, lift), fix_fov=fov)
                except Exception: continue
                if best is None or e.mean() < best[1].mean(): best = (p, e)
    return best
