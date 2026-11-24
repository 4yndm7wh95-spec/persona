"""弃牌堆入场（= P5R 暂停菜单 ITEM 页）的 3D Joker 逐帧渲染 + 每帧的区域形状：原版序号 177–185 九帧，第 186 帧起是 2D 立绘。
输出：research/p5r/menu3d_item/f00.png … f08.png（裁到不透明区域）+ frames.json（每帧在原版屏幕坐标里的左上角）；
      p5mod/src/Vfx/PileDiscardEntrance.cs（生成的：每帧左上角、黑区 / 红底多边形，原版 1920×1080 坐标）。

结论（2026-11-23，牌堆会话重做；之前会话 E 的版本只拟合了最后一帧、前面按比例推近，姿势和原版对不上）：
- 动作 = main_0010（转身、手扫过脸、手举到头边），对；机位要逐帧解：tools/p5/menu3d/fit/item_example/
  （landmarks.py 原版逐帧量的脸部特征点，refine.py 特征点 + 黑区里白色身体 / 手臂轮廓细调，final_fit.py 定稿）。
  最后用的是 item_example/joint.py：九帧一起解，特征点误差 + 机位轨迹平滑，fov 30（用 182 那只手的大小定的）。
  动作帧不是匀速的（joint.py 的 SCHEDULE：手 181 还伸在右边画外、182 张开在右边、183 已经扫到脸前），按逐帧叠原版挑的。每帧单独再加轮廓细调（refine.py）会让动作帧来回跳、机位乱转，反而不像，没用。
  对照（红线 = 我们的剪影叠在原版上）：脸、扫过的手、举起的手都对得上，183 那一帧手挡在脸前也对。
- 原版这一段的画面：主菜单的黑白星纹底（Joker 在上面是黑的）+ 黑区（Joker 进去变白，P 系列的撞色）+ 右下慢慢露出来的红底。
  黑区 / 红底的形状逐帧从原版截图里取（item_example/regions_v4.py：Joker 身上靠渲染和原版的明暗是否一致来判断在哪一区）。
- 最后一帧和 2D 立绘几乎同一个姿势（手举到头边），原版也是这里硬切。
用法：python3 tools/p5/menu3d/item_entrance.py
"""
import json, os, subprocess, sys, numpy as np
from pathlib import Path
from PIL import Image
import m3d

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'tools/p5/menu3d/fit')); sys.path.insert(0, str(ROOT / 'tools/p5/menu3d/fit/item_example'))
OUT = ROOT / 'research/p5r/menu3d_item'
GODOT = os.environ.get('GODOT', '/root/godot/Godot_v4.5.1-stable_mono_linux_x86_64/Godot_v4.5.1-stable_mono_linux.x86_64')
EXT = 140          # 我们的排版把原版缩到 0.977、左移 80：原版右边缘落在 1795，帧图要多渲到右边 / 下边外面，伸到右边的手臂才不会被截断
HIDE = ['smartphone', 'card', 'school_bag', 'gun_outline', 'gun_main'] + [f'ef line_obj_0{i}' for i in range(1, 9)]
# 原版序号: (main_0010 的帧, 机位 [x, y, z, 旋转 x, y, z, fov])
FRAMES = {
    177: (0.00, [4.444, 96.947, 123.478, 28.599, 12.391, -7.952, 29.999]),
    178: (0.59, [7.463, 107.658, 120.221, 24.705, 12.264, -8.574, 29.999]),
    179: (1.68, [12.177, 117.018, 115.269, 19.665, 10.01, -8.694, 29.999]),
    180: (2.77, [14.78, 126.906, 107.093, 14.185, 6.346, -8.537, 29.999]),
    181: (3.40, [14.256, 136.809, 97.847, 8.422, 1.412, -8.451, 29.999]),
    182: (4.60, [9.677, 146.254, 88.303, 2.665, -4.73, -7.699, 29.999]),
    183: (7.00, [0.655, 154.607, 86.271, -2.349, -11.195, -5.394, 29.999]),
    184: (7.13, [-9.577, 162.093, 85.852, -6.814, -17.514, -2.583, 29.999]),
    185: (8.22, [-17.784, 168.941, 81.15, -10.921, -23.217, -0.308, 29.999]),
}


def cam(c, ext=0):
    f = 540 / np.tan(np.radians(c[6]) / 2)
    return {'position': c[:3], 'rotation_degrees': c[3:6], 'fov': float(np.degrees(2 * np.arctan((540 + ext) / f)))}


