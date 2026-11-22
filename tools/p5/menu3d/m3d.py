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
