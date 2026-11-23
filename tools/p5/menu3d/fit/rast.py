"""在 Python 里把菜单 3D Joker 的剪影光栅化（去掉道具网格），比开 Godot 快很多，拟合时用来算轮廓重合度。"""
import numpy as np
from PIL import Image, ImageDraw
import common  # noqa: F401
import m3d
PROPS = {'smartphone', 'card', 'school_bag', 'gun_outline', 'gun_main'} | {f'ef line_obj_0{i}' for i in range(1, 9)}
TRIS = np.concatenate([np.array(m['triangles'], int).reshape(-1, 3) + m['offset'] for m in m3d.MESHES if m['name'] not in PROPS])

def silhouette(V, cam, W=480, H=270):
    """V: 顶点（模型空间）；cam: Godot 机位 dict（按 1920×1080 投影后缩到 W×H）→ bool 掩码"""
    xy, z = m3d.project(V, cam, 1920, 1080)
    xy = xy * (W / 1920.0)
    im = Image.new('L', (W, H), 0); d = ImageDraw.Draw(im); ok = z > 1
    for a, b, c in TRIS:
        if ok[a] and ok[b] and ok[c]:
            d.polygon([tuple(xy[a]), tuple(xy[b]), tuple(xy[c])], fill=255)
    return np.asarray(im) > 0
