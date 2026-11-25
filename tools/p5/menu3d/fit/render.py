"""用 Godot（render_f16.gd）真渲染，以及把渲染结果叠在原版帧上看（暗部蓝、亮部黄）。"""
import json, subprocess, numpy as np
from PIL import Image, ImageDraw
from common import ROOT, OUT, GODOT, orig
HIDE = ['smartphone', 'card', 'school_bag', 'gun_outline', 'gun_main'] + [f'ef line_obj_0{i}' for i in range(1, 9)]

def render(items, size=(960, 540), ss=1, tag='ov'):
    """items: [(片段, 帧, 机位 dict, 输出 png[, 顶点文件 .bin（float32，可选：混合帧 / 改过的顶点）]), …]"""
    jobs = []
    for it in items:
        c, f, cam, o = it[:4]
        j = {'clip': c, 'frame': float(f), 'camera': cam, 'mode': 'color', 'hide': HIDE, 'out': str(o)}
        if len(it) > 4 and it[4]: j['verts'] = str(it[4])
        jobs.append(j)
    jp = OUT / f'{tag}_jobs.json'; json.dump({'size': list(size), 'ss': ss, 'jobs': jobs}, open(jp, 'w'))
    subprocess.run(['xvfb-run', '-a', '-s', '-screen 0 1920x1080x24', GODOT, '--path', '.', '--rendering-driver', 'opengl3',
                    '--script', 'render_f16.gd', '--', str(ROOT), str(jp)], cwd=ROOT / 'tools/p5/menu3d', capture_output=True, check=True)

def overlay(idx, png, label=''):
    o = np.asarray(orig(idx, (960, 540))).astype(float)
    r = Image.open(png).convert('RGBA').resize((960, 540))
    a = np.asarray(r)[..., 3] > 128; rr = np.asarray(r.convert('RGB')).astype(float).mean(-1)
    out = o * 0.6; dark = a & (rr < 128); light = a & (rr >= 128)
    out[dark] = out[dark] * 0.4 + np.array([0, 120, 255]) * 0.6
    out[light] = out[light] * 0.4 + np.array([255, 230, 0]) * 0.6
    im = Image.fromarray(out.clip(0, 255).astype(np.uint8)); ImageDraw.Draw(im).text((6, 6), label, fill='white')
    return im