def godot(jobs, size, ss, tmp):
    jp = tmp / 'jobs.json'
    json.dump({'size': list(size), 'ss': ss, 'jobs': jobs}, open(jp, 'w'))
    subprocess.run(['xvfb-run', '-a', '-s', '-screen 0 1920x1080x24', GODOT, '--path', '.', '--rendering-driver', 'opengl3', '--script', 'render_f16.gd', '--', str(ROOT), str(jp)],
                   cwd=Path(__file__).parent, check=True, capture_output=True)


def edge_out(poly, m=14):
    """贴着原版画面边的点推到边外 40px（C# 那边再把右 / 下边外的点推到屏幕边外：我们的排版把原版缩小了，原版的右 / 下边缘在屏幕里面）"""
    out = []
    for x, y in poly:
        x = -40.0 if x <= m else 1960.0 if x >= 1920 - m else x
        y = -40.0 if y <= m else 1120.0 if y >= 1080 - m else y
        out.append([x, y])
    return out


def cs_vec(p): return f'new({p[0]:.1f}f, {p[1]:.1f}f)'


def cs_polys(polys):
    return 'new Vector2[][] { ' + ', '.join('new Vector2[] { ' + ', '.join(cs_vec(q) for q in p) + ' }' for p in polys) + ' }'


def main():
    from regions_v4 import regions
    OUT.mkdir(parents=True, exist_ok=True)
    tmp = OUT / '_tmp'; tmp.mkdir(exist_ok=True)
    idxs = sorted(FRAMES)
    jobs = [{'clip': 'main_0010', 'frame': float(FRAMES[i][0]), 'camera': cam(FRAMES[i][1], EXT), 'mode': 'color', 'hide': HIDE, 'out': str(tmp / f'r{i}.png')} for i in idxs]
    godot(jobs, (1920 + 2 * EXT, 1080 + 2 * EXT), 2, tmp)
    small = [{'clip': 'main_0010', 'frame': float(FRAMES[i][0]), 'camera': cam(FRAMES[i][1]), 'mode': 'color', 'hide': HIDE, 'out': str(tmp / f's{i}.png')} for i in idxs]
    godot(small, (960, 540), 1, tmp)
    for old in OUT.glob('f*.png'): old.unlink()
    meta = {'ext': EXT, 'note': '第 k 帧 = 原版序号 177 + k；PNG 在原版 1920×1080 屏幕坐标里的左上角 = (x, y)；原版 2D 立绘 CAMPITEM_2D01 = 缩放 0.5、左上 (0, 0)', 'frames': {}}
    at, dark, red = [], [], []
    for k, i in enumerate(idxs):
        im = Image.open(tmp / f'r{i}.png').convert('RGBA')
        ys, xs = np.nonzero(np.asarray(im)[..., 3] > 0)
        x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
        im.crop((x0, y0, x1, y1)).save(OUT / f'f{k:02d}.png', optimize=True)
        meta['frames'][str(k)] = {'orig': i, 'x': int(x0) - EXT, 'y': int(y0) - EXT, 'w': int(x1 - x0), 'h': int(y1 - y0)}
        at.append((int(x0) - EXT, int(y0) - EXT))
        reg, _ = regions(i, tmp / f's{i}.png')
        dark.append([edge_out(p) for p in reg['dark']]); red.append([edge_out(p) for p in reg['red']])
    json.dump(meta, open(OUT / 'frames.json', 'w'), indent=1, ensure_ascii=False)
    for p in tmp.iterdir(): p.unlink()
    tmp.rmdir()
    cs = ROOT / 'p5mod/src/Vfx/PileDiscardEntrance.cs'
    cs.write_text('// 生成的文件（tools/p5/menu3d/item_entrance.py），别手改。\n'
                  'using Godot;\n\nnamespace P5Joker;\n\n'
                  '/// <summary>弃牌堆入场（原版 ITEM 页序号 177–185）逐帧数据，原版 1920×1080 屏幕坐标：3D 帧图左上角、黑区（Joker 在里面反色变白）和红底的多边形。</summary>\n'
                  'internal static class PileDiscardEntrance\n{\n'
                  '\tpublic static readonly Vector2[] FrameAt = { ' + ', '.join(cs_vec(p) for p in at) + ' };\n'
                  '\tpublic static readonly Vector2[][][] Dark =\n\t{\n' + ''.join('\t\t' + cs_polys(d) + ',\n' for d in dark) + '\t};\n'
                  '\tpublic static readonly Vector2[][][] Red =\n\t{\n' + ''.join('\t\t' + cs_polys(r) + ',\n' for r in red) + '\t};\n}\n', encoding='utf-8')
    print(json.dumps(meta['frames']))
    import morph_regions
    morph_regions.main(cs, 'PileDiscardEntrance', ['Dark', 'Red'], 4)


if __name__ == '__main__':
    main()
