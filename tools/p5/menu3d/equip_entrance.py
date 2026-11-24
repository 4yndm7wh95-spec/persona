"""牌组入场（= P5R 暂停菜单 EQUIP 页）的 3D Joker 逐帧渲染（对照用）+ 游戏里实时 3D 的关键帧：原版序号 233–239 七帧，第 240 帧起是 2D 立绘 CAMPEQUIP_2D01。
输出：research/p5r/menu3d_equip/f00.png … f06.png（裁到不透明区域）+ frames.json（每帧在原版屏幕坐标里的左上角）；
      MenuJoker3DKeys.cs（runtime_keys.py）。色块是 PileDeck.cs 自己设计的（PileField），不再用从原版截图抠出来的区域（用户 2026-11-24）。

结论（2026-11-24，牌堆会话）：
- 动作 = main_0005（手在脸边 → 手举过头顶 → 拔枪 → 枪口朝右下），带枪（gun_main / gun_outline 不藏）。
- 机位逐帧解：tools/p5/menu3d/fit/equip_example/（landmarks.py 233、234、239 的脸部特征点；235–238 是纯剪影，
  refine.py 在黑区里比白色剪影；zones.py 黑区分界线从网格图读）。镜头从 Joker 正前方绕到他左前方、一路推近，fov 30。
- 原版画面：233–235 主菜单的星纹弧带 + 黑区（弧线以上，Joker 进去变纯白）+ 右上红底 / 浅灰板；236–239 左上星纹拼贴、
  黑区在一条左上→右下斜线的右边、右边装备栏、右下红底。（之前区域形状逐帧从原版截图取：equip_example/regions_eq.py，现在只留作对照。）
- 原版 240 硬切成 2D 立绘；我们在 final.json 里多一个收尾关键帧「240」（枪甩下来、头身对上立绘，fit/equip_example/fit240.py），切的时候再用一道斜线扫过去（PileDeck）。
用法：python3 tools/p5/menu3d/equip_entrance.py"""
import json, os, subprocess, sys, numpy as np
from pathlib import Path
from PIL import Image
import m3d

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'tools/p5/menu3d/fit')); sys.path.insert(0, str(ROOT / 'tools/p5/menu3d/fit/equip_example'))
from item_entrance import cam, godot
OUT = ROOT / 'research/p5r/menu3d_equip'
EXT = 140
CLIP = 'main_0005'
HIDE = ['smartphone', 'card', 'school_bag'] + [f'ef line_obj_0{i}' for i in range(1, 9)]
# 原版序号: (main_0005 的帧, 机位 [x, y, z, 旋转 x, y, z, fov])
FRAMES = json.load(open(ROOT / 'tools/p5/menu3d/fit/equip_example/final.json'))
FRAMES = {int(k): (v[0], v[1]) for k, v in FRAMES.items()}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    tmp = OUT / '_tmp'; tmp.mkdir(exist_ok=True)
    idxs = sorted(FRAMES)
    jobs = [{'clip': CLIP, 'frame': float(FRAMES[i][0]), 'camera': cam(FRAMES[i][1], EXT), 'mode': 'color', 'hide': HIDE, 'out': str(tmp / f'r{i}.png')} for i in idxs]
    godot(jobs, (1920 + 2 * EXT, 1080 + 2 * EXT), 2, tmp)
    for old in OUT.glob('f*.png'): old.unlink()
    meta = {'ext': EXT, 'note': '第 k 帧 = 原版序号 233 + k；PNG 在原版 1920×1080 屏幕坐标里的左上角 = (x, y)；原版 2D 立绘 CAMPEQUIP_2D01 = 缩放 0.4996、左上 (0, 0)', 'frames': {}}
    for k, i in enumerate(idxs):
        im = Image.open(tmp / f'r{i}.png').convert('RGBA')
        ys, xs = np.nonzero(np.asarray(im)[..., 3] > 0)
        x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
        im.crop((x0, y0, x1, y1)).save(OUT / f'f{k:02d}.png', optimize=True)
        meta['frames'][str(k)] = {'orig': i, 'clip_frame': FRAMES[i][0], 'x': int(x0) - EXT, 'y': int(y0) - EXT, 'w': int(x1 - x0), 'h': int(y1 - y0)}
    json.dump(meta, open(OUT / 'frames.json', 'w'), indent=1, ensure_ascii=False)
    for p in tmp.iterdir(): p.unlink()
    tmp.rmdir()
    print(json.dumps(meta['frames']))
    # 关键帧给游戏里实时渲染的 MenuJoker3D
    import runtime_keys
    runtime_keys.main()


if __name__ == '__main__':
    main()
