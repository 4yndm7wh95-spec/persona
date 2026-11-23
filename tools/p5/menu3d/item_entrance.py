"""弃牌堆入场（= P5R 暂停菜单 ITEM 页）的 3D Joker 逐帧渲染：原版大约 177–186 十帧（镜头从远处推近：右边站着的小 Joker → 转身、手扫过 → 定格成立绘）。
结果写到 research/p5r/menu3d_item/：f00.png … f09.png（裁到不透明区域）+ frames.json（每帧在原版屏幕坐标里的左上角）。

结论（2026-11-23，会话 E）：
- 立绘 CAMPITEM_2D01 在原版里是缩放 0.5、左上 (0, 0)（序号 196）。用 tools/p5/menu3d/fit/artfit.py 对 29 个片段末帧拟合剪影，
  没有像 SKILL 页那样明显的赢家（main_0035 0.807、main_0032 0.792、blend_0000 0.791、main_0010 0.785 …），立绘左上举起的手都对不上。
- 所以动作用 main_0010（Codex 给 ITEM 页挑的片段，转身、手伸出去张开扫过，同原版 183 那只扫过的手；拟合排第四），
  最后一帧用它拟合出来的机位 FINAL；前面每帧同一个朝向，沿视线拉远（zoom 倍）、平移到原版那一帧头的位置（HEAD，按原版逐帧量的粗略位置）。
- 动作只有形状、没有表情；最后一帧和 2D 立绘不完全一样（立绘那只举起的手），切立绘时界面在动，看不出来。
用法：python3 tools/p5/menu3d/item_entrance.py    （GODOT 环境变量 = Godot 4.5.1 mono 可执行文件）
"""
import json, os, subprocess, numpy as np
from pathlib import Path
from PIL import Image
import m3d

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'research/p5r/menu3d_item'
GODOT = os.environ.get('GODOT', '/opt/tools/Godot_v4.5.1-stable_mono_linux_x86_64/Godot_v4.5.1-stable_mono_linux.x86_64')
FINAL = np.array([98.29, 222.4, 80.88, -30.7, 35.5, 0.0, 15.01])        # main_0010 末帧对立绘剪影拟合的机位（IoU 0.785）
EXT = 60
HIDE = ['smartphone', 'card', 'school_bag', 'gun_outline', 'gun_main'] + [f'ef line_obj_0{i}' for i in range(1, 9)]
# 第 i 帧：(main_0010 的帧, 头中心在原版屏幕上的位置, 拉远倍数)
FRAMES = [
    (0.0, (1400, 400), 3.4), (1.1, (1340, 390), 3.2), (2.2, (1100, 380), 2.6), (3.3, (1000, 300), 2.0), (4.4, (860, 260), 1.6),
    (5.6, (760, 220), 1.25), (6.7, (640, 220), 1.06), (7.8, (700, 280), 1.0), (8.9, (730, 315), 1.0), (10.0, None, 1.0),
]


def head_center(V):
    return V[m3d.mesh_slice('c0001_faceUI_main', 0)].mean(0)


def camera(V, head_xy, zoom):
    p = FINAL.copy()
    R = m3d.rot_yxz(p[3:6])
    hc = head_center(V)
    C = hc + (p[:3] - head_center(m3d.verts('main_0010', 10.0))) * zoom if head_xy is not None else p[:3].copy()
    if head_xy is not None:
        f = 540 / np.tan(np.radians(p[6]) / 2)
        cam = {'position': C.tolist(), 'rotation_degrees': p[3:6].tolist(), 'fov': float(p[6])}
        for _ in range(3):                                           # 平移到头的目标位置
            xy, z = m3d.project(hc[None].astype(np.float64), cam)
            C = C + R[:, 0] * (xy[0, 0] - head_xy[0]) * z[0] / f - R[:, 1] * (xy[0, 1] - head_xy[1]) * z[0] / f
            cam['position'] = C.tolist()
    f = 540 / np.tan(np.radians(p[6]) / 2)
    fov = np.degrees(2 * np.arctan((540 + EXT) / f))
    return {'position': [float(v) for v in C], 'rotation_degrees': p[3:6].tolist(), 'fov': float(fov)}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    tmp = OUT / '_tmp'; tmp.mkdir(exist_ok=True)
    jobs = []
    for i, (fr, head, zoom) in enumerate(FRAMES):
        V = m3d.verts('main_0010', fr).astype(np.float64)
        jobs.append({'clip': 'main_0010', 'frame': float(fr), 'camera': camera(V, head, zoom), 'mode': 'color', 'hide': HIDE, 'out': str(tmp / f'r{i}.png')})
    jp = tmp / 'jobs.json'
    json.dump({'size': [1920 + 2 * EXT, 1080 + 2 * EXT], 'ss': 2, 'jobs': jobs}, open(jp, 'w'))
    subprocess.run(['xvfb-run', '-a', '-s', '-screen 0 1920x1080x24', GODOT, '--path', '.', '--rendering-driver', 'opengl3', '--script', 'render_f16.gd', '--', str(ROOT), str(jp)],
                   cwd=Path(__file__).parent, check=True, capture_output=True)
    meta = {'ext': EXT, 'note': '每帧 PNG 在原版 1920×1080 屏幕坐标里的左上角 = (x, y)；原版 2D 立绘 CAMPITEM_2D01 = 缩放 0.5、左上 (0, 0)', 'frames': {}}
    for i in range(len(FRAMES)):
        im = Image.open(tmp / f'r{i}.png').convert('RGBA')
        a = np.asarray(im)[..., 3]
        ys, xs = np.nonzero(a > 0)
        x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
        im.crop((x0, y0, x1, y1)).save(OUT / f'f{i:02d}.png', optimize=True)
        meta['frames'][str(i)] = {'x': int(x0) - EXT, 'y': int(y0) - EXT, 'w': int(x1 - x0), 'h': int(y1 - y0)}
    json.dump(meta, open(OUT / 'frames.json', 'w'), indent=1, ensure_ascii=False)
    for p in tmp.iterdir(): p.unlink()
    tmp.rmdir()
    print(json.dumps(meta['frames']))


if __name__ == '__main__':
    main()
