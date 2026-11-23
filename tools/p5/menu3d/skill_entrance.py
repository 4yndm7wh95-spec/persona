"""消耗堆入场（= P5R 暂停菜单 SKILL 页）的 3D Joker 逐帧渲染：原版 frame 112–122 各一张，对着原版逐帧拟合出来的机位和动作。
结果写到 research/p5r/menu3d_skill/：f112.png … f122.png（裁到不透明区域）+ frames.json（每帧在原版屏幕坐标里的左上角）。

拟合结论（2026-11-23，脚本和对比图在会话 scratchpad，结论记在这里）：
- SKILL 页最后定格的姿势是 main_0016（只有 2 帧的定格片段）。用它对原版 2D 立绘 CAMPSKILL_2D01 的整个剪影拟合机位，
  IoU 0.85，在 37 个候选姿势里明显最好 → 机位 C2 = 位置 (-57, 44, 73.6)，旋转 (41.2, -48.9, 11.1)，fov 40.5（低机位仰拍）。
- 原版 112–118：Joker 低头站着、膝盖刚抬起（main_0015 第 2.5→3 帧，几乎不动）；机位在 C2 的位置不动，只是俯 2～5°、
  从左摇到右 8°（逐帧按双眼位置解出来）。
- 原版 118→119：镜头往上仰约 4°（原版头部那一下往下跳 160 像素就是这个），动作从 main_0015 第 4.5 帧往 main_0016 混合
  （权重 0.13 / 0.48 / 0.70 / 0.74，逐帧按双眼 + 反色区里白腿的轮廓解出来，白腿 IoU 0.46～0.73）。
- 123 起换成 2D 立绘（立绘 = main_0016 + C2）。
顶点混合是 Python 侧按顶点线性插值（两个姿势很接近，看不出问题），用 render_f16.gd 的 "verts" 渲染。
画布四边各多渲 60 像素（同焦距），这样放进我们排版（缩放 0.961）以后上 / 左 / 下都不会露出渲染边缘。
用法：python3 tools/p5/menu3d/skill_entrance.py
"""
import json, subprocess, numpy as np
from pathlib import Path
from PIL import Image
import m3d

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'research/p5r/menu3d_skill'
GODOT = '/root/godot/Godot_v4.5.1-stable_mono_linux_x86_64/Godot_v4.5.1-stable_mono_linux.x86_64'
C2 = np.array([-57.0, 44.0, 73.6, 41.2, -48.9, 11.1, 40.5])
EXT = 60
HIDE = ['smartphone', 'card', 'school_bag', 'gun_outline', 'gun_main'] + [f'ef line_obj_0{i}' for i in range(1, 9)]
# 原版帧号: (动作, 机位偏移 (俯仰, 偏航) 度)。动作 = ('clip', 片段, 帧) 或 ('blend', 片段, 帧, 往 main_0016 的权重)
FRAMES = {
    112: (('clip', 'main_0015', 2.5), (-1.78, -4.51)),
    113: (('clip', 'main_0015', 2.6), (-1.83, -3.46)),
    114: (('clip', 'main_0015', 2.7), (-1.89, -2.42)),
    115: (('clip', 'main_0015', 2.8), (-2.52, -1.39)),
    116: (('clip', 'main_0015', 2.85), (-3.07, -0.64)),
    117: (('clip', 'main_0015', 2.92), (-3.73, 0.51)),
    118: (('clip', 'main_0015', 3.0), (-5.46, 3.52)),
    119: (('blend', 'main_0015', 4.5, 0.126), (-1.30, 4.00)),
    120: (('blend', 'main_0015', 4.5, 0.476), (-1.30, 3.40)),
    121: (('blend', 'main_0015', 4.5, 0.696), (-1.30, 3.30)),
    122: (('blend', 'main_0015', 4.5, 0.739), (-1.10, 3.50)),
}

def pose(spec):
    if spec[0] == 'clip':
        return m3d.verts(spec[1], spec[2]).astype(np.float64)
    a = m3d.verts(spec[1], spec[2]).astype(np.float64); b = m3d.verts('main_0016', 0).astype(np.float64)
    return a * (1 - spec[3]) + b * spec[3]

def camera(delta):
    p = C2.copy(); p[3] += delta[0]; p[4] += delta[1]
    f = 540 / np.tan(np.radians(p[6]) / 2)
    fov = np.degrees(2 * np.arctan((540 + EXT) / f))            # 同焦距、画布四边各多 EXT 像素
    return {'position': p[:3].tolist(), 'rotation_degrees': p[3:6].tolist(), 'fov': float(fov)}

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    tmp = OUT / '_tmp'; tmp.mkdir(exist_ok=True)
    jobs = []
    for idx, (spec, delta) in FRAMES.items():
        vb = tmp / f'v{idx}.bin'; pose(spec).astype('<f4').tofile(vb)
        jobs.append({'clip': spec[1], 'frame': float(spec[2]), 'verts': str(vb), 'camera': camera(delta), 'mode': 'color', 'hide': HIDE, 'out': str(tmp / f'r{idx}.png')})
    jp = tmp / 'jobs.json'
    json.dump({'size': [1920 + 2 * EXT, 1080 + 2 * EXT], 'ss': 2, 'jobs': jobs}, open(jp, 'w'))
    subprocess.run(['xvfb-run', '-a', '-s', '-screen 0 1920x1080x24', GODOT, '--path', '.', '--rendering-driver', 'opengl3', '--script', 'render_f16.gd', '--', str(ROOT), str(jp)],
                   cwd=Path(__file__).parent, check=True, capture_output=True)
    meta = {'ext': EXT, 'note': '每帧 PNG 在原版 1920×1080 屏幕坐标里的左上角 = (x, y)；原版 2D 立绘位置 = 缩放 0.50795、左上 (0.47, 18.05)', 'frames': {}}
    for idx in FRAMES:
        im = Image.open(tmp / f'r{idx}.png').convert('RGBA')
        a = np.asarray(im)[..., 3]
        ys, xs = np.nonzero(a > 0)
        x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
        im.crop((x0, y0, x1, y1)).save(OUT / f'f{idx}.png', optimize=True)
        meta['frames'][str(idx)] = {'x': int(x0) - EXT, 'y': int(y0) - EXT, 'w': int(x1 - x0), 'h': int(y1 - y0)}
    json.dump(meta, open(OUT / 'frames.json', 'w'), indent=1, ensure_ascii=False)
    for p in tmp.iterdir(): p.unlink()
    tmp.rmdir()
    print(json.dumps(meta['frames']))

if __name__ == '__main__':
    main()
