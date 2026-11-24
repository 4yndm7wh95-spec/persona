"""入场区域多边形的逐帧补间：读生成的 <页>Entrance.cs 里每帧的多边形（原版 1920×1080 坐标），相邻两帧之间按符号距离场线性插值出 SUB−1 个中间形状，
写回同一个文件（数组长度 = (帧数 − 1)·SUB + 1，第 j 个 = 原版第 j / SUB 帧），游戏里按 u·SUB 取形状，跟着实时渲染的 3D Joker 一起平滑变化。
用法：python3 tools/p5/menu3d/morph_regions.py <cs 文件> <类名> <数组名,…> [SUB=4]"""
import re, sys, numpy as np
from pathlib import Path
from PIL import Image, ImageDraw
from scipy.ndimage import distance_transform_edt as edt, binary_closing, binary_opening
sys.path.insert(0, str(Path(__file__).parent / 'fit')); sys.path.insert(0, str(Path(__file__).parent / 'fit/item_example'))
from regions import polys
from item_entrance import edge_out, cs_polys
S = 2                         # 光栅化：原版坐标 ÷ 2（960×540 + 外围），够准又快
PAD = 60                      # 画面外的余量（贴边的点推到了 −40 / 1960 / 1120）

def parse(cs, name):
    blk = cs[cs.index(f' {name} ='):]; blk = blk[:blk.index('\t};') + 3]
    frames = []
    for line in blk.split('\n'):
        if 'Vector2[][]' not in line: continue
        ps = re.findall(r'new Vector2\[\] \{([^}]*)\}', line)
        frames.append([[(float(x), float(y)) for x, y in re.findall(r'new\(([-\d.]+)f, ([-\d.]+)f\)', p)] for p in ps])
    return frames

def mask(ps):
    W, H = 1920 // S + 2 * PAD, 1080 // S + 2 * PAD
    im = Image.new('L', (W, H)); d = ImageDraw.Draw(im)
    for p in ps:
        if len(p) > 2: d.polygon([(x / S + PAD, y / S + PAD) for x, y in p], fill=255)
    return np.asarray(im) > 0

def sdf(m): return edt(~m) - edt(m)

def to_polys(m):
    out = []
    for p in polys(m, 1.2):
        out.append(edge_out([((x / 2 - PAD) * S, (y / 2 - PAD) * S) for x, y in p]))     # polys 已经 ×2（按 960 宽的掩码写的）
    return out

def disk(r):
    y, x = np.mgrid[-r:r + 1, -r:r + 1]
    return x * x + y * y <= r * r

def smooth(m, r=12):
    """抹掉小于约 2r 像素（×S = 原版像素）的凸起和缺口（原版 Joker 的手臂 / 衣角被抠进区域里留下的小舌头，用户 2026-11-24）。"""
    if not m.any(): return m
    m = binary_closing(m, structure=disk(r), border_value=1)
    return binary_opening(m, structure=disk(r), border_value=0) | (m & ~binary_opening(np.ones_like(m), structure=disk(r)))

def main(path, cls, names, sub=4, r=12):
    path = Path(path); cs = path.read_text()
    for name in names:
        frames = parse(cs, name)
        keys = [smooth(mask(f), r) for f in frames]
        polys_k = [to_polys(m) for m in keys]
        out = []
        for k in range(len(frames)):
            out.append(polys_k[k])
            if k == len(frames) - 1: break
            a, b = keys[k], keys[k + 1]
            if (a == b).all():
                out += [polys_k[k]] * (sub - 1); continue
            da, db = sdf(a), sdf(b)
            lo = min(a.sum(), b.sum())
            for j in range(1, sub):
                t = j / sub
                m = ((1 - t) * da + t * db) < 0
                # 两帧的形状不重叠（区域整块换了位置）时距离场平均会变空：这一段改成中点硬切
                if lo > 0 and m.sum() < 0.6 * lo:
                    out.append(polys_k[k] if t < 0.5 else polys_k[k + 1])
                else:
                    out.append(to_polys(m))
        blk_start = cs.index(f' {name} =')
        line_start = cs.rindex('\n', 0, blk_start) + 1
        blk_end = cs.index('\t};', blk_start) + 4
        new = f'\tpublic static readonly Vector2[][][] {name} =\n\t{{\n' + ''.join('\t\t' + cs_polys(d) + ',\n' for d in out) + '\t};\n'
        cs = cs[:line_start] + new + cs[blk_end:]
    if 'public const int Sub' not in cs:
        cs = cs.replace(f'internal static class {cls}\n{{\n', f'internal static class {cls}\n{{\n\t/// <summary>每个原版帧分几步（区域形状在相邻两帧之间按距离场补间，tools/p5/menu3d/morph_regions.py）。</summary>\n\tpublic const int Sub = {sub};\n')
    path.write_text(cs)
    print('ok', path.name, {n: len(parse(cs, n)) for n in names})

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3].split(','), int(sys.argv[4]) if len(sys.argv) > 4 else 4)
