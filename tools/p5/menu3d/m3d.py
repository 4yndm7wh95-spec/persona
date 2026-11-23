"""Python 侧读取菜单 3D Joker 的 float16 顶点数据（research/p5r/menu3d_data），按 Godot Camera3D 的约定投影，找几个特征点（双眼、头顶、下巴、手、膝盖）。"""
import json, numpy as np
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
D = ROOT / 'research/p5r/menu3d_data'
MESHES = json.load(open(D / 'meshes.json'))
CLIPS = {c['name']: c for c in json.load(open(D / 'clips.json'))}
NV = sum(m['vertices'] for m in MESHES)
_cache = {}

def frames(clip):
    if clip not in _cache:
        a = np.fromfile(D / f'{clip}.f16', dtype='<f2').astype(np.float32)
        _cache[clip] = a.reshape(-1, NV, 3)
    return _cache[clip]

def verts(clip, f):
    fr = frames(clip); n = len(fr)
    f0 = int(np.clip(np.floor(f), 0, n - 1)); f1 = min(f0 + 1, n - 1); t = float(f - np.floor(f))
    return fr[f0] * (1 - t) + fr[f1] * t if f1 != f0 and t > 1e-4 else fr[f0]

def mesh_slice(name, k=0):
    hits = [m for m in MESHES if m['name'] == name]
    m = hits[k]
    return slice(m['offset'], m['offset'] + m['vertices'])

def rot_yxz(deg):
    x, y, z = np.radians(deg)
    Rx = np.array([[1, 0, 0], [0, np.cos(x), -np.sin(x)], [0, np.sin(x), np.cos(x)]])
    Ry = np.array([[np.cos(y), 0, np.sin(y)], [0, 1, 0], [-np.sin(y), 0, np.cos(y)]])
    Rz = np.array([[np.cos(z), -np.sin(z), 0], [np.sin(z), np.cos(z), 0], [0, 0, 1]])
    return Ry @ Rx @ Rz          # Godot EULER_ORDER_YXZ

def project(P, cam, W=1920, H=1080):
    """cam: position, rotation_degrees, fov (vertical, keep_height), h_offset, v_offset → pixel xy, depth"""
    R = rot_yxz(cam['rotation_degrees']); C = np.array(cam['position'], float)
    q = (P - C) @ R                      # world → camera (columns of R are camera axes)
    q[:, 0] -= cam.get('h_offset', 0.0); q[:, 1] -= cam.get('v_offset', 0.0)
    f = (H / 2) / np.tan(np.radians(cam['fov']) / 2)
    z = -q[:, 2]
    x = W / 2 + f * q[:, 0] / z; y = H / 2 - f * q[:, 1] / z
    return np.stack([x, y], 1), z

def landmarks(V):
    out = {}
    out['eye_r'] = V[mesh_slice('eye r0_main')].mean(0)
    out['eye_l'] = V[mesh_slice('eye l0_main')].mean(0)
    hair = V[mesh_slice('c0001_hair_002a')]
    out['hair_top'] = hair[np.argmax(hair[:, 1])]
    face = V[mesh_slice('c0001_faceUI_main', 0)]
    out['face_low'] = face[np.argmin(face[:, 1])]
    return out

def euler_yxz(R):
    """rotation matrix (columns = camera axes) → Godot rotation_degrees (EULER_ORDER_YXZ)"""
    x = np.arcsin(np.clip(-R[1, 2], -1, 1)); y = np.arctan2(R[0, 2], R[2, 2]); z = np.arctan2(R[1, 0], R[1, 1])
    return np.degrees([x, y, z])

def look_at(pos, target, up=(0, 1, 0)):
    """camera at pos looking at target → Godot camera dict (fov to be filled in)"""
    pos = np.asarray(pos, float); zc = pos - np.asarray(target, float); zc /= np.linalg.norm(zc)
    xc = np.cross(np.asarray(up, float), zc); xc /= np.linalg.norm(xc); yc = np.cross(zc, xc)
    return {'position': pos.tolist(), 'rotation_degrees': euler_yxz(np.stack([xc, yc, zc], 1)).tolist()}

FACE = dict(nose=1873, chin=1919, earR=2823, earL=1989, jawR=2725, jawL=1895)

def head_points(V):
    """固定顶点的脸部特征点（模型空间）：双眼球中心、鼻尖、下巴底、两耳、两腮"""
    out = {'eyeR': V[mesh_slice('eye r0_main')].mean(0), 'eyeL': V[mesh_slice('eye l0_main')].mean(0)}
    for k, i in FACE.items(): out[k] = V[i]
    return out
